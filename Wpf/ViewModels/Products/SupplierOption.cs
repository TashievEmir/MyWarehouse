using Application.DTOs.Suppliers;
using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Пункт списка поставщиков. Последний пункт — заглушка «создать нового»,
/// у неё <see cref="Supplier"/> пустая. Устроено так же, как у категорий.
/// </summary>
public class SupplierOption
{
    public SupplierResponse? Supplier { get; }

    public bool IsCreateNew => Supplier is null;

    public string Name => Supplier?.Name ?? Loc.T("Supplier_CreateNew");

    private SupplierOption(SupplierResponse? supplier) => Supplier = supplier;

    public static SupplierOption For(SupplierResponse supplier) => new(supplier);

    public static SupplierOption CreateNew() => new(null);

    /// <summary>Имя списка для средств доступности: иначе туда уходит имя типа.</summary>
    public override string ToString() => Name;
}
