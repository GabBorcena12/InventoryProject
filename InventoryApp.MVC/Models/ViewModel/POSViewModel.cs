using InventoryApp.Core.Models.PosModels;

namespace InventoryApp.MVC.Models.ViewModel
{
    public class POSViewModel
    {
        public List<Product> POSProducts { get; set; } = new();
        public List<Discount> POSDiscounts { get; set; } = new();
        public class CompleteTransactionRequest
        {
            public TransactionHeaderDto? Header { get; set; }
            public List<TransactionDetailDto>? Cart { get; set; }
            public List<AuditLogDto>? AuditLogs { get; set; }
        }

        public class TransactionHeaderDto
        {
            public string? ORNumber { get; set; }
            public DateTime TransactionDate { get; set; }
            public string? PaymentMethod { get; set; }
            public decimal RegularDiscount { get; set; }
            public decimal StatutoryDiscount { get; set; }
            public decimal VATIncluded { get; set; }
            public decimal VATExcluded { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal AmountTendered { get; set; }
            public decimal ChangeAmount { get; set; }
            public string? CashierName { get; set; }
            public string? TerminalId { get; set; }
            public string? Cart { get; set; }

        }

        public class TransactionDetailDto
        {
            public string? Name { get; set; }
            public decimal Qty { get; set; }
            public decimal PricePerKg { get; set; }
            public decimal StepQty { get; set; }
            public string? Sku { get; set; }
            public bool? IsDiscount { get; set; }
            public bool? IsRegularItem { get; set; }
            public bool? IsStatutoryDiscountable { get; set; }
            public bool? IsDiscountRemovableOnUpdateCart { get; set; }
            public bool? IsSeniorDiscountAppliedToItem { get; set; }
            public bool? IsBuyTakeDiscount { get; set; }
            public bool? IsRegularDiscountItem { get; set; }
            public int? MaxQtyForStatutoryDiscountable { get; set; }
            public string? ReplacedSKU { get; set; }
            public string? RelatedSKUForSeniorPwdDiscount { get; set; }
            public string? RemoveLabel { get; set; }
            public string? DiscountRateLabel { get; set; }
        }

        public class AuditLogDto
        {
            public DateTime Timestamp { get; set; }
            public string? Action { get; set; }
            public DiscountTypeDto? OldDiscountType { get; set; }
            public DiscountTypeDto? NewDiscountType { get; set; }
            public string? PerformedBy { get; set; }
            public string? Reason { get; set; }
            public string? TransactionId { get; set; }
            public List<AuditLogItemDto>? ItemsAffected { get; set; }
        }

        public class DiscountTypeDto
        {
            public string? Name { get; set; }
            public decimal Amount { get; set; }
        }

        public class AuditLogItemDto
        {
            public string? Name { get; set; }
            public string? Sku { get; set; }
            public decimal? DiscountAmount { get; set; }
        }

    }
}
