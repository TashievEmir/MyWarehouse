using Application.DTOs.Customers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface ICustomerService
    {
        Task<long> CreateAsync(CreateCustomerRequest request, CancellationToken ct);

        Task UpdateAsync(UpdateCustomerRequest request, CancellationToken ct);

        Task DeleteAsync(long id, CancellationToken ct);

        Task<CustomerResponse?> GetAsync(long id, CancellationToken ct);

        Task<List<CustomerResponse>> GetAllAsync(CancellationToken ct);
    }
}
