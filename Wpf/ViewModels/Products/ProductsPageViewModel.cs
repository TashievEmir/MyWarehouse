using Wpf.Common;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Страница «Товары»: каталог со справочником и приёмка по штрихкоду.
/// </summary>
public class ProductsPageViewModel : ViewModelBase
{
    public ProductCatalogViewModel Catalog { get; }
    public ReceivingViewModel Receiving { get; }

    public ProductsPageViewModel(ProductCatalogViewModel catalog, ReceivingViewModel receiving)
    {
        Catalog = catalog;
        Receiving = receiving;

        // Приход меняет карточки и остатки — каталог не должен показывать старое
        Receiving.Received += () => _ = Catalog.LoadAsync();

        _ = Catalog.LoadAsync();
    }

    private bool _isCatalogSelected = true;
    public bool IsCatalogSelected
    {
        get => _isCatalogSelected;
        set
        {
            if (!SetProperty(ref _isCatalogSelected, value))
                return;

            OnPropertyChanged(nameof(IsReceivingSelected));

            if (value)
                _ = Catalog.LoadAsync();
        }
    }

    public bool IsReceivingSelected
    {
        get => !_isCatalogSelected;
        set
        {
            if (value)
                IsCatalogSelected = false;
        }
    }
}
