using InventoryApp.Core.Authorizations;
using InventoryApp.Core.Models;
using InventoryApp.Models;
using InventoryApp.MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = RoleConstants.AdminsOnly)]
    public class SecurityController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public SecurityController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager
        ) : base(context) // pass to BaseController
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        #region UI
        public IActionResult UnconfirmedAccounts()
        {
            var users = _userManager.Users.Where(u => !u.EmailConfirmed).ToList();
            return View(users);
        }

        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        public async Task<IActionResult> AssignRoles()
        {
            var username = HttpContext.Session.GetString("_Username");
            // exlcude main account
            var users = _userManager.Users
                .Where(u => u.Email != "admin@gaji.com" && u.UserName != username
                )
                .ToList();

            var model = new List<UserWithRolesViewModel>();

            // Get the current logged-in user
            var currentUser = await _userManager.GetUserAsync(User);
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            // All possible roles
            string[] allRoles = { "SuperUser", "Inventory-Admin", "POS-Admin", "Inventory-User", "POS-User" };

            // Determine which roles are assignable
            List<string> assignableRoles = new();

            if (currentRoles.Contains("SuperUser"))
            {
                assignableRoles = allRoles.ToList(); // full access
            }
            else if (currentRoles.Contains("Inventory-Admin"))
            {
                assignableRoles = new List<string> { "Inventory-Admin", "Inventory-User" };
            }
            else if (currentRoles.Contains("POS-Admin"))
            {
                assignableRoles = new List<string> { "POS-Admin", "POS-User" };
            }

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add(new UserWithRolesViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Roles = roles.ToList(),
                    AssignableRoles = assignableRoles,
                    IsDisabled = user.IsDisabled
                });
            }
            return View(model);
        }



        #endregion
        #region POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId) as ApplicationUser;
            if (user != null)
            {
                user.IsDisabled = true;
                await _userManager.UpdateAsync(user);

                LogAudit("Disable Account", "User", user.Id, $"Account disabled for {user.Email}");
                _context.SaveChanges();
                TempData["ToastMessage"] = $"Account for {user.UserName} has been disabled.";
                return RedirectToAction("AssignRoles");
            }

            TempData["ErrorMessage"] = "Error encountered while trying to disable account.";
            return RedirectToAction("AssignRoles");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId) as ApplicationUser;
            if (user != null)
            {
                user.IsDisabled = false;
                await _userManager.UpdateAsync(user);

                LogAudit("Enable Account", "User", user.Id, $"Account enabled for {user.Email}");
                _context.SaveChanges();
                TempData["ToastMessage"] = $"Account for {user.UserName} has been enabled.";
                return RedirectToAction("AssignRoles");
            }

            TempData["ErrorMessage"] = "Error encountered while trying to enable account.";
            return RedirectToAction("AssignRoles");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, role))
            {
                var result = await _userManager.RemoveFromRoleAsync(user, role);

                if (result.Succeeded)
                {
                    LogAudit("Remove Role", "User", user.Id, $"Role '{role}' removed from user: {user.Email}");
                    _context.SaveChanges();
                    TempData["ToastMessage"] = $"Role {role} removed from {user.UserName} successfully.";
                    return RedirectToAction("AssignRoles");
                }
            }

            TempData["ErrorMessage"] = "Error encountered while trying to remove role.";
            return RedirectToAction("AssignRoles");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAccount(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && !user.EmailConfirmed)
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var result = await _userManager.ConfirmEmailAsync(user, token);

                if (result.Succeeded)
                {
                    user.IsConfirmed = true;
                    user.LockoutEnabled = false;
                    LogAudit("Confirm Account", "User", user.Id, $"Email confirmed for user: {user.Email}");
                    _context.SaveChanges();
                    TempData["ToastMessage"] = $"{user.UserName} email confirmed successfully.";
                    return RedirectToAction("UnconfirmedAccounts");
                }
            }
            TempData["ErrorMessage"] = $"Unable to confirm {user.UserName}. Please try again later.";
            return RedirectToAction("UnconfirmedAccounts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            // 🔹 Check if NewPassword and ConfirmPassword match
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "New password and confirmation password do not match.");
                TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                TempData["ErrorMessage"] = "User not found.";
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Error encountered while trying to change password.";
                return View(model);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                LogAudit("Change Password", "User", user.Id, $"Password changed for user: {user.Email}");
                _context.SaveChanges();
                TempData["ToastMessage"] = "Password changed successfully.";
                return RedirectToAction("ChangePassword");
            }

            TempData["ErrorMessage"] = "Error encountered while trying to change password.";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !await _userManager.IsInRoleAsync(user, role))
            {
                var result = await _userManager.AddToRoleAsync(user, role);

                if (result.Succeeded)
                {
                    LogAudit("Assign Role", "User", user.Id, $"Role '{role}' assigned to user: {user.Email}");
                    _context.SaveChanges();
                    TempData["ToastMessage"] = $"Role {role} assigned to {user.UserName} successful.";
                    return RedirectToAction("AssignRoles");
                }
            }

            TempData["ErrorMessage"] = "Error encountered while trying to assign new role.";
            return RedirectToAction("AssignRoles");
        }

        #endregion


    }
}
