// Models/DisplayItem.cs
using System.ComponentModel.DataAnnotations;
using InventoryApp.Core.Models;
public class DisplayItem
{
    public int Id { get; set; }

    public int RepackItemId { get; set; }
    
    public RepackItem RepackItem { get; set; }

    public int QuantityDisplayed { get; set; }

    public int QuantitySold { get; set; } = 0;

    public bool IsSoldOut { get; set; } = false;

    public DateTime DisplayedOn { get; set; } = DateTime.Now;

    public bool IsDeleted { get; set; } = false;

    [Display(Name = "Date Created")]
    [DataType(DataType.DateTime)]
    public DateTime DateCreated { get; set; } = DateTime.Now;

    [Display(Name = "Created By")]
    public string CreatedBy { get; set; } = "System";

}
