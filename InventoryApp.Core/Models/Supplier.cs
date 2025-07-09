using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace InventoryApp.Core.Models
{
    // Models/Supplier.cs
    public class Supplier
    {
        public int Id { get; set; }

        [Display(Name = "Supplier Name")]
        public required string Name { get; set; }

        [Display(Name = "Contact Person")]
        public required string ContactPerson { get; set; }

        [Display(Name = "Contact Number")]
        public required string ContactNumber { get; set; }

        [Display(Name = "Address")]
        public required string Address { get; set; }

        [Display(Name = "Average Days To Deliver")]
        public int AverageDaysToDeliver { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = "System";

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}
