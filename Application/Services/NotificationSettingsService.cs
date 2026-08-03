using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Notifications;
using Application.Localization;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    /// <summary>Настройки почтовых напоминаний: в базе одна строка.</summary>
    public class NotificationSettingsService : INotificationSettingsService
    {
        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public NotificationSettingsService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
        }

        public async Task<NotificationSettingsResponse> GetAsync(CancellationToken ct)
        {
            var settings = await _db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(ct);

            return settings is null
                ? new NotificationSettingsResponse()
                : new NotificationSettingsResponse(settings);
        }

        public async Task SaveAsync(SaveNotificationSettingsRequest request, CancellationToken ct)
        {
            if (request.UserId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            var times = ParseTimes(request.SendTimes);

            if (request.IsEnabled && times.Count == 0)
                throw new DomainException(Tr.T("Err_NeedSendTimes"));

            var settings = await _db.NotificationSettings.FirstOrDefaultAsync(ct);

            var normalized = string.Join(",", times.Select(t => t.ToString(@"hh\:mm")));

            if (settings is null)
            {
                settings = new NotificationSettings(
                    request.IsEnabled, request.SmtpHost, request.SmtpPort, request.UseSsl,
                    request.Username, request.Password, request.FromAddress, request.FromName, normalized);

                _db.NotificationSettings.Add(settings);
            }
            else
            {
                settings.Update(
                    request.IsEnabled, request.SmtpHost, request.SmtpPort, request.UseSsl,
                    request.Username, request.Password, request.FromAddress, request.FromName, normalized);
            }

            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                request.UserId,
                ActivityType.TemplateSaved,
                Tr.T("Log_NotificationsSaved"),
                Tr.F("Log_NotificationsDetails", request.IsEnabled ? Tr.T("Common_On") : Tr.T("Common_Off"), normalized),
                "NotificationSettings",
                settings.Id,
                ct);
        }

        /// <summary>
        /// Разбирает «10:00, 14:00, 18:00». Мусор молча отбрасываем, дубли убираем,
        /// порядок делаем по возрастанию — слоты нумеруются именно так.
        /// </summary>
        public static List<TimeSpan> ParseTimes(string? raw)
        {
            var result = new List<TimeSpan>();

            foreach (var part in (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TimeSpan.TryParse(part.Trim(), out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1))
                    result.Add(new TimeSpan(time.Hours, time.Minutes, 0));
            }

            return result.Distinct().OrderBy(t => t).ToList();
        }
    }
}
