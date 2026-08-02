using Application.DTOs.Activity;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    /// <summary>
    /// История действий. Пишется из сервисов на каждое изменяющее действие,
    /// читается экраном «История действий».
    /// </summary>
    public interface IActivityLogService
    {
        /// <summary>Имя пользователя сервис подставляет сам по его Id.</summary>
        Task LogAsync(
            long userId,
            ActivityType type,
            string title,
            string? details = null,
            string? entityType = null,
            long? entityId = null,
            CancellationToken ct = default);

        Task<List<ActivityEntryResponse>> GetAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            string? search,
            long? userId,
            CancellationToken ct);

        /// <summary>Пользователи, встречающиеся в истории — для фильтра.</summary>
        Task<List<ActivityUserResponse>> GetUsersAsync(CancellationToken ct);
    }
}
