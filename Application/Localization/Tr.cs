using System.Globalization;
using System.Resources;

namespace Application.Localization
{
    /// <summary>
    /// Все тексты приложения лежат в Application/Localization/Strings.resx (русский)
    /// и Strings.ky.resx (кыргызский). Файл общий для слоя Application и для WPF,
    /// поэтому язык правится в одном месте.
    ///
    /// Язык берётся из CultureInfo.CurrentUICulture — её выставляет UI при старте
    /// и при переключении языка.
    /// </summary>
    public static class Tr
    {
        /// <summary>Тот же менеджер использует WPF-обёртка Loc.</summary>
        public static readonly ResourceManager Resources =
            new("Application.Localization.Strings", typeof(Tr).Assembly);

        /// <summary>Неизвестный ключ возвращается как есть — пропуск сразу видно на экране.</summary>
        public static string T(string key)
            => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string F(string key, params object?[] args)
            => string.Format(CultureInfo.CurrentUICulture, T(key), args);
    }
}
