using InventoryApp.Controllers;
using InventoryApp.Core.Authorizations;
using InventoryApp.Core.Models;
using InventoryApp.Core.Models.PosModels;
using InventoryApp.MVC.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Text.RegularExpressions;

[Authorize(Roles = RoleConstants.InventoryRoles)]
public class InventoryController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventoryService;
    
    public InventoryController(ApplicationDbContext context, InventoryService inventoryService) : base(context)
    {
        _inventoryService = inventoryService;
        _context = context;
    }

    #region Product
    [HttpGet]
    public IActionResult Products(string productName, int page = 1, int pageSize = 10)
    {
        var query = _context.Products.Where(p => !p.IsDeleted).AsQueryable();

        if (!string.IsNullOrEmpty(productName))
        {
            query = query.Where(p => p.ProductName.Contains(productName));
        }

        var totalItems = query.Count();

        var products = query
            .OrderBy(p => p.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.ProductName = productName;

        return View(products);
    }

    [HttpGet]
    public IActionResult CreateProduct()
    {
        ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)));

        // Load existing SKUs into memory so we can use Regex on them
        var allSkus = _context.Products
            .Where(p => !string.IsNullOrEmpty(p.MasterSku))
            .Select(p => p.MasterSku)
            .AsEnumerable();

        string nextSku = "SKU0001";

        if (allSkus.Any())
        {
            // Find SKUs that have a trailing number, extract prefix, numeric value and padding length
            var numericSkus = allSkus
                .Select(s => new { Sku = s, Match = Regex.Match(s, @"(\d+)$") })
                .Where(x => x.Match.Success)
                .Select(x => new
                {
                    Sku = x.Sku,
                    Prefix = x.Sku.Substring(0, x.Match.Index),
                    Number = long.Parse(x.Match.Value),
                    Pad = x.Match.Value.Length
                })
                .ToList();

            if (numericSkus.Any())
            {
                // take the highest numeric suffix and increment
                var max = numericSkus.OrderByDescending(x => x.Number).First();
                nextSku = max.Prefix + (max.Number + 1).ToString().PadLeft(max.Pad, '0');
            }
            else
            {
                // No numeric suffix found on any SKU — fallback to lexicographically-last + "1"
                var last = allSkus.OrderByDescending(s => s).FirstOrDefault();
                nextSku = string.IsNullOrEmpty(last) ? "SKU0001" : last + "1";
            }
        }

        var model = new InventoryApp.Core.Models.Product
        {
            MasterSku = nextSku
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(InventoryApp.Core.Models.Product product)
    {
        try
        {
            var modelError = await CaptureModelValidationErrorsAsync("Product", "Create", "Failed", product.Id, product.ProductName);
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(product);
            }

            ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)));

            // Validation: Master SKU required
            if (string.IsNullOrWhiteSpace(product.MasterSku))
            {
                ModelState.AddModelError(nameof(product.MasterSku), "Master SKU is required for POS items.");
            }

            // Validation: At least one variant
            if (product.Variants == null || product.Variants.Count == 0)
            {
                ModelState.AddModelError("", "At least one product variant is required.");
            }
            else
            {
                for (int i = 0; i < product.Variants.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(product.Variants[i].VariantSku))
                    {
                        ModelState.AddModelError($"Variants[{i}].VariantSku", $"Please provide a Variant SKU for row {i + 1}.");
                        break;
                    }
                    else
                    {
                        // Check if the same SKU exists in database
                        var isVariantExist = await _context.ProductVariants
                            .AnyAsync(v => v.VariantSku == product.Variants[i].VariantSku);

                        if (isVariantExist)
                        {
                            ModelState.AddModelError("", $"{product.Variants[i].VariantSku} already exists.");
                            break;
                        }

                        // Check if the same SKU exists in another row
                        bool isDuplicateInVariants = product.Variants
                            .Where((v, idx) => idx != i)
                            .Any(v => v.VariantSku?.Trim() == product.Variants[i].VariantSku);

                        if (isDuplicateInVariants)
                        {
                            ModelState.AddModelError($"Variants[{i}].VariantSku", $"Duplicate SKU '{product.Variants[i].VariantSku}' found. Please check and try again.");
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(product.Variants[i].VariantCode))
                    {
                        ModelState.AddModelError($"Variants[{i}].VariantCode", $"Please provide a Variant Code for row {i + 1}.");
                        break;
                    }

                    if (product.Variants[i].VariantVolume == 0)
                    {
                        ModelState.AddModelError($"Variants[{i}].VariantVolume", $"Please provide a Volume for row {i + 1}.");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(product.Variants[i].Image))
                    {
                        product.Variants[i].Image = "Default.png";
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            // Set audit info
            product.CreatedBy = User?.Identity?.Name ?? "System";
            product.DateCreated = DateTime.Now;

            _context.Products.Add(product);
            LogAudit("Create", nameof(CreateProduct), product.Id.ToString(), $"Product created {product.ProductName} by {ViewBag.Username}.");

            _context.SaveChanges();

            TempData["ToastMessage"] = "Product created successfully!";
            return RedirectToAction("Products");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error encountered during saving. Please try again.";
            return View(product); // re-display form with entered data
        }
    }

    [HttpGet]
    public IActionResult EditProduct(int id)
    {
        var product = _context.Products
            .Include(p => p.Variants)
            .FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)), product.UnitOfMeasure);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(InventoryApp.Core.Models.Product product)
    {
        try
        {
            var modelError = await CaptureModelValidationErrorsAsync("Product", "Edit", "Failed", product.Id, product.ProductName);
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(product);
            }

            ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)), product.UnitOfMeasure);

            // Validation: Master SKU required
            if (string.IsNullOrWhiteSpace(product.MasterSku))
            {
                ModelState.AddModelError(nameof(product.MasterSku), "Master SKU is required for POS items.");
            }

            // Validation: At least one variant
            if (product.Variants == null || product.Variants.Count == 0)
            {
                ModelState.AddModelError("", "At least one product variant is required.");
            }
            else
            {
                for (int i = 0; i < product.Variants.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(product.Variants[i].VariantSku))
                    {
                        ModelState.AddModelError($"Variants[{i}].VariantSku", $"Please provide a Variant SKU for row {i + 1}.");
                        break;
                    }
                    else
                    {
                        // Check if the same SKU exists in database
                        var isVariantExist = await _context.ProductVariants
                            .AnyAsync(v => v.VariantSku == product.Variants[i].VariantSku);

                        if (isVariantExist)
                        {
                            ModelState.AddModelError("", $"{product.Variants[i].VariantSku} already exists.");
                            break;
                        }

                        // Check if the same SKU exists in another row
                        bool isDuplicateInVariants = product.Variants
                            .Where((v, idx) => idx != i)
                            .Any(v => v.VariantSku?.Trim() == product.Variants[i].VariantSku);

                        if (isDuplicateInVariants)
                        {
                            ModelState.AddModelError($"Variants[{i}].VariantSku", $"Duplicate SKU '{product.Variants[i].VariantSku}' found. Please check and try again.");
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(product.Variants[i].VariantCode))
                    {
                        ModelState.AddModelError($"Variants[{i}].VariantCode", $"Please provide a Variant Code for row {i + 1}.");
                        break;
                    }

                    if (product.Variants[i].VariantVolume == 0)
                    {
                        ModelState.AddModelError($"Variants[{i}].VariantVolume", $"Please provide a Volume for row {i + 1}.");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(product.Variants[i].Image))
                    {
                        product.Variants[i].Image = "Default.png";
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            // Get existing product from DB
            var existingProduct = _context.Products
                .Include(p => p.Variants)
                .FirstOrDefault(p => p.Id == product.Id);

            if (existingProduct == null)
            {
                return NotFound();
            }

            // Update main product properties
            existingProduct.ProductName = product.ProductName;
            existingProduct.ProductAlias = product.ProductAlias;
            existingProduct.Volume = product.Volume;
            existingProduct.UnitOfMeasure = product.UnitOfMeasure;
            existingProduct.RestockThreshold = product.RestockThreshold;
            existingProduct.MasterSku = product.MasterSku;
            existingProduct.IsStatutoryDiscountable = product.IsStatutoryDiscountable;
            existingProduct.MaxQtyForStatutoryDiscountable = product.MaxQtyForStatutoryDiscountable;

            // Replace variants with updated list
            existingProduct.Variants.Clear();
            foreach (var variant in product.Variants)
            {
                existingProduct.Variants.Add(new ProductVariant
                {
                    Id = variant.Id,
                    ProductId = existingProduct.Id,
                    VariantSku = variant.VariantSku,
                    VariantCode = variant.VariantCode,
                    VariantVolume = variant.VariantVolume,
                    Image = variant.Image ?? "Default.png"
                });
            }

            _context.SaveChanges();

            LogAudit("Update", nameof(EditProduct), product.Id.ToString(), $"Product updated {product.ProductName} by {ViewBag.Username}.");
            TempData["ToastMessage"] = "Product updated successfully!";
            return RedirectToAction("Products");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error encountered during saving. Please try again.";
            return View(product);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DisableProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction("Products");
        }

        product.IsDisabled = true;
        LogAudit("Update", nameof(DisableProduct), product.Id.ToString(), $"{product.ProductName} has been disabled by {ViewBag.Username}.");
        _context.SaveChanges();

        TempData["ToastMessage"] = "Product has been disabled.";
        return RedirectToAction("Products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EnableProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction("Products");
        }

        product.IsDisabled = false;
        LogAudit("Update", nameof(EnableProduct), product.Id.ToString(), $"{product.ProductName} has been enabled by {ViewBag.Username}.");
        _context.SaveChanges();

        TempData["ToastMessage"] = "Product has been activated.";
        return RedirectToAction("Products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction("Products");
        }

        product.IsDeleted = true;
        product.IsDisabled = true;

        LogAudit("Delete", nameof(DeleteProduct), product.Id.ToString(), $"{product.ProductName} has been removed by {ViewBag.Username}.");
        _context.SaveChanges();
        TempData["ToastMessage"] = "Product has been deleted.";
        return RedirectToAction("Products");
    }
    #endregion Product

    #region Expiry Items
    [HttpGet]
    public IActionResult NearExpiryItems(int page = 1, int pageSize = 10)
    {
        var query = _context.Inventory
            .Include(i => i.product)
            .Include(j => j.repackItems)
            .Include(k => k.supplier)
            .Where(i => i.CurrentQty > 0
                        && i.ExpiryDate >= DateTime.Now
                        && i.ExpiryDate <= DateTime.Now.AddDays(90))
            .OrderBy(i => i.ExpiryDate);

        int totalItems = query.Count();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        var inventories = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(inventories);
    }
    #endregion

    #region Out Items
    [HttpGet]
    public async Task<IActionResult> OutItems(DateTime? startDate, DateTime? endDate, string productName = "", int page = 1, int pageSize = 10)
    {
        var today = DateTime.Today;
        startDate ??= new DateTime(today.Year, today.Month, 1);
        endDate ??= today;

        var query = _context.Sales
            .Include(s => s.Inventory)
            .Include(s => s.repackItem).ThenInclude(r => r.product)
            .Where(s => s.SalesChannel == "Out Items" &&
                        s.DateSold.Date >= startDate && s.DateSold.Date <= endDate);

        if (!string.IsNullOrWhiteSpace(productName))
        {
            query = query.Where(s => s.repackItem.product.ProductName.Contains(productName));
        }

        var totalRecords = await query.CountAsync();

        var sales = await query
            .OrderByDescending(s => s.DateSold)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
        ViewBag.CurrentPage = page;
        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        ViewBag.ProductName = productName;

        return View(sales);
    }
    #endregion

    #region Inventory
    [HttpGet]
    public IActionResult PurchaseOrder(DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 10)
    {
        // Default to current month up to today
        if (!startDate.HasValue || !endDate.HasValue)
        {
            var now = DateTime.Now;
            startDate ??= new DateTime(now.Year, now.Month, 1); // first day of the month
            endDate ??= now.Date; // today
        }

        var query = _context.Inventory
            .Include(i => i.product)
            .Include(j => j.repackItems)
            .Include(k => k.supplier)
            .Where(i => i.DateCreated.Date >= startDate.Value && i.DateCreated.Date <= endDate.Value);

        var totalRecords = query.Count();

        var inventories = query
            .OrderByDescending(i => i.DateCreated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
        ViewBag.CurrentPage = page;
        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

        return View(inventories);
    }

    [HttpGet]
    public async Task<IActionResult> Inventory(
        string productName,
        DateTime? startDate,
        DateTime? endDate,
        string sortColumn = "ProductName",
        string sortOrder = "asc",
        int page = 1,
        int pageSize = 10)
    {
        // Set default date range: first day of month to today
        var now = DateTime.Now;
        startDate ??= new DateTime(now.Year, now.Month, 1);
        endDate ??= now.Date;

        var inventoryQuery = _context.Inventory
            .Include(i => i.product)
            .Include(i => i.repackItems)
            .Where(i => i.DateCreated.Date >= startDate.Value && i.DateCreated.Date <= endDate.Value)
            .AsQueryable();

        if (!string.IsNullOrEmpty(productName))
        {
            inventoryQuery = inventoryQuery.Where(i => i.product.ProductName.Contains(productName));
        }

        switch (sortColumn)
        {
            case "ProductName":
                inventoryQuery = sortOrder == "asc"
                    ? inventoryQuery.OrderBy(i => i.product.ProductName)
                    : inventoryQuery.OrderByDescending(i => i.product.ProductName);
                break;
            case "BatchNo":
                inventoryQuery = sortOrder == "asc"
                    ? inventoryQuery.OrderBy(i => i.BatchNo)
                    : inventoryQuery.OrderByDescending(i => i.BatchNo);
                break;
            default:
                inventoryQuery = inventoryQuery.OrderBy(i => i.product.ProductName);
                break;
        }

        var totalItems = await inventoryQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        var inventoryList = await inventoryQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.ProductName = productName;
        ViewBag.SortColumn = sortColumn;
        ViewBag.SortOrder = sortOrder;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

        return View(inventoryList);
    }

    [HttpGet]
    public async Task<IActionResult> ViewInventory(int id)
    {
        var inventory = await _context.Inventory
            .Include(i => i.product)
            .Include(i => i.supplier)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inventory == null)
        {
            return NotFound();
        }

        return View(inventory);
    }

    [HttpGet]
    public IActionResult CreateInventory()
    {
        // Generate date part
        var today = DateTime.Now;
        string datePart = today.ToString("yyyyMMdd");

        // Generate random 8-character alphanumeric string
        string randomPart = _inventoryService.GenerateRandomString(8);

        // Final batch number: BT-YYYYMMDD-XXXXXXXX
        string batchNo = $"BT-{datePart}-{randomPart}";

        ViewBag.BatchNo = batchNo;
        ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name");

        // Pass MasterSku along with the product
        ViewBag.Products = _context.Products
            .Where(p => !p.IsDisabled)
            .Select(p => new
            {
                Id = p.Id,
                Name = p.ProductName,
                MasterSku = p.MasterSku,
                InitialQuantity = p.Volume
            })
            .ToList();


        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInventory(Inventory model)
    {
        var modelError = await CaptureModelValidationErrorsAsync("Inventory", "Create", "Failed", model.Id, model.SKU);
        if (!string.IsNullOrEmpty(modelError))
        {
            TempData["ErrorMessage"] = modelError;
            return View(model);
        }

        if (ModelState.IsValid)
        {
            model.CurrentQty = model.InitialQuantity;
            model.DateCreated = DateTime.Now;
            model.CreatedBy = ViewBag.Username ?? "System";

            _context.Inventory.Add(model);
            LogAudit("Create", nameof(CreateInventory), model.Id.ToString(), $"{model.SKU} has been created by {ViewBag.Username}.");
            _context.SaveChanges();

            TempData["ToastMessage"] = "Inventory created successfully!";
            return RedirectToAction("Inventory");
        }

        ViewBag.BatchNo = model.BatchNo;
        ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name", model.SupplierId);
        ViewBag.Products = new SelectList(_context.Products.Where(p => !p.IsDisabled), "Id", "ProductName", model.ProductId);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteInventoryConfirmed(int id)
    {
        var inventory = _context.Inventory.FirstOrDefault(i => i.Id == id);
        if (inventory == null)
            return NotFound();

        inventory.IsDeleted = true;
        LogAudit("Delete", nameof(DeleteInventoryConfirmed), id.ToString(), $"{inventory.SKU} has been removed by {ViewBag.Username}.");
        _context.SaveChanges();

        TempData["ToastMessage"] = "Inventory removed successfully!";
        return RedirectToAction("Inventory");
    }
    #endregion

    #region Operating Expenses
    [HttpGet]
    public IActionResult OperatingExpenses(DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 10)
    {
        // Default: current month up to today
        if (!startDate.HasValue || !endDate.HasValue)
        {
            var now = DateTime.Now;
            startDate ??= new DateTime(now.Year, now.Month, 1);
            endDate ??= now.Date;
        }

        var query = _context.OperatingExpenses
            .Where(e => e.Date.Date >= startDate.Value.Date && e.Date.Date <= endDate.Value.Date);

        var totalRecords = query.Count();

        var expenses = query
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
        ViewBag.CurrentPage = page;
        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

        return View(expenses);
    }

    [HttpGet]
    public IActionResult CreateExpense()
    {
        var model = new OperatingExpense
        {
            Date = DateTime.Now
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(OperatingExpense expense)
    {
        var modelError = await CaptureModelValidationErrorsAsync("OperatingExpense", "Create", "Failed", expense.Id, expense.Description);
        if (!string.IsNullOrEmpty(modelError))
        {
            TempData["ErrorMessage"] = modelError;
            return View(expense);
        }

        if (ModelState.IsValid)
        {
            expense.CreatedBy = ViewBag.Username ?? "System";
            _context.OperatingExpenses.Add(expense);
            LogAudit("Create", nameof(CreateExpense), expense.Id.ToString(), $"{expense.Description} has been created by {ViewBag.Username}.");
            _context.SaveChanges();

            TempData["ToastMessage"] = "Expense recorded successfully.";
            return RedirectToAction("OperatingExpenses");
        }
        return View(expense);
    }

    [HttpGet]
    public IActionResult EditExpense(int id)
    {
        var expense = _context.OperatingExpenses.FirstOrDefault(e => e.Id == id && !e.IsDeleted);
        if (expense == null)
            return NotFound();

        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditExpense(int id, OperatingExpense expense)
    {
        if (id != expense.Id)
            return BadRequest();

        if (ModelState.IsValid)
        {
            var existing = _context.OperatingExpenses.FirstOrDefault(e => e.Id == id && !e.IsDeleted);
            if (existing == null)
                return NotFound();

            // update fields
            existing.Date = expense.Date;
            existing.Category = expense.Category;
            existing.Description = expense.Description;
            existing.Amount = expense.Amount;
            existing.Notes = expense.Notes;

            _context.Update(existing);
            LogAudit("Update", nameof(EditExpense), expense.Id.ToString(), $"{expense.Description} has been updated by {ViewBag.Username}.");
            _context.SaveChanges();

            return RedirectToAction(nameof(OperatingExpenses));
        }

        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteExpense(int id)
    {
        var expense = _context.OperatingExpenses.Find(id);

        if (expense == null)
        {
            TempData["ErrorMessage"] = "Expense not found.";
            return RedirectToAction("OperatingExpenses");
        }

        expense.IsDeleted = true;
        LogAudit("Delete", nameof(EditExpense), expense.Id.ToString(), $"{expense.Description} has been removed by {ViewBag.Username}.");
        _context.SaveChanges();

        TempData["ToastMessage"] = "Expense removed successfully.";
        return RedirectToAction("OperatingExpenses");
    }
    #endregion

    #region Repack
    [HttpGet]
    public IActionResult Repack(int id, int page = 1, int pageSize = 10)
    {
        var inventory = _context.Inventory
            .Include(p => p.repackItems)
                .ThenInclude(r => r.product)
            .Include(p => p.product)
            .FirstOrDefault(p => p.Id == id);

        if (inventory == null)
        {
            TempData["ErrorMessage"] = "Inventory not found.";
            return RedirectToAction("Index"); // or return View with error message
        }

        var totalRepackItems = inventory.repackItems.Count;
        var totalPages = (int)Math.Ceiling((double)totalRepackItems / pageSize);

        var repackItems = inventory.repackItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.InventoryName = inventory.product?.ProductName ?? "Unnamed Product";
        ViewBag.InventoryId = id;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.InventoryTotalWeight = inventory.InitialQuantity;

        return View("Repack", repackItems);
    }

    [HttpGet]
    public IActionResult CreateStock(int id)
    {
        var inventory = _context.Inventory
            .Include(i => i.product)
            .ThenInclude(p => p.Variants)
            .FirstOrDefault(i => i.Id == id);

        if (inventory == null) return NotFound("Inventory Not Found");


        var model = new RepackItem
        {
            InventoryId = inventory.Id,
            ProductName = inventory.product?.ProductName ?? "Unknown",
            ProductId = inventory.product?.Id ?? 0,
            inventory = inventory,
            product = inventory.product,
            BatchNo = inventory.BatchNo
        };

        ViewBag.UOM = inventory.product?.UnitOfMeasure ?? UnitOfMeasure.Piece;
        ViewBag.Variants = inventory.product?.Variants ?? new List<ProductVariant>();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStock(RepackItem model)
    {
        var modelError = await CaptureModelValidationErrorsAsync("RepackItem", "Create", "Failed", model.Id, model.ProductName);
        if (!string.IsNullOrEmpty(modelError))
        {
            TempData["ErrorMessage"] = modelError;
            return View(model);
        }

        var inventory = _context.Inventory
            .Include(i => i.product)
            .ThenInclude(p => p.Variants)
            .FirstOrDefault(i => i.Id == model.InventoryId);

        if (inventory == null) return NotFound("Inventory Not Found");

        ViewBag.UOM = inventory.product?.UnitOfMeasure ?? UnitOfMeasure.Piece;
        ViewBag.Variants = inventory.product?.Variants ?? new List<ProductVariant>();


        // Check for existing repack items *before* saving
        var existingRepackItems = _context.RepackItem
            .Where(r => r.InventoryId == model.InventoryId
                     && r.VariantSku == model.VariantSku)
            .ToList();

        if (existingRepackItems.Any())
        {
            var existingPrice = existingRepackItems.First().PricePerUnit;

            if (model.PricePerUnit != existingPrice)
            {
                TempData["ErrorMessage"] = $"Stock Price must match the existing stocks price value: {existingPrice}.";
                return View(model);
            }
        }
        // --- New Quantity Checker ---
        var totalRepackedQty = existingRepackItems.Sum(r => r.InitialQty * r.QuantityValue);
        var newRepackedQty = model.InitialQty * model.QuantityValue;
        var availableInventoryQty = inventory.InitialQuantity; // total available in inventory

        if (totalRepackedQty + newRepackedQty > availableInventoryQty)
        {
            TempData["ErrorMessage"] = $"Cannot stock {model.ProductName} units. Only {availableInventoryQty - totalRepackedQty} units left in inventory.";
            return View(model);
        }

        if (model.PricePerUnit <= 0)
        {
            TempData["ErrorMessage"] = "The unit price must be greater than zero. Please verify and enter a valid value.";
            return View(model);
        }


        if (ModelState.IsValid)
        { 
            model.CreatedBy = ViewBag.Username ?? "System";
            model.Id = 0;
            _context.RepackItem.Add(model);
            LogAudit("Create", nameof(CreateStock), model.Id.ToString(), $"{model.ProductName} has been created by {ViewBag.Username}.");
            _context.SaveChanges();

            TempData["ToastMessage"] = "Stock added successfully.";
            return RedirectToAction("Repack", new { id = model.InventoryId });
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ViewStock(int id)
    {
        var repack = await _context.RepackItem
            .Include(r => r.product)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (repack == null)
        {
            return NotFound();
        }

        return View(repack);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteRepackConfirmed(int id)
    {
        var repackItem = _context.RepackItem.FirstOrDefault(r => r.Id == id);
        if (repackItem == null)
            return NotFound();

        int inventoryId = repackItem.InventoryId;
        repackItem.IsDeleted = true;
        LogAudit("Delete", nameof(DeleteRepackConfirmed), repackItem.Id.ToString(), $"{repackItem.ProductName} has been removed by {ViewBag.Username}.");
        _context.SaveChanges();

        TempData["ToastMessage"] = "Stock removed successfully.";
        return RedirectToAction("Repack", new { id = inventoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsDisplayed(int id, int quantityToRelease, string? reason)
    {
        // mark item as displayed
        var displayed = _inventoryService.MarkAsDisplayed(id, quantityToRelease, ViewBag.Username ?? "System");
        if (!displayed.Item1 || displayed.Item3 == null)
        {
            TempData["ErrorMessage"] = displayed.Item2;
        }
        else
        {
            LogAudit("Add", nameof(MarkAsDisplayed), id.ToString(), $"{displayed.Item2} by {ViewBag.Username}");
            // release the item directly
            var released = _inventoryService.MarkAsReleased(displayed.Item3, quantityToRelease, "Out Items", ViewBag.Username ?? "System", reason ?? "System generated: Item marked as defective or unsuitable for resale.");

            if (!released.Item1)
                TempData["ErrorMessage"] = released.Item2;
            else
            {
                LogAudit("Add", nameof(MarkAsReleased), id.ToString(), $"{released.Item2} by {ViewBag.Username}");
                TempData["ToastMessage"] = released.Item2;
                return RedirectToAction("OutItems");
            }
        }
        return RedirectToAction("Repack", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MarkAsReleased(int id, int quantitySold, string salesChannel, string? reason = null)
    {
        var released = _inventoryService.MarkAsReleased(id, quantitySold, salesChannel, ViewBag.Username ?? "System", reason);

        if (!released.Item1)
            TempData["ErrorMessage"] = released.Item2;
        else
        {
            LogAudit("Add", nameof(MarkAsReleased), id.ToString(), $"{released.Item2} by {ViewBag.Username}");
            TempData["ToastMessage"] = released.Item2;
        }

        return RedirectToAction("DisplayItems");
    }
    #endregion

    #region Supplier
    [HttpGet]
    public IActionResult Suppliers(string supplierName, int page = 1, int pageSize = 10)
    {
        var query = _context.Suppliers.Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(supplierName))
        {
            query = query.Where(s => s.Name.Contains(supplierName));
        }

        var totalSuppliers = query.Count();
        var suppliers = query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalSuppliers / pageSize);
        ViewBag.SupplierName = supplierName;

        return View(suppliers);
    }

    [HttpGet]
    public IActionResult CreateSupplier()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSupplier(Supplier supplier)
    {
        try 
        {
            var modelError = await CaptureModelValidationErrorsAsync("Supplier", "Create", "Failed", supplier.Id, supplier.Name);
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(supplier);
            }

            if (ModelState.IsValid)
            {
                supplier.CreatedBy = ViewBag.Username ?? "System";
                supplier.IsDeleted = false;
                _context.Suppliers.Add(supplier);
                LogAudit("Create", nameof(CreateSupplier), supplier.Id.ToString(), $"{supplier.Name} has been added by {ViewBag.Username}");
                _context.SaveChanges();

                TempData["ToastMessage"] = "Supplier created successfully.";
                return RedirectToAction(nameof(Suppliers));
            }
            return View(supplier);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error encountered during saving. Please try again.";
            return View(supplier); // re-display form with entered data
        }
    }

    [HttpGet]
    public IActionResult EditSupplier(int id)
    {
        var supplier = _context.Suppliers.FirstOrDefault(s => s.Id == id && !s.IsDeleted);
        if (supplier == null)
        {
            TempData["ErrorMessage"] = "Supplier not found.";
            return RedirectToAction(nameof(Suppliers));
        }
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSupplier(Supplier supplier)
    {
        try 
        {
            var modelError = await CaptureModelValidationErrorsAsync("Supplier", "Edit", "Failed", supplier.Id, supplier.Name);
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(supplier);
            }

            if (!ModelState.IsValid)
            {
                return View(supplier);
            }

            var existingSupplier = _context.Suppliers.FirstOrDefault(s => s.Id == supplier.Id && !s.IsDeleted);
            if (existingSupplier == null)
            {
                TempData["ErrorMessage"] = "Supplier not found.";
                return RedirectToAction(nameof(Suppliers));
            }

            existingSupplier.Name = supplier.Name;
            existingSupplier.ContactPerson = supplier.ContactPerson;
            existingSupplier.ContactNumber = supplier.ContactNumber;
            existingSupplier.Address = supplier.Address;
            existingSupplier.AverageDaysToDeliver = supplier.AverageDaysToDeliver;
            existingSupplier.Notes = supplier.Notes;
            existingSupplier.UpdatedBy = ViewBag.Username ?? "System";
            existingSupplier.UpdatedAt = DateTime.Now;

            LogAudit("Update", nameof(EditSupplier), supplier.Id.ToString(), $"{supplier.Name} has been updated by {ViewBag.Username}");
            _context.SaveChanges();

            TempData["ToastMessage"] = "Supplier updated successfully.";
            return RedirectToAction(nameof(Suppliers));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error encountered during saving. Please try again.";
            return View(supplier); // re-display form with entered data
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteSupplier(int id)
    {
        var supplier = _context.Suppliers.FirstOrDefault(s => s.Id == id && !s.IsDeleted);
        if (supplier == null)
        {
            TempData["ErrorMessage"] = "Supplier not found.";
            return RedirectToAction(nameof(Suppliers));
        }

        supplier.IsDeleted = true;
        supplier.UpdatedBy = ViewBag.Username ?? "System";
        supplier.UpdatedAt = DateTime.Now;

        LogAudit("Delete", nameof(DeleteSupplier), supplier.Id.ToString(), $"{supplier.Name} has been removed by {ViewBag.Username}");
        _context.SaveChanges();

        TempData["ToastMessage"] = "Supplier deleted successfully.";
        return RedirectToAction(nameof(Suppliers));
    }
    #endregion

}
