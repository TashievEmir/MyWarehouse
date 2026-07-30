using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    public class CreateSaleRequest
    {
        public long? CustomerId { get; set; }
        public long UserId { get; set; }

        /// <summary>Скидка на весь чек, в деньгах.</summary>
        public decimal DiscountAmount { get; set; }

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        /// <summary>
        /// Сколько клиент вносит. Для оплаты не в долг сумма сверх итога считается
        /// сдачей и не сохраняется, для долга это предоплата.
        /// </summary>
        public decimal PaidAmount { get; set; }

        public List<SaleLineRequest> Items { get; set; } = new();
    }
}
