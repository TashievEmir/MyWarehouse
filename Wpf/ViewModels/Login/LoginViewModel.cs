using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Auths;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Common;
using Wpf.Services;
using Wpf.Views.Login;

namespace Wpf.ViewModels.Login;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly SessionService _session;

    private string _username;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _errorMessage;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand LoginCommand { get; }

    public LoginViewModel(IAuthService auth, SessionService session)
    {
        _auth = auth;
        _session = session;
        LoginCommand = new RelayCommand<PasswordBox>(Login);
    }

    private async void Login(PasswordBox passwordBox)
    {
        try
        {
            var user = await _auth.LoginAsync(
                new LoginRequest
                {
                    Username = Username,
                    Password = passwordBox.Password
                },
                CancellationToken.None);

            ErrorMessage = "";

            _session.User = user;

            // 🔥 ШАГ 3 — открыть MainWindow
            var main = App.Services.GetRequiredService<MainWindow>();
            main.Show();

            // 🔥 закрыть окно логина
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is LoginView)
                    window.Close();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}