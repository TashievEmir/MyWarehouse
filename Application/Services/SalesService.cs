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

        public async Task RegisterDebtPaymentAsync(long saleId, decimal amount, long userId, CancellationToken ct)
        {
            var sale = await _db.Sales.FindAsync([saleId], ct)
                   ?? throw new Exception("Sale not found");

            sale.Pay(amount);

            _db.DebtPayments.Add(new DebtPayment(saleId, userId, amount));

            await _db.SaveChangesAsync(ct);
        }
    }
}
