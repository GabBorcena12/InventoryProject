using InventoryApp.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace InventoryApp.DbContext.Data_Seeder
{
    public static class DataSeeder
    {
        public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roleNames = { "SuperUser", "Inventory-Admin","POS-Admin", "Inventory-User", "POS-User", "Default" };

            // Create roles if they don't exist
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create default admin user
            var adminEmail = "admin@gaji.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail.Replace("@gaji.com", "").Replace("@gmail.com", ""),
                    Email = adminEmail,
                    EmailConfirmed = true,
                    IsConfirmed = true,
                    LockoutEnabled = false,
                };

                var result = await userManager.CreateAsync(adminUser, $"Admin@2025");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "SuperUser");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error: {error.Description}");
                    }
                }
            }
        }
    }
}
