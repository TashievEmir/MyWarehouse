using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Categories;
using Application.DTOs.Products;
using Wpf.Common;
using Wpf.Services;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Панель карточки товара: правка полей, списание количества и удаление.
/// </summary>
public class ProductEditorViewModel : ViewModelBase
{
    private readonly IProductService _products;
    private readonly SessionService _session;
    private readonly Func<Task> _reloadCatalog;
    private readonly Action _close;

    public long ProductId { get; }

    /// <summary>Категория товара из базы — запасной вариант, если выбор ещё не сделан.</summary>
    private readonly long _productCategoryId;

    private string _title;
    /// <summary>Заголовок панели: не скачет при правке, обновляется после сохранения.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public ObservableCollection<CategoryResponse> Categories { get; }

    public IReadOnlyList<WriteOffReasonOption> Reasons => WriteOffReasonOption.All;

    public ICommand SaveCommand { get; }
    public ICommand WriteOffCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CloseCommand { get; }

    public ProductEditorViewModel(
        ProductListItem product,
        ObservableCollection<CategoryResponse> categories,
        IProductService products,
        SessionService session,
        Func<Task> reloadCatalog,
        Action close)
    {
        _products = products;
        _session = session;
        _reloadCatalog = reloadCatalog;
        _close = close;

        Categories = categories;

        ProductId = product.ProductId;
        _productCategoryId = product.CategoryId;
        _title = product.Name;

        _name = product.Name;
        _sku = product.SKU;
        _barcode = product.Barcode ?? "";
        _description = product.Description ?? "";
        _price = product.PricePerUnit;
        _cost = product.CostPerUnit ?? 0m;
        _inStock = product.InStock;
        _stockInput = product.InStock.ToString();
        _hasHistory = product.HasHistory;
        _selectedCategory = categories.FirstOrDefault(c => c.Id == product.CategoryId);
        _selectedReason = Reasons[0];

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        WriteOffCommand = new AsyncRelayCommand(WriteOffAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        CloseCommand = new RelayCommand(() => _close());
    }

    // ===================== Поля карточки =====================

    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _sku;
    public string SKU
    {
        get => _sku;
        set => SetProperty(ref _sku, value);
    }

    private string _barcode;
    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    private string _description;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private CategoryResponse? _selectedCategory;
    public CategoryResponse? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    /// <summary>
    /// Список категорий общий с каталогом: после его перечитывания в коллекции
    /// лежат уже другие объекты, поэтому выбор восстанавливаем по Id.
    /// </summary>
    public void RestoreCategory(long? categoryId)
        => SelectedCategory = Categories.FirstOrDefault(c => c.Id == (categoryId ?? _productCategoryId));

    private decimal _price;
    public decimal PricePerUnit
    {
        get => _price;
        set => SetProperty(ref _price, value < 0 ? 0 : value);
    }

    private decimal _cost;
    public decimal CostPerUnit
    {
        get => _cost;
        set => SetProperty(ref _cost, value < 0 ? 0 : value);
    }

    private int _inStock;
    public int InStock
    {
        get => _inStock;
        private set
        {
            if (SetProperty(ref _inStock, value))
            {
                OnPropertyChanged(nameof(CanWriteOff));
                OnPropertyChanged(nameof(InStockText));

                StockInput = value.ToString();
            }
        }
    }

    /// <summary>Подпись «на складе: N шт.» под названием карточки.</summary>
    public string InStockText => Loc.F("Editor_InStock", InStock);

    private string _stockInput;
    /// <summary>
    /// Остаток в поле правки. Держим строкой отдельно от <see cref="InStock"/>:
    /// недописанное или ошибочное значение не должно менять карточку.
    /// </summary>
    public string StockInput
    {
        get => _stockInput;
        set => SetProperty(ref _stockInput, value);
    }

    private bool _hasHistory;
    public bool HasHistory
    {
        get => _hasHistory;
        private set
        {
            if (SetProperty(ref _hasHistory, value))
            {
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(DeleteHint));
            }
        }
    }

