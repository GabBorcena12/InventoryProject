namespace InventoryApp.MVC.Models.ViewModel
{
    public class UserWithRolesViewModel
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> AssignableRoles { get; set; }

        public bool IsDisabled { get; set; }
    }

}
