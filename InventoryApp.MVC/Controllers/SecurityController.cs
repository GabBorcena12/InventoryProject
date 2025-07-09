using InventoryApp.Core.Models;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
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

        public IActionResult ForceLogout()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        public IActionResult AssignRoles()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }
        #endregion
        #region POST

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
                    LogAudit("Confirm Account", "User", user.Id, $"Email confirmed for user: {user.Email}");
                    _context.SaveChanges();
                }
            }

            return RedirectToAction("UnconfirmedAccounts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceLogout(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.UpdateSecurityStampAsync(user); // Invalidates sessions

                LogAudit("Force Logout", "User", user.Id, $"Forced logout for user: {user.Email}");
                _context.SaveChanges();
            }

            return RedirectToAction("ForceLogout");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                LogAudit("Change Password", "User", user.Id, $"Password changed for user: {user.Email}");
                _context.SaveChanges();
                return RedirectToAction("ChangePassword");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

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
                }
            }

            return RedirectToAction("AssignRoles");
        }

        #endregion


    }
}
