using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Sales;

namespace Wpf.Views.Sales;

public partial class SalesView : UserControl
{
    /// <summary>
    /// Поле сканера живёт в шаблоне вкладки, поэтому ссылку на него забираем
    /// при загрузке — при переключении вкладки шаблон создаётся заново.
    /// </summary>
    private TextBox? _scanBox;

    public SalesView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SalesViewModel>();
    }

    private void OnScanBoxLoaded(object sender, RoutedEventArgs e)
    {
        _scanBox = (TextBox)sender;
        _scanBox.Focus();
    }

    /// <summary>Кнопки кассы возвращают фокус сканеру, чтобы можно было пробивать дальше.</summary>
    private void OnReturnFocusToScanner(object sender, RoutedEventArgs e) => _scanBox?.Focus();
}
