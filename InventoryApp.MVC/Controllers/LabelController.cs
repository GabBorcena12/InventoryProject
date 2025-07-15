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

        #region Public

        [HttpGet]
        public IActionResult LabelView()
        {
            var model = new LabelPrintViewModel
            {
                Products = GetProductSelectList()
            };

            ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)));

            return View(model);
        }

        [HttpPost]
        public IActionResult LabelView(LabelPrintViewModel model)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == model.ProductId);
            if (product == null)
                return BadRequest("Invalid product.");

            if (!ModelState.IsValid)
                return BadRequest("Invalid input data.");

            model.ProductName = product.ProductName;

            var qrContent = BuildQrCodeJson(model);
            var qrBytes = Helper.Helper.GenerateQrCode(qrContent);
            model.QrCodeImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";

            return PartialView("LabelPreview", model); // ✅ Must return a full or partial HTML view
        }


        [HttpGet]
        public JsonResult GetAllBatches()
        {
            var batches = _context.Inventory
                .Where(i => i.CurrentQty > 0)
                .Select(i => i.BatchNo)
                .Distinct()
                .ToList();

            return Json(batches);
        }

        [HttpGet]
        public JsonResult GetBatchInfo(string batchNo)
        {
            var inventory = _context.Inventory
                .Where(i => i.BatchNo == batchNo && i.CurrentQty > 0)
                .OrderByDescending(i => i.Id)
                .FirstOrDefault();

            if (inventory == null)
            {
                return Json(new { success = false });
            }

            var product = _context.Products.FirstOrDefault(p => p.Id == inventory.ProductId);
            if (product == null)
            {
                return Json(new { success = false });
            }

            return Json(new
            {
                success = true,
                productId = product.Id,
                productName = product.ProductAlias,
                packagingType = product.UnitOfMeasure.ToString(),
                currentQty = inventory.CurrentQty,
                priceSuggestion = Math.Ceiling(inventory.PricePerUnit * (inventory.InitialQuantity / (decimal)inventory.product.Volume) / inventory.InitialQuantity)
            });
        }

        #endregion

        #region Private

        private List<SelectListItem> GetProductSelectList()
        {
            return _context.Products
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.ProductName
                }).ToList();
        }

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

        private string BuildQrCodeJson(LabelPrintViewModel model)
        {
            return $@"{{
              ""ProductName"": ""{model.ProductName}"",
              ""SellingPrice"": ""{model.SellingPrice:N2}"",
              ""Grams"": ""{(model.PackagingType == "Grams" ? model.WeightOrPieces : "")}"",
              ""Piece"": ""{(model.PackagingType == "Piece" ? model.WeightOrPieces : "")}"",
              ""BatchNo"": ""{model.BatchNumber}""
            }}";
        }

        #endregion
    }
}
