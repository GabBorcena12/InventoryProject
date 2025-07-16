using InventoryApp.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.Controllers
{
    public class BaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BaseController(ApplicationDbContext context)
        {
            _context = context;
        }
        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {            
            var username = HttpContext.Session.GetString("_Username");
            var userRoles = HttpContext.Session.GetString("_UserRoles");
            ViewBag.Username = username;
            ViewBag.UserRoles = userRoles;
            base.OnActionExecuting(context);
        }

        protected void LogAudit(string action, string entity, string entityId, string description)
        {
            var log = new AuditLog
            {
                Action = action,
                EntityName = entity,
                EntityId = entityId ?? "",
                Description = description,
                PerformedBy = ViewBag.Username ?? User?.Identity?.Name ?? "System"
            };

            _context.AuditLogs.Add(log);
        }

        protected async Task<string> CaptureModelValidationErrorsAsync(string module, string action, string status, int id, string name)
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
            return string.Join("; ", allErrors);
        }
    }
}
