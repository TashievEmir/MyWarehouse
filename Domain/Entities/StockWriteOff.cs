using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Списание товара со склада: остаток уменьшается, а причина и автор остаются
    /// в истории — иначе расхождение потом никак не объяснить.
    /// </summary>
    public class StockWriteOff
    {
        public long Id { get; private set; }

        public long ProductId { get; private set; }
        public Product Product { get; private set; } = null!;

        public long UserId { get; private set; }

        public int Quantity { get; private set; }

        public WriteOffReason Reason { get; private set; }
        public string? Comment { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        private StockWriteOff() { }

        public StockWriteOff(long productId, long userId, int quantity, WriteOffReason reason, string? comment)
        {
            if (quantity <= 0)
                throw new DomainException("Write-off quantity must be positive");

            ProductId = productId;
            UserId = userId;
            Quantity = quantity;
            Reason = reason;
            Comment = comment;
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
