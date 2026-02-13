using Domain.Entities;

namespace Application.DTOs.Auths
{
    public class LoginResponse
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Firstname { get; set; } = "";
        public string Patronymic { get; set; } = "";
        public List<string> Roles { get; set; } = new();

        public LoginResponse(User u)
        {
            UserId = u.Id;
            Username = u.Username;
            Lastname = u.LastName;
            Firstname = u.FirstName;
            Patronymic = u.Patronymic;
            Roles = u.Roles.Select(r => r.Role.Name).ToList();
        }
    }
}
