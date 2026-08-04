namespace Domain.Enums
{
    /// <summary>Что именно произошло — от типа зависят иконка и цвет в истории.</summary>
    public enum ActivityType
    {
        SaleClosed       = 1,
        PriceChanged     = 2,
        CreditSale       = 3,
        PurchaseSaved    = 4,
        StockWrittenOff  = 5,
        DebtPaid         = 6,
        ProductCreated   = 7,
        SaleReturned     = 8,
        LoggedIn         = 9,
        TemplateSaved    = 10,
        StockAdjusted    = 11,
        UserCreated      = 12,
        UserUpdated      = 13,
        UserDeleted      = 14,
    }
}
