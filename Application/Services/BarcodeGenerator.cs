namespace Application.Services
{
    /// <summary>
    /// Генерация внутренних штрихкодов EAN-13 для товаров, приехавших без кода.
    ///
    /// Формат намеренно тот же, что у заводских кодов: продавцу не приходится
    /// переклеивать уже промаркированный товар, а сканер и касса работают со
    /// всем ассортиментом одинаково.
    ///
    /// Первая цифра — префикс 2. Он международно зарезервирован под внутренние
    /// коды магазина, поэтому сгенерированный код гарантированно не совпадёт
    /// с заводским. Последняя цифра — контрольная, считается по стандарту.
    /// </summary>
    public static class BarcodeGenerator
    {
        /// <summary>Префикс внутренних кодов магазина.</summary>
        public const char InternalPrefix = '2';

        public const int Length = 13;

        /// <summary>Сколько цифр отведено под порядковый номер: 13 − префикс − контрольная.</summary>
        private const int SequenceDigits = Length - 2;

        /// <summary>Больше этого номера в 11 цифр не помещается.</summary>
        public static readonly long MaxSequence = (long)Math.Pow(10, SequenceDigits) - 1;

        /// <summary>Собирает полный код по порядковому номеру.</summary>
        public static string Compose(long sequence)
        {
            if (sequence < 1 || sequence > MaxSequence)
                throw new ArgumentOutOfRangeException(nameof(sequence));

            var body = InternalPrefix + sequence.ToString(new string('0', SequenceDigits));

            return body + CheckDigit(body);
        }

        /// <summary>
        /// Контрольная цифра EAN-13 по первым двенадцати: цифры на нечётных
        /// позициях берутся как есть, на чётных — втрое, и результат дополняется
        /// до ближайшего десятка.
        /// </summary>
        public static int CheckDigit(string first12)
        {
            if (first12 is null || first12.Length != Length - 1 || !first12.All(char.IsDigit))
                throw new ArgumentException("Нужны ровно 12 цифр", nameof(first12));

            var sum = 0;

            for (var i = 0; i < first12.Length; i++)
            {
                var digit = first12[i] - '0';

                sum += i % 2 == 0 ? digit : digit * 3;
            }

            return (10 - sum % 10) % 10;
        }

        /// <summary>Проверяет длину, состав и контрольную цифру.</summary>
        public static bool IsValid(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            code = code.Trim();

            if (code.Length != Length || !code.All(char.IsDigit))
                return false;

            return CheckDigit(code[..(Length - 1)]) == code[^1] - '0';
        }

        /// <summary>Код выдан нами, а не заводом.</summary>
        public static bool IsInternal(string? code)
            => IsValid(code) && code!.Trim()[0] == InternalPrefix;

        /// <summary>
        /// Порядковый номер внутреннего кода. null — код не наш: заводской,
        /// другой длины или с битой контрольной цифрой.
        /// </summary>
        public static long? SequenceOf(string? code)
        {
            if (!IsInternal(code))
                return null;

            return long.TryParse(code!.Trim()[1..(Length - 1)], out var sequence) ? sequence : null;
        }

        /// <summary>
        /// Следующий свободный номер по уже выданным кодам. Считаем от
        /// наибольшего занятого, а не от количества товаров: освободившиеся
        /// номера переиспользовать нельзя — этикетки могли остаться наклеенными.
        /// </summary>
        public static long NextSequence(IEnumerable<string?> existingBarcodes)
        {
            var max = existingBarcodes
                .Select(SequenceOf)
                .Where(s => s is not null)
                .Select(s => s!.Value)
                .DefaultIfEmpty(0)
                .Max();

            return max + 1;
        }
    }
}
