using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Receipts
{
    /// <summary>Блок печатного чека.</summary>
    public class ReceiptBlockResponse
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Hint { get; set; } = "";
        public bool IsEnabled { get; set; }

        /// <summary>Обязательный блок — выключить нельзя.</summary>
        public bool IsLocked { get; set; }
    }

    public class ReceiptTemplateResponse
    {
        public string ShopName { get; set; } = "";
        public string? Tin { get; set; }
        public string? Address { get; set; }
        public string? FooterText { get; set; }

        public List<ReceiptBlockResponse> Blocks { get; set; } = new();
    }

    /// <summary>Сохранение шаблона: блоки приходят уже в нужном порядке.</summary>
    public class SaveReceiptTemplateRequest
    {
        public long UserId { get; set; }

        public string ShopName { get; set; } = "";
        public string? Tin { get; set; }
        public string? Address { get; set; }
        public string? FooterText { get; set; }

        public List<ReceiptBlockState> Blocks { get; set; } = new();
    }

    public class ReceiptBlockState
    {
        public string Key { get; set; } = "";
        public bool IsEnabled { get; set; }
    }
}
