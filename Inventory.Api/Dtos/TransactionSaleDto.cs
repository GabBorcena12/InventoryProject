namespace Inventory.Api.Dtos
{
    public class TransactionSalesDto
    {
        public string? ORNo { get; set; }
        public DateTime TransactionDate { get; set; }
        public string CashierName { get; set; }
        public string PaymentMethod { get; set; }
        public decimal TotalAmount { get; set; }

    }
}
