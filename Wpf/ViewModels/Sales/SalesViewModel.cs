using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Customers;
using Wpf.Common;
using Wpf.Services;

namespace Wpf.ViewModels.Sales;

/// <summary>
/// Касса: несколько параллельных чеков во вкладках. Живёт всё время работы
/// приложения, поэтому открытые чеки не теряются при переходе на другую страницу.
/// </summary>
public class SalesViewModel : ViewModelBase
{
    private readonly IProductService _products;
    private readonly ISalesService _sales;
    private readonly ICustomerService _customerService;
    private readonly SessionService _session;

    private int _tabCounter;

    public ObservableCollection<SaleTabViewModel> Tabs { get; } = new();

    /// <summary>Клиенты общие для всех вкладок: заведённый в одной виден в остальных.</summary>
    public ObservableCollection<CustomerResponse> Customers { get; } = new();

    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SelectTabCommand { get; }

    public SalesViewModel(
        IProductService products,
        ISalesService sales,
        ICustomerService customerService,
        SessionService session)
    {
        _products = products;
        _sales = sales;
        _customerService = customerService;
        _session = session;

        NewTabCommand = new RelayCommand(AddTab);
        CloseTabCommand = new RelayCommand<SaleTabViewModel>(CloseTab);
        SelectTabCommand = new RelayCommand<SaleTabViewModel>(tab => SelectedTab = tab);

        AddTab();

        _ = LoadCustomersAsync();
    }

    private SaleTabViewModel? _selectedTab;
    public SaleTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public bool CanCloseTab => Tabs.Count > 1;

    private void AddTab()
    {
        _tabCounter++;

        var tab = new SaleTabViewModel(
            $"Чек {_tabCounter}",
            _products,
            _sales,
            _customerService,
            _session,
            Customers);

        Tabs.Add(tab);

        SelectedTab = tab;

        OnPropertyChanged(nameof(CanCloseTab));
    }

    private void CloseTab(SaleTabViewModel tab)
    {
        if (Tabs.Count == 1)
            return;

        if (tab.Lines.Count > 0)
        {
            var answer = MessageBox.Show(
                $"В «{tab.Title}» осталось {tab.CartSummary}. Закрыть вкладку и потерять чек?",
                "Закрытие вкладки",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;
        }

        var index = Tabs.IndexOf(tab);

        Tabs.Remove(tab);

        SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];

        OnPropertyChanged(nameof(CanCloseTab));
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customerService.GetAllAsync(CancellationToken.None);

            Customers.Clear();

            foreach (var customer in customers.OrderBy(c => c.Name))
                Customers.Add(customer);
        }
        catch
        {
            // список клиентов нужен только для продажи в долг —
            // не мешаем кассиру работать, если он не загрузился
        }
    }
}
