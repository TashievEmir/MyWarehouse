using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Receipts;

namespace Wpf.Views.Receipts;

public partial class ReceiptTemplateView : UserControl
{
    public ReceiptTemplateView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ReceiptTemplateViewModel>();
    }
}
