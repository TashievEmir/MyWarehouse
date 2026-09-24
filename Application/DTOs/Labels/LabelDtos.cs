namespace Application.DTOs.Labels
{
    /// <summary>Товар в списке маркировки.</summary>
    public class LabelProductResponse
    {
        public long ProductId { get; set; }

        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }

        public string CategoryName { get; set; } = "";

        public decimal PricePerUnit { get; set; }
        public int InStock { get; set; }

        public bool HasBarcode => !string.IsNullOrWhiteSpace(Barcode);

        /// <summary>Код выдан нами, а не заводом — такой можно перевыпустить.</summary>
        public bool IsInternalBarcode { get; set; }
    }

    /// <summary>Что попадёт на этикетку и сколько штук печатать.</summary>
    public class LabelOptions
    {
        public bool ShowName { get; set; } = true;
        public bool ShowPrice { get; set; } = true;
        public bool ShowSku { get; set; } = true;

        public int Copies { get; set; } = 1;
    }
}
