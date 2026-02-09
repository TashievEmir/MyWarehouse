using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Categories;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IDataContext _db;

        public CategoryService(IDataContext db)
        {
            _db = db;
        }

        public async Task<long> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
        {
            var category = new Category(request.Name, request.Description);

            _db.Categories.Add(category);
            await _db.SaveChangesAsync(ct);

            return category.Id;
        }

        public async Task UpdateAsync(UpdateCategoryRequest request, CancellationToken ct)
        {
            var category = await _db.Categories
                .FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                ?? throw new DomainException("Category not found");

            category.Update(request.Name, request.Description);

            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(long id, CancellationToken ct)
        {
            var category = await _db.Categories
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new DomainException("Category not found");

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<CategoryResponse?> GetAsync(long id, CancellationToken ct)
        {
            var category = await _db.Categories
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return category == null ? null : new CategoryResponse(category);
        }

        public async Task<List<CategoryResponse>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Categories
                .Select(x => new CategoryResponse(x))
                .ToListAsync(ct);
        }
    }
}
