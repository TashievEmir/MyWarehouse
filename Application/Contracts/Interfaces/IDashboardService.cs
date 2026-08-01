using Application.DTOs.Dashboard;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface IDashboardService
    {
        /// <summary>
        /// Сводка главной страницы одним запросом: касса за сегодня, график выручки
        /// за <paramref name="revenueDays"/> дней, долги, заканчивающийся товар и топ продаж.
        /// </summary>
        Task<DashboardResponse> GetSnapshotAsync(
            int revenueDays,
            int topDays,
            int lowStockThreshold,
            CancellationToken ct);
    }
}
