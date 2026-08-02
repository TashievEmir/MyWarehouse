using Application.DTOs.Receipts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    /// <summary>Шаблон печатного чека: что печатается и в каком порядке.</summary>
    public interface IReceiptTemplateService
    {
        /// <summary>Возвращает сохранённый шаблон, а если его ещё нет — значения по умолчанию.</summary>
        Task<ReceiptTemplateResponse> GetAsync(CancellationToken ct);

        Task SaveAsync(SaveReceiptTemplateRequest request, CancellationToken ct);
    }
}
