using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Inventories;
using Wpf.Common;

namespace Wpf.ViewModels.Products;

public class ProductsViewModel
{
    private readonly IInventoryService _inventory;

    public ObservableCollection<InventoryResponse> Products { get; set; } = new();

    public ICommand LoadProductsCommand { get; }

    public ProductsViewModel(IInventoryService inventory)
    {
        _inventory = inventory;

        LoadProductsCommand = new AsyncRelayCommand(LoadProducts);
        
        LoadProductsCommand.Execute(null);
    }

    private async Task LoadProducts()
    {
        var products = await _inventory.GetAllAsync(CancellationToken.None);

        Products.Clear();

        foreach (var p in products)
            Products.Add(p);
    }
}