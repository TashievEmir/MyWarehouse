using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Activity;

namespace Wpf.Views.Activity;

public partial class ActivityLogView : UserControl
{
    public ActivityLogView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ActivityLogViewModel>();
    }
}
