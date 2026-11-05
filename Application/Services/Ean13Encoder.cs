namespace Application.Services
{
    /// <summary>
    /// Переводит 13 цифр в полосы EAN-13 — для показа кода на экране.
    ///
    /// На печати полосы рисует сам принтер своей командой, здесь они нужны
    /// только чтобы владелец увидел наклейку до того, как её напечатает.
    ///
    /// Символ состоит из 95 модулей: краевые метки, шесть левых цифр,
    /// центральная метка, шесть правых. Первая цифра не кодируется полосами
    /// вовсе — её задаёт чередование двух наборов в левой половине.
    /// </summary>
    public static class Ean13Encoder
    {
        /// <summary>Ширина символа в модулях.</summary>
        public const int Modules = 95;

        // Левая половина, нечётный набор
        private static readonly string[] L =
        [
            "0001101", "0011001", "0010011", "0111101", "0100011",
            "0110001", "0101111", "0111011", "0110111", "0001011",
        ];

        // Левая половина, чётный набор
        private static readonly string[] G =
        [
            "0100111", "0110011", "0011011", "0100001", "0011101",
            "0111001", "0000101", "0010001", "0001001", "0010111",
        ];

        // Правая половина
        private static readonly string[] R =
        [
            "1110010", "1100110", "1101100", "1000010", "1011100",
            "1001110", "1010000", "1000100", "1001000", "1110100",
        ];

        /// <summary>Какой набор берётся для каждой из шести левых цифр — по первой цифре кода.</summary>
        private static readonly string[] Parity =
        [
            "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
            "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL",
        ];

        /// <summary>
        /// Полосы кода: true — чёрный модуль. Возвращает null, если код
        /// не проходит проверку, — рисовать нечитаемый символ нельзя.
        /// </summary>
        public static bool[]? Encode(string? code)
        {
            if (!BarcodeGenerator.IsValid(code))
                return null;

            var digits = code!.Trim().Select(c => c - '0').ToArray();

            var bits = new List<char>(Modules);

            // Краевая метка
            bits.AddRange("101");

            var parity = Parity[digits[0]];

            for (var i = 0; i < 6; i++)
            {
                var digit = digits[i + 1];

                bits.AddRange(parity[i] == 'L' ? L[digit] : G[digit]);
            }

            // Центральная метка
            bits.AddRange("01010");

            for (var i = 7; i < 13; i++)
                bits.AddRange(R[digits[i]]);

            bits.AddRange("101");

            return bits.Select(b => b == '1').ToArray();
        }

        /// <summary>
        /// Разбивка цифр под полосами так, как её печатают: первая цифра стоит
        /// слева от символа, остальные — двумя группами под половинами.
        /// </summary>
        public static (string Lead, string Left, string Right) SplitDigits(string code)
        {
            code = code.Trim();

            return (code[..1], code[1..7], code[7..]);
        }
    }
}
