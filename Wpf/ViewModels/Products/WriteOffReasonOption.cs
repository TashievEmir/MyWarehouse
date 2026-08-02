using Domain.Enums;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>Причина списания с человеческой подписью для выпадающего списка.</summary>
public class WriteOffReasonOption
{
    public WriteOffReason Reason { get; }
    public string Name { get; }

    private WriteOffReasonOption(WriteOffReason reason, string name)
    {
        Reason = reason;
        Name = name;
    }

    public static IReadOnlyList<WriteOffReasonOption> All { get; } =
    [
        new(WriteOffReason.Damage, Loc.T("WriteOff_Damage")),
        new(WriteOffReason.Shortage, Loc.T("WriteOff_Shortage")),
        new(WriteOffReason.ReturnToSupplier, Loc.T("WriteOff_ReturnToSupplier")),
        new(WriteOffReason.Other, Loc.T("WriteOff_Other"))
    ];
}
