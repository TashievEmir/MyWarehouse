using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;

namespace Wpf.Localization;

/// <summary>
/// Единая точка доступа к переводам. Тексты лежат в общем для всех слоёв файле
/// Application/Localization/Strings.resx (русский) и Strings.ky.resx
/// (кыргызский) — чтобы поправить формулировку, достаточно отредактировать один
/// файл и пересобрать проект.
///
/// В разметке используется <c>{loc:Tr Ключ}</c>, в коде — <see cref="T"/> и
/// <see cref="F"/>. Смена языка поднимает уведомление по индексатору, поэтому
/// подписи в XAML перерисовываются сразу, без перезапуска.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string Russian = "ru";
    public const string Kyrgyz = "ky";

    // Полные культуры нужны для дат и чисел; ресурсы находятся по родителю ("ru" / "ky")
    private const string RussianCulture = "ru-RU";
    private const string KyrgyzCulture = "ky-KG";

    private static readonly ResourceManager Manager = Application.Localization.Tr.Resources;

    public static Loc Instance { get; } = new();

    private readonly string _settingsPath;

    private Loc()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyWarehouse");

        Directory.CreateDirectory(folder);

        _settingsPath = Path.Combine(folder, "lang.txt");
        _language = ReadSaved();
        _culture = CultureOf(_language);
    }

    private static CultureInfo CultureOf(string code)
    {
        // Кыргызской локали может не оказаться в системе — тексты всё равно
        // возьмутся из ресурсов, а форматы дат и чисел откатим на русские
        try
        {
            return new CultureInfo(code == Kyrgyz ? KyrgyzCulture : RussianCulture);
        }
        catch (CultureNotFoundException)
        {
            return new CultureInfo(RussianCulture);
        }
    }

    private CultureInfo _culture;
    private string _language;

    /// <summary>Культура интерфейса: она же используется в форматировании дат и чисел.</summary>
    public CultureInfo Culture => _culture;

    public string Language => _language;

    /// <summary>
    /// Язык для разметки. Календарь DatePicker берёт формат даты именно отсюда,
    /// а не из CurrentCulture — без этого даты показывались как 8/10/2026.
    /// </summary>
    public System.Windows.Markup.XmlLanguage XmlLanguage
        => System.Windows.Markup.XmlLanguage.GetLanguage(_culture.IetfLanguageTag);

    public bool IsKyrgyz => _language == Kyrgyz;

    /// <summary>Код языка, на который переключит кнопка.</summary>
    public string ToggleCode => IsKyrgyz ? "RU" : "KY";

    /// <summary>Культура для поиска ресурсов — нейтральная «ru» или «ky».</summary>
    private CultureInfo ResourceCulture
    {
        get
        {
            try
            {
                return new CultureInfo(_language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }

    /// <summary>Перевод по ключу. Неизвестный ключ возвращается как есть — заметно при вёрстке.</summary>
    public string this[string key] => Manager.GetString(key, ResourceCulture) ?? key;

    public static string T(string key) => Instance[key];

    public static string F(string key, params object?[] args)
        => string.Format(Instance.Culture, Instance[key], args);

    /// <summary>Событие для вью-моделей: их строки собраны в коде и сами не обновятся.</summary>
    public static event Action? LanguageChanged;

    public void Toggle() => SetLanguage(IsKyrgyz ? Russian : Kyrgyz);

    public void SetLanguage(string code)
    {
        if (_language == code)
            return;

        _language = code;
        _culture = CultureOf(code);

        ApplyToThreads();
        ApplyToOpenWindows();
        Save(code);

        // "Item[]" перечитывает все привязки к индексатору разом
        Raise("Item[]");
        Raise(nameof(Language));
        Raise(nameof(IsKyrgyz));
        Raise(nameof(ToggleCode));
        Raise(nameof(Culture));

        LanguageChanged?.Invoke();
    }

    /// <summary>Ставит сохранённый язык при старте приложения.</summary>
    public void Apply() => ApplyToThreads();

    /// <summary>Календарь DatePicker берёт язык из окна — обновляем уже открытые.</summary>
    private void ApplyToOpenWindows()
    {
        // Application без префикса — это проект Application, а не WPF
        var app = System.Windows.Application.Current;

        if (app is null)
            return;

        foreach (System.Windows.Window window in app.Windows)
            window.Language = XmlLanguage;
    }

    private void ApplyToThreads()
    {
        // Форматы — по полной культуре, тексты слоя Application — по CurrentUICulture
        var resources = ResourceCulture;

        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = resources;
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = resources;
    }

    private string ReadSaved()
    {
        // Язык — мелкая настройка: при любой проблеме тихо берём русский
        try
        {
            return File.Exists(_settingsPath) && File.ReadAllText(_settingsPath).Trim() == Kyrgyz
                ? Kyrgyz
                : Russian;
        }
        catch
        {
            return Russian;
        }
    }

    private void Save(string code)
    {
        try
        {
            File.WriteAllText(_settingsPath, code);
        }
        catch
        {
            // ignored
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
