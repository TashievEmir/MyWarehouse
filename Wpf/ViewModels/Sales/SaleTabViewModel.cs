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

using Wpf.Localization;

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

    /// <summary>После смены языка перерисовываем вычисляемые подписи вкладки.</summary>
    public void RefreshTexts() => OnPropertyChanged(string.Empty);

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
                ShowError(Loc.F("Sales_BarcodeNotFound", barcode));
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
                line.UpdateStock(found.InStock);
                line.Quantity++;
            }

            RaiseTotals();

            if (line.ExceedsStock)
                ShowError(Loc.F("Sales_OnlyInStock", found.Name, found.InStock));
            else if (line.HasNoPrice)
                ShowError(Loc.F("Sales_LineNoPrice", found.Name));
            else
                ShowInfo(Loc.F("Sales_LineAdded", found.Name, line.Quantity));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Receiving_ScanError", ex.Message));
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
        ? Loc.T("Sales_CartEmpty")
        : Loc.F("Sales_CartSummary", LinesCount, ItemsCount);

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
                NewCustomerEmail = "";
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

    private string _newCustomerEmail = "";
    /// <summary>Почта обязательна: на неё уходят напоминания о долге.</summary>
    public string NewCustomerEmail
    {
        get => _newCustomerEmail;
        set => SetProperty(ref _newCustomerEmail, value);
    }

    private DateTime? _dueDate = DateTime.Today.AddDays(7);
    /// <summary>Когда клиент обещает закрыть долг — до этой даты просрочки нет.</summary>
    public DateTime? DueDate
    {
        get => _dueDate;
        set => SetProperty(ref _dueDate, value);
    }

    private async Task CreateCustomerAsync()
    {
        var name = NewCustomerName.Trim();

        if (name.Length == 0)
        {
            ShowError(Loc.T("Sales_NeedCustomerName"));
            return;
        }

        var email = NewCustomerEmail.Trim();

        if (email.Length == 0)
        {
            ShowError(Loc.T("Sales_NeedCustomerEmail"));
            return;
        }

        if (!LooksLikeEmail(email))
        {
            ShowError(Loc.T("Sales_BadEmail"));
            return;
        }

        var existing = Customers.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null)
        {
            SelectedCustomer = existing;
            IsCreatingCustomer = false;

            ShowInfo(Loc.F("Sales_CustomerExists", existing.Name));
            return;
        }

        try
        {
            var phone = NewCustomerPhone.Trim();

            var id = await _customerService.CreateAsync(
                new CreateCustomerRequest
                {
                    Name = name,
                    Phone = phone.Length == 0 ? null : phone,
                    Email = email,
                },
                CancellationToken.None);

            var created = await _customerService.GetAsync(id, CancellationToken.None);

            if (created is null)
            {
                ShowError(Loc.T("Sales_CustomerCreateFailed"));
                return;
            }

            Customers.Add(created);

            SelectedCustomer = created;
            IsCreatingCustomer = false;

            ShowInfo(Loc.F("Sales_CustomerAdded", created.Name));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Sales_CustomerCreateError", ex.Message));
        }
    }

    /// <summary>Грубая проверка почты: полноценная валидация тут излишня.</summary>
    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');

        return at > 0
               && at < value.Length - 1
               && value.IndexOf('.', at) > at + 1
               && !value.EndsWith('.')
               && !value.Contains(' ');
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
            ShowError(Loc.T("Sales_CartEmptyScan"));
            return;
        }

        if (_session.User is null)
        {
            ShowError(Loc.T("Sales_NoLogin"));
            return;
        }

        if (Lines.FirstOrDefault(l => l.HasNoPrice) is { } noPrice)
        {
            ShowError(Loc.F("Sales_ProductNoPrice", noPrice.Name));
            return;
        }

        if (Lines.FirstOrDefault(l => l.ExceedsStock) is { } noStock)
        {
            ShowError(Loc.F("Sales_ProductNoStock", noStock.Name, noStock.InStock));
            return;
        }

        if (IsCash && CashGiven < Total)
        {
            ShowError(Loc.F("Sales_CashTooLittle", CashGiven.ToString("N2", Loc.Instance.Culture), Total.ToString("N2", Loc.Instance.Culture)));
            return;
        }

        if (IsCredit && SelectedCustomer is null)
        {
            ShowError(Loc.T("Sales_CreditNeedCustomer"));
            return;
        }

        if (IsCredit && DueDate is null)
        {
            ShowError(Loc.T("Sales_NeedDueDate"));
            return;
        }

        if (IsCredit && DueDate!.Value.Date < DateTime.Today)
        {
            ShowError(Loc.T("Sales_DueDatePast"));
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
                // Срок считаем до конца дня: платёж «сегодня» не должен стать просрочкой
                DueDate = IsCredit ? new DateTimeOffset(DueDate!.Value.Date.AddDays(1).AddTicks(-1)) : null,
                Items = Lines.Select(l => l.ToRequest()).ToList()
            };

            var change = IsCash ? Change : 0m;
            var debt = IsCredit ? DebtAmount : 0m;

            var saleId = await _sales.CreateSaleAsync(request, CancellationToken.None);

            Reset();

            var message = Loc.F("Sales_Closed", saleId);

            if (change > 0)
                message += Loc.F("Sales_ClosedChange", change.ToString("N2", Loc.Instance.Culture));

            if (debt > 0)
                message += Loc.F("Sales_ClosedDebt", debt.ToString("N2", Loc.Instance.Culture));

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
