using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Поставщик. Заводится прямо на приёмке и дальше подставляется из списка,
    /// чтобы одно и то же имя не писали каждый раз заново и по-разному.
    /// </summary>
    public class Supplier
    {
        public long Id { get; private set; }

        public string Name { get; private set; }

        private Supplier() { }

        public Supplier(string name)
        {
            Name = Normalize(name);
        }

        public void Rename(string name)
        {
            Name = Normalize(name);
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Supplier name is required");

            return name.Trim();
        }
    }
}
