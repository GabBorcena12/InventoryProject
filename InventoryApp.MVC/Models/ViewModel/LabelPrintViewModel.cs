using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventoryApp.Models
{
    public class LabelPrintViewModel
    {
        [Required(ErrorMessage = "Please select a product.")]
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        public string? ProductName { get; set; } // Optional, used in preview

        [Required(ErrorMessage = "Please select packaging type.")]
        [Display(Name = "Packaging")]
        public string PackagingType { get; set; }

        [Required(ErrorMessage = "Please enter the weight or number of pieces.")]
        [Display(Name = "Weight or Pieces")]
        public string WeightOrPieces { get; set; }

        [Required(ErrorMessage = "Please enter the selling price.")]
        [Range(0.01, 999999, ErrorMessage = "Enter a valid price.")]
        [Display(Name = "Selling Price")]
        public decimal SellingPrice { get; set; }

        [Display(Name = "SKU")]
        public string SKU { get; set; }

        [Required(ErrorMessage = "Please specify how many labels to print.")]
        [Range(1, 1000, ErrorMessage = "Number of labels must be between 1 and 1000.")]
        [Display(Name = "No. of Labels to Print")]
        public int NumberOfLabels { get; set; }

        // Dropdown for product selection
        public List<SelectListItem> Products { get; set; } = new();
        public string? QrCodeImageBase64 { get; set; }

    }
}
