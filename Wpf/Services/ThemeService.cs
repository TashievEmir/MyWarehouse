using System.ComponentModel;
using System.IO;
using System.Windows;

namespace Wpf.Services;

public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Переключение светлой и тёмной темы. Словарь темы всегда лежит первым в
/// MergedDictionaries и подменяется целиком, поэтому кисти в разметке
/// обязаны браться через DynamicResource.
/// </summary>
public class ThemeService : INotifyPropertyChanged
{
    private const string LightUri = "Resources/Theme.Light.xaml";
    private const string DarkUri  = "Resources/Theme.Dark.xaml";

    private readonly string _settingsPath;

    public ThemeService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWarehouse");

        Directory.CreateDirectory(folder);

        _settingsPath = Path.Combine(folder, "theme.txt");
        _theme = ReadSaved();
    }

    private AppTheme _theme;
    public AppTheme Theme
    {
        get => _theme;
        private set
        {
            if (_theme == value)
                return;

            _theme = value;

            Raise(nameof(Theme));
            Raise(nameof(IsDark));
            Raise(nameof(ToggleLabel));
        }
    }

    public bool IsDark => Theme == AppTheme.Dark;

    /// <summary>Подпись кнопки: показывает, на что переключимся.</summary>
    public string ToggleLabel => Localization.Loc.T(IsDark ? "Theme_Light" : "Theme_Dark");

    /// <summary>Ставит сохранённую тему при старте приложения.</summary>
    public void Apply() => Apply(Theme);

    public void Toggle() => Apply(IsDark ? AppTheme.Light : AppTheme.Dark);

    private void Apply(AppTheme theme)
    {
        // Application без префикса — это проект Application, а не WPF
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;

        var updated = new ResourceDictionary
        {
            Source = new Uri(theme == AppTheme.Dark ? DarkUri : LightUri, UriKind.Relative),
        };

        if (dictionaries.Count == 0)
            dictionaries.Add(updated);
        else
            dictionaries[0] = updated;

        Theme = theme;

        Save(theme);
    }

    private AppTheme ReadSaved()
    {
        try
        {
            return File.Exists(_settingsPath) && File.ReadAllText(_settingsPath).Trim() == nameof(AppTheme.Dark)
                ? AppTheme.Dark
                : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    private void Save(AppTheme theme)
    {
        // Тема — мелкая настройка: если записать не вышло, приложение работает дальше
        try
        {
            File.WriteAllText(_settingsPath, theme.ToString());
        }
        catch
        {
            // ignored
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
