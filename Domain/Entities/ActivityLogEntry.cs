using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Запись истории действий: кто, когда и что сделал. Имя пользователя хранится
    /// строкой — история должна читаться, даже если учётку потом переименуют.
    /// </summary>
    public class ActivityLogEntry
    {
        public long Id { get; private set; }

        public DateTimeOffset OccurredAt { get; private set; }

        public long UserId { get; private set; }
        public string UserName { get; private set; } = "";

        public ActivityType Type { get; private set; }

        public string Title { get; private set; } = "";
        public string? Details { get; private set; }

        /// <summary>К чему относится событие: Sale, Product, Purchase — для перехода на объект.</summary>
        public string? EntityType { get; private set; }
        public long? EntityId { get; private set; }

        private ActivityLogEntry() { }

        public ActivityLogEntry(
            long userId,
            string userName,
            ActivityType type,
            string title,
            string? details = null,
            string? entityType = null,
            long? entityId = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Activity title is required");

            UserId = userId;
            UserName = string.IsNullOrWhiteSpace(userName) ? "—" : userName;
            Type = type;
            Title = title;
            Details = details;
            EntityType = entityType;
            EntityId = entityId;
            OccurredAt = DateTimeOffset.UtcNow;
        }
    }
}
