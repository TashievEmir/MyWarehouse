using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Users;
using Wpf.Common;
using Wpf.Localization;
using Wpf.Services;

namespace Wpf.ViewModels.Users;

/// <summary>Роль в списке выбора: галочка хранится вместе с самой ролью.</summary>
public class RoleOption : ViewModelBase
{
    public int Id { get; }
    public string Name { get; }
    public string Title { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public RoleOption(RoleResponse role)
    {
        Id = role.Id;
        Name = role.Name;
        Title = role.Title;
    }
}

/// <summary>Строка списка сотрудников.</summary>
public class UserListItem
{
    public long Id { get; }
    public string Username { get; }
    public string FullName { get; }
    public string RolesText { get; }
    public bool IsActive { get; }
    public bool HasHistory { get; }

    public List<int> RoleIds { get; }
    public string LastName { get; }
    public string FirstName { get; }
    public string? Patronymic { get; }

    public string StateText => IsActive ? Loc.T("Users_Active") : Loc.T("Users_Disabled");

    public string StateBrushKey => IsActive ? "SuccessBrush" : "MutedBrush";

    public UserListItem(UserListItemResponse user)
    {
        Id = user.Id;
        Username = user.Username;
        LastName = user.LastName;
        FirstName = user.FirstName;
        Patronymic = user.Patronymic;
        IsActive = user.IsActive;
        HasHistory = user.HasHistory;
        RoleIds = user.RoleIds;

        FullName = string.Join(" ", new[] { user.LastName, user.FirstName, user.Patronymic }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        RolesText = string.Join(", ", user.RoleTitles);
    }
}

/// <summary>
/// Пользователи: список слева, карточка справа. Заводить и править может
/// менеджер и админ, ограничения по ролям проверяет слой Application.
/// </summary>
public class UsersViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly SessionService _session;

    public ObservableCollection<UserListItem> Items { get; } = new();
    public ObservableCollection<RoleOption> Roles { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public UsersViewModel(IUserService users, SessionService session)
    {
        _users = users;
        _session = session;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new RelayCommand<UserListItem>(Select);
        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        CloseCommand = new RelayCommand(CloseEditor);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(string.Empty);
            _ = LoadAsync();
        };
    }

    // ===================== Список =====================

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearch));
                _ = LoadAsync();
            }
        }
    }

    public bool HasSearch => SearchText.Length > 0;

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public string CountText => Items.Count == 0 ? "" : Loc.F("Users_Count", Items.Count);

    // ===================== Карточка =====================

    private bool _hasEditor;
    public bool HasEditor
    {
        get => _hasEditor;
        private set => SetProperty(ref _hasEditor, value);
    }

    private long _editedId;
    /// <summary>0 — заводим нового сотрудника.</summary>
    public long EditedId
    {
        get => _editedId;
        private set
        {
            if (SetProperty(ref _editedId, value))
            {
                OnPropertyChanged(nameof(IsNew));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(IsSelf));
            }
        }
    }

    public bool IsNew => EditedId == 0;

    public bool IsSelf => EditedId != 0 && EditedId == (_session.User?.UserId ?? -1);

    public string EditorTitle => IsNew
        ? Loc.T("Users_NewTitle")
        : string.Join(" ", new[] { LastName, FirstName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private string _lastName = "";
    public string LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
                OnPropertyChanged(nameof(EditorTitle));
        }
    }

    private string _firstName = "";
    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
                OnPropertyChanged(nameof(EditorTitle));
        }
    }

    private string _patronymic = "";
    public string Patronymic
    {
        get => _patronymic;
        set => SetProperty(ref _patronymic, value);
    }

    private string _username = "";
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private bool _hasHistory;
    /// <summary>За сотрудником числятся операции — удалять его нельзя.</summary>
    public bool HasHistory
    {
        get => _hasHistory;
        private set
        {
            if (SetProperty(ref _hasHistory, value))
            {
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(DeleteHint));
            }
        }
    }

    public bool CanDelete => !IsNew && !HasHistory && !IsSelf;

    public string DeleteHint => HasHistory
        ? Loc.T("Users_DeleteBlocked")
        : Loc.T("Users_DeleteHint");

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public bool HasStatus => StatusMessage.Length > 0;

    // ===================== Загрузка =====================

    public async Task LoadAsync()
    {
        try
        {
            if (Roles.Count == 0)
            {
                foreach (var role in await _users.GetRolesAsync(CancellationToken.None))
                    Roles.Add(new RoleOption(role));
            }

            var users = await _users.GetAllAsync(SearchText, CancellationToken.None);

            Items.Clear();

            foreach (var user in users)
                Items.Add(new UserListItem(user));

            IsEmpty = Items.Count == 0;

            OnPropertyChanged(nameof(CountText));

            // Карточка могла остаться открытой на удалённом сотруднике
            if (!IsNew && Items.All(i => i.Id != EditedId))
                CloseEditor();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Users_LoadFailed", ex.Message));
        }
    }

    private void Select(UserListItem item)
    {
        if (item is null)
            return;

        EditedId = item.Id;
        LastName = item.LastName;
        FirstName = item.FirstName;
        Patronymic = item.Patronymic ?? "";
        Username = item.Username;
        Password = "";
        IsActive = item.IsActive;
        HasHistory = item.HasHistory;

        foreach (var role in Roles)
            role.IsSelected = item.RoleIds.Contains(role.Id);

        StatusMessage = "";
        HasEditor = true;
    }

    private void StartNew()
    {
        EditedId = 0;
        LastName = "";
        FirstName = "";
        Patronymic = "";
        Username = "";
        Password = "";
        IsActive = true;
        HasHistory = false;

        foreach (var role in Roles)
            role.IsSelected = false;

        StatusMessage = "";
        HasEditor = true;
    }

    private void CloseEditor()
    {
        HasEditor = false;
        EditedId = 0;
        StatusMessage = "";
    }

    // ===================== Сохранение и удаление =====================

    private async Task SaveAsync()
    {
        if (_session.User is null)
        {
            ShowError(Loc.T("Users_NoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            var id = await _users.SaveAsync(new SaveUserRequest
            {
                Id = EditedId,
                Username = Username,
                LastName = LastName,
                FirstName = FirstName,
                Patronymic = Patronymic,
                Password = Password,
                IsActive = IsActive,
                RoleIds = Roles.Where(r => r.IsSelected).Select(r => r.Id).ToList(),
                ActorId = _session.User.UserId,
            }, CancellationToken.None);

            EditedId = id;
            Password = "";

            await LoadAsync();

            // Список перечитан — подтягиваем свежие данные в карточку
            if (Items.FirstOrDefault(i => i.Id == id) is { } saved)
                Select(saved);

            ShowInfo(Loc.T("Users_Saved"));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_session.User is null)
        {
            ShowError(Loc.T("Users_NoLogin"));
            return;
        }

        var answer = MessageBox.Show(
            Loc.F("Users_DeleteConfirm", EditorTitle),
            Loc.T("Users_DeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            await _users.DeleteAsync(EditedId, _session.User.UserId, CancellationToken.None);

            CloseEditor();

            await LoadAsync();

            ShowInfo(Loc.T("Users_Deleted"));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowInfo(string message)
    {
        StatusIsError = false;
        StatusMessage = message;
    }

    private void ShowError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
