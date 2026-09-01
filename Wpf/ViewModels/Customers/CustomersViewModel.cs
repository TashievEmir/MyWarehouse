using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Customers;
using Wpf.Common;
using Wpf.Localization;

namespace Wpf.ViewModels.Customers;

/// <summary>Строка списка клиентов.</summary>
public class CustomerListItem
{
    public long Id { get; }
    public string Name { get; }
    public string Phone { get; }
    public string Email { get; }

    /// <summary>Непогашенный остаток по всем чекам клиента.</summary>
    public decimal Debt { get; }

    public bool HasDebt => Debt > 0;

    public string DebtText => Debt.ToString("N2", Loc.Instance.Culture);

    /// <summary>Без почты клиент не попадёт в рассылку напоминаний.</summary>
    public bool HasEmail => Email.Length > 0;

    public string PhoneText => Phone.Length > 0 ? Phone : "—";

    public string EmailText => Email.Length > 0 ? Email : Loc.T("Customers_NoEmail");

    public string Address { get; }
    public string Note { get; }

    /// <summary>Заметку показываем значком в списке — текст уходит в подсказку.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Когда клиента завели — только для показа, не правится.</summary>
    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public CustomerListItem(CustomerResponse customer, decimal debt)
    {
        Id = customer.Id;
        Name = customer.Name;
        Phone = customer.Phone ?? "";
        Email = customer.Email ?? "";
        Address = customer.Address ?? "";
        Note = customer.Note ?? "";
        CreatedAt = customer.CreatedAt;
        Debt = debt;
    }
}

/// <summary>
/// Клиенты: список слева, карточка справа. Именно этим людям уходят
/// напоминания о долге, поэтому почта здесь важнее телефона.
/// </summary>
public class CustomersViewModel : ViewModelBase
{
    private readonly ICustomerService _customers;
    private readonly ISalesService _sales;

    public ObservableCollection<CustomerListItem> Items { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public CustomersViewModel(ICustomerService customers, ISalesService sales)
    {
        _customers = customers;
        _sales = sales;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new RelayCommand<CustomerListItem>(Select);
        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        CloseCommand = new RelayCommand(CloseEditor);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(string.Empty);
            _ = LoadAsync();
        };
    }

    // ===================== Список =====================

