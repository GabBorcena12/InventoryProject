// Dtos/InventoryDto.cs
namespace Inventory.API.Dtos
{
    public class InventoryDto
    {
        public int Id { get; set; }
        public string BatchNo { get; set; }
        public string SKU { get; set; }
        public decimal CostPerUnit { get; set; }
        public decimal PricePerUnit { get; set; }
        public int InitialQuantity { get; set; }
        public int CurrentQty { get; set; }
        public string Status { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string ProductName { get; set; }
        public string SupplierName { get; set; }
        public List<RepackItemDto> RepackItems { get; set; }
        public List<SaleDto> Sales { get; set; }
    }
}