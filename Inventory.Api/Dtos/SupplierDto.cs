
// Dtos/SupplierDto.cs
namespace Inventory.API.Dtos
{
    public class SupplierDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string ContactNumber { get; set; }
        public string Address { get; set; }
    }
}