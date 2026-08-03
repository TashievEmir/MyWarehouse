using Application.DTOs.Categories;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Пункт списка категорий. Последний пункт — заглушка «создать новую»,
/// у неё <see cref="Category"/> пустая.
/// </summary>
public class CategoryOption
{
    public CategoryResponse? Category { get; }

    public bool IsCreateNew => Category is null;

    public string Name => Category?.Name ?? Loc.T("Category_CreateNew");

    private CategoryOption(CategoryResponse? category) => Category = category;

    public static CategoryOption For(CategoryResponse category) => new(category);

    public static CategoryOption CreateNew() => new(null);

    /// <summary>Имя списка для средств доступности: иначе туда уходит имя типа.</summary>
    public override string ToString() => Name;
}
