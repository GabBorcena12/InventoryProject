using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryApp.Core.Models.PosModels
{
    /* Discount and Product Data*/
    public class Discount
    {
        [Key]
        public int id { get; set; }
        [Required]
        [Display(Name = "SKU Discount")]
        public string discountSKU { get; set; } // example : DISC0001

        [Required]
        [Display(Name = "Name")]
        public string name { get; set; } // example : TOEI CAT TUNA 250g BUY 1 TAKE 1 , TOEI CAT 10% off 

        [Display(Name = "Discount Type")]
        public string skuDiscountType { get; set; } // Options : REGULAR DISCOUNT, STATUTORY, BUY&TAKE

        [Display(Name = "Rate Type")]
        public string? amountType { get; set; } //  Options :  "amount" or "percent"

        [Display(Name = "Discount Rate")]
        public decimal amount { get; set; }
        [Display(Name = "Buy Quantity")]
        public int buyQty { get; set; }

        [Display(Name = "Take Quantity")]
        public int takeQty { get; set; }

        [Display(Name = "Related Item SKU")]
        public string relatedSKU { get; set; } // Sku having the discount

        [Display(Name = "Activate Discount")]
        public bool isActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class Product
    {
        [Key]
        public int id { get; set; }
        [Required]
        [Display(Name = "Product Name")]
        public string Name { get; set; }
        [Required]
        [Display(Name = "SKU")]
        public string Sku { get; set; }

        [Display(Name = "Price")]
        public decimal PricePerKg { get; set; }
        [Display(Name = "Qty Displayed")]
        public int QtyDisplayed { get; set; }
        [Display(Name = "Qty Sold")]
        public int QtySold { get; set; }
        [Display(Name = "Image File Name")]
        public string ImageUrl { get; set; } = null;
        [Display(Name = "Statutory Discountable")]
        public bool IsStatutoryDiscountable { get; set; } = false;
        [Display(Name = "No of Items Statutory Discountable")]
        public int maxQtyForStatutoryDiscountable { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    
    /* Auditing Models*/
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public int? OldDiscountTypeId { get; set; }

        [ForeignKey(nameof(OldDiscountTypeId))]
        public DiscountType OldDiscountType { get; set; }
        public int? NewDiscountTypeId { get; set; }

        [ForeignKey(nameof(NewDiscountTypeId))]
        public DiscountType NewDiscountType { get; set; }

        public string PerformedBy { get; set; }
        public string?  Reason { get; set; }
        public string TransactionId { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation to log items
        public List<AuditLogItem> ItemsAffected { get; set; } = new();
    }
    
    public class DiscountType
    {
        [Key]
        public int DiscountTypeId { get; set; } // primary key
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    public class AuditLogItem
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Sku { get; set; }
        public decimal DiscountAmount { get; set; }

        // Link back to AuditLog
        public int AuditLogId { get; set; }
        [ForeignKey(nameof(AuditLogId))]
        public AuditLog AuditLog { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    [Index(nameof(ORNumber), IsUnique = true)]
    public class TransactionHeader
    {
        [Key]
        public int TransactionHeaderId { get; set; }
        public string ORNumber { get; set; }
        public DateTime TransactionDate { get; set; }
        public string PaymentMethod { get; set; } // e.g., Cash, Card, GCash
        public decimal RegularDiscount { get; set; }
        public decimal StatutoryDiscount { get; set; }
        public decimal VATIncluded { get; set; }
        public decimal VATExcluded { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountTendered { get; set; }
        public decimal ChangeAmount { get; set; }

        // Cashier / Store Info
        public string CashierName { get; set; }
        public string TerminalId { get; set; }
        public string Cart { get; set; }
        public bool IsVoided { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // Relationships
        public virtual ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();

    }

    public class TransactionDetail
    {
        [Key]
        public int TransactionDetailId { get; set; }

        // Link to the header
        public int TransactionHeaderId { get; set; }
        [ForeignKey(nameof(TransactionHeaderId))]
        public TransactionHeader TransactionHeader { get; set; }


        // Item Info
        public string Name { get; set; }
        public decimal Qty { get; set; }
        public decimal PricePerKg { get; set; }
        public decimal StepQty { get; set; }
        public string Sku { get; set; }

        // Discount & Flags
        public bool? IsDiscount { get; set; }
        public bool? IsRegularItem { get; set; }
        public bool? IsStatutoryDiscountable { get; set; }
        public bool? IsDiscountRemovableOnUpdateCart { get; set; }
        public bool? IsSeniorDiscountAppliedToItem { get; set; }
        public bool? IsBuyTakeDiscount { get; set; }
        public bool? IsRegularDiscountItem { get; set; }

        public int? MaxQtyForStatutoryDiscountable { get; set; }
        public string ReplacedSKU { get; set; }
        public string RelatedSKUForSeniorPwdDiscount { get; set; }
        public string RemoveLabel { get; set; }
        public string DiscountRateLabel { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    public class TransactionRepackItem
    {
        [Key]
        public int Id { get; set; }

        // Link to TransactionDetail
        public int TransactionDetailId { get; set; }
        [ForeignKey(nameof(TransactionDetailId))]
        public TransactionDetail TransactionDetail { get; set; }

        // Link to RepackItem
        public int RepackItemId { get; set; }
        [ForeignKey(nameof(RepackItemId))]
        public RepackItem RepackItem { get; set; }

        public int AllocatedQty { get; set; }
        public bool IsVoided { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    public class CreditMemo
    {
        [Key]
        public int Id { get; set; }

        public string CreditMemoNumber { get; set; } = string.Empty;

        public int TransactionDetailId { get; set; }

        [ForeignKey("TransactionDetailId")]
        public TransactionDetail TransactionDetail { get; set; } // transaction detail table
        public int? SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sale Sale { get; set; } //released items table
        public string TransactionOrNumber { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public decimal Qty { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Reason { get; set; }
        public bool IsBroken { get; set; } = false;    

        public string? IssuedBy { get; set; }
        public bool IsVoided { get; set; } = false;
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    }

}
