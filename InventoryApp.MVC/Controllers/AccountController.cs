using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using InventoryApp.Core.Models;

namespace InventoryApp.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var errorExists = await CheckForErrorDuringLogin(model);

            var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, false, false);
            if (result.Succeeded && !errorExists)
            {
                var user = await _userManager.FindByNameAsync(model.Username);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);

                    HttpContext.Session.SetString("_Username", model.Username);
                    HttpContext.Session.SetString("_UserRoles", string.Join(",", roles));
                    return RedirectToAction("Index", "Home");
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
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser { UserName = model.Username, Email = model.Email };
            var createUserResult = await _userManager.CreateAsync(user, model.Password);

            if (!createUserResult.Succeeded)
            {
                foreach (var error in createUserResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, "User");

            if (!addToRoleResult.Succeeded)
            {
                foreach (var error in addToRoleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }
            return RedirectToAction("Login", "Account");
        }

        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove("_Username");
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        #region private
        private async Task<bool> CheckForErrorDuringLogin(LoginViewModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                ViewBag.Error = "Invalid username or password";
                return true;
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.Error = "Account is not confirmed. Please check your email inbox.";
                return true;
            }
            return false;
        }
        #endregion
    }
}
