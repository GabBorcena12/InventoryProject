namespace InventoryApp.MVC.Models.ViewModel
{
    public class CartItem
    {
        public int cartId { get; set; }
        public string name { get; set; }
        public int qty { get; set; }
        public decimal pricePerKg { get; set; }
        public int stepQty { get; set; }
        public string sku { get; set; }
        public bool isStatutoryDiscountable { get; set; }
        public bool isRegularItem { get; set; }
        public bool isDiscountRemovableOnUpdateCart { get; set; }
        public int maxQtyForStatutoryDiscountable { get; set; }
    }
}
