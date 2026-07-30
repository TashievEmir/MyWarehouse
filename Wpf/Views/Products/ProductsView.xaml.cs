using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Products;

namespace Wpf.Views.Products;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ProductsPageViewModel>();

        // Сканер работает как клавиатура — поле ввода штрихкода должно быть под фокусом
        Loaded += (_, _) => BarcodeBox.Focus();
    }

    /// <summary>Поле новой категории появляется по выбору в списке — сразу даём в него печатать.</summary>
    private void OnCategoryEditorVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox box || !box.IsVisible)
            return;

        box.Dispatcher.BeginInvoke(new Action(() => box.Focus()), DispatcherPriority.Input);
    }

    /// <summary>Кнопки панели прихода возвращают фокус сканеру.</summary>
    private void OnReturnFocusToScanner(object sender, RoutedEventArgs e)
        => BarcodeBox.Focus();

    /// <summary>
    /// Enter в полях строки сканирования возвращает фокус на поле штрихкода,
    /// чтобы можно было сразу сканировать дальше.
    /// </summary>
    private void OnScanFieldKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (sender is TextBox box)
        {
            // Значения полей привязаны по LostFocus — сначала фиксируем ввод
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        BarcodeBox.Focus();
        e.Handled = true;
    }
}
