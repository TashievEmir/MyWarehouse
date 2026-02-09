using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Categories
{
    public class CategoryResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }

        public CategoryResponse(Category c)
        {
            Id = c.Id;
            Name = c.Name;
            Description = c.Description;
        }
    }
}
