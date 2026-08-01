using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    /// <summary>Строка списка чеков.</summary>
    public class ReceiptListItemResponse
    {
        public long SaleId { get; set; }
        public DateTimeOffset SaleDate { get; set; }

        public string CashierName { get; set; } = "";
        public string? CustomerName { get; set; }

        public int PositionsCount { get; set; }
        public int ItemsCount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
    }

    /// <summary>Позиция чека.</summary>
    public class ReceiptLineResponse
    {
        public string ProductName { get; set; } = "";
        public string? Barcode { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;
    }

    /// <summary>Чек целиком: шапка, позиции и итоги.</summary>
    public class ReceiptDetailsResponse
    {
        public long SaleId { get; set; }
        public DateTimeOffset SaleDate { get; set; }

        public string CashierName { get; set; } = "";
        public string? CustomerName { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }

        public decimal DebtLeft => TotalAmount - PaidAmount;

        public PaymentMethod PaymentMethod { get; set; }

        public List<ReceiptLineResponse> Lines { get; set; } = new();
    }
}
