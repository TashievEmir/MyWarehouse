using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Customers;
using Application.DTOs.Sales;
using Domain.Enums;
using Wpf.Common;
using Wpf.Services;

namespace Wpf.ViewModels.Sales;

/// <summary>Скидка задаётся либо процентом от суммы, либо суммой в сомах.</summary>
public enum DiscountMode
{
    Percent,
    Amount
}

/// <summary>
/// Одна вкладка кассы — независимый чек. Вкладок может быть несколько,
/// чтобы отложить клиента с проблемной оплатой и обслужить следующего.
/// </summary>
public class SaleTabViewModel : ViewModelBase
{
    private readonly IProductService _products;
    private readonly ISalesService _sales;
    private readonly ICustomerService _customerService;
    private readonly SessionService _session;

    public string Title { get; }

    public ObservableCollection<SaleLineItem> Lines { get; } = new();

    /// <summary>Список клиентов общий для всех вкладок.</summary>
    public ObservableCollection<CustomerResponse> Customers { get; }

    public ICommand ScanCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SetDiscountCommand { get; }
    public ICommand StartNewCustomerCommand { get; }
    public ICommand CreateCustomerCommand { get; }
    public ICommand CancelNewCustomerCommand { get; }
    public ICommand CloseDealCommand { get; }

    public SaleTabViewModel(
        string title,
        IProductService products,
        ISalesService sales,
        ICustomerService customerService,
        SessionService session,
        ObservableCollection<CustomerResponse> customers)
    {
        Title = title;

        _products = products;
        _sales = sales;
        _customerService = customerService;
        _session = session;

        Customers = customers;

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        RemoveLineCommand = new RelayCommand<SaleLineItem>(RemoveLine);
        ClearCommand = new RelayCommand(Clear);
        SetDiscountCommand = new RelayCommand<string>(SetDiscount);
        StartNewCustomerCommand = new RelayCommand(() => IsCreatingCustomer = true);
        CreateCustomerCommand = new AsyncRelayCommand(CreateCustomerAsync);
        CancelNewCustomerCommand = new RelayCommand(() => IsCreatingCustomer = false);
        CloseDealCommand = new AsyncRelayCommand(CloseDealAsync);

        Lines.CollectionChanged += OnLinesChanged;
    }

    // ===================== Сканирование =====================

    private string _barcodeInput = "";
    public string BarcodeInput
    {
        get => _barcodeInput;
        set => SetProperty(ref _barcodeInput, value);
    }

    private async Task ScanAsync()
    {
        var barcode = BarcodeInput.Trim();

        BarcodeInput = "";

        if (barcode.Length == 0)
            return;

        try
        {
            var found = await _products.FindByBarcodeAsync(barcode, CancellationToken.None);

            if (found is null)
            {
                ShowError($"Штрихкод {barcode} не найден — товар не заведён");
                return;
            }

            var line = Lines.FirstOrDefault(l => l.ProductId == found.ProductId);

            if (line is null)
            {
                line = new SaleLineItem(found);
                line.PropertyChanged += OnLineChanged;

                Lines.Insert(0, line);
            }
            else
            {
                line.Quantity++;
            }

            RaiseTotals();

            if (line.ExceedsStock)
                ShowError($"{found.Name}: на складе только {found.InStock} шт.");
            else if (line.HasNoPrice)
                ShowError($"{found.Name}: не указана цена — введите её в строке");
            else
                ShowInfo($"{found.Name} · {line.Quantity} шт.");
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка при сканировании: {ex.Message}");
        }
    }

    private void RemoveLine(SaleLineItem line)
    {
        line.PropertyChanged -= OnLineChanged;

        Lines.Remove(line);
    }

    private void Clear()
    {
        foreach (var line in Lines)
            line.PropertyChanged -= OnLineChanged;

        Lines.Clear();

        StatusMessage = "";
    }

    // ===================== Суммы =====================

    public decimal Subtotal => Lines.Sum(l => l.Sum);

    private DiscountMode _discountMode = DiscountMode.Percent;
    public DiscountMode SelectedDiscountMode
    {
        get => _discountMode;
        set
        {
            if (!SetProperty(ref _discountMode, value))
                return;

            OnPropertyChanged(nameof(IsPercentDiscount));
            OnPropertyChanged(nameof(IsAmountDiscount));
            RaiseTotals();
        }
    }

