using System.Collections.ObjectModel;
using Application.DTOs.Products;
using Wpf.Common;

using Wpf.Localization;

namespace Wpf.ViewModels.Statistics;

/// <summary>Категория в статистике: сводка и раскрывающийся список товаров.</summary>
public class CategoryStatItem : ViewModelBase
{
    public long CategoryId { get; }
    public string Name { get; }

    public int InStock { get; }
    public int Received { get; }

    public ObservableCollection<ProductStatItem> Products { get; } = new();

    /// <summary>«12 / 30» — остаток из поступившего за период.</summary>
    public string StockRatio => $"{InStock} / {Received}";

    public string ProductsCountText => Loc.F("Stat_Positions", Products.Count);

    /// <summary>Доля остатка от прихода — ширина полоски прогресса.</summary>
    public double StockShare => Received > 0 ? Math.Min(1d, (double)InStock / Received) : 0d;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public CategoryStatItem(CategoryStockResponse category)
    {
        CategoryId = category.CategoryId;
        Name = category.Name;
        InStock = category.InStock;
        Received = category.Received;

        foreach (var product in category.Products)
            Products.Add(new ProductStatItem(product));
    }
}
