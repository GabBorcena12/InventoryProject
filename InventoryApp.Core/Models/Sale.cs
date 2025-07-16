namespace InventoryApp.Core.Models
{
    public class Sale
    {
        public int Id { get; set; }

        public int? InventoryId { get; set; }
        public Inventory? Inventory { get; set; }

        public int? RepackItemId { get; set; }
        public RepackItem? repackItem { get; set; }

        public int? DisplayItemId { get; set; }
        public DisplayItem? displayItem { get; set; }

        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        
        public string SalesChannel { get; set; }

        public DateTime DateSold { get; set; }

        public string SoldBy { get; set; }

        public string? Reason { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
