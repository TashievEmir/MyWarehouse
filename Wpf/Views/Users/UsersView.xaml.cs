using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Users;

namespace Wpf.Views.Users;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<UsersViewModel>();
    }
}
