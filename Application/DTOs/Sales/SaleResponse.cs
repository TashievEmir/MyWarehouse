using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    public class SaleResponse
    {
        public long SaleId { get; set; }
        public DateTimeOffset SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Debt => TotalAmount - PaidAmount;
        public bool IsCredit { get; set; }

        public long? CustomerId { get; set; }
        public long UserId { get; set; }

        public List<SaleItemResponse> Items { get; set; } = new();

        public SaleResponse() { }

        public SaleResponse(Sale sale)
        {
            SaleId = sale.Id;
            SaleDate = sale.SaleDate;
            TotalAmount = sale.TotalAmount;
            PaidAmount = sale.PaidAmount;
            IsCredit = sale.IsCredit;
            CustomerId = sale.CustomerId;
            UserId = sale.UserId;

            Items = sale.SaleItems
                .Select(x => new SaleItemResponse(x))
                .ToList();
        }
    }
}
