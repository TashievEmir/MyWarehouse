using Domain.Enums;

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
        new(WriteOffReason.Damage, "Порча"),
        new(WriteOffReason.Shortage, "Недостача"),
        new(WriteOffReason.ReturnToSupplier, "Возврат поставщику"),
        new(WriteOffReason.Other, "Другое")
    ];
}
