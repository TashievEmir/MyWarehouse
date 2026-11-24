using System.Globalization;

namespace Application.Services
{
    /// <summary>
    /// Версия приложения вида «1.4.2». Отдельный тип, а не строка: сравнение
    /// строк дало бы «1.10.0» меньше «1.9.0», и обновление бы не предлагалось.
    ///
    /// Четвёртое число (ревизию сборки) намеренно игнорируем — пользователю
    /// важны только значимые выпуски.
    /// </summary>
    public readonly record struct AppVersion(int Major, int Minor, int Patch) : IComparable<AppVersion>
    {
        public static readonly AppVersion None = new(0, 0, 0);

        public bool IsKnown => this != None;

        /// <summary>
        /// Разбирает «1.4.2», «v1.4.2» и «1.4.2.17». Всё, что не разобралось,
        /// возвращает false — молча считать такое нулём опасно: клиенту
        /// предложили бы обновиться на мусор.
        /// </summary>
        public static bool TryParse(string? text, out AppVersion version)
        {
            version = None;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var raw = text.Trim();

            // Тег релиза в GitHub принято писать как «v1.4.2»
            if (raw.StartsWith('v') || raw.StartsWith('V'))
                raw = raw[1..];

            var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length is < 1 or > 4)
                return false;

            var numbers = new int[3];

            for (var i = 0; i < 3; i++)
            {
                if (i >= parts.Length)
                {
                    numbers[i] = 0;
                    continue;
                }

                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    return false;

                numbers[i] = value;
            }

            version = new AppVersion(numbers[0], numbers[1], numbers[2]);

            return true;
        }

        public static AppVersion Parse(string? text)
            => TryParse(text, out var version)
                ? version
                : throw new FormatException($"Не похоже на версию: «{text}»");

        public int CompareTo(AppVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;

            return Patch.CompareTo(other.Patch);
        }

        public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;
        public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
        public static bool operator <=(AppVersion a, AppVersion b) => a.CompareTo(b) <= 0;
        public static bool operator >=(AppVersion a, AppVersion b) => a.CompareTo(b) >= 0;

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}
