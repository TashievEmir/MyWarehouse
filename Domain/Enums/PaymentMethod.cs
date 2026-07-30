namespace Domain.Enums
{
    public enum PaymentMethod
    {
        /// <summary>Продажи, записанные до появления способов оплаты.</summary>
        Unknown = 0,

        Cash = 1,
        Card = 2,
        Transfer = 3,

        /// <summary>В долг: чек закрыт частично или вовсе не оплачен.</summary>
        Credit = 4
    }
}
