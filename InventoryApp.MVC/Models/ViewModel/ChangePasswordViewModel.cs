using System.ComponentModel.DataAnnotations;

namespace InventoryApp.Models
{
    public class ChangePasswordViewModel
    {
        [Required]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
    }
}
