using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Wpf.Common;
using Wpf.Services;

namespace Wpf.ViewModels.Statistics;

/// <summary>
/// Долги клиентов: продажи, закрытые не полностью. Здесь же принимают оплату —
/// целиком или частями.
/// </summary>
public class DebtsViewModel : ViewModelBase
{
    private readonly ISalesService _sales;
    private readonly SessionService _session;

    public ObservableCollection<DebtItem> Debts { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand PayFullCommand { get; }
    public ICommand PayCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public DebtsViewModel(ISalesService sales, SessionService session)
    {
        _sales = sales;
        _session = session;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new RelayCommand<DebtItem>(Select);
        CloseCommand = new RelayCommand(CloseSelection);
        PayFullCommand = new RelayCommand(() => PaymentAmount = Selected?.Debt ?? 0m);
        PayCommand = new AsyncRelayCommand(PayAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");
    }

    // ===================== Список =====================

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearch));
                _ = LoadAsync();
            }
        }
    }

    public bool HasSearch => SearchText.Length > 0;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private decimal _totalDebt;
    public decimal TotalDebt
    {
        get => _totalDebt;
        private set => SetProperty(ref _totalDebt, value);
    }

    public string CountText => Debts.Count == 0 ? "" : $"{Debts.Count} долг(ов)";

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var debts = await _sales.GetDebtsAsync(SearchText, CancellationToken.None);

            var selectedId = Selected?.SaleId;

            Debts.Clear();

            foreach (var debt in debts)
                Debts.Add(new DebtItem(debt));

            TotalDebt = Debts.Sum(d => d.Debt);
            IsEmpty = Debts.Count == 0;

            // Долг мог закрыться — тогда карточку справа показывать уже нечего
            if (selectedId is { } id)
            {
                Selected = Debts.FirstOrDefault(d => d.SaleId == id);

                if (Selected is null)
                    PaymentAmount = 0m;
            }

            OnPropertyChanged(nameof(CountText));
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить долги: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ===================== Оплата =====================

    private DebtItem? _selected;
    public DebtItem? Selected
    {
        get => _selected;
        private set
        {
            if (SetProperty(ref _selected, value))
                OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => Selected is not null;

    private decimal _paymentAmount;
    public decimal PaymentAmount
    {
        get => _paymentAmount;
        set => SetProperty(ref _paymentAmount, value < 0 ? 0 : value);
    }

    private void Select(DebtItem debt)
    {
        Selected = debt;

        // По умолчанию гасим долг целиком — так делают чаще всего
        PaymentAmount = debt.Debt;

        StatusMessage = "";
    }

    private void CloseSelection()
    {
        Selected = null;
        PaymentAmount = 0m;
    }

    private async Task PayAsync()
    {
        if (Selected is null)
            return;

        if (PaymentAmount <= 0)
        {
            ShowError("Укажите сумму платежа");
            return;
        }

        if (PaymentAmount > Selected.Debt)
        {
            ShowError($"Долг составляет {Selected.Debt:N2} — принять больше нельзя");
            return;
        }

        if (_session.User is null)
        {
            ShowError("Оплату нельзя принять: не выполнен вход в систему");
            return;
        }

        IsBusy = true;

        try
        {
            var amount = PaymentAmount;
            var saleId = Selected.SaleId;
            var rest = Selected.Debt - amount;

            await _sales.RegisterDebtPaymentAsync(saleId, amount, _session.User.UserId, CancellationToken.None);

            await LoadAsync();

            if (rest <= 0)
            {
                CloseSelection();
                ShowInfo($"Долг по чеку №{saleId} закрыт");
            }
            else
            {
                ShowInfo($"Принято {amount:N2} · остаток долга {rest:N2}");
            }
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
