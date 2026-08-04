namespace Application.DTOs.Users
{
    public class RoleResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        /// <summary>Подпись для интерфейса: в базе роли лежат латиницей.</summary>
        public string Title { get; set; } = "";

        public override string ToString() => Title;
    }

    public class UserListItemResponse
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string? Patronymic { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<int> RoleIds { get; set; } = new();

        /// <summary>Названия ролей для интерфейса.</summary>
        public List<string> RoleTitles { get; set; } = new();

        /// <summary>За сотрудником числятся продажи, платежи или списания — удалять нельзя.</summary>
        public bool HasHistory { get; set; }
    }

    public class SaveUserRequest
    {
        /// <summary>0 — создаём нового.</summary>
        public long Id { get; set; }

        public string Username { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string? Patronymic { get; set; }

        /// <summary>При правке пустой пароль означает «оставить прежний».</summary>
        public string? Password { get; set; }

        public bool IsActive { get; set; } = true;

        public List<int> RoleIds { get; set; } = new();

        /// <summary>Кто выполняет операцию — для истории и проверки прав.</summary>
        public long ActorId { get; set; }
    }
}