    public bool IsPercentDiscount
    {
        get => SelectedDiscountMode == DiscountMode.Percent;
        set { if (value) SelectedDiscountMode = DiscountMode.Percent; }
    }

    public bool IsAmountDiscount
    {
        get => SelectedDiscountMode == DiscountMode.Amount;
        set { if (value) SelectedDiscountMode = DiscountMode.Amount; }
    }

    private decimal _discountInput;
    public decimal DiscountInput
    {
        get => _discountInput;
        set
        {
            if (SetProperty(ref _discountInput, value < 0 ? 0 : value))
                RaiseTotals();
        }
    }

    public decimal DiscountAmount => SelectedDiscountMode == DiscountMode.Percent
        ? Math.Round(Subtotal * Math.Min(DiscountInput, 100m) / 100m, 2)
        : Math.Min(DiscountInput, Subtotal);

    public decimal Total => Subtotal - DiscountAmount;

    public bool HasLines => Lines.Count > 0;

    public int LinesCount => Lines.Count;

    public int ItemsCount => Lines.Sum(l => l.Quantity);

    public string CartSummary => Lines.Count == 0
        ? "Чек пуст"
        : $"{LinesCount} поз. · {ItemsCount} шт.";

    private void SetDiscount(string value)
    {
        if (!decimal.TryParse(value, out var percent))
            return;

        SelectedDiscountMode = DiscountMode.Percent;
        DiscountInput = percent;
    }

    // ===================== Оплата =====================

    private PaymentMethod _payment = PaymentMethod.Cash;
    public PaymentMethod SelectedPayment
    {
        get => _payment;
        set
        {
            if (!SetProperty(ref _payment, value))
                return;

            OnPropertyChanged(nameof(IsCash));
            OnPropertyChanged(nameof(IsCard));
            OnPropertyChanged(nameof(IsTransfer));
            OnPropertyChanged(nameof(IsCredit));

            RaiseTotals();
        }
    }

    public bool IsCash
    {
        get => SelectedPayment == PaymentMethod.Cash;
        set { if (value) SelectedPayment = PaymentMethod.Cash; }
    }

    public bool IsCard
    {
        get => SelectedPayment == PaymentMethod.Card;
        set { if (value) SelectedPayment = PaymentMethod.Card; }
    }

    public bool IsTransfer
    {
        get => SelectedPayment == PaymentMethod.Transfer;
        set { if (value) SelectedPayment = PaymentMethod.Transfer; }
    }

    public bool IsCredit
    {
        get => SelectedPayment == PaymentMethod.Credit;
        set { if (value) SelectedPayment = PaymentMethod.Credit; }
    }

    private decimal _cashGiven;
    /// <summary>Сколько наличных дал клиент.</summary>
    public decimal CashGiven
    {
        get => _cashGiven;
        set
        {
            if (SetProperty(ref _cashGiven, value < 0 ? 0 : value))
                OnPropertyChanged(nameof(Change));
        }
    }

    public decimal Change => CashGiven > Total ? CashGiven - Total : 0m;

    private decimal _prepaidAmount;
    /// <summary>Сколько внесено сразу при продаже в долг.</summary>
    public decimal PrepaidAmount
    {
        get => _prepaidAmount;
        set
        {
            if (SetProperty(ref _prepaidAmount, value < 0 ? 0 : value))
                OnPropertyChanged(nameof(DebtAmount));
        }
    }

    public decimal DebtAmount => Total > PrepaidAmount ? Total - PrepaidAmount : 0m;

    // ===================== Клиент =====================

