using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Receipts;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

using Application.Localization;

namespace Application.Services
{
    public class ReceiptTemplateService : IReceiptTemplateService
    {
        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public ReceiptTemplateService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
        }

        // Известные блоки чека: подписи, подсказки и обязательность живут в коде,
        // в базе хранится только порядок и признак «включён»
        private static readonly IReadOnlyList<(string Key, string TextKey, bool Locked, bool Default)> Known =
        [
            ("logo",     "Block_Logo",     false, true),
            ("address",  "Block_Address",  false, true),
            ("number",   "Block_Number",   true,  true),
            ("cashier",  "Block_Cashier",  false, true),
            ("barcode",  "Block_Barcode",  false, false),
            ("customer", "Block_Customer", false, true),
            ("qr",       "Block_Qr",       false, false),
        ];

        private static string DefaultShopName => Tr.T("Template_DefaultShopName");
        private static string DefaultFooter   => Tr.T("Template_DefaultFooter");

        public async Task<ReceiptTemplateResponse> GetAsync(CancellationToken ct)
        {
            var template = await _db.ReceiptTemplates.AsNoTracking().FirstOrDefaultAsync(ct);

            var response = new ReceiptTemplateResponse
            {
                ShopName   = template?.ShopName ?? DefaultShopName,
                Tin        = template?.Tin,
                Address    = template?.Address,
                FooterText = template?.FooterText ?? DefaultFooter,
            };

            response.Blocks = BuildBlocks(template?.Blocks);

            return response;
        }

        public async Task SaveAsync(SaveReceiptTemplateRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopName))
                throw new DomainException(Tr.T("Err_NeedShopName"));

            if (request.UserId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            // Обязательные блоки нельзя выключить даже запросом мимо интерфейса
            var blocks = request.Blocks
                .Where(b => Known.Any(k => k.Key == b.Key))
                .Select(b => new
                {
                    b.Key,
                    Enabled = Known.First(k => k.Key == b.Key).Locked || b.IsEnabled,
                })
                .ToList();

            foreach (var known in Known)
            {
                if (blocks.All(b => b.Key != known.Key))
                    blocks.Add(new { known.Key, Enabled = known.Default });
            }

            var packed = string.Join(",", blocks.Select(b => $"{b.Key}:{(b.Enabled ? 1 : 0)}"));

            var template = await _db.ReceiptTemplates.FirstOrDefaultAsync(ct);

            if (template is null)
            {
                template = new ReceiptTemplate(request.ShopName, request.Tin, request.Address, request.FooterText, packed);

                _db.ReceiptTemplates.Add(template);
            }
            else
            {
                template.Update(request.ShopName, request.Tin, request.Address, request.FooterText, packed);
            }

            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                request.UserId,
                ActivityType.TemplateSaved,
                Tr.T("Log_TemplateSaved"),
                Tr.F("Log_TemplateDetails", template.ShopName, blocks.Count(b => b.Enabled), blocks.Count),
                "ReceiptTemplate",
                template.Id,
                ct);
        }

        // Разбирает «logo:1,address:0» и дополняет недостающие блоки значениями по умолчанию
        private static List<ReceiptBlockResponse> BuildBlocks(string? packed)
        {
            var saved = (packed ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split(':'))
                .Where(parts => parts.Length == 2)
                .Select((parts, index) => new { Key = parts[0], Enabled = parts[1] == "1", Order = index })
                .ToList();

            var result = new List<ReceiptBlockResponse>();

            foreach (var item in saved)
            {
                var known = Known.FirstOrDefault(k => k.Key == item.Key);

                if (known.Key is null)
                    continue;

                result.Add(new ReceiptBlockResponse
                {
                    Key       = known.Key,
                    Name      = Tr.T(known.TextKey + "_Name"),
                    Hint      = Tr.T(known.TextKey + "_Hint"),
                    IsLocked  = known.Locked,
                    IsEnabled = known.Locked || item.Enabled,
                });
            }

            foreach (var known in Known)
            {
                if (result.All(b => b.Key != known.Key))
                {
                    result.Add(new ReceiptBlockResponse
                    {
                        Key       = known.Key,
                        Name      = Tr.T(known.TextKey + "_Name"),
                        Hint      = Tr.T(known.TextKey + "_Hint"),
                        IsLocked  = known.Locked,
                        IsEnabled = known.Locked || known.Default,
                    });
                }
            }

            return result;
        }
    }
}
