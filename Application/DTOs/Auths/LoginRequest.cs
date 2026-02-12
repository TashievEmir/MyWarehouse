using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Auths
{
    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
