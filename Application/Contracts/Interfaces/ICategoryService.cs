using Application.DTOs.Categories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface ICategoryService
    {
        Task<long> CreateAsync(CreateCategoryRequest request, CancellationToken ct);

        Task UpdateAsync(UpdateCategoryRequest request, CancellationToken ct);

        Task DeleteAsync(long id, CancellationToken ct);

        Task<CategoryResponse?> GetAsync(long id, CancellationToken ct);

        Task<List<CategoryResponse>> GetAllAsync(CancellationToken ct);
    }
}
