using System.ComponentModel;
using Application.DTOs.Auths;

namespace Wpf.Services;

/// <summary>
/// Хранит данные вошедшего пользователя, чтобы хедер мог их показать.
/// </summary>
public class SessionService : INotifyPropertyChanged
{
    private LoginResponse? _user;

    public LoginResponse? User
    {
        get => _user;
        set
        {
            _user = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(User)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RoleTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPrivileged)));
        }
    }

    public string DisplayName
    {
        get
        {
            if (_user is null) return Localization.Loc.T("Session_Guest");

            var fullName = $"{_user.Lastname} {_user.Firstname}".Trim();

            return fullName.Length > 0 ? fullName : _user.Username;
        }
    }

    public string RoleTitle => _user?.Roles.FirstOrDefault() ?? Localization.Loc.T("Session_NotAuthorized");

    /// <summary>
    /// Админу и менеджеру открыто всё приложение. Кассиру — только рабочие
    /// экраны: касса, каталог и приёмка. Роль не распознана — считаем кассиром.
    /// </summary>
    public bool IsPrivileged => _user?.Roles.Any(r => r is "Admin" or "Manager") ?? false;

    public event PropertyChangedEventHandler? PropertyChanged;
}
