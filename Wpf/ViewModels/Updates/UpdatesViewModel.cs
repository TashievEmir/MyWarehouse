using System.Windows;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Updates;
using Wpf.Common;
using Wpf.Localization;

namespace Wpf.ViewModels.Updates;

/// <summary>
/// Раздел «Обновление»: проверить, что вышло, и поставить с согласия владельца.
///
/// Сам процесс установки закрывает приложение — поэтому согласие спрашивается
/// явно, а перед этим показывается, что именно изменилось и сколько весит.
/// </summary>
public class UpdatesViewModel : ViewModelBase
{
    private readonly IUpdateService _updates;
    private readonly IAppVersionProvider _version;

    public ICommand CheckCommand { get; }
    public ICommand InstallCommand { get; }

    public UpdatesViewModel(IUpdateService updates, IAppVersionProvider version)
    {
        _updates = updates;
        _version = version;

        CheckCommand = new AsyncRelayCommand(CheckAsync);
        InstallCommand = new AsyncRelayCommand(InstallAsync);

        Loc.LanguageChanged += () => OnPropertyChanged(string.Empty);
    }

    // ===================== Что показываем =====================

    public string CurrentVersion => _version.CurrentVersion;

    public string InstallDirectory => _version.InstallDirectory;

    private UpdateCheckResult? _result;

    private string _availableVersion = "";
    public string AvailableVersion
    {
        get => _availableVersion;
        private set => SetProperty(ref _availableVersion, value);
    }

    private string _notes = "";
    /// <summary>Что нового — текст из манифеста.</summary>
    public string Notes
    {
        get => _notes;
        private set
        {
            if (SetProperty(ref _notes, value))
                OnPropertyChanged(nameof(HasNotes));
        }
    }

    public bool HasNotes => Notes.Length > 0;

    private string _sizeText = "";
    public string SizeText
    {
        get => _sizeText;
        private set
        {
            if (SetProperty(ref _sizeText, value))
                OnPropertyChanged(nameof(HasSize));
        }
    }

    public bool HasSize => SizeText.Length > 0;

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            if (SetProperty(ref _isUpdateAvailable, value))
                OnPropertyChanged(nameof(CanInstall));
        }
    }

    private bool _isUpToDate;
    /// <summary>Проверка прошла и обновлений нет — показываем это явно.</summary>
    public bool IsUpToDate
    {
        get => _isUpToDate;
        private set => SetProperty(ref _isUpToDate, value);
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanCheck));
                OnPropertyChanged(nameof(CanInstall));
            }
        }
    }

    public bool CanCheck => !IsBusy;

    public bool CanInstall => IsUpdateAvailable && !IsBusy;

    private string _stageText = "";
    /// <summary>Что происходит прямо сейчас: качаем, проверяем, распаковываем.</summary>
    public string StageText
    {
        get => _stageText;
        private set
        {
            if (SetProperty(ref _stageText, value))
                OnPropertyChanged(nameof(HasStage));
        }
    }

    public bool HasStage => StageText.Length > 0;

    private int _percent;
    public int Percent
    {
        get => _percent;
        private set => SetProperty(ref _percent, value);
    }

    private bool _isPercentKnown;
    /// <summary>У проверки и распаковки доли нет — полоса крутится без числа.</summary>
    public bool IsPercentKnown
    {
        get => _isPercentKnown;
        private set
        {
            if (SetProperty(ref _isPercentKnown, value))
                OnPropertyChanged(nameof(IsPercentUnknown));
        }
    }

    public bool IsPercentUnknown => !IsPercentKnown;

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public bool HasStatus => StatusMessage.Length > 0;

    // ===================== Проверка =====================

    private async Task CheckAsync()
    {
        IsBusy = true;

        Reset();

        try
        {
            var result = await _updates.CheckAsync(CancellationToken.None);

            _result = result;

            AvailableVersion = result.AvailableVersion ?? "";

            if (result.IsFailed)
            {
                ShowError(Loc.F("Updates_CheckFailed", result.Error));
                return;
            }

            IsUpdateAvailable = result.IsUpdateAvailable;
            IsUpToDate = !result.IsUpdateAvailable;

            if (!result.IsUpdateAvailable)
            {
                ShowInfo(Loc.F("Updates_UpToDate", result.CurrentVersion));
                return;
            }

            Notes = (result.Notes ?? "").Trim();
            SizeText = result.SizeBytes > 0 ? FormatSize(result.SizeBytes) : "";

            ShowInfo(Loc.F("Updates_Found", result.AvailableVersion));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Updates_CheckFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reset()
    {
        IsUpdateAvailable = false;
        IsUpToDate = false;
        Notes = "";
        SizeText = "";
        StageText = "";
        StatusMessage = "";
        Percent = 0;
    }

    // ===================== Установка =====================

    private async Task InstallAsync()
    {
        if (_result is null || !_result.IsUpdateAvailable)
            return;

        // Установка закрывает приложение — спрашиваем явно
        var answer = MessageBox.Show(
            Loc.F("Updates_ConfirmText", _result.AvailableVersion, _result.CurrentVersion),
            Loc.T("Updates_ConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        StatusMessage = "";

        var progress = new Progress<UpdateProgress>(Report);

        try
        {
            await _updates.InstallAsync(progress, CancellationToken.None);

            // Дальше работает скрипт: он ждёт нашего выхода, подменяет файлы
            // и запускает приложение заново
            ShowInfo(Loc.T("Updates_Restarting"));

            await Task.Delay(1200);

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StageText = "";

            ShowError(Loc.F("Updates_InstallFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Report(UpdateProgress progress)
    {
        StageText = progress.Stage switch
        {
            UpdateStage.Downloading => Loc.T("Updates_StageDownloading"),
            UpdateStage.Verifying   => Loc.T("Updates_StageVerifying"),
            UpdateStage.Extracting  => Loc.T("Updates_StageExtracting"),
            UpdateStage.Preparing   => Loc.T("Updates_StagePreparing"),
            UpdateStage.Restarting  => Loc.T("Updates_StageRestarting"),
            _                       => "",
        };

        IsPercentKnown = progress.Percent is not null;

        if (progress.Percent is { } percent)
            Percent = percent;
    }

    /// <summary>Размер архива человеческими единицами.</summary>
    private static string FormatSize(long bytes)
    {
        const long mb = 1024 * 1024;

        return bytes >= mb
            ? Loc.F("Updates_SizeMb", (bytes / (double)mb).ToString("N1", Loc.Instance.Culture))
            : Loc.F("Updates_SizeKb", (bytes / 1024d).ToString("N0", Loc.Instance.Culture));
    }

    private void ShowInfo(string message)
    {
        StatusIsError = false;
        StatusMessage = message;
    }

    private void ShowError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
