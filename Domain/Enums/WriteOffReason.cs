namespace Domain.Enums
{
    /// <summary>Почему товар списали со склада.</summary>
    public enum WriteOffReason
    {
        /// <summary>Испорчен, разбит, просрочен.</summary>
        Damage = 1,

        /// <summary>Недостача по итогам пересчёта.</summary>
        Shortage = 2,

        /// <summary>Возврат поставщику.</summary>
        ReturnToSupplier = 3,

        Other = 4
    }
}
