using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Dashboard
{
    /// <summary>Сколько денег пришло каждым способом оплаты за сегодня.</summary>
    public class PaymentSliceResponse
    {
        public PaymentMethod Method { get; set; }
        public decimal Amount { get; set; }
        public int Receipts { get; set; }
    }

    /// <summary>Выручка одного дня для графика.</summary>
    public class DailyRevenueResponse
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public int Receipts { get; set; }
    }

    /// <summary>Товар, который заканчивается на складе.</summary>
    public class LowStockResponse
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = "";
        public string? Barcode { get; set; }
        public string CategoryName { get; set; } = "";
        public int InStock { get; set; }
    }

    /// <summary>Товар в топе продаж за период.</summary>
    public class TopProductResponse
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }
}
