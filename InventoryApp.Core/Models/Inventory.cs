using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace InventoryApp.Core.Models;

public class Inventory
{
    public int Id { get; set; }

    [Display(Name = "Batch No")]
    public string BatchNo { get; set; }

    [Display(Name = "SKU")]
    public string? SKU { get; set; }

    [Display(Name = "Cost Per Unit")]
    public decimal CostPerUnit { get; set; }

    [Display(Name = "Price Per Unit")]
    public decimal PricePerUnit { get; set; }

    [Display(Name = "Initial Quantity")]
    public int InitialQuantity { get; set; }

    [Display(Name = "Current Quantity In Stocks")]
    public int CurrentQty { get; set; }

    [Display(Name = "Status")]
    public InventoryStatus Status { get; set; }

    [Display(Name = "Expiry Date")]
    public DateTime? ExpiryDate { get; set; }

    [Display(Name = "Date Created")]
    [DataType(DataType.DateTime)]
    public DateTime DateCreated { get; set; } = DateTime.Now;

    [Display(Name = "Created By")]
    public string CreatedBy { get; set; } = "System";

    public bool IsDeleted { get; set; } = false;

    [Required(ErrorMessage = "Product is required")]

    [ValidateNever]
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
            
    [Display(Name = "Total Sold Qty")] 
    public int TotalSold => InitialQuantity - CurrentQty;
    public int ProductId { get; set; }

    [ValidateNever]
    public Product product { get; set; }

    public int SupplierId { get; set; }

    public Supplier? supplier { get; set; }

    [ValidateNever]
    public ICollection<RepackItem> repackItems { get; set; }

}

public enum InventoryStatus
{
    [Display(Name = "In Stock")]
    InStock,

    [Display(Name = "No Stock")]
    NoStock,

    [Display(Name = "Low Stock")]
    LowStock
}
