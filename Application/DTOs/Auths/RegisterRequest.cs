using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Auths
{
    public class RegisterRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Firstname { get; set; } = "";
        public string Patronymic { get; set; } = "";
        public List<int> RoleIds { get; set; } = new();
    }
}
