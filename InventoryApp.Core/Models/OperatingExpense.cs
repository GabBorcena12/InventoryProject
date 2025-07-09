using System.ComponentModel.DataAnnotations;
namespace InventoryApp.Core.Models
{
    public class OperatingExpense
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public required string Description { get; set; }
        public ExpenseCategory Category { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }

        public int Day => Date.Day;
        public int Month => Date.Month;
        public int Year => Date.Year;
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Date Created")]
        [DataType(DataType.DateTime)]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = "System";
    }
}

public enum ExpenseCategory
{
    [Display(Name = "Supplies")]
    Supplies,

    [Display(Name = "Utility Bill")]
    UtilityBill,

    [Display(Name = "Platform Fees")]
    PlatformFees,

    [Display(Name = "Shopee Sales Discount")]
    ShopeeSalesDiscount,

    [Display(Name = "Freight In")]
    FreightIn,

    [Display(Name = "Miscellaneous")]
    Miscellaneous,

    [Display(Name = "Business Licenses and Permit")]
    BusinessLicensesAndPermit,

    [Display(Name = "Equipment")]
    Equipment
}
