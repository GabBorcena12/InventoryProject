// Dtos/SaleDto.cs
namespace Inventory.API.Dtos
{
    public class SaleDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string SalesChannel { get; set; }
        public DateTime DateSold { get; set; }
    }
}