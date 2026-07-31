using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Sales;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class SalesService : ISalesService
    {
        private readonly IDataContext _db;

        public SalesService(IDataContext db)
        {
            _db = db;
        }

        public async Task<long> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct)
        {
            if (request.Items.Count == 0)
                throw new DomainException("Чек пуст");

            if (request.UserId <= 0)
                throw new DomainException("Не указан продавец");

            if (request.PaymentMethod == PaymentMethod.Credit && request.CustomerId is null)
                throw new DomainException("Для продажи в долг нужно выбрать клиента");

            if (request.CustomerId is { } customerId &&
                !await _db.Customers.AnyAsync(x => x.Id == customerId, ct))
            {
                throw new DomainException("Клиент не найден");
            }

            // В чеке позиция на товар одна — одинаковые товары складываем,
            // иначе не пройдёт составной ключ SaleItems.
            var lines = request.Items
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Price = g.First().Price
                })
                .ToList();

            var subtotal = lines.Sum(l => l.Price * l.Quantity);

            if (request.DiscountAmount < 0)
                throw new DomainException("Скидка не может быть отрицательной");

            if (request.DiscountAmount > subtotal)
                throw new DomainException($"Скидка {request.DiscountAmount:N2} больше суммы чека {subtotal:N2}");

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                var sale = new Sale(request.CustomerId, request.UserId);

                foreach (var line in lines)
                {
                    if (line.Quantity <= 0)
                        throw new DomainException("Количество должно быть больше нуля");

                    if (line.Price < 0)
                        throw new DomainException("Цена не может быть отрицательной");

                    var inventory = await _db.Inventories
                        .FirstOrDefaultAsync(x => x.ProductId == line.ProductId, ct)
                        ?? throw new DomainException("Товар не найден на складе");

                    if (inventory.Quantity < line.Quantity)
                    {
                        var name = await _db.Products
                            .Where(p => p.Id == line.ProductId)
                            .Select(p => p.Name)
                            .FirstOrDefaultAsync(ct);

                        throw new DomainException(
                            $"«{name}»: на складе {inventory.Quantity} шт., а в чеке {line.Quantity}");
                    }

                    inventory.Decrease(line.Quantity);

                    sale.AddItem(line.ProductId, line.Quantity, line.Price);
                }

                sale.ApplyDiscount(request.DiscountAmount);
                sale.SetPaymentMethod(request.PaymentMethod);

                if (request.PaymentMethod == PaymentMethod.Credit)
                {
                    if (request.PaidAmount < 0)
                        throw new DomainException("Предоплата не может быть отрицательной");

                    if (request.PaidAmount > sale.TotalAmount)
                        throw new DomainException("Предоплата больше суммы чека");

                    if (request.PaidAmount > 0)
                        sale.Pay(request.PaidAmount);
                }
                else
                {
                    if (request.PaidAmount < sale.TotalAmount)
                        throw new DomainException("Оплата меньше суммы чека");

                    // сдача не хранится: чек оплачен ровно на свою сумму
                    if (sale.TotalAmount > 0)
                        sale.Pay(sale.TotalAmount);
                }

                _db.Sales.Add(sale);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return sale.Id;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<SaleResponse?> GetSaleAsync(long saleId, CancellationToken ct)
        {
            return await _db.Sales
            .Where(x => x.Id == saleId)
            .Select(x => new SaleResponse(x))
            .FirstOrDefaultAsync(ct);
        }
        
        public async Task<List<SaleResponse>> GetSalesByPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            return await _db.Sales
            .Where(x => x.SaleDate >= from && x.SaleDate <= to)
            .Select(x => new SaleResponse(x))
            .ToListAsync(ct);
        }

        public async Task<List<DebtResponse>> GetDebtsAsync(string? search, CancellationToken ct)
        {
            // Даты платежей забираем списком и берём последнюю в памяти:
            // SQLite не умеет сортировать DateTimeOffset в SQL
            var rows = await _db.Sales
                .AsNoTracking()
                .Where(s => s.PaidAmount < s.TotalAmount)
                .Select(s => new
                {
                    s.Id,
                    s.SaleDate,
                    s.CustomerId,
                    s.TotalAmount,
                    s.PaidAmount,
                    s.PaymentMethod,
                    CustomerName = _db.Customers
                        .Where(c => c.Id == s.CustomerId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                    CustomerPhone = _db.Customers
                        .Where(c => c.Id == s.CustomerId)
                        .Select(c => c.Phone)
                        .FirstOrDefault(),
                    PaymentDates = s.DebtPayments.Select(p => p.PaymentDate).ToList(),
                })
                .ToListAsync(ct);

            var debts = rows
                .Select(r => new DebtResponse
                {
                    SaleId          = r.Id,
                    SaleDate        = r.SaleDate,
                    CustomerId      = r.CustomerId,
                    CustomerName    = r.CustomerName ?? "Без клиента",
                    CustomerPhone   = r.CustomerPhone,
                    TotalAmount     = r.TotalAmount,
                    PaidAmount      = r.PaidAmount,
                    PaymentMethod   = r.PaymentMethod,
                    PaymentsCount   = r.PaymentDates.Count,
                    LastPaymentDate = r.PaymentDates.Count == 0 ? null : r.PaymentDates.Max(),
                })
                .ToList();

            // Поиск в памяти: SQLite сравнивает LIKE без учёта регистра только для латиницы
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                debts = debts
                    .Where(d => d.CustomerName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                                || (d.CustomerPhone?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
                                || d.SaleId.ToString().Contains(term))
                    .ToList();
            }

            return debts
                .OrderByDescending(d => d.SaleDate)
                .ToList();
        }

        public async Task RegisterDebtPaymentAsync(long saleId, decimal amount, long userId, CancellationToken ct)
        {
            if (amount <= 0)
                throw new DomainException("Сумма платежа должна быть больше нуля");

            if (userId <= 0)
                throw new DomainException("Не указан сотрудник");

            var sale = await _db.Sales.FirstOrDefaultAsync(x => x.Id == saleId, ct)
                       ?? throw new DomainException("Продажа не найдена");

            var debt = sale.TotalAmount - sale.PaidAmount;

            if (debt <= 0)
                throw new DomainException("Долг по этой продаже уже закрыт");

            if (amount > debt)
                throw new DomainException($"Долг составляет {debt:N2} — принять больше нельзя");

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                sale.Pay(amount);

                _db.DebtPayments.Add(new DebtPayment(saleId, userId, amount));

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}
