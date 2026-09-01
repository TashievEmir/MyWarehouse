using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Notifications;
using Application.DTOs.Sales;
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
        private readonly INotificationSettingsProvider _settings;
        private readonly IEmailSender _email;

        public DebtReminderService(
            IDataContext db,
            ISalesService sales,
            INotificationSettingsProvider settings,
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

            var settings = _settings.Get();

            if (!settings.IsEnabled || !settings.IsConfigured)
                return result;

            var times = ParseTimes(settings.SendTimes);

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

        public async Task<List<DebtResponse>> GetReachableDebtorsAsync(CancellationToken ct)
        {
            var debts = await _sales.GetDebtsAsync(null, ct);

            return debts
                .Where(d => !string.IsNullOrWhiteSpace(d.CustomerEmail))
                .OrderByDescending(d => d.Debt)
                .ToList();
        }

        public async Task SendToDebtorAsync(long saleId, CancellationToken ct)
        {
            var settings = _settings.Get();

            if (!settings.IsConfigured)
                throw new DomainException(Tr.T("Err_MailNotConfigured"));

            var debts = await _sales.GetDebtsAsync(null, ct);

            var debt = debts.FirstOrDefault(d => d.SaleId == saleId)
                ?? throw new DomainException(Tr.F("Err_DebtNotFound", saleId));

            if (string.IsNullOrWhiteSpace(debt.CustomerEmail))
                throw new DomainException(Tr.F("Err_DebtorNoEmail", debt.CustomerName));

            var now = DateTime.Now;

            // Отдельный ключ: досрочных отправок за день может быть сколько угодно,
            // и они не должны занимать слоты плановой рассылки
            var slotKey = $"{now:yyyy-MM-dd}#manual-{now:HHmmss}";

            var row = new DebtReminder(saleId, slotKey, debt.CustomerEmail!, debt.Debt);

            _db.DebtReminders.Add(row);

            try
            {
                await _email.SendAsync(settings, BuildMessage(debt, now), ct);

                row.MarkSent();
            }
            catch (Exception ex)
            {
                // Неудачу тоже сохраняем — она должна попасть в историю отправок
                row.RegisterFailure(ex.Message);

                await _db.SaveChangesAsync(CancellationToken.None);

                throw;
            }

            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Разбирает «10:00, 14:00». Мусор молча отбрасываем, дубли убираем,
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

        private static EmailMessage BuildMessage(DebtResponse debt, DateTime now)
        {
            var due = debt.DueDate?.ToLocalTime().Date;

            // Досрочное письмо уходит до срока, плановое — в срок или после,
            // а срока может не быть вовсе: формулировка подстраивается
            string when;

            if (due is null)
            {
                when = Tr.T("Mail_DueNotSet");
            }
            else
            {
                var daysLate = (now.Date - due.Value).Days;

                when = daysLate switch
                {
                    < 0 => Tr.F("Mail_DueIn", -daysLate),
                    0 => Tr.T("Mail_DueToday"),
                    _ => Tr.F("Mail_Overdue", daysLate),
                };
            }

            var body =
                Tr.F("Mail_Greeting", debt.CustomerName) + "\n\n" +
                Tr.F("Mail_Body",
                    debt.SaleId,
                    debt.Debt.ToString("N2"),
                    due?.ToString("dd.MM.yyyy") ?? "—",
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
