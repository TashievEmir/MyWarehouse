using Application.DTOs.Updates;

namespace Application.Contracts.Interfaces
{
    /// <summary>
    /// Версия запущенного приложения. Отдельный контракт, потому что берётся
    /// она из сборки — а слой Application о сборках ничего не знает.
    /// </summary>
    public interface IAppVersionProvider
    {
        string CurrentVersion { get; }

        /// <summary>Папка, куда установлено приложение.</summary>
        string InstallDirectory { get; }
    }

    /// <summary>
    /// Откуда берём сведения о выпусках. Реализация ходит по HTTP, но слой
    /// Application этого не знает — сменить GitHub на другой хостинг можно,
    /// не трогая логику сравнения версий.
    /// </summary>
    public interface IUpdateFeed
    {
        Task<UpdateManifest> GetLatestAsync(CancellationToken ct);
    }

    /// <summary>
    /// Как применяем обновление: скачать, проверить, подготовить подмену.
    /// Отделено от получения манифеста намеренно — это разные причины
    /// для изменения и разные виды сбоев.
    /// </summary>
    public interface IUpdateInstaller
    {
        Task InstallAsync(
            UpdateManifest manifest,
            IProgress<UpdateProgress>? progress,
            CancellationToken ct);
    }

    /// <summary>То, что вызывает интерфейс: проверить и поставить.</summary>
    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckAsync(CancellationToken ct);

        /// <summary>
        /// Ставит выпуск, который вернула проверка. Приложение будет закрыто
        /// и запущено заново уже обновлённым.
        /// </summary>
        Task InstallAsync(IProgress<UpdateProgress>? progress, CancellationToken ct);
    }
}
