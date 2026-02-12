using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Application.DTOs.Auths
{
    public class LoginResponse
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public List<string> Roles { get; set; } = new();

        public LoginResponse(User u)
        {
            UserId = u.Id;
            Username = u.Username;
            FullName = u.FullName;
            Roles = u.Roles.Select(r => r.Role.Name).ToList();
        }
    }
}
