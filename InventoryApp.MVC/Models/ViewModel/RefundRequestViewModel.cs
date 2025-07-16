namespace InventoryApp.MVC.Models.ViewModel
{
    public class RefundRequestViewModel
    {
        public int TransactionHeaderId { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public bool IsBroken { get; set; }
        public string Reason { get; set; }
    }

}
