using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Categories
{
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }
}
