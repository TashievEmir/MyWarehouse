using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Dashboard;

namespace Wpf.Views.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
    }
}
