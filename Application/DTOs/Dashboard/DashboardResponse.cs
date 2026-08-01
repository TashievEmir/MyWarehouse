using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Dashboard
{
    /// <summary>Сводка для главной страницы: всё, что нужно знать про сегодняшний день.</summary>
    public class DashboardResponse
    {
        // ── Сегодня ──
        public decimal TodayRevenue { get; set; }
        public int TodayReceipts { get; set; }
        public decimal AverageReceipt { get; set; }

        /// <summary>Выручка минус себестоимость проданного.</summary>
        public decimal TodayProfit { get; set; }

        /// <summary>Сколько потрачено на закупки сегодня.</summary>
        public decimal TodayPurchases { get; set; }

        /// <summary>Сколько штук списано сегодня.</summary>
        public int TodayWrittenOff { get; set; }

        // ── Долги ──
        public decimal TotalDebt { get; set; }
        public int DebtorsCount { get; set; }

        // ── Требует внимания ──
        public int ProductsWithoutPrice { get; set; }
        public int ProductsOutOfStock { get; set; }

        // ── Блоки ──
        public List<PaymentSliceResponse> Payments { get; set; } = new();
        public List<DailyRevenueResponse> Revenue { get; set; } = new();
        public List<LowStockResponse> LowStock { get; set; } = new();
        public List<TopProductResponse> TopProducts { get; set; } = new();
    }
}
