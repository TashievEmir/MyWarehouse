using Application.Contracts.Interfaces;
using Application.DTOs.Updates;
using Application.Localization;
using Domain.Exceptions;

namespace Application.Services
{
    /// <summary>
    /// Решает, есть ли что ставить, и запускает установку.
    ///
    /// Сам ничего не качает и никуда не ходит: получение манифеста и подмена
    /// файлов живут за своими контрактами. Здесь остаётся только то, ради чего
    /// фича и нужна, — сравнение версий.
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly IUpdateFeed _feed;
        private readonly IUpdateInstaller _installer;
        private readonly IAppVersionProvider _version;

        /// <summary>
        /// Манифест последней проверки. Установка берёт его отсюда, а не
        /// перечитывает: ставить надо ровно то, на что согласился владелец.
        /// </summary>
        private UpdateManifest? _pending;

        public UpdateService(
            IUpdateFeed feed,
            IUpdateInstaller installer,
            IAppVersionProvider version)
        {
            _feed = feed;
            _installer = installer;
            _version = version;
        }

        public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct)
        {
            var current = _version.CurrentVersion;

            _pending = null;

            UpdateManifest manifest;

            try
            {
                manifest = await _feed.GetLatestAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Недоступный сервер — обычное дело: интернет отвалился,
                // выпусков ещё нет. Это не повод падать исключением в UI.
                return new UpdateCheckResult { CurrentVersion = current, Error = ex.Message };
            }

            if (!AppVersion.TryParse(current, out var mine))
                return new UpdateCheckResult { CurrentVersion = current, Error = Tr.T("Err_BadCurrentVersion") };

            if (!AppVersion.TryParse(manifest.Version, out var theirs))
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    Error = Tr.F("Err_BadManifestVersion", manifest.Version),
                };
            }

            var available = theirs > mine;

            if (available)
            {
                // Манифест без ссылки или без контрольной суммы ставить нельзя,
                // и узнать об этом лучше сейчас, а не после скачивания
                if (string.IsNullOrWhiteSpace(manifest.PackageUrl) || string.IsNullOrWhiteSpace(manifest.Sha256))
                {
                    return new UpdateCheckResult
                    {
                        CurrentVersion = current,
                        AvailableVersion = theirs.ToString(),
                        Error = Tr.T("Err_IncompleteManifest"),
                    };
                }

                _pending = manifest;
            }

            return new UpdateCheckResult
            {
                CurrentVersion = current,
                AvailableVersion = theirs.ToString(),
                IsUpdateAvailable = available,
                Notes = manifest.Notes,
                SizeBytes = manifest.SizeBytes,
                ReleasedAt = manifest.ReleasedAt,
            };
        }

        public Task InstallAsync(IProgress<UpdateProgress>? progress, CancellationToken ct)
        {
            var manifest = _pending
                ?? throw new DomainException(Tr.T("Err_NothingToInstall"));

            return _installer.InstallAsync(manifest, progress, ct);
        }
    }
}
