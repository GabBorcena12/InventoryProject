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

            string[] roleNames = { "Admin", "User" };

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
                    UserName = adminEmail.Replace("@gaji.com", ""),
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, $"Admin@2025");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error: {error.Description}");
                    }
                }
            }

            // Create default regular user
            var regularEmail = "staff@gaji.com";
            var regularUser = await userManager.FindByEmailAsync(regularEmail);

            if (regularUser == null)
            {
                regularUser = new ApplicationUser
                {
                    UserName = regularEmail.Replace("@gaji.com", ""),
                    Email = regularEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(regularUser, $"Staff@2025");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(regularUser, "User");
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