    // ===================== Списание =====================

    private int _writeOffQuantity = 1;
    public int WriteOffQuantity
    {
        get => _writeOffQuantity;
        set => SetProperty(ref _writeOffQuantity, value < 1 ? 1 : value);
    }

    private WriteOffReasonOption _selectedReason;
    public WriteOffReasonOption SelectedReason
    {
        get => _selectedReason;
        set => SetProperty(ref _selectedReason, value);
    }

    private string _writeOffComment = "";
    public string WriteOffComment
    {
        get => _writeOffComment;
        set => SetProperty(ref _writeOffComment, value);
    }

    public bool CanWriteOff => InStock > 0 && !IsBusy;

    // ===================== Удаление =====================

    public bool CanDelete => !HasHistory && !IsBusy;

    public string DeleteHint => HasHistory
        ? Loc.T("Editor_DeleteBlocked")
        : Loc.T("Editor_DeleteAllowed");

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanWriteOff));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
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

    // ===================== Действия =====================

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ShowError(Loc.T("Editor_NeedName"));
            return;
        }

        if (SelectedCategory is null)
        {
            ShowError(Loc.T("Editor_NeedCategory"));
            return;
        }

        if (PricePerUnit <= 0)
        {
            ShowError(Loc.T("Editor_NeedPrice"));
            return;
        }

        if (!int.TryParse((StockInput ?? "").Trim(), out var stock) || stock < 0)
        {
            ShowError(Loc.T("Editor_StockBadNumber"));
            return;
        }

        // Остаток — не движение товара, а правка учёта, поэтому нужен автор
        var stockChanged = stock != InStock;

        if (stockChanged && _session.User is null)
        {
            ShowError(Loc.T("Editor_StockNoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            await _products.UpdateAsync(new UpdateProductRequest
            {
                ProductId = ProductId,
                UserId = _session.User?.UserId ?? 0,
                Name = Name,
                SKU = SKU,
                Barcode = Barcode,
                Description = Description,
                CategoryId = SelectedCategory.Id,
                PricePerUnit = PricePerUnit,
                CostPerUnit = CostPerUnit
            }, CancellationToken.None);

            if (stockChanged)
            {
                await _products.AdjustStockAsync(new AdjustStockRequest
                {
                    ProductId = ProductId,
                    UserId = _session.User!.UserId,
                    Quantity = stock
                }, CancellationToken.None);

                InStock = stock;
            }

            Title = Name.Trim();

            await _reloadCatalog();

            ShowInfo(Loc.T("Editor_Saved"));
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

    private async Task WriteOffAsync()
    {
        if (WriteOffQuantity > InStock)
        {
            ShowError(Loc.F("Editor_WriteOffTooMuch", InStock));
            return;
        }

        if (_session.User is null)
        {
            ShowError(Loc.T("Editor_WriteOffNoLogin"));
            return;
        }

        var answer = MessageBox.Show(
            Loc.F("Editor_WriteOffConfirm", WriteOffQuantity, Title, SelectedReason.Name),
            Loc.T("Editor_WriteOffConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            var quantity = WriteOffQuantity;

            await _products.WriteOffAsync(new WriteOffProductRequest
            {
                ProductId = ProductId,
                UserId = _session.User.UserId,
                Quantity = quantity,
                Reason = SelectedReason.Reason,
                Comment = WriteOffComment
            }, CancellationToken.None);

            InStock -= quantity;
            HasHistory = true;

            WriteOffQuantity = 1;
            WriteOffComment = "";

            await _reloadCatalog();

            ShowInfo(Loc.F("Editor_WriteOffDone", quantity, InStock));
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
        var answer = MessageBox.Show(
            Loc.F("Editor_DeleteConfirm", Title),
            Loc.T("Editor_DeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            await _products.DeleteAsync(ProductId, CancellationToken.None);

            await _reloadCatalog();

            _close();
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
