using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Шаблон печатного чека: шапка магазина, подвал и порядок блоков.
    /// Строка блоков хранится как «ключ:вкл» через запятую — таблица на семь
    /// строк того не стоит, а порядок и состав меняются целиком.
    /// </summary>
    public class ReceiptTemplate
    {
        public long Id { get; private set; }

        public string ShopName { get; private set; } = "";
        public string? Tin { get; private set; }
        public string? Address { get; private set; }
        public string? FooterText { get; private set; }

        /// <summary>«logo:1,address:1,number:1,cashier:1,barcode:0,customer:1,qr:0»</summary>
        public string Blocks { get; private set; } = "";

        public DateTimeOffset UpdatedAt { get; private set; }

        private ReceiptTemplate() { }

        public ReceiptTemplate(string shopName, string? tin, string? address, string? footerText, string blocks)
        {
            Update(shopName, tin, address, footerText, blocks);
        }

        public void Update(string shopName, string? tin, string? address, string? footerText, string blocks)
        {
            if (string.IsNullOrWhiteSpace(shopName))
                throw new DomainException("Название магазина обязательно");

            ShopName = shopName.Trim();
            Tin = string.IsNullOrWhiteSpace(tin) ? null : tin.Trim();
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            FooterText = string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim();
            Blocks = blocks;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
