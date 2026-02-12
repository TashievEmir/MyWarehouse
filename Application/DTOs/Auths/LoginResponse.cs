using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Auths
{
    public class LoginResponse
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";

        public LoginResponse(User u)
        {
            UserId = u.Id;
            Username = u.Username;
            FullName = u.FullName;
            Role = u.Role.Name;
        }
    }
}
