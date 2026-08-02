using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Activity;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

using Application.Localization;

namespace Application.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IDataContext _db;

        public ActivityLogService(IDataContext db)
        {
            _db = db;
        }

        public async Task LogAsync(
            long userId,
            ActivityType type,
            string title,
            string? details = null,
            string? entityType = null,
            long? entityId = null,
            CancellationToken ct = default)
        {
            var userName = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => (u.LastName + " " + u.FirstName).Trim())
                .FirstOrDefaultAsync(ct) ?? Tr.T("Log_Unknown");

            _db.ActivityLog.Add(new ActivityLogEntry(userId, userName, type, title, details, entityType, entityId));

            await _db.SaveChangesAsync(ct);
        }

        public async Task<List<ActivityEntryResponse>> GetAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            string? search,
            long? userId,
            CancellationToken ct)
        {
            var query = _db.ActivityLog.AsNoTracking();

            if (userId is { } id)
                query = query.Where(x => x.UserId == id);

            var entries = await query
                .Select(x => new ActivityEntryResponse
                {
                    Id         = x.Id,
                    OccurredAt = x.OccurredAt,
                    UserId     = x.UserId,
                    UserName   = x.UserName,
                    Type       = x.Type,
                    Title      = x.Title,
                    Details    = x.Details,
                    EntityType = x.EntityType,
                    EntityId   = x.EntityId,
                })
                .ToListAsync(ct);

            // Даты и поиск — в памяти: SQLite не сравнивает DateTimeOffset
            // и не знает регистра кириллицы
            var term = search?.Trim();

            return entries
                .Where(e => from is null || e.OccurredAt >= from.Value)
                .Where(e => toExclusive is null || e.OccurredAt < toExclusive.Value)
                .Where(e => string.IsNullOrWhiteSpace(term)
                            || Contains(e.UserName, term)
                            || Contains(e.Title, term)
                            || Contains(e.Details, term))
                .OrderByDescending(e => e.OccurredAt)
                .ToList();
        }

        public async Task<List<ActivityUserResponse>> GetUsersAsync(CancellationToken ct)
        {
            var entries = await _db.ActivityLog
                .AsNoTracking()
                .Select(x => new { x.UserId, x.UserName })
                .ToListAsync(ct);

            return entries
                .GroupBy(x => x.UserId)
                .Select(g => new ActivityUserResponse
                {
                    UserId   = g.Key,
                    UserName = g.First().UserName,
                })
                .OrderBy(u => u.UserName)
                .ToList();
        }

        private static bool Contains(string? value, string term)
            => value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
}
