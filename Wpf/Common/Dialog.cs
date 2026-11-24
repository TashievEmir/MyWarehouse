using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Wpf.Localization;
using Wpf.Views.Common;

namespace Wpf.Common;

/// <summary>
/// Подтверждения в оформлении приложения — замена системному MessageBox.
///
/// Статический вызов намеренно: подтверждение спрашивают прямо из команд
/// вью-моделей, и городить ради одной строки инъекцию зависимости значило бы
/// менять конструкторы шести вью-моделей ради того же, что уже делает Loc.
/// Привязки к UI это не добавляет: MessageBox был ровно таким же статическим.
/// </summary>
public static class Dialog
{
    /// <summary>Обычный вопрос: продолжить или нет.</summary>
    public static bool Confirm(string title, string message, string? confirmText = null)
        => Show(title, message, confirmText ?? Loc.T("Common_Continue"), PackIconKind.HelpCircleOutline, danger: false);

    /// <summary>
    /// Вопрос о необратимом действии: удаление, возврат, списание.
    /// Кнопка подтверждения красная — чтобы не нажали на автомате.
    /// </summary>
    public static bool ConfirmDanger(string title, string message, string? confirmText = null)
        => Show(title, message, confirmText ?? Loc.T("Common_Delete"), PackIconKind.AlertCircleOutline, danger: true);

    private static bool Show(string title, string message, string confirmText, PackIconKind icon, bool danger)
    {
        var owner = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;

        var accentKey = danger ? "DangerBrush" : "PrimaryBrush";

        var window = new ConfirmWindow
        {
            TitleText = title,
            Message = message,
            ConfirmText = confirmText,
            CancelText = Loc.T("Common_Cancel"),
            Icon = icon,
            AccentBrush = Brush(accentKey, Brushes.Gray),
            AccentSoftBrush = Soft(accentKey),
        };

        if (owner is not null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        window.CoverOwner();

        if (danger)
            MakeConfirmDangerous(window);

        return window.ShowDialog() == true;
    }

    /// <summary>Кисть из темы; при отсутствии ключа берём запасную, а не падаем.</summary>
    private static Brush Brush(string key, Brush fallback)
        => System.Windows.Application.Current?.TryFindResource(key) as Brush ?? fallback;

    /// <summary>Та же кисть, но блёклая — под подложку значка.</summary>
    private static Brush Soft(string key)
    {
        if (Brush(key, Brushes.Gray) is not SolidColorBrush solid)
            return Brushes.Transparent;

        var soft = new SolidColorBrush(solid.Color) { Opacity = 0.15 };

        soft.Freeze();

        return soft;
    }

    /// <summary>
    /// Красим кнопку подтверждения в опасный цвет. Стиль PrimaryButton
    /// задаёт фон через шаблон, поэтому перекрываем его на самой кнопке.
    /// </summary>
    private static void MakeConfirmDangerous(ConfirmWindow window)
    {
        window.Loaded += (_, _) =>
        {
            if (window.FindName("ConfirmButton") is Button button)
                button.Background = Brush("DangerBrush", Brushes.IndianRed);
        };
    }
}
