namespace InventoryApp.MVC.Models.ViewModel.Report
{
    public class MonthlySalesDto
    {
        public DateTime DateSold { get; set; }
        public string BatchNo { get; set; }
        public string RepackDescription { get; set; }
        public int Quantity { get; set; }
        public decimal CapitalPrice { get; set; } // per unit
        public decimal SellingPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalProfit { get; set; }
        public string SalesChannel { get; set; }
        public decimal CostPriceUnit { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class TransactionSalesDto
    {
        public string? ORNo { get; set; }
        public DateTime TransactionDate { get; set; }
        public string CashierName { get; set; }
        public string PaymentMethod { get; set; }
        public decimal TotalAmount { get; set; }
        
    }

}
