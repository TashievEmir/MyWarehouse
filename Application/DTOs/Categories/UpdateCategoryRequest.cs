using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Categories
{
    public class UpdateCategoryRequest
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }
}
