using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Statistics;

namespace Wpf.Views.Statistics;

public partial class StatisticsView : UserControl
{
    public StatisticsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<StatisticsViewModel>();
    }
}
