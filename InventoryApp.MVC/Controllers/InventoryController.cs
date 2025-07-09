using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryApp.Models;
using InventoryApp.DbContext;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventoryApp.Controllers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using InventoryApp.Core.Models;

[Authorize(Roles = "Admin,User")]
public class InventoryController : BaseController
{
    private readonly ApplicationDbContext _context;

    public InventoryController(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    #region Product
    public IActionResult Products(string productName, int page = 1, int pageSize = 10)
    {
        var query = _context.Products.Where(p => !p.IsDisabled).AsQueryable();

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


    public IActionResult CreateProduct()
    {
        ViewBag.UnitOfMeasureList = new SelectList(Enum.GetValues(typeof(UnitOfMeasure)));
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateProduct(Product product)
    {
        if (ModelState.IsValid)
        {
            product.CreatedBy = ViewBag.Username ?? "System";
            _context.Products.Add(product);

            LogAudit("Create", "Product", product.Id.ToString(), $"Created product '{product.ProductName}'");

            _context.SaveChanges();
            TempData["ToastMessage"] = "Product created successfully!";
            return RedirectToAction("Products");
        }

        return View(product);
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

        LogAudit("Disable", "Product", product.Id.ToString(), $"Disabled product '{product.ProductName}'");

        _context.SaveChanges();
        TempData["ToastMessage"] = "Product has been disabled.";
        return RedirectToAction("Products");
    }

    #endregion Product

    #region Expiry Items
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
    public async Task<IActionResult> OutItems(DateTime? startDate, DateTime? endDate, string productName = "", int page = 1, int pageSize = 10)
    {
        var today = DateTime.Today;
        startDate ??= new DateTime(today.Year, today.Month, 1);
        endDate ??= today;

        var query = _context.Sales
            .Include(s => s.Inventory)
            .Include(s => s.RepackItem).ThenInclude(r => r.product)
            .Where(s => s.SalesChannel == "Out Items" &&
                        s.DateSold.Date >= startDate && s.DateSold.Date <= endDate);

        if (!string.IsNullOrWhiteSpace(productName))
        {
            query = query.Where(s => s.RepackItem.product.ProductName.Contains(productName));
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
    public IActionResult CreateInventory()
    {
        // Generate date part
        var today = DateTime.Now;
        string datePart = today.ToString("yyyyMMdd");

        // Generate random 8-character alphanumeric string
        string randomPart = GenerateRandomString(8);

        // Final batch number: BT-YYYYMMDD-XXXXXXXX
        string batchNo = $"BT-{datePart}-{randomPart}";

        ViewBag.BatchNo = batchNo;
        ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name");
        ViewBag.Products = new SelectList(_context.Products.Where(p => !p.IsDisabled), "Id", "ProductName");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInventory(Inventory model)
    {
        if (ModelState.IsValid)
        {
            model.CurrentQty = model.InitialQuantity;
            model.DateCreated = DateTime.Now;
            model.CreatedBy = ViewBag.Username ?? "System";

            _context.Inventory.Add(model);

            LogAudit("Create", "Inventory", model.Id.ToString(), $"Created inventory batch '{model.BatchNo}' for product ID {model.ProductId}");

            _context.SaveChanges();
            return RedirectToAction("Inventory");
        }

        var modelError = await CatchModelNotValidError();
        if (!string.IsNullOrEmpty(modelError))
            ViewBag.ErrorMessage = modelError;

        ViewBag.BatchNo = model.BatchNo;
        ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name", model.SupplierId);
        ViewBag.Products = new SelectList(_context.Products.Where(p => !p.IsDisabled), "Id", "ProductName", model.ProductId);

        return View(model);
    }

    // POST: Inventory/DeleteInventoryConfirmed
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteInventoryConfirmed(int id)
    {
        var inventory = _context.Inventory.FirstOrDefault(i => i.Id == id);
        if (inventory == null)
            return NotFound();

        inventory.IsDeleted = true;

        LogAudit("Delete", "Inventory", inventory.Id.ToString(), $"Soft-deleted inventory batch '{inventory.BatchNo}'");

        _context.SaveChanges();
        TempData["ToastMessage"] = "Inventory deleted successfully!";
        return RedirectToAction("Inventory");
    }

    #endregion

    #region Operating Expenses
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateExpense(OperatingExpense expense)
    {
        if (ModelState.IsValid)
        {
            expense.CreatedBy = ViewBag.Username;
            _context.OperatingExpenses.Add(expense);

            LogAudit("Create", "OperatingExpense", expense.Id.ToString(), $"Created expense '{expense.Description}' of {expense.Amount}");

            _context.SaveChanges();
            TempData["Success"] = "Expense recorded successfully.";
            return RedirectToAction("OperatingExpenses");
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
            TempData["Error"] = "Expense not found.";
            return RedirectToAction("OperatingExpenses");
        }

        expense.IsDeleted = true;

        LogAudit("Delete", "OperatingExpense", expense.Id.ToString(), $"Soft-deleted expense '{expense.Description}'");

        _context.SaveChanges();
        TempData["Success"] = "Expense deleted successfully.";
        return RedirectToAction("OperatingExpenses");
    }

    #endregion

    #region Repack
    public IActionResult Repack(int id, int page = 1, int pageSize = 10)
    {
        var inventory = _context.Inventory
            .Include(p => p.repackItems)
                .ThenInclude(r => r.product)
            .Include(p => p.product)
            .FirstOrDefault(p => p.Id == id);

        if (inventory == null)
        {
            TempData["Error"] = "Inventory not found.";
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

    public IActionResult CreateRepack(int id)
    {
        var inventory = _context.Inventory
            .Include(i => i.product)
            .FirstOrDefault(i => i.Id == id);

        if (inventory == null) return NotFound("Inventory Not Found");

        var model = new RepackItem
        {
            InventoryId = inventory.Id,
            ProductName = inventory.product?.ProductName ?? "Unknown",
            ProductId = inventory.product?.Id ?? 0  // <-- set ProductId here
        };

        ViewBag.UOM = inventory.product?.UnitOfMeasure ?? UnitOfMeasure.Piece;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateRepack(RepackItem model)
    {
        if (ModelState.IsValid)
        {
            model.CreatedBy = ViewBag.Username ?? "System";
            model.Id = 0;
            _context.RepackItem.Add(model);

            LogAudit("Create", "RepackItem", model.Id.ToString(), $"Created repack item of product ID {model.ProductId} with {model.InitialQty}");

            _context.SaveChanges();
            return RedirectToAction("Repack", new { id = model.InventoryId });
        }

        return View(model);
    }

    public async Task<IActionResult> ViewRepack(int id)
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

        LogAudit("Delete", "RepackItem", repackItem.Id.ToString(), $"Soft-deleted repack item of product ID {repackItem.ProductId}");

        _context.SaveChanges();
        TempData["ToastMessage"] = "Repack item deleted successfully.";
        return RedirectToAction("Repack", new { id = inventoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MarkAsDisplayed(int id, int quantityToDisplay)
    {
        var item = _context.RepackItem.FirstOrDefault(r => r.Id == id);
        if (item == null)
            return BadRequest();

        var availableQty = item.InitialQty - item.SoldQty;
        var newTotalDisplayed = item.QuantityDisplayed + quantityToDisplay;

        bool isInvalidQuantity =
            availableQty <= 0 ||
            quantityToDisplay <= 0 ||
            newTotalDisplayed > availableQty;

        if (isInvalidQuantity)
        {
            TempData["ToastMessage"] = "Unable to mark items as displayed. Please verify that the quantity does not exceed the available stock.";
            return RedirectToAction("Repack", new { id = item.InventoryId });
        }

        item.QuantityDisplayed = newTotalDisplayed;

        var displayEntry = new DisplayItem
        {
            RepackItemId = item.Id,
            QuantityDisplayed = quantityToDisplay,
            CreatedBy = ViewBag.Username ?? "System"
        };

        _context.DisplayItems.Add(displayEntry);

        LogAudit("Update", "DisplayItem", displayEntry.Id.ToString(), $"Marked {quantityToDisplay} as displayed for repack item ID {item.Id}");

        _context.SaveChanges();
        TempData["ToastMessage"] = $"Marked {quantityToDisplay} unit(s) as Displayed.";
        return RedirectToAction("Repack", new { id = item.InventoryId });
    }
    #endregion

    #region Display Items
    // GET: Inventory/DisplayItems
    public async Task<IActionResult> DisplayItems(string productName = "", int page = 1, int pageSize = 10)
    {
        var query = _context.DisplayItems
            .Include(d => d.RepackItem)
                .ThenInclude(r => r.product)
            .Where(i => !i.IsSoldOut);

        if (!string.IsNullOrWhiteSpace(productName))
        {
            query = query.Where(i => i.RepackItem.product.ProductName.Contains(productName));
        }

        var totalItems = await query.CountAsync();

        var displayItems = await query
            .OrderByDescending(i => i.DisplayedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.ProductName = productName;

        return View(displayItems);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MarkAsSold(int id, int quantitySold, string salesChannel)
    {
        var displayItem = _context.DisplayItems
            .Include(d => d.RepackItem)
            .FirstOrDefault(d => d.Id == id);

        if (displayItem == null)
            return NotFound();

        var repackItem = displayItem.RepackItem;
        if (repackItem == null)
        {
            TempData["ToastMessage"] = "Repack item not found.";
            return RedirectToAction("Repack", new { id = 0 });
        }

        if (repackItem.SoldQty + quantitySold > repackItem.InitialQty)
        {
            TempData["ToastMessage"] = "Cannot sell more than the available repacked quantity.";
            return RedirectToAction("Repack", new { id = repackItem.InventoryId });
        }

        var inventory = _context.Inventory.FirstOrDefault(i => i.Id == repackItem.InventoryId);
        if (inventory == null)
        {
            TempData["ToastMessage"] = "Inventory not found.";
            return RedirectToAction("Repack", new { id = repackItem.InventoryId });
        }

        int requiredQty = quantitySold * repackItem.QuantityValue;
        if (inventory.CurrentQty < requiredQty)
        {
            TempData["ToastMessage"] = "Insufficient inventory quantity.";
            return RedirectToAction("Repack", new { id = repackItem.InventoryId });
        }

        if (displayItem.QuantityDisplayed < quantitySold)
        {
            TempData["ToastMessage"] = "Item to be sold is more than displayed items. Please check again.";
            return RedirectToAction("Repack", new { id = repackItem.InventoryId });
        }

        displayItem.QuantityDisplayed -= quantitySold;
        displayItem.QuantitySold += quantitySold;
        if (displayItem.QuantityDisplayed == 0)
            displayItem.IsSoldOut = true;

        repackItem.SoldQty += quantitySold;
        repackItem.QuantityDisplayed -= quantitySold;
        inventory.CurrentQty -= requiredQty;

        var sale = new Sale
        {
            InventoryId = inventory.Id,
            RepackItemId = repackItem.Id,
            DisplayItemId = displayItem.Id,
            Quantity = quantitySold,
            TotalPrice = quantitySold * repackItem.PricePerUnit,
            SalesChannel = salesChannel,
            DateSold = DateTime.Now,
            CreatedBy = ViewBag.Username ?? "System"
        };

        _context.Sales.Add(sale);

        LogAudit("Create", "Sale", sale.Id.ToString(), $"Sold {quantitySold} units of product ID {repackItem.ProductId} on '{salesChannel}'");

        _context.SaveChanges();
        TempData["ToastMessage"] = "Sold quantity updated successfully.";
        return RedirectToAction("DisplayItems");
    }
    #endregion

    #region Supplier
    public IActionResult Suppliers(string supplierName, int page = 1, int pageSize = 10)
    {
        var query = _context.Suppliers.AsQueryable();

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

    public IActionResult CreateSupplier()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateSupplier(Supplier supplier)
    {
        if (ModelState.IsValid)
        {
            supplier.CreatedBy = ViewBag.Username ?? "System";
            _context.Suppliers.Add(supplier);
            _context.SaveChanges();
            return RedirectToAction(nameof(Suppliers));
        }

        return View(supplier);
    }

    #endregion

    #region Sales
    public async Task<IActionResult> Sales(DateTime? startDate, DateTime? endDate, string productName, int page = 1, int pageSize = 10)
    {
        // Default to current month up to today
        var now = DateTime.Today;
        DateTime from = startDate?.Date ?? new DateTime(now.Year, now.Month, 1);
        DateTime to = endDate?.Date ?? now;

        ViewBag.StartDate = from.ToString("yyyy-MM-dd");
        ViewBag.EndDate = to.ToString("yyyy-MM-dd");
        ViewBag.ProductName = productName;

        var allSales = _context.Sales
            .Include(s => s.Inventory)
            .Include(s => s.RepackItem).ThenInclude(r => r.product)
            .Where(s =>
                s.SalesChannel != "Out Items" &&
                s.DateSold.Date >= from &&
                s.DateSold.Date <= to &&
                (string.IsNullOrEmpty(productName) ||
                 s.RepackItem.product.ProductName.Contains(productName)))
            .OrderByDescending(s => s.DateSold);

        var totalSales = await allSales.CountAsync();
        var sales = await allSales
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalSales / pageSize);

        return View(sales);
    }
    #endregion

    #region Private
    private string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var stringChars = new char[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] data = new byte[length];
            rng.GetBytes(data);
            for (int i = 0; i < length; i++)
            {
                var index = data[i] % chars.Length;
                stringChars[i] = chars[index];
            }
        }
        return new string(stringChars);
    }


    private async Task<string> CatchModelNotValidError()
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
    #endregion
}
