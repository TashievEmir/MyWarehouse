using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Wpf.Localization;
using Wpf.ViewModels.Login;

namespace Wpf.Views.Login;

public partial class LoginView : Window
{
    private bool _showPassword;

    public LoginView(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        Loaded += (_, _) => LoginBox.Focus();
    }

    /// <summary>Переключение языка доступно до входа — кассир может не читать по-русски.</summary>
    private void ToggleLanguage(object sender, RoutedEventArgs e) => Loc.Instance.Toggle();

    /// <summary>Показать или скрыть пароль, сохраняя введённое значение.</summary>
    private void TogglePassword(object sender, RoutedEventArgs e)
    {
        if (_showPassword)
        {
            PasswordBox.Password = VisiblePassword.Text;
            PasswordBox.Visibility = Visibility.Visible;
            VisiblePassword.Visibility = Visibility.Collapsed;
            EyeIcon.Kind = PackIconKind.EyeOutline;
        }
        else
        {
            VisiblePassword.Text = PasswordBox.Password;
            VisiblePassword.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            EyeIcon.Kind = PackIconKind.EyeOffOutline;
        }

        _showPassword = !_showPassword;
    }

    /// <summary>Enter в поле пароля входит в систему — привычно и быстрее мыши.</summary>
    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not LoginViewModel vm)
            return;

        // Видимый пароль держится в отдельном поле — переносим его обратно
        if (_showPassword)
            PasswordBox.Password = VisiblePassword.Text;

        if (vm.LoginCommand.CanExecute(PasswordBox))
            vm.LoginCommand.Execute(PasswordBox);

        e.Handled = true;
    }
}
