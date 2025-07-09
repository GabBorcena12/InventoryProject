namespace Inventory.API.Dtos
{
    public class RepackItemDto
    {
        public int Id { get; set; }
        public string ProductName { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal Discount { get; set; }
        public int QuantityValue { get; set; }
        public int InitialQty { get; set; }
        public int QuantityDisplayed { get; set; }
        public int SoldQty { get; set; }
        public string VariantCode { get; set; }
    }
}