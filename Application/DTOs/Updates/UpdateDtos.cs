namespace Application.DTOs.Updates
{
    /// <summary>
    /// Описание выпуска, лежащее на стороне разработчика: его читает клиент,
    /// чтобы понять, есть ли что ставить.
    /// </summary>
    public class UpdateManifest
    {
        public string Version { get; set; } = "";

        /// <summary>Что нового — показывается пользователю до согласия.</summary>
        public string? Notes { get; set; }

        public DateTimeOffset? ReleasedAt { get; set; }

        /// <summary>Прямая ссылка на zip со сборкой. Только https.</summary>
        public string PackageUrl { get; set; } = "";

        /// <summary>SHA-256 архива в hex. Без него обновление не ставится.</summary>
        public string Sha256 { get; set; } = "";

        public long SizeBytes { get; set; }
    }

    /// <summary>Что показать пользователю после нажатия «Проверить обновление».</summary>
    public class UpdateCheckResult
    {
        public required string CurrentVersion { get; init; }

        /// <summary>Пусто, если до сервера не достучались.</summary>
        public string? AvailableVersion { get; init; }

        public bool IsUpdateAvailable { get; init; }

        public string? Notes { get; init; }

        public long SizeBytes { get; init; }

        public DateTimeOffset? ReleasedAt { get; init; }

        /// <summary>Заполнено, если проверка не удалась: сеть, манифест, формат.</summary>
        public string? Error { get; init; }

        public bool IsFailed => Error is not null;
    }

    /// <summary>Этап установки — на него опирается подпись под полосой прогресса.</summary>
    public enum UpdateStage
    {
        Downloading,
        Verifying,
        Extracting,
        Preparing,
        Restarting
    }

    public class UpdateProgress
    {
        public UpdateStage Stage { get; init; }

        /// <summary>0..100. Для этапов без измеримого объёма — null.</summary>
        public int? Percent { get; init; }
    }
}
