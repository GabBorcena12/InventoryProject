using InventoryApp.Core.Authorizations;
using InventoryApp.Core.Models;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = RoleConstants.InventoryRoles)]
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
            var product = _context.Products.FirstOrDefault(p => p.Id == model.ProductId && !p.IsDisabled);
            if (product == null)
                return BadRequest("Invalid product.");

            if (!ModelState.IsValid)
                return BadRequest("Invalid input data.");

            //model.ProductName = product.ProductName;

            var qrContent = BuildQrCodeJson(model);
            var qrBytes = Helper.Helper.GenerateQrCode(qrContent);
            model.QrCodeImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";

            return PartialView("LabelPreview", model); // ✅ Must return a full or partial HTML view
        }


        [HttpGet]
        public JsonResult GetAllProductVariant()
        {
            var variantSku = _context.Products
            .Where(d => !d.IsDisabled)
            .SelectMany(p => p.Variants) // Flatten the list of variants from each product
            .Select(v => v.VariantSku)  // Select only the VariantCode
            .Distinct()                  // Ensure they are unique
            .ToList();


            return Json(variantSku);
        }

        [HttpGet]
        public JsonResult GetProductVariantInfo(string variantSku)
        {
            var variantInfo = _context.Products
                .Where(d => !d.IsDisabled)
                .SelectMany(p => p.Variants, (p, v) => new { Product = p, Variant = v })
                .Where(x => x.Variant.VariantSku == variantSku)
                .Select(x => new
                {
                    x.Product.Id,
                    x.Product.ProductName,
                    x.Product.ProductAlias,
                    x.Product.Volume,
                    x.Product.UnitOfMeasure,
                    x.Variant.VariantSku,
                    x.Variant.VariantCode,
                    x.Variant.VariantVolume,
                    x.Variant.Image
                })
                .FirstOrDefault();


            if (variantInfo == null)
            {
                return Json(new { success = false });
            }

            var product = _context.Products.FirstOrDefault(p => p.Id == variantInfo.Id);
            if (product == null)
            {
                return Json(new { success = false });
            }

            var suggestedPricePerUnit = _context.RepackItem.Where(r => r.VariantSku == variantSku)
                .Select(r => r.PricePerUnit)
                .FirstOrDefault();

            return Json(new
            {
                success = true,
                productId = variantInfo.Id,
                productName = variantInfo.VariantCode,
                packagingType = product.UnitOfMeasure.ToString(),
                variantVolume = variantInfo.VariantVolume,
                priceSuggestion = suggestedPricePerUnit.ToString() ?? "0"
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
              ""SKU"": ""{model.SKU}""
            }}";
        }

        #endregion
    }
}
