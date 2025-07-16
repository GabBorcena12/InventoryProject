using Microsoft.AspNetCore.Identity;

namespace InventoryApp.Core.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Custom fields
        public bool IsConfirmed { get; set; } = false;
        public bool IsDisabled { get; set; } = false;
    }
}
