namespace Application.DTOs.Products
{
    /// <summary>
    /// Правка остатка прямо в каталоге: не приход и не списание, а исправление
    /// учёта по факту пересчёта. Количество задаётся целиком, а не приращением.
    /// </summary>
    public class AdjustStockRequest
    {
        public long ProductId { get; set; }
        public long UserId { get; set; }

        public int Quantity { get; set; }
    }
}
