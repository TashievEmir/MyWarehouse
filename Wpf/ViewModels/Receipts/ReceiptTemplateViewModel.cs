using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Receipts;
using Wpf.Common;
using Wpf.Services;

using Wpf.Localization;

namespace Wpf.ViewModels.Receipts;

/// <summary>Блок чека в редакторе.</summary>
public class ReceiptBlockItem : ViewModelBase
{
    public string Key { get; }
    public string Name { get; }
    public string Hint { get; }
    public bool IsLocked { get; }

    /// <summary>Обязательный блок не переключается.</summary>
    public bool CanToggle => !IsLocked;

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            // Обязательный блок выключить нельзя
            if (IsLocked)
                value = true;

            SetProperty(ref _isEnabled, value);
        }
    }

    public ReceiptBlockItem(ReceiptBlockResponse block)
    {
        Key = block.Key;
        Name = block.Name;
        Hint = block.Hint;
        IsLocked = block.IsLocked;
        _isEnabled = block.IsEnabled;
    }

    public ReceiptBlockState ToState() => new() { Key = Key, IsEnabled = IsEnabled };
}

/// <summary>
/// Редактор шаблона чека: шапка, порядок блоков и подвал.
/// Любое изменение сразу перерисовывает предпросмотр ленты.
/// </summary>
public class ReceiptTemplateViewModel : ViewModelBase
{
    private static readonly CultureInfo Russian = new("ru-RU");

    private readonly IReceiptTemplateService _templates;
    private readonly SessionService _session;

    public ObservableCollection<ReceiptBlockItem> Blocks { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand TestPrintCommand { get; }

    public ReceiptTemplateViewModel(IReceiptTemplateService templates, SessionService session)
    {
        _templates = templates;
        _session = session;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        MoveUpCommand = new RelayCommand<ReceiptBlockItem>(block => Move(block, -1));
        MoveDownCommand = new RelayCommand<ReceiptBlockItem>(block => Move(block, +1));
        TestPrintCommand = new RelayCommand(() => ShowInfo(Loc.T("Template_TestPrintInfo")));

        Blocks.CollectionChanged += (_, __) => RefreshPreview();
    }

    // ===================== Поля шапки =====================

    private string _shopName = "";
    public string ShopName
    {
        get => _shopName;
        set { if (SetProperty(ref _shopName, value)) RefreshPreview(); }
    }

    private string _tin = "";
    public string Tin
    {
        get => _tin;
        set { if (SetProperty(ref _tin, value)) RefreshPreview(); }
    }

    private string _address = "";
    public string Address
    {
        get => _address;
        set { if (SetProperty(ref _address, value)) RefreshPreview(); }
    }

    private string _footerText = "";
    public string FooterText
    {
        get => _footerText;
        set { if (SetProperty(ref _footerText, value)) RefreshPreview(); }
    }

    public string RoleChip => _session.User?.Roles.FirstOrDefault() switch
    {
        "Admin"   => Loc.T("Template_Role_Admin"),
        "Manager" => Loc.T("Template_Role_Manager"),
        _         => Loc.T("Template_Role_Viewer"),
    };

    // ===================== Предпросмотр =====================

    private string _preview = "";
    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    /// <summary>Собирает ленту так же, как её увидит принтер: только включённые блоки.</summary>
    private void RefreshPreview()
    {
        var enabled = Blocks.Where(b => b.IsEnabled).Select(b => b.Key).ToHashSet();

        var text = new StringBuilder();

        if (enabled.Contains("logo"))
            text.AppendLine(ShopName.ToUpper(Russian));

        if (enabled.Contains("address"))
        {
            if (!string.IsNullOrWhiteSpace(Address)) text.AppendLine(Address);
            if (!string.IsNullOrWhiteSpace(Tin)) text.AppendLine(Loc.F("Template_Preview_Tin", Tin));
        }

        text.AppendLine("--------------------------------");

        if (enabled.Contains("number"))
            text.AppendLine(Loc.F("Template_Preview_Number", DateTime.Now.ToString("dd.MM.yyyy HH:mm", Loc.Instance.Culture)));

        if (enabled.Contains("cashier"))
            text.AppendLine(Loc.T("Template_Preview_Cashier"));

        text.AppendLine("--------------------------------");

        text.AppendLine(Loc.T("Template_Preview_Item1"));
        if (enabled.Contains("barcode"))
            text.AppendLine("  4870001234567");
        text.AppendLine(Loc.T("Template_Preview_Item1Line"));

        text.AppendLine(Loc.T("Template_Preview_Item2"));
        if (enabled.Contains("barcode"))
            text.AppendLine("  4870007654321");
        text.AppendLine(Loc.T("Template_Preview_Item2Line"));

        text.AppendLine("--------------------------------");
        text.AppendLine(Loc.T("Template_Preview_Sum"));
        text.AppendLine(Loc.T("Template_Preview_Discount"));
        text.AppendLine(Loc.T("Template_Preview_Total"));
        text.AppendLine(Loc.T("Template_Preview_Cash"));
        text.AppendLine(Loc.T("Template_Preview_Change"));

        if (enabled.Contains("customer"))
        {
            text.AppendLine("--------------------------------");
            text.AppendLine(Loc.T("Template_Preview_Customer"));
            text.AppendLine(Loc.T("Template_Preview_Debt"));
        }

        if (enabled.Contains("qr"))
        {
            text.AppendLine("--------------------------------");
            text.AppendLine(Loc.T("Template_Preview_Qr"));
        }

        if (!string.IsNullOrWhiteSpace(FooterText))
        {
            text.AppendLine("--------------------------------");
            text.AppendLine(FooterText);
        }

        Preview = text.ToString().TrimEnd();
    }

    // ===================== Порядок блоков =====================

    private void Move(ReceiptBlockItem block, int offset)
    {
        var index = Blocks.IndexOf(block);
        var target = index + offset;

        if (index < 0 || target < 0 || target >= Blocks.Count)
            return;

        Blocks.Move(index, target);
    }

    // ===================== Загрузка и сохранение =====================

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            var template = await _templates.GetAsync(CancellationToken.None);

            ShopName = template.ShopName;
            Tin = template.Tin ?? "";
            Address = template.Address ?? "";
            FooterText = template.FooterText ?? "";

            foreach (var block in Blocks)
                block.PropertyChanged -= OnBlockChanged;

            Blocks.Clear();

            foreach (var block in template.Blocks)
            {
                var item = new ReceiptBlockItem(block);

                item.PropertyChanged += OnBlockChanged;

                Blocks.Add(item);
            }

            StatusMessage = "";

            OnPropertyChanged(nameof(RoleChip));

            RefreshPreview();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Template_LoadFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnBlockChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ShopName))
        {
            ShowError(Loc.T("Template_NeedShopName"));
            return;
        }

        if (_session.User is null)
        {
            ShowError(Loc.T("Template_NoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            await _templates.SaveAsync(new SaveReceiptTemplateRequest
            {
                UserId     = _session.User.UserId,
                ShopName   = ShopName,
                Tin        = Tin,
                Address    = Address,
                FooterText = FooterText,
                Blocks     = Blocks.Select(b => b.ToState()).ToList(),
            }, CancellationToken.None);

            ShowInfo(Loc.T("Template_Saved"));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

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