    /// <summary>Полный список: поиск фильтрует его в памяти, клиентов немного.</summary>
    private List<CustomerListItem> _all = new();

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearch));
                ApplyFilter();
            }
        }
    }

    public bool HasSearch => SearchText.Length > 0;

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public string CountText => Items.Count == 0 ? "" : Loc.F("Customers_Count", Items.Count);

    /// <summary>Сколько клиентов останутся без писем — почта не заполнена.</summary>
    public string NoEmailHint
    {
        get
        {
            var without = _all.Count(c => !c.HasEmail);

            return without == 0 ? "" : Loc.F("Customers_NoEmailCount", without);
        }
    }

    public bool HasNoEmailHint => NoEmailHint.Length > 0;

    private void ApplyFilter()
    {
        var query = SearchText.Trim();

        var filtered = query.Length == 0
            ? _all
            : _all.Where(c =>
                c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || c.Phone.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || c.Email.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || c.Address.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        Items.Clear();

        foreach (var item in filtered)
            Items.Add(item);

        IsEmpty = Items.Count == 0;

        OnPropertyChanged(nameof(CountText));
    }

    // ===================== Карточка =====================

    private bool _hasEditor;
    public bool HasEditor
    {
        get => _hasEditor;
        private set => SetProperty(ref _hasEditor, value);
    }

    private long _editedId;
    /// <summary>0 — заводим нового клиента.</summary>
    public long EditedId
    {
        get => _editedId;
        private set
        {
            if (SetProperty(ref _editedId, value))
            {
                OnPropertyChanged(nameof(IsNew));
                OnPropertyChanged(nameof(IsExisting));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    public bool IsNew => EditedId == 0;

    /// <summary>Карточка открыта на сохранённом клиенте — можно удалять.</summary>
    public bool IsExisting => !IsNew;

    public string EditorTitle => IsNew
        ? Loc.T("Customers_NewTitle")
        : (Name.Trim().Length > 0 ? Name : Loc.T("Customers_NewTitle"));

    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(EditorTitle));
        }
    }

    private string _phone = "";
    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    private string _email = "";
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _address = "";
    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    private string _note = "";
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    private decimal _editedDebt;
    /// <summary>Долг выбранного клиента — справочно, правится погашением в «Долгах».</summary>
    public decimal EditedDebt
    {
        get => _editedDebt;
        private set
        {
            if (SetProperty(ref _editedDebt, value))
            {
                OnPropertyChanged(nameof(HasEditedDebt));
                OnPropertyChanged(nameof(EditedDebtText));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(DeleteHint));
            }
        }
    }

    private string _createdAtText = "";
    /// <summary>Дата регистрации выбранного клиента — справочно.</summary>
    public string CreatedAtText
    {
        get => _createdAtText;
        private set
        {
            if (SetProperty(ref _createdAtText, value))
                OnPropertyChanged(nameof(HasCreatedAt));
        }
    }

    public bool HasCreatedAt => CreatedAtText.Length > 0;

    public bool HasEditedDebt => EditedDebt > 0;

    public string EditedDebtText => EditedDebt.ToString("N2", Loc.Instance.Culture);

    /// <summary>Клиента с непогашенным долгом удалять нельзя — потеряется история.</summary>
    public bool CanDelete => !IsNew && !HasEditedDebt;

    public string DeleteHint => HasEditedDebt
        ? Loc.T("Customers_DeleteBlocked")
        : Loc.T("Customers_DeleteHint");

    private void Select(CustomerListItem item)
    {
        if (item is null)
            return;

        EditedId = item.Id;
        Name = item.Name;
        Phone = item.Phone;
        Email = item.Email;
        Address = item.Address;
        Note = item.Note;
        EditedDebt = item.Debt;
        CreatedAtText = Loc.F("Customers_Since", item.CreatedAtText);

        StatusMessage = "";
        HasEditor = true;
    }

    private void StartNew()
    {
        EditedId = 0;
        Name = "";
        Phone = "";
        Email = "";
        Address = "";
        Note = "";
        EditedDebt = 0;

        // Нового клиента ещё не завели — показывать нечего
        CreatedAtText = "";

        StatusMessage = "";
        HasEditor = true;
    }

    private void CloseEditor()
    {
        HasEditor = false;
        EditedId = 0;
        StatusMessage = "";
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanSave));
        }
    }

    public bool CanSave => !IsBusy;

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

    // ===================== Загрузка =====================

    public async Task LoadAsync()
    {
        try
        {
            var customers = await _customers.GetAllAsync(CancellationToken.None);

            // Долги приходят по чекам — сводим к сумме на клиента
            var debts = await _sales.GetDebtsAsync(null, CancellationToken.None);

            var byCustomer = debts
                .Where(d => d.CustomerId is not null)
                .GroupBy(d => d.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Debt));

            _all = customers
                .Select(c => new CustomerListItem(c, byCustomer.GetValueOrDefault(c.Id)))
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            ApplyFilter();

            OnPropertyChanged(nameof(NoEmailHint));
            OnPropertyChanged(nameof(HasNoEmailHint));

            // Карточка могла остаться открытой на удалённом клиенте
            if (!IsNew && _all.All(c => c.Id != EditedId))
                CloseEditor();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Customers_LoadFailed", ex.Message));
        }
    }

    // ===================== Сохранение и удаление =====================

    private async Task SaveAsync()
    {
        var name = Name.Trim();

        if (name.Length == 0)
        {
            ShowError(Loc.T("Customers_NeedName"));
            return;
        }

        var email = Email.Trim();

        // Почта необязательна, но кривую лучше не пускать: на неё уйдут напоминания
        if (email.Length > 0 && !LooksLikeEmail(email))
        {
            ShowError(Loc.T("Customers_BadEmail"));
            return;
        }

        var phone = Phone.Trim();
        var address = Address.Trim();
        var note = Note.Trim();

        IsBusy = true;

        try
        {
            long id;

            if (IsNew)
            {
                id = await _customers.CreateAsync(new CreateCustomerRequest
                {
                    Name = name,
                    Phone = phone.Length == 0 ? null : phone,
                    Email = email.Length == 0 ? null : email,
                    Address = address.Length == 0 ? null : address,
                    Note = note.Length == 0 ? null : note,
                }, CancellationToken.None);
            }
            else
            {
                id = EditedId;

                await _customers.UpdateAsync(new UpdateCustomerRequest
                {
                    Id = id,
                    Name = name,
                    Phone = phone.Length == 0 ? null : phone,
                    Email = email.Length == 0 ? null : email,
                    Address = address.Length == 0 ? null : address,
                    Note = note.Length == 0 ? null : note,
                }, CancellationToken.None);
            }

            await LoadAsync();

            if (_all.FirstOrDefault(c => c.Id == id) is { } saved)
                Select(saved);

            ShowInfo(Loc.T("Customers_Saved"));
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

    private async Task DeleteAsync()
    {
        if (IsNew)
            return;

        if (HasEditedDebt)
        {
            ShowError(Loc.T("Customers_DeleteBlocked"));
            return;
        }

        var answer = MessageBox.Show(
            Loc.F("Customers_DeleteConfirm", EditorTitle),
            Loc.T("Customers_DeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            await _customers.DeleteAsync(EditedId, CancellationToken.None);

            CloseEditor();

            await LoadAsync();

            ShowInfo(Loc.T("Customers_Deleted"));
        }
        catch (Exception)
        {
            // За клиентом остались чеки: внешний ключ не даст его удалить.
            // Показываем понятную причину вместо текста от базы.
            ShowError(Loc.T("Customers_DeleteHasHistory"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Грубая проверка почты — та же, что в кассе.</summary>
    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');

        return at > 0
               && at < value.Length - 1
               && value.IndexOf('.', at) > at + 1
               && !value.EndsWith('.')
               && !value.Contains(' ');
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
