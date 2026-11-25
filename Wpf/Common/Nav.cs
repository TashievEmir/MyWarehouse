using System.Windows;
using MaterialDesignThemes.Wpf;

namespace Wpf.Common;

/// <summary>
/// Присоединённые свойства для пунктов бокового меню: иконка и ключ страницы,
/// по которому определяется активный пункт.
/// </summary>
public static class Nav
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached(
            "Icon",
            typeof(PackIconKind),
            typeof(Nav),
            new PropertyMetadata(default(PackIconKind)));

    public static PackIconKind GetIcon(DependencyObject element)
        => (PackIconKind)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, PackIconKind value)
        => element.SetValue(IconProperty, value);

    /// <summary>Плашка справа от пункта: счётчик долгов, отметка «АДМИН».</summary>
    public static readonly DependencyProperty BadgeProperty =
        DependencyProperty.RegisterAttached(
            "Badge",
            typeof(object),
            typeof(Nav),
            new PropertyMetadata(null));

    public static object? GetBadge(DependencyObject element)
        => element.GetValue(BadgeProperty);

    public static void SetBadge(DependencyObject element, object? value)
        => element.SetValue(BadgeProperty, value);

    public static readonly DependencyProperty PageKeyProperty =
        DependencyProperty.RegisterAttached(
            "PageKey",
            typeof(string),
            typeof(Nav),
            new PropertyMetadata(""));

    public static string GetPageKey(DependencyObject element)
        => (string)element.GetValue(PageKeyProperty);

    public static void SetPageKey(DependencyObject element, string value)
        => element.SetValue(PageKeyProperty, value);

    /// <summary>
    /// Ключи всех страниц раздела через пробел. Нужны пункту с выпадающим
    /// списком: сам он никуда не ведёт, но подсвечивается, пока открыт
    /// любой его подпункт.
    /// </summary>
    public static readonly DependencyProperty PageKeysProperty =
        DependencyProperty.RegisterAttached(
            "PageKeys",
            typeof(string),
            typeof(Nav),
            new PropertyMetadata(""));

    public static string GetPageKeys(DependencyObject element)
        => (string)element.GetValue(PageKeysProperty);

    public static void SetPageKeys(DependencyObject element, string value)
        => element.SetValue(PageKeysProperty, value);
}
