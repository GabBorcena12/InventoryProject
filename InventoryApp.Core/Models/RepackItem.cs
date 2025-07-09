using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryApp.Core.Models
{
    public class RepackItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        [Display(Name = "Price Per Unit")]
        public decimal PricePerUnit { get; set; }

        [Display(Name = "Discount")]
        public decimal Discount { get; set; } = 0;

        [Display(Name = "Volume")]
        public int QuantityValue { get; set; }

        [Display(Name = "Initial Quantity")]
        public int InitialQty { get; set; }

        [Display(Name = "Displayed Item Quantity")]
        public int QuantityDisplayed { get; set; }

        [Display(Name = "Sold Quantity")]
        public int SoldQty { get; set; } = 0;

        [Display(Name = "Total Sales")]
        public decimal TotalSales => PricePerUnit * SoldQty;

        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? product { get; set; }

        [Display(Name = "Inventory ID")]
        public int InventoryId { get; set; }

        [ValidateNever]
        public Inventory? inventory { get; set; }

        [Display(Name = "Variant Code")]
        public string VariantCode =>
            inventory != null
                ? $"{inventory.BatchNo}--{inventory.product?.ProductName}--{QuantityValue}-{inventory?.product?.UnitOfMeasure}"
                : string.Empty;

        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = "System";

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}
