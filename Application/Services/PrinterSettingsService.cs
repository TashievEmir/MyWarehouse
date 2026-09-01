using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Printing;
using Application.Localization;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public class PrinterSettingsService : IPrinterSettingsService
    {
        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public PrinterSettingsService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
        }

        public async Task<PrinterSettingsResponse> GetAsync(CancellationToken ct)
        {
            var settings = await _db.PrinterSettings.AsNoTracking().FirstOrDefaultAsync(ct);

            if (settings is null)
                return new PrinterSettingsResponse();

            return new PrinterSettingsResponse
            {
                AutoPrint = settings.AutoPrint,
                PrinterName = settings.PrinterName,
                CharsPerLine = settings.CharsPerLine,
                Codepage = settings.Codepage,
                CutPaper = settings.CutPaper,
                OpenCashDrawer = settings.OpenCashDrawer,
                FeedLines = settings.FeedLines,
            };
        }

        public async Task SaveAsync(SavePrinterSettingsRequest request, CancellationToken ct)
        {
            if (request.UserId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            var settings = await _db.PrinterSettings.FirstOrDefaultAsync(ct);

            if (settings is null)
            {
                settings = new Domain.Entities.PrinterSettings(
                    request.AutoPrint,
                    request.PrinterName,
                    request.CharsPerLine,
                    request.Codepage,
                    request.CutPaper,
                    request.OpenCashDrawer,
                    request.FeedLines);

                _db.PrinterSettings.Add(settings);
            }
            else
            {
                settings.Update(
                    request.AutoPrint,
                    request.PrinterName,
                    request.CharsPerLine,
                    request.Codepage,
                    request.CutPaper,
                    request.OpenCashDrawer,
                    request.FeedLines);
            }

            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                request.UserId,
                ActivityType.TemplateSaved,
                Tr.T("Log_PrinterSaved"),
                Tr.F(
                    "Log_PrinterDetails",
                    settings.PrinterName.Length == 0 ? Tr.T("Printer_DefaultName") : settings.PrinterName,
                    settings.AutoPrint ? Tr.T("Common_On") : Tr.T("Common_Off")),
                "PrinterSettings",
                settings.Id,
                ct);
        }
    }
}
