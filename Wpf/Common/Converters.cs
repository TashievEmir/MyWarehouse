using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wpf.Common;

/// <summary>
/// bool -> Visibility. ConverterParameter="Invert" переворачивает результат.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;

        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Показывает элемент, только если ширина окна не меньше порога из ConverterParameter.
/// Используется для адаптивного скрытия второстепенных частей хедера.
/// </summary>
public class MinWidthToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width)
            return Visibility.Visible;

        if (!double.TryParse(parameter as string, NumberStyles.Any, CultureInfo.InvariantCulture, out var threshold))
            return Visibility.Visible;

        return width >= threshold ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Пустая строка -> Visible. Нужен для подсказок поверх пустых полей ввода.
/// </summary>
public class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// null -> Visible. Нужен для подсказок в полях, где значение выбирается, а не вводится.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// true, если все переданные значения равны. Нужен для подсветки активного пункта меню.
/// </summary>
public class EqualsConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is null)
            return false;

        return values.Skip(1).All(v => Equals(values[0], v));
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// "Иван Петров" -> "ИП". Для аватара в хедере.
/// </summary>
public class InitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (value as string ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(p => char.ToUpper(p[0], culture));

        var initials = string.Concat(parts);

        return initials.Length > 0 ? initials : "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