    private CustomerResponse? _selectedCustomer;
    public CustomerResponse? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetProperty(ref _selectedCustomer, value);
    }

    private bool _isCreatingCustomer;
    public bool IsCreatingCustomer
    {
        get => _isCreatingCustomer;
        set
        {
            if (SetProperty(ref _isCreatingCustomer, value) && value)
            {
                NewCustomerName = "";
                NewCustomerPhone = "";
            }
        }
    }

    private string _newCustomerName = "";
    public string NewCustomerName
    {
        get => _newCustomerName;
        set => SetProperty(ref _newCustomerName, value);
    }

    private string _newCustomerPhone = "";
    public string NewCustomerPhone
    {
        get => _newCustomerPhone;
        set => SetProperty(ref _newCustomerPhone, value);
    }

    private async Task CreateCustomerAsync()
    {
        var name = NewCustomerName.Trim();

        if (name.Length == 0)
        {
            ShowError("Введите имя клиента");
            return;
        }

        var existing = Customers.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null)
        {
            SelectedCustomer = existing;
            IsCreatingCustomer = false;

            ShowInfo($"Клиент «{existing.Name}» уже есть — выбран он");
            return;
        }

        try
        {
            var phone = NewCustomerPhone.Trim();

            var id = await _customerService.CreateAsync(
                new CreateCustomerRequest { Name = name, Phone = phone.Length == 0 ? null : phone },
                CancellationToken.None);

            var created = await _customerService.GetAsync(id, CancellationToken.None);

            if (created is null)
            {
                ShowError("Не удалось создать клиента");
                return;
            }

            Customers.Add(created);

            SelectedCustomer = created;
            IsCreatingCustomer = false;

            ShowInfo($"Клиент «{created.Name}» добавлен");
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось создать клиента: {ex.Message}");
        }
    }

    // ===================== Закрытие сделки =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanCloseDeal));
        }
    }

    public bool CanCloseDeal => !IsBusy && Lines.Count > 0;

    private async Task CloseDealAsync()
    {
        if (Lines.Count == 0)
        {
            ShowError("Чек пуст — отсканируйте товары");
            return;
        }

        if (_session.User is null)
        {
            ShowError("Продажу нельзя закрыть: не выполнен вход в систему");
            return;
        }

        if (Lines.FirstOrDefault(l => l.HasNoPrice) is { } noPrice)
        {
            ShowError($"«{noPrice.Name}»: укажите цену");
            return;
        }

        if (Lines.FirstOrDefault(l => l.ExceedsStock) is { } noStock)
        {
            ShowError($"«{noStock.Name}»: на складе только {noStock.InStock} шт.");
            return;
        }

        if (IsCash && CashGiven < Total)
        {
            ShowError($"Получено {CashGiven:N2} — меньше суммы чека {Total:N2}");
            return;
        }

        if (IsCredit && SelectedCustomer is null)
        {
            ShowError("Для продажи в долг выберите клиента");
            return;
        }

        IsBusy = true;

        try
        {
            var request = new CreateSaleRequest
            {
                UserId = _session.User.UserId,
                CustomerId = IsCredit ? SelectedCustomer!.Id : null,
                DiscountAmount = DiscountAmount,
                PaymentMethod = SelectedPayment,
                PaidAmount = IsCredit ? PrepaidAmount : Total,
                Items = Lines.Select(l => l.ToRequest()).ToList()
            };

            var change = IsCash ? Change : 0m;
            var debt = IsCredit ? DebtAmount : 0m;

            var saleId = await _sales.CreateSaleAsync(request, CancellationToken.None);

            Reset();

            var message = $"Чек №{saleId} закрыт";

            if (change > 0)
                message += $" · сдача {change:N2}";

            if (debt > 0)
                message += $" · долг {debt:N2}";

            ShowInfo(message);
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

    /// <summary>Готовит вкладку к следующему клиенту.</summary>
    private void Reset()
    {
        Clear();

        DiscountInput = 0;
        SelectedDiscountMode = DiscountMode.Percent;
        SelectedPayment = PaymentMethod.Cash;
        CashGiven = 0;
        PrepaidAmount = 0;
        SelectedCustomer = null;
        IsCreatingCustomer = false;
    }

    // ===================== Состояние =====================

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

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseTotals();

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleLineItem.Sum) or nameof(SaleLineItem.Quantity))
            RaiseTotals();
    }

    private void RaiseTotals()
    {
        // правку чека считаем ответом на замечание: старая ошибка больше не актуальна
        if (StatusIsError)
            StatusMessage = "";

        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(DebtAmount));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(LinesCount));
        OnPropertyChanged(nameof(ItemsCount));
        OnPropertyChanged(nameof(CartSummary));
        OnPropertyChanged(nameof(CanCloseDeal));
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
