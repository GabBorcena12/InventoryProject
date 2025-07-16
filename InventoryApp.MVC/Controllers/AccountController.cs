using InventoryApp.Core.Models;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InventoryApp.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var modelError = await CaptureModelValidationErrorsAsync("Login", "Login", "Failed", 0, "Login");
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(model);
            }

            if (ModelState.IsValid)
            {
                var errorResult = await CheckAccountAvailability(model);
                if (!string.IsNullOrWhiteSpace(errorResult))
                {
                    TempData["ErrorMessage"] = errorResult;
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, false, false);
                if (result.Succeeded && string.IsNullOrWhiteSpace(errorResult))
                {
                    var user = await _userManager.FindByNameAsync(model.Username);
                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);

                        HttpContext.Session.SetString("_Username", model.Username);
                        HttpContext.Session.SetString("_UserRoles", string.Join(",", roles));
                        
                        LogAudit("Login", "User", user.Id, $"{model.Username} has logged in successfully.");
                        return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Invalid username or password.";
                }
            }            
            return View(model);
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            var modelError = await CaptureModelValidationErrorsAsync("Register", "Register", "Failed", 0, "Register");
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(model);
            }

            // Check if username already exists
            var existingUserByName = await _userManager.FindByNameAsync(model.Username);
            if (existingUserByName != null)
            {
                ModelState.AddModelError(string.Empty, "Username is already taken.");
                TempData["ErrorMessage"] = "Username is already taken.";
                return View(model);
            }

            // Check if email already exists
            var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingUserByEmail != null)
            {
                ModelState.AddModelError(string.Empty, "Email is already registered.");
                TempData["ErrorMessage"] = "Email is already registered.";
                return View(model);
            }

            var user = new ApplicationUser { UserName = model.Username, Email = model.Email };
            var createUserResult = await _userManager.CreateAsync(user, model.Password);

            if (!createUserResult.Succeeded)
            {
                foreach (var error in createUserResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    TempData["ErrorMessage"] = error.Description;
                }

                return View(model);
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, "Default");

            if (!addToRoleResult.Succeeded)
            {
                foreach (var error in addToRoleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    TempData["ErrorMessage"] = error.Description;
                }

                return View(model);
            }

            TempData["ToastMessage"] = $"{user.UserName} registered successfully.";
            return RedirectToAction("Login", "Account");
        }

        public async Task<IActionResult> Logout()
        {
            var username = HttpContext.Session.GetString("_Username");
            HttpContext.Session.Remove("_Username");
            await _signInManager.SignOutAsync();
            LogAudit("Remove", "Session Remove", "", $"{username } has logout.");
            return RedirectToAction("Login");
        }

        #region private
        private async Task<string> CheckAccountAvailability(LoginViewModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                return "Invalid username or password";
            }

            if (!await _userManager.IsEmailConfirmedAsync(user) || !user.IsConfirmed)
            {
                return "Account is not confirmed. Please check your email inbox.";
            }

            if (user.IsDisabled)
            {
                return "Account is disabled. Please check with your admin.";
            }

            return null;
        }

        public async Task<string> CaptureModelValidationErrorsAsync(string module, string action, string status, int id, string name)
        {
            var allErrors = new List<string>();

            foreach (var key in ModelState.Keys)
            {
                var state = ModelState[key];
                foreach (var error in state.Errors)
                {
                    allErrors.Add($"[{key}] {error.ErrorMessage}");
                }
            }
            var errors = string.Join("; ", allErrors);
            return errors;
        }
        
        protected void LogAudit(string action, string entity, string entityId, string description)
        {
            var log = new AuditLog
            {
                Action = action,
                EntityName = entity,
                EntityId = entityId,
                Description = description,
                PerformedBy = ViewBag.Username ?? User?.Identity?.Name ?? "System"
            };

            _context.AuditLogs.Add(log);
        }
        #endregion
    }
}
