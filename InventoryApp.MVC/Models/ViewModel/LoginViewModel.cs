using System.ComponentModel.DataAnnotations;

namespace InventoryApp.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

}