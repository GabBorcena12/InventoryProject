// Models/DisplayItem.cs
using InventoryApp.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
public class DisplayItem
{
    public int Id { get; set; }

    [Display(Name = "Displayed Quantity")]
    public int QuantityDisplayed { get; set; }

    [Display(Name = "Quantity Sold")]
    public int QuantitySold { get; set; } = 0;

    [Display(Name = "Sold Out")]
    public bool IsSoldOut { get; set; } = false;

    [Display(Name = "Date Displayed")]
    public DateTime DisplayedOn { get; set; }

    [Display(Name = "Displayed By")]
    public string DisplayedBy { get; set; }

    [Display(Name = "RepackItem Id")]
    public int RepackItemId { get; set; }

    [ValidateNever]
    public RepackItem RepackItem { get; set; }   

    public bool IsDeleted { get; set; } = false;

}
