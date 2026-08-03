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

using Application.Localization;

namespace Application.Services
{
    public class SalesService : ISalesService
    {
        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public SalesService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
        }

        public async Task<long> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct)
        {
            if (request.Items.Count == 0)
                throw new DomainException(Tr.T("Err_CartEmpty"));

            if (request.UserId <= 0)
                throw new DomainException(Tr.T("Err_NoSeller"));

            if (request.PaymentMethod == PaymentMethod.Credit && request.CustomerId is null)
                throw new DomainException(Tr.T("Err_CreditNeedCustomer"));

            // Без срока напоминать не о чем и просрочку не посчитать
            if (request.PaymentMethod == PaymentMethod.Credit && request.DueDate is null)
                throw new DomainException(Tr.T("Err_CreditNeedDueDate"));

            if (request.CustomerId is { } customerId &&
                !await _db.Customers.AnyAsync(x => x.Id == customerId, ct))
            {
                throw new DomainException(Tr.T("Err_CustomerNotFound"));
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
                throw new DomainException(Tr.T("Err_DiscountNegative"));

            if (request.DiscountAmount > subtotal)
                throw new DomainException(Tr.F("Err_DiscountTooBig", request.DiscountAmount, subtotal));

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                var sale = new Sale(request.CustomerId, request.UserId);

                foreach (var line in lines)
                {
                    if (line.Quantity <= 0)
                        throw new DomainException(Tr.T("Err_QuantityPositive"));

                    if (line.Price < 0)
                        throw new DomainException(Tr.T("Err_PriceNegative"));

                    var inventory = await _db.Inventories
                        .FirstOrDefaultAsync(x => x.ProductId == line.ProductId, ct)
                        ?? throw new DomainException(Tr.T("Err_ProductNotInStock"));

                    if (inventory.Quantity < line.Quantity)
                    {
                        var name = await _db.Products
                            .Where(p => p.Id == line.ProductId)
                            .Select(p => p.Name)
                            .FirstOrDefaultAsync(ct);

                        throw new DomainException(
                            Tr.F("Err_NotEnoughStock", name, inventory.Quantity, line.Quantity));
                    }

                    inventory.Decrease(line.Quantity);

                    sale.AddItem(line.ProductId, line.Quantity, line.Price);
                }

                sale.ApplyDiscount(request.DiscountAmount);
                sale.SetPaymentMethod(request.PaymentMethod);

                if (request.PaymentMethod == PaymentMethod.Credit)
                {
                    if (request.PaidAmount < 0)
                        throw new DomainException(Tr.T("Err_PrepaidNegative"));

                    if (request.PaidAmount > sale.TotalAmount)
                        throw new DomainException(Tr.T("Err_PrepaidTooBig"));

                    if (request.PaidAmount > 0)
                        sale.Pay(request.PaidAmount);

                    sale.SetDueDate(request.DueDate!.Value);
                }
                else
                {
                    if (request.PaidAmount < sale.TotalAmount)
                        throw new DomainException(Tr.T("Err_PaymentTooSmall"));

                    // сдача не хранится: чек оплачен ровно на свою сумму
                    if (sale.TotalAmount > 0)
                        sale.Pay(sale.TotalAmount);
                }

                _db.Sales.Add(sale);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                await LogSaleAsync(sale, lines.Count, ct);

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

        public async Task ReturnSaleAsync(long saleId, long userId, CancellationToken ct)
        {
            if (userId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            var sale = await _db.Sales
                .Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == saleId, ct)
                ?? throw new DomainException(Tr.T("Err_ReceiptNotFound"));

            if (sale.IsReturned)
                throw new DomainException(Tr.F("Err_AlreadyReturned", saleId));

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                foreach (var item in sale.SaleItems)
                {
                    var inventory = await _db.Inventories
                        .FirstOrDefaultAsync(x => x.ProductId == item.ProductId, ct);

                    // Карточку товара могли удалить — тогда возвращать некуда,
                    // но сам чек всё равно должен закрыться возвратом
                    inventory?.Increase(item.Quantity);
                }

                sale.MarkReturned();

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            await _activity.LogAsync(
                userId,
                ActivityType.SaleReturned,
                Tr.T("Log_SaleReturned"),
                Tr.F("Log_ReturnDetails", saleId, sale.SaleItems.Sum(i => i.Quantity), sale.TotalAmount.ToString("N2")),
                "Sale",
                saleId,
                ct);
        }

        // Событие в историю: детали пишем коротко и с числами
        private async Task LogSaleAsync(Sale sale, int positions, CancellationToken ct)
        {
            var payment = sale.PaymentMethod switch
            {
                PaymentMethod.Cash     => Tr.T("PaymentLower_Cash"),
                PaymentMethod.Card     => Tr.T("PaymentLower_Card"),
                PaymentMethod.Transfer => Tr.T("PaymentLower_Transfer"),
                PaymentMethod.Credit   => Tr.T("PaymentLower_Credit"),
                _                      => Tr.T("PaymentLower_Unknown"),
            };

            var details = Tr.F("Log_SaleDetails", positions, sale.TotalAmount.ToString("N2"), payment);

            if (sale.DiscountAmount > 0)
                details += Tr.F("Log_SaleDiscount", sale.DiscountAmount.ToString("N2"));

            if (sale.IsCredit)
                details += Tr.F("Log_SaleDebt", (sale.TotalAmount - sale.PaidAmount).ToString("N2"));

            await _activity.LogAsync(
                sale.UserId,
                sale.PaymentMethod == PaymentMethod.Credit ? ActivityType.CreditSale : ActivityType.SaleClosed,
                Tr.T(sale.PaymentMethod == PaymentMethod.Credit ? "Log_SaleCredit" : "Log_SaleClosed"),
                details,
                "Sale",
                sale.Id,
                ct);
        }

        public async Task<List<ReceiptListItemResponse>> GetReceiptsAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            string? search,
            CancellationToken ct)
        {
            var receipts = await _db.Sales
                .AsNoTracking()
                .Select(s => new
                {
                    Item = new ReceiptListItemResponse
                    {
                        SaleId         = s.Id,
                        SaleDate       = s.SaleDate,
                        CashierName    = _db.Users
                            .Where(u => u.Id == s.UserId)
                            .Select(u => (u.LastName + " " + u.FirstName).Trim())
                            .FirstOrDefault() ?? Tr.T("Log_Unknown"),
                        CustomerName   = _db.Customers
                            .Where(c => c.Id == s.CustomerId)
                            .Select(c => c.Name)
                            .FirstOrDefault(),
                        PositionsCount = s.SaleItems.Count,
                        ItemsCount     = s.SaleItems.Sum(i => i.Quantity),
                        PaymentMethod  = s.PaymentMethod,
                        TotalAmount    = s.TotalAmount,
                        PaidAmount     = s.PaidAmount,
                        IsReturned     = s.IsReturned,
                    },
                    Products = s.SaleItems.Select(i => i.Product.Name).ToList(),
                })
                .ToListAsync(ct);

            // По датам и поиску отбираем в памяти: SQLite не сравнивает
            // DateTimeOffset в SQL, а LIKE не знает регистра кириллицы
            var term = search?.Trim();

            return receipts
                .Where(r => from is null || r.Item.SaleDate >= from.Value)
                .Where(r => toExclusive is null || r.Item.SaleDate < toExclusive.Value)
                .Where(r => string.IsNullOrWhiteSpace(term)
                            || r.Item.SaleId.ToString().Contains(term)
                            || Contains(r.Item.CashierName, term)
                            || Contains(r.Item.CustomerName, term)
                            || r.Products.Any(p => Contains(p, term)))
                .Select(r => r.Item)
                .OrderByDescending(r => r.SaleDate)
                .ToList();
        }

        public async Task<ReceiptDetailsResponse?> GetReceiptAsync(long saleId, CancellationToken ct)
        {
            return await _db.Sales
                .AsNoTracking()
                .Where(s => s.Id == saleId)
                .Select(s => new ReceiptDetailsResponse
                {
                    SaleId         = s.Id,
                    SaleDate       = s.SaleDate,
                    CashierName    = _db.Users
                        .Where(u => u.Id == s.UserId)
                        .Select(u => (u.LastName + " " + u.FirstName).Trim())
                        .FirstOrDefault() ?? Tr.T("Log_Unknown"),
                    CustomerName   = _db.Customers
                        .Where(c => c.Id == s.CustomerId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                    Subtotal       = s.Subtotal,
                    DiscountAmount = s.DiscountAmount,
                    TotalAmount    = s.TotalAmount,
                    PaidAmount     = s.PaidAmount,
                    PaymentMethod  = s.PaymentMethod,
                    IsReturned     = s.IsReturned,
                    Lines = s.SaleItems
                        .Select(i => new ReceiptLineResponse
                        {
                            ProductName = i.Product.Name,
                            Barcode     = i.Product.Barcode,
                            Quantity    = i.Quantity,
                            Price       = i.PriceAtSale,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync(ct);
        }

        private static bool Contains(string? value, string term)
            => value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);

        public async Task<List<DebtResponse>> GetDebtsAsync(string? search, CancellationToken ct)
        {
            // Даты платежей забираем списком и берём последнюю в памяти:
            // SQLite не умеет сортировать DateTimeOffset в SQL
            var rows = await _db.Sales
                .AsNoTracking()
                .Where(s => s.PaidAmount < s.TotalAmount && !s.IsReturned)
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
                    CustomerEmail = _db.Customers
                        .Where(c => c.Id == s.CustomerId)
                        .Select(c => c.Email)
                        .FirstOrDefault(),
                    s.DueDate,
                    PaymentDates = s.DebtPayments.Select(p => p.PaymentDate).ToList(),
                })
                .ToListAsync(ct);

            var debts = rows
                .Select(r => new DebtResponse
                {
                    SaleId          = r.Id,
                    SaleDate        = r.SaleDate,
                    CustomerId      = r.CustomerId,
                    CustomerName    = r.CustomerName ?? Tr.T("Log_NoCustomer"),
                    CustomerPhone   = r.CustomerPhone,
                    CustomerEmail   = r.CustomerEmail,
                    DueDate         = r.DueDate,
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
                throw new DomainException(Tr.T("Err_PaymentPositive"));

            if (userId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            var sale = await _db.Sales.FirstOrDefaultAsync(x => x.Id == saleId, ct)
                       ?? throw new DomainException(Tr.T("Err_SaleNotFound"));

            var debt = sale.TotalAmount - sale.PaidAmount;

            if (debt <= 0)
                throw new DomainException(Tr.T("Err_DebtAlreadyClosed"));

            if (amount > debt)
                throw new DomainException(Tr.F("Err_DebtTooMuch", debt.ToString("N2")));

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

            var left = sale.TotalAmount - sale.PaidAmount;

            await _activity.LogAsync(
                userId,
                ActivityType.DebtPaid,
                Tr.T("Log_DebtPaid"),
                left > 0
                    ? Tr.F("Log_DebtPaidRest", saleId, amount.ToString("N2"), left.ToString("N2"))
                    : Tr.F("Log_DebtPaidClosed", saleId, amount.ToString("N2")),
                "Sale",
                saleId,
                ct);
        }
    }
}
