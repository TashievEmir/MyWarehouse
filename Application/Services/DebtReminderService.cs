using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Notifications;
using Application.Localization;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    /// <summary>
    /// Напоминания о долге по почте. Каждый день, начиная с обещанного срока,
    /// письмо уходит в заданные времена — по одному на слот.
    ///
    /// Слот считается закрытым, даже если письмо не ушло: иначе неверные
    /// настройки SMTP заставляли бы приложение долбиться в почту каждую минуту.
    /// </summary>
    public class DebtReminderService : IDebtReminderService
    {
        private readonly IDataContext _db;
        private readonly ISalesService _sales;
        private readonly INotificationSettingsService _settings;
        private readonly IEmailSender _email;

        public DebtReminderService(
            IDataContext db,
            ISalesService sales,
            INotificationSettingsService settings,
            IEmailSender email)
        {
            _db = db;
            _sales = sales;
            _settings = settings;
            _email = email;
        }

        public async Task<ReminderRunResult> RunAsync(CancellationToken ct)
        {
            var result = new ReminderRunResult();

            var settings = await _settings.GetAsync(ct);

            if (!settings.IsEnabled || settings.SmtpHost.Length == 0)
                return result;

            var times = NotificationSettingsService.ParseTimes(settings.SendTimes);

            if (times.Count == 0)
                return result;

            var now = DateTime.Now;

            // Слоты, чьё время сегодня уже наступило
            var dueSlots = times
                .Select((time, index) => (time, index))
                .Where(s => s.time <= now.TimeOfDay)
                .ToList();

            if (dueSlots.Count == 0)
                return result;

            var debts = await _sales.GetDebtsAsync(null, ct);

            var overdue = debts
                .Where(d => d.DueDate is not null && d.DueDate.Value.ToLocalTime().Date <= now.Date)
                .ToList();

            result.WithoutEmail = overdue.Count(d => string.IsNullOrWhiteSpace(d.CustomerEmail));

            var today = now.ToString("yyyy-MM-dd");

            foreach (var debt in overdue.Where(d => !string.IsNullOrWhiteSpace(d.CustomerEmail)))
            {
                // Слот считается открытым, пока письмо не ушло и попытки не исчерпаны
                var open = new List<DebtReminder>();

                foreach (var (_, index) in dueSlots)
                {
                    var slotKey = $"{today}#{index}";

                    var row = await _db.DebtReminders
                        .FirstOrDefaultAsync(r => r.SaleId == debt.SaleId && r.SlotKey == slotKey, ct);

                    if (row is null)
                    {
                        row = new DebtReminder(debt.SaleId, slotKey, debt.CustomerEmail!, debt.Debt);
                        _db.DebtReminders.Add(row);

                        open.Add(row);
                    }
                    else if (row.IsPending)
                    {
                        open.Add(row);
                    }
                }

                if (open.Count == 0)
                    continue;

                // Приложение могло простоять весь день выключенным: письмо шлём
                // только за последний открытый слот, остальные закрываем
                for (var i = 0; i < open.Count - 1; i++)
                {
                    open[i].MarkSkipped();
                    result.Skipped++;
                }

                ct.ThrowIfCancellationRequested();

                var slot = open[^1];

                slot.UpdateAmount(debt.Debt);

                try
                {
                    await _email.SendAsync(settings, BuildMessage(debt, now), ct);

                    slot.MarkSent();
                    result.Sent++;
                }
                catch (Exception ex)
                {
                    slot.RegisterFailure(ex.Message);

                    // Попытки не исчерпаны — вернёмся к этому слоту на следующем проходе
                    if (slot.IsPending)
                        result.Retrying++;
                    else
                        result.Failed++;

                    result.Error ??= ex.Message;
                }

                await _db.SaveChangesAsync(ct);
            }

            return result;
        }

        public async Task SendTestAsync(string recipient, CancellationToken ct)
        {
            recipient = (recipient ?? "").Trim();

            if (recipient.Length == 0)
                throw new DomainException(Tr.T("Err_NeedTestRecipient"));

            var settings = await _settings.GetAsync(ct);

            if (settings.SmtpHost.Length == 0)
                throw new DomainException(Tr.T("Err_NeedSmtpHost"));

            await _email.SendAsync(settings, new EmailMessage
            {
                To = recipient,
                Subject = Tr.T("Mail_TestSubject"),
                Body = Tr.T("Mail_TestBody"),
            }, ct);
        }

        private static EmailMessage BuildMessage(DTOs.Sales.DebtResponse debt, DateTime now)
        {
            var due = debt.DueDate!.Value.ToLocalTime().Date;
            var daysLate = (now.Date - due).Days;

            var when = daysLate <= 0
                ? Tr.T("Mail_DueToday")
                : Tr.F("Mail_Overdue", daysLate);

            var body =
                Tr.F("Mail_Greeting", debt.CustomerName) + "\n\n" +
                Tr.F("Mail_Body",
                    debt.SaleId,
                    debt.Debt.ToString("N2"),
                    due.ToString("dd.MM.yyyy"),
                    when) + "\n\n" +
                Tr.T("Mail_Signature");

            return new EmailMessage
            {
                To = debt.CustomerEmail!,
                Subject = Tr.F("Mail_Subject", debt.Debt.ToString("N2")),
                Body = body,
            };
        }
    }
}
