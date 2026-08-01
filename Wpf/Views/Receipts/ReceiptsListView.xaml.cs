using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Receipts;

namespace Wpf.Views.Receipts;

public partial class ReceiptsListView : UserControl
{
    public ReceiptsListView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ReceiptsListViewModel>();
    }
}
