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
}
