using Domain.Entities;

namespace Application.DTOs.Suppliers
{
    public class SupplierResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";

        public SupplierResponse()
        {
        }

        public SupplierResponse(Supplier supplier)
        {
            Id = supplier.Id;
            Name = supplier.Name;
        }
    }

    public class CreateSupplierRequest
    {
        public string Name { get; set; } = "";
    }
}
