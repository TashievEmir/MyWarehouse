using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Notifications;

namespace Wpf.Views.Notifications;

public partial class NotificationsView : UserControl
{
    public NotificationsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<NotificationsViewModel>();
    }
}
