namespace InventoryApp.MVC.Models.ViewModel
{
    public class AuditLogDto
    {
        public DateTime Timestamp { get; set; }
        public string TransactionId { get; set; }
        public string Action { get; set; }
        public string PerformedBy { get; set; }
        public string Reason { get; set; }
        public string OldDiscountType { get; set; }
        public string NewDiscountType { get; set; }
    }

}
