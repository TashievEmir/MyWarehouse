using Application.DTOs.Printing;

namespace Application.Contracts.Interfaces
{
    public interface IPrinterSettingsService
    {
        Task<PrinterSettingsResponse> GetAsync(CancellationToken ct);

        Task SaveAsync(SavePrinterSettingsRequest request, CancellationToken ct);
    }
}
