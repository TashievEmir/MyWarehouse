using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Customers;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IDataContext _db;

        public CustomerService(IDataContext db)
        {
            _db = db;
        }

        public async Task<long> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
        {
            var customer = new Customer(request.Name, request.Phone, request.Email);

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);

            return customer.Id;
        }

        public async Task UpdateAsync(UpdateCustomerRequest request, CancellationToken ct)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                ?? throw new DomainException("Customer not found");

            customer.Update(request.Name, request.Phone, request.Email);

            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(long id, CancellationToken ct)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new DomainException("Customer not found");

            _db.Customers.Remove(customer);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<CustomerResponse?> GetAsync(long id, CancellationToken ct)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return customer == null ? null : new CustomerResponse(customer);
        }

        public async Task<List<CustomerResponse>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Customers
                .Select(x => new CustomerResponse(x))
                .ToListAsync(ct);
        }
    }
}
