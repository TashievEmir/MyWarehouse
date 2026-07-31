using Wpf.Common;

namespace Wpf.ViewModels.Statistics;

/// <summary>
/// Страница «Статистика»: остатки по категориям, долги клиентов и журнал закупок.
/// Раздел перечитывает данные при каждом открытии — цифры не должны быть вчерашними.
/// </summary>
public class StatisticsPageViewModel : ViewModelBase
{
    public StockStatisticsViewModel Stock { get; }
    public DebtsViewModel Debts { get; }
    public PurchaseLogViewModel Purchases { get; }

    public StatisticsPageViewModel(
        StockStatisticsViewModel stock,
        DebtsViewModel debts,
        PurchaseLogViewModel purchases)
    {
        Stock = stock;
        Debts = debts;
        Purchases = purchases;
    }

    public object Current => _section switch
    {
        Section.Debts     => Debts,
        Section.Purchases => Purchases,
        _                 => Stock,
    };

    private enum Section
    {
        Stock,
        Debts,
        Purchases,
    }

    private Section _section = Section.Stock;

    public bool IsStockSelected
    {
        get => _section == Section.Stock;
        set { if (value) Switch(Section.Stock); }
    }

    public bool IsDebtsSelected
    {
        get => _section == Section.Debts;
        set { if (value) Switch(Section.Debts); }
    }

    public bool IsPurchasesSelected
    {
        get => _section == Section.Purchases;
        set { if (value) Switch(Section.Purchases); }
    }

    private void Switch(Section section)
    {
        if (_section == section)
            return;

        _section = section;

        OnPropertyChanged(nameof(IsStockSelected));
        OnPropertyChanged(nameof(IsDebtsSelected));
        OnPropertyChanged(nameof(IsPurchasesSelected));
        OnPropertyChanged(nameof(Current));

        Reload();
    }

    private void Reload()
    {
        switch (_section)
        {
            case Section.Debts:
                _ = Debts.LoadAsync();
                break;

            case Section.Purchases:
                _ = Purchases.LoadAsync();
                break;

            default:
                _ = Stock.LoadAsync();
                break;
        }
    }
}
