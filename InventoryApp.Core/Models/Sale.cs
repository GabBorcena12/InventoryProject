namespace InventoryApp.Core.Models
{
    public class Sale
    {
        public int Id { get; set; }

        public int? InventoryId { get; set; }
        public Inventory? Inventory { get; set; }

        public int? RepackItemId { get; set; }
        public RepackItem? RepackItem { get; set; }

        public int? DisplayItemId { get; set; }
        public DisplayItem? DisplayItem { get; set; }

        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }

        public string SalesChannel { get; set; }

        public string CreatedBy { get; set; } = "System";

        public DateTime DateSold { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}
