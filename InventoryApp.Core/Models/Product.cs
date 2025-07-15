using System.ComponentModel.DataAnnotations;

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
    }

    public enum UnitOfMeasure
    {
        [Display(Name ="Grams")]
        Grams,

        [Display(Name = "Piece")]
        Piece
    }
}