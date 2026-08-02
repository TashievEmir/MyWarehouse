using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Activity
{
    public class ActivityEntryResponse
    {
        public long Id { get; set; }
        public DateTimeOffset OccurredAt { get; set; }

        public long UserId { get; set; }
        public string UserName { get; set; } = "";

        public ActivityType Type { get; set; }

        public string Title { get; set; } = "";
        public string? Details { get; set; }

        public string? EntityType { get; set; }
        public long? EntityId { get; set; }
    }

    /// <summary>Пользователь в фильтре истории.</summary>
    public class ActivityUserResponse
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = "";
    }
}
