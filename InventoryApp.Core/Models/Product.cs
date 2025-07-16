using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryApp.Core.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        [Display(Name = "Product Alias")]
        public string? ProductAlias { get; set; }

        [Display(Name = "Volume")]
        public int Volume { get; set; } = 0;

        [Display(Name = "Unit of Measure")]

        public UnitOfMeasure UnitOfMeasure { get; set; } = 0;

        [Display(Name = "Restock Threshold")]
        public int RestockThreshold { get; set; }

        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = "System";

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;

        public bool IsDisabled { get; set; } = false;

        // POS Related Properties
        [Display(Name = "Master SKU")]
        public string MasterSku { get; set; }

        public List<ProductVariant> Variants { get; set; } = new();

        [Display(Name = "Eligible for Statutory Discount?")]
        [Description("Indicates whether this product qualifies for statutory discounts, such as Senior Citizen or PWD discounts, in accordance with local regulations.")]
        public bool IsStatutoryDiscountable { get; set; } = false;

        [Display(Name = "Maximum Quantity Eligible for Discount")]
        [Description("The maximum quantity of this product that can be purchased at the statutory discount rate in a single transaction.")]
        public int MaxQtyForStatutoryDiscountable { get; set; } = 1;

    }

    public class ProductVariant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; } // Foreign Key to Product

        [Display(Name = "SKU")]
        public string VariantSku { get; set; }

        public string VariantCode { get; set; }

        public int VariantVolume { get; set; }

        [Display(Name = "Image")]
        public string? Image { get; set; }
    }

    public enum UnitOfMeasure
    {
        [Display(Name ="Grams")]
        Grams,

        [Display(Name = "Piece")]
        Piece
    }
}