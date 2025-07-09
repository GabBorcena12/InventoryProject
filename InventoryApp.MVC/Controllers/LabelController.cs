using InventoryApp.Core.Models;
using InventoryApp.DbContext;
using InventoryApp.Helper;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = "Admin,User")]
    public class LabelController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public LabelController(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }
        
        [HttpGet]
        public IActionResult LabelView(int? productId = null)
        {
            var model = new LabelPrintViewModel
            {
                Products = GetProductSelectList()
            };

            ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)));

            if (productId.HasValue)
            {
                ViewBag.BatchList = _context.Inventory
                    .Where(i => i.ProductId == productId && i.CurrentQty > 0)
                    .Select(i => i.BatchNo)
                    .Distinct()
                    .ToList();
            }
            else
            {
                ViewBag.BatchList = new List<string>();
            }

            return View(model);
        }

        [HttpGet]
        public JsonResult GetBatches(int productId)
        {
            var batches = _context.Inventory
                .Where(i => i.ProductId == productId && i.CurrentQty > 0)
                .Select(i => i.BatchNo)
                .Distinct()
                .ToList();

            return Json(batches);
        }


        [HttpPost]
        public IActionResult LabelView(LabelPrintViewModel model)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == model.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Invalid product.");
                model.Products = GetProductSelectList();
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                model.Products = GetProductSelectList();
                ViewBag.ErrorMessage = GetModelStateErrors();
                return View(model);
            }

            model.ProductName = product.ProductName;

            var qrContent = BuildQrCodeJson(model);
            var qrBytes = Helper.Helper.GenerateQrCode(qrContent);
            model.QrCodeImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
            return View("LabelPreview", model);
        }

        // Loads the product list for dropdown
        private List<SelectListItem> GetProductSelectList()
        {
            return _context.Products
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.ProductName
                }).ToList();
        }

        // Collects validation errors
        private List<string> GetModelStateErrors()
        {
            var errors = new List<string>();

            foreach (var key in ModelState.Keys)
            {
                foreach (var error in ModelState[key].Errors)
                {
                    errors.Add($"Field: {key}, Error: {error.ErrorMessage}");
                }
            }

            return errors;
        }

        // Builds JSON string for QR code content
        private string BuildQrCodeJson(LabelPrintViewModel model)
        {
            return $@"{{
              ""ProductName"": ""{model.ProductName}"",
              ""SellingPrice"": ""{model.SellingPrice:N2}"",
              ""Grams"": ""{(model.PackagingType == "Weight" ? model.WeightOrPieces : "")}"",
              ""Piece"": ""{(model.PackagingType == "Piece" ? model.WeightOrPieces : "")}"",
              ""BatchNo"": ""{model.BatchNumber}""
            }}";
        }
    }
}
