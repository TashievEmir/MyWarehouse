using System.Windows;

namespace Wpf.Views.Login;

public partial class LoginView : Window
{
    private bool _showPassword = false;
    
    public LoginView()
    {
        InitializeComponent();
    }
    
    private void TogglePassword(object sender, RoutedEventArgs e)
    {
        if (_showPassword)
        {
            PasswordBox.Password = VisiblePassword.Text;
            PasswordBox.Visibility = Visibility.Visible;
            VisiblePassword.Visibility = Visibility.Collapsed;
        }
        else
        {
            VisiblePassword.Text = PasswordBox.Password;
            VisiblePassword.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
        }

        _showPassword = !_showPassword;
    }
    
}