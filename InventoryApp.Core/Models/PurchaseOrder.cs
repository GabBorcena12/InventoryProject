namespace InventoryApp.Core.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public required string BatchNo { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string? SKU { get; set; }
        public required string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal CostPerUnit { get; set; } 
        public decimal TotalPurchaseCost => Quantity * CostPerUnit;
        public DeliveryStatus deliveryStatus{ get; set; }
        public string? Notes { get; set; }

        public int SupplierId { get; set; }
        public required Supplier Supplier { get; set; }

        public int DaysToDeliver => (DeliveryDate - OrderDate).Days;
        public bool IsDeleted { get; set; } = false;
    }
}

public enum DeliveryStatus
{ 
    Ordered,
    Delivered,
    Incomplete,
    Problematic
}