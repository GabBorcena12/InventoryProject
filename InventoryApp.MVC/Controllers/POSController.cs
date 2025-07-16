using InventoryApp.Controllers;
using InventoryApp.Core.Authorizations;
using InventoryApp.Core.Models;
using InventoryApp.Core.Models.PosModels;
using InventoryApp.MVC.Models.ViewModel;
using InventoryApp.MVC.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.MVC.Controllers
{
    public class POSController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<POSController> _logger; 
        private readonly InventoryService _inventoryService;

        public POSController(ILogger<POSController> logger, ApplicationDbContext context, InventoryService inventoryService) : base(context)
        {
            _inventoryService = inventoryService;
            _logger = logger;
            _context = context;
        }

        #region Refund
        [HttpGet]
        public async Task<IActionResult> RefundList(string search = "", int page = 1, int pageSize = 10)
        {
            var query = _context.CreditMemos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(cm =>
                    cm.CreditMemoNumber.Contains(search) ||
                    cm.TransactionOrNumber.Contains(search));
            }

            int totalItems = await query.CountAsync();

            var refunds = await query
                .OrderByDescending(cm => cm.IssuedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search;

            return View(refunds);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidCreditMemoConfirmed(int id)
        {
            // Fetch credit memo with related detail, transaction header, and repack item using join
            var creditMemoData = await (from cm in _context.CreditMemos
                join td in _context.POSTransactionDetails on cm.TransactionDetailId equals td.TransactionDetailId
                join th in _context.POSTransactionHeaders on td.TransactionHeaderId equals th.TransactionHeaderId
                join tri in _context.TransactionRepackItems on td.TransactionDetailId equals tri.TransactionDetailId into triGroup
                from tri in triGroup.DefaultIfEmpty()
                where cm.Id == id
                select new
                {
                    CreditMemo = cm,
                    TransactionDetail = td,
                    TransactionHeader = th,
                    TransactionRepackItem = tri
                }).FirstOrDefaultAsync();

            var creditMemo = creditMemoData.CreditMemo;
            var detail = creditMemoData.TransactionDetail;
            var repackItem = creditMemoData.TransactionRepackItem;

            if (creditMemoData == null)
            {
                TempData["ErrorMessage"] = "Credit Memo not found.";
                return RedirectToAction(nameof(RefundList));
            }

            if (creditMemo.IsBroken)
            {
                TempData["ErrorMessage"] = $"Unable to void Credit Memo {creditMemo.CreditMemoNumber}. The item is damaged, defective, or not eligible for resale.";
                return RedirectToAction(nameof(RefundList));
            }

            if (creditMemo.IsVoided)
            {
                TempData["ErrorMessage"] = $"Credit Memo {creditMemo.CreditMemoNumber} is already voided.";
                return RedirectToAction(nameof(RefundList));
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Step 1: Revert inventory for resellable item
                    await _inventoryService.VoidCreditMemo(id, ViewBag.Username ?? "System");

                    // Step 2 : Revert VoidTransactionDetail
                    await _inventoryService.RevertChangesOnVoidTransactionDetailForCreditMemo(creditMemo, ViewBag.Username ?? "System");

                    LogAudit("Update", nameof(VoidCreditMemoConfirmed), creditMemo.Id.ToString(), $"{creditMemo.CreditMemoNumber} has been void by {ViewBag.Username}");
                    await _context.SaveChangesAsync();

                    TempData["ToastMessage"] = $"Credit Memo {creditMemo.CreditMemoNumber} has been voided successfully.";
                    await dbTransaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Error encountered while voiding Credit Memo. {ex.Message}";
                }
            });
            return RedirectToAction(nameof(RefundList));
        }
        
        [HttpGet]
        public async Task<IActionResult> CreateRefund()
        {
            // Get distinct OR Numbers (exclude voided)
            var orNumbers = await _context.POSTransactionHeaders
                .Where(d => !d.IsVoided && !string.IsNullOrEmpty(d.ORNumber))
                .Select(t => new
                {
                    t.TransactionHeaderId,
                    t.ORNumber
                })
                .OrderBy(x => x.ORNumber)
                .ToListAsync();

            ViewBag.ORNumbers = orNumbers;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRefund(RefundRequestViewModel request)
        {

            // reload ORNumbers + Products in case of failure
            ViewBag.ORNumbers = await _context.POSTransactionHeaders
                .Where(d => !d.IsVoided)
                .Select(t => new { t.TransactionHeaderId, t.ORNumber })
                .ToListAsync();

            ViewBag.Products = await _context.RepackItem
                .GroupBy(p => new { p.VariantSku, p.VariantCode })
                .Select(g => new { VariantSku = g.Key.VariantSku, VariantCode = g.Key.VariantCode })
                .ToListAsync();

            if (!ModelState.IsValid)
                return View(request);

            var transactionHeader = await _context.POSTransactionHeaders
                .FirstOrDefaultAsync(h => h.TransactionHeaderId == request.TransactionHeaderId);

            if (transactionHeader == null)
            {
                TempData["ErrorMessage"] = "Transaction not found.";
                return View(request);
            }

            if (transactionHeader.IsVoided)
            {
                TempData["ErrorMessage"] = $"Transaction receipt {transactionHeader.ORNumber} has already been voided.";
                return View(request);
            }

            var transactionDetail = await _context.POSTransactionDetails
                .FirstOrDefaultAsync(d => d.TransactionHeaderId == request.TransactionHeaderId && d.Sku == request.Sku.ToString());

            if (transactionDetail == null)
            {
                TempData["ErrorMessage"] = "Item not found in this transaction.";
                return View(request);
            }

            var transactionRepackItem = await _context.TransactionRepackItems
                .FirstOrDefaultAsync(d => d.TransactionDetailId == transactionDetail.TransactionDetailId);

            if (transactionRepackItem == null)
            {
                TempData["ErrorMessage"] = "No repack item allocation found for this transaction.";
                return View(request);
            }

            if (transactionRepackItem.IsVoided)
            {
                TempData["ErrorMessage"] = "This item has already been voided.";
                return View(request);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // ✅ Step 1: Process refund logic
                    if (request.IsBroken)
                    {
                        await _inventoryService.RefundBrokenItem(transactionDetail, transactionHeader, transactionRepackItem, request, ViewBag.Username ?? "System");
                    }
                    else
                    {
                        await _inventoryService.RefundStillSellable(transactionDetail, transactionHeader, request, ViewBag.Username ?? "System");
                    }

                    LogAudit("Create", nameof(CreateRefund), null, $"{request.Sku} has been refunded by {ViewBag.Username}");
                    await _context.SaveChangesAsync();
                    TempData["ToastMessage"] = $"{request.Sku} is refunded successfully!";

                    await dbTransaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Error encountered while processing refund. {ex.Message}";
                }
            });
            return RedirectToAction("RefundList", "POS");
        }

        [HttpGet]
        public async Task<JsonResult> GetProductsByOR(int transactionHeaderId)
        {
            var products = await (
                from d in _context.POSTransactionDetails
                join r in _context.TransactionRepackItems
                    on d.TransactionDetailId equals r.TransactionDetailId
                where d.TransactionHeaderId == transactionHeaderId
                  && d.IsRegularItem == true
                  && r.IsVoided == false
                group new { d, r } by new { d.Sku, d.Name } into g
                select new
                {
                    Id = g.Key.Sku,
                    Display = g.Key.Sku + " - " + g.Key.Name,
                    QuantitySold = g.Select(x => x.d).Distinct().Sum(x => x.Qty), // prevent double-count
                    RepackItems = g.Select(x => x.r) // only non-voided repack items
                }
            )
            .OrderBy(x => x.Display)
            .ToListAsync();



            return Json(products);
        }
        #endregion

        #region POS Terminal
        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult GetProducts()
        {
            var products = _context.POSProduct
                .Where(p => p.IsActive && p.QtyDisplayed > 0 && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToList();

            return Json(new { success = true, products });
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult GetDiscounts()
        {
            var discounts = _context.POSDiscount
                .Where(d => d.isActive && !d.IsDeleted)
                .OrderBy(d => d.name)
                .ToList();

            return Json(new { success = true, discounts });
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult POSUI()
        {
            try
            {
                // Get the last OR number (highest so far)
                var lastOrNumber = _context.POSTransactionHeaders
                    .OrderByDescending(t => t.ORNumber)
                    .Select(t => t.ORNumber)
                    .FirstOrDefault();

                long lastSequence = 0;

                if (lastOrNumber != null)
                {
                    // Extract the last segment after the last "-"
                    var parts = lastOrNumber.Split('-');
                    if (parts.Length > 0)
                    {
                        var lastPart = parts[^1]; // last element
                        if (long.TryParse(lastPart, out long seq))
                        {
                            lastSequence = seq;
                        }
                    }
                }

                // Send to view
                ViewBag.LastTransactionNo = lastSequence.ToString("D9");
                ViewBag.Username = HttpContext.Session.GetString("_Username");
                ViewBag.IsBusinessNonVat = false;
                ViewBag.StoreCode = "SC01";
                ViewBag.TerminalId = "001";
                ViewBag.VatPercentage = "1.12";

                var products = new List<InventoryApp.Core.Models.PosModels.Product>();
                var discounts = new List<InventoryApp.Core.Models.PosModels.Discount>();

                var model = new POSViewModel
                {
                    POSProducts = products,
                    POSDiscounts = discounts
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading POSUI data.");
                return StatusCode(500, "An error occurred while loading POS data.");
            }
        }

        [HttpPost]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> CompleteTransaction([FromBody] POSViewModel.CompleteTransactionRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                return BadRequest(new { success = false, message = string.Join("; ", errors) });
            }

            if (request?.Header == null || request.Cart == null || !request.Cart.Any())
            {
                return BadRequest(new { success = false, message = "Invalid transaction data." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            IActionResult result = Ok(new { success = true, message = "Transaction completed successfully." });

            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 🔹 Map header
                    var headerEntity = new TransactionHeader
                    {
                        ORNumber = request.Header.ORNumber,
                        TransactionDate = request.Header.TransactionDate,
                        PaymentMethod = request.Header.PaymentMethod,
                        RegularDiscount = request.Header.RegularDiscount,
                        StatutoryDiscount = request.Header.StatutoryDiscount,
                        VATIncluded = request.Header.VATIncluded,
                        VATExcluded = request.Header.VATExcluded,
                        TotalAmount = request.Header.TotalAmount,
                        AmountTendered = request.Header.AmountTendered,
                        ChangeAmount = request.Header.ChangeAmount,
                        CashierName = request.Header.CashierName,
                        TerminalId = request.Header.TerminalId,
                        Cart = request.Header.Cart,
                        CreatedBy = ViewBag.Username ?? "System",
                        CreatedAt = DateTime.Now
                    };

                    foreach (var item in request.Cart)
                    {
                        headerEntity.TransactionDetails.Add(new TransactionDetail
                        {
                            Name = item.Name,
                            Qty = item.Qty,
                            PricePerKg = item.PricePerKg,
                            StepQty = item.StepQty,
                            Sku = item.Sku,
                            IsDiscount = item.IsDiscount,
                            IsRegularItem = item.IsRegularItem,
                            IsStatutoryDiscountable = item.IsStatutoryDiscountable,
                            IsDiscountRemovableOnUpdateCart = item.IsDiscountRemovableOnUpdateCart,
                            IsSeniorDiscountAppliedToItem = item.IsSeniorDiscountAppliedToItem,
                            IsBuyTakeDiscount = item.IsBuyTakeDiscount,
                            IsRegularDiscountItem = item.IsRegularDiscountItem,
                            MaxQtyForStatutoryDiscountable = item.MaxQtyForStatutoryDiscountable,
                            ReplacedSKU = item.ReplacedSKU,
                            RelatedSKUForSeniorPwdDiscount = item.RelatedSKUForSeniorPwdDiscount,
                            RemoveLabel = item.RemoveLabel,
                            DiscountRateLabel = item.DiscountRateLabel,
                        });
                    }

                    _context.POSTransactionHeaders.Add(headerEntity);
                    await _context.SaveChangesAsync();

                    // 🔹 Allocate stock (FIFO)
                    foreach (var detail in headerEntity.TransactionDetails)
                    {
                        if (detail.IsRegularItem == true)
                        {
                            await _inventoryService.AllocateFifoToTransaction(detail.TransactionDetailId, detail.Sku, (int)detail.Qty, ViewBag.Username ?? "System");
                        }
                    }

                    await _context.SaveChangesAsync();

                    // 🔹 Save audit logs
                    if (request.AuditLogs != null && request.AuditLogs.Any())
                    {
                        foreach (var log in request.AuditLogs)
                        {
                            var auditLogEntity = new Core.Models.PosModels.AuditLog
                            {
                                Timestamp = log.Timestamp,
                                Action = log.Action,
                                PerformedBy = log.PerformedBy,
                                Reason = log.Reason,
                                TransactionId = log.TransactionId,
                                CreatedBy = ViewBag.Username ?? "System",
                                CreatedAt = DateTime.Now
                            };

                            if (!string.IsNullOrWhiteSpace(log.OldDiscountType?.Name))
                            {
                                auditLogEntity.OldDiscountType = new Core.Models.PosModels.DiscountType
                                {
                                    Name = log.OldDiscountType.Name,
                                    Amount = log.OldDiscountType.Amount
                                };
                            }
                            if (!string.IsNullOrWhiteSpace(log.NewDiscountType?.Name))
                            {
                                auditLogEntity.NewDiscountType = new Core.Models.PosModels.DiscountType
                                {
                                    Name = log.NewDiscountType.Name,
                                    Amount = log.NewDiscountType.Amount
                                };
                            }

                            if (log.ItemsAffected != null)
                            {
                                foreach (var item in log.ItemsAffected)
                                {
                                    auditLogEntity.ItemsAffected.Add(new Core.Models.PosModels.AuditLogItem
                                    {
                                        Name = item.Name,
                                        Sku = item.Sku,
                                        DiscountAmount = (decimal)item.DiscountAmount
                                    });
                                }
                            }

                            _context.POSAuditLogs.Add(auditLogEntity);
                        }

                        LogAudit("Create", nameof(CompleteTransaction), null, $"Transaction {request.Header.ORNumber} has been created by {ViewBag.Username}");
                        await _context.SaveChangesAsync();
                    }
                    await dbTransaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Error completing transaction.");

                    // 🔹 Update result instead of throwing
                    result = BadRequest(new
                    {
                        success = false,
                        message = "Error completing transaction: " + ex.Message
                    });
                }
            });
            return result;
        }

        #endregion

        #region Product
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.InventoryRoles)]
        public IActionResult AddToPosItems(int id, int quantityToDisplay)
        {
            var item = _context.RepackItem.FirstOrDefault(r => r.Id == id);
            if (item == null)
                return BadRequest();

            var product = _context.Products
                .Include(p => p.Variants)
                .FirstOrDefault(p => p.Id == item.ProductId);

            var availableQty = item.InitialQty - item.SoldQty;
            var newTotalDisplayed = item.QuantityDisplayed + quantityToDisplay;

            bool isInvalidQuantity =
                availableQty <= 0 ||
                quantityToDisplay <= 0 ||
                newTotalDisplayed > availableQty;

            if (isInvalidQuantity)
            {
                TempData["ErrorMessage"] = "Unable to add item to POS System. Please verify that the quantity does not exceed the available stock.";
                return RedirectToAction("Repack", "Inventory", new { id = item.InventoryId });
            }

            // Update the displayed quantity in RepackItem
            item.QuantityDisplayedToPOS += quantityToDisplay;
            item.QuantityDisplayed = newTotalDisplayed;

            // Check if POS already has this VariantSku
            var existingPosProduct = _context.POSProduct
                .FirstOrDefault(p => p.Sku == item.VariantSku && p.IsActive);

            if (existingPosProduct != null)
            {
                // Just update the QtyDisplayed
                existingPosProduct.QtyDisplayed += quantityToDisplay;
                existingPosProduct.CreatedBy = ViewBag.Username ?? "System";
                existingPosProduct.CreatedAt = DateTime.Now;
            }
            else
            {
                // Add a new POS product entry
                var posEntry = new InventoryApp.Core.Models.PosModels.Product
                {
                    Name = item.VariantCode,
                    Sku = item.VariantSku,
                    PricePerKg = item.PricePerUnit,
                    QtyDisplayed = item.QuantityDisplayed,
                    QtySold = 0,
                    ImageUrl = product?.Variants.FirstOrDefault(v => v.VariantSku == item.VariantSku)?.Image ?? "Default.png",
                    IsStatutoryDiscountable = product?.IsStatutoryDiscountable ?? false,
                    maxQtyForStatutoryDiscountable = product?.MaxQtyForStatutoryDiscountable ?? 0,
                    IsActive = true,
                    CreatedBy = ViewBag.Username ?? "System",
                    CreatedAt = DateTime.Now,
                };

                _context.POSProduct.Add(posEntry);
            }

            LogAudit("Add", nameof(AddToPosItems), item.Id.ToString(), $"{item.ProductName} has been added to POS Item List by {ViewBag.Username}");
            _context.SaveChanges();
            TempData["ToastMessage"] = $"{item.ProductName} has been {(existingPosProduct != null ? "updated" : "added")} to POS Items.";
            return RedirectToAction("Repack", "Inventory", new { id = item.InventoryId });
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> ProductList(string productFilter = "", bool showInactive = false, int page = 1, int pageSize = 10)
        {
            var query = _context.POSProduct
                .Where(d => !d.IsDeleted)
                .AsQueryable();

            // 🔹 Optional name filter
            if (!string.IsNullOrWhiteSpace(productFilter))
            {
                query = query.Where(p => p.Name.ToLower().Contains(productFilter.ToLower()) || p.Sku.ToLower().Contains(productFilter.ToLower()));
            }

            // 🔹 Only show active unless explicitly asked
            if (!showInactive)
            {
                query = query.Where(p => p.IsActive);
            }

            // 🔹 Count total items
            var totalItems = await query.CountAsync();

            // 🔹 Apply paging
            var posProducts = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 🔹 Pass filters to view
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.ProductNameFilter = productFilter;
            ViewBag.ShowInactive = showInactive;

            return View(posProducts);
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult EditProduct(int id)
        {
            var product = _context.POSProduct.FirstOrDefault(p => p.id == id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction("ProductList");
            }
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult EditProduct(Core.Models.PosModels.Product model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fix the validation errors.";
                return View(model);
            }

            var existing = _context.POSProduct.FirstOrDefault(p => p.id == model.id);
            if (existing == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction("ProductList");
            }

            existing.Name = model.Name;
            existing.Sku = model.Sku;
            existing.PricePerKg = model.PricePerKg;
            existing.QtyDisplayed = model.QtyDisplayed;
            existing.QtySold = model.QtySold;
            existing.ImageUrl = model.ImageUrl;
            existing.IsStatutoryDiscountable = model.IsStatutoryDiscountable;
            existing.maxQtyForStatutoryDiscountable = model.maxQtyForStatutoryDiscountable;

            LogAudit("Update", nameof(EditProduct), existing.id.ToString(), $"{existing.Name} has been updated by {ViewBag.Username}");
            _context.SaveChanges();

            TempData["ToastMessage"] = "Product updated successfully.";
            return RedirectToAction("ProductList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> ToggleProductStatus(int id)
        {
            var product = await _context.POSProduct.FindAsync(id);
            if (product == null)
                return NotFound();

            // Toggle the IsActive property
            product.IsActive = !product.IsActive;

            _context.Update(product);
            LogAudit("Update", nameof(ToggleProductStatus), product.id.ToString(), $"{product.Name} has been toggled by {ViewBag.Username}");
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = product.IsActive
                ? $"{product.Name} has been enabled."
                : $"{product.Name} has been disabled.";

            // Redirect back to the list
            return RedirectToAction(nameof(ProductList));
        }
        #endregion

        #region Discount
        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult CreateDiscount()
        {
            // Example: fetch SKUs from POSProducts
            var products = _context.POSProduct
            .Where(p => p.IsActive)
            .Select(p => new SelectListItem
            {
                Value = p.Sku,
                Text = $"{p.Sku} - {p.Name}"
            })
            .ToList();


            var lastDiscount = _context.POSDiscount
                .OrderByDescending(d => d.discountSKU)
                .FirstOrDefault();

            string newCode;
            if (lastDiscount == null)
            {
                newCode = "DISC0001";
            }
            else
            {
                int lastNumber = int.Parse(lastDiscount.discountSKU.Substring(4));
                newCode = $"DISC{(lastNumber + 1).ToString("D4")}";
            }

            var model = new Discount
            {
                discountSKU = newCode
            };

            ViewBag.Products = products;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> CreateDiscount(Discount model)
        {
            var products = _context.POSProduct
            .Where(p => p.IsActive)
            .Select(p => new SelectListItem
            {
                Value = p.Sku,
                Text = $"{p.Sku} - {p.Name}"
            })
            .ToList();
            ViewBag.Products = products;

            // make sure to only add one active discount before allowing
            var existingDiscount = _context.POSDiscount
                .FirstOrDefault(p => p.relatedSKU == model.relatedSKU && p.isActive);

            if (existingDiscount != null)
            {
                TempData["ErrorMessage"] = $"An active discount already exists for {model.relatedSKU}.";
                return View(model);
            }

            var modelError = await CaptureModelValidationErrorsAsync("Discount", "Create", "Failed", model.id, model.name);
            if (!string.IsNullOrEmpty(modelError))
            {
                TempData["ErrorMessage"] = modelError;
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors and try again.";
                return View(model);
            }

            try
            {
                model.CreatedAt = DateTime.Now;
                model.CreatedBy = ViewBag.UserName ?? "System";

                _context.POSDiscount.Add(model);
                LogAudit("Create", nameof(CreateDiscount), model.id.ToString(), $"{model.name} has been created by {ViewBag.Username}");
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "Discount created successfully.";
                return RedirectToAction(nameof(DiscountList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating discount");
                TempData["ErrorMessage"] = "An error occurred while creating the discount.";
                return View(model);
            }
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> DiscountList(string search = "", bool showInactive = false, int page = 1, int pageSize = 10)
        {
            var query = _context.POSDiscount.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => d.name.Contains(search) || d.discountSKU.Contains(search));

            if (!showInactive)
                query = query.Where(d => d.isActive);

            var totalItems = await query.CountAsync();
            var discounts = await query
                .OrderBy(d => d.name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search;
            ViewBag.ShowInactive = showInactive;

            return View(discounts);
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult EditDiscount(int id)
        {
            var products = _context.POSProduct
            .Where(p => p.IsActive)
            .Select(p => new SelectListItem
            {
                Value = p.Sku,
                Text = $"{p.Sku} - {p.Name}"
            })
            .ToList();
            ViewBag.Products = products;

            var discount = _context.POSDiscount.FirstOrDefault(d => d.id == id);
            if (discount == null)
            {
                TempData["ErrorMessage"] = "Discount not found.";
                return RedirectToAction(nameof(DiscountList));
            }
            return View(discount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> EditDiscount(Discount model)
        {
            var products = _context.POSProduct
            .Where(p => p.IsActive)
            .Select(p => new SelectListItem
            {
                Value = p.Sku,
                Text = $"{p.Sku} - {p.Name}"
            })
            .ToList();
            ViewBag.Products = products;

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors and try again.";
                return View(model);
            }

            var existing = _context.POSDiscount.FirstOrDefault(d => d.id == model.id);
            if (existing == null)
            {
                TempData["ErrorMessage"] = "Discount not found.";
                return RedirectToAction(nameof(DiscountList));
            }

            // make sure we only have one active discount
            var existingDiscount = _context.POSDiscount
                .FirstOrDefault(p => p.relatedSKU == model.relatedSKU && p.isActive);

            if (existingDiscount != null 
                && model.isActive
                && existingDiscount.id != model.id)
            {
                TempData["ErrorMessage"] = $"An active discount already exists for {model.relatedSKU}.";
                return View(model);
            }

            try
            {
                existing.discountSKU = model.discountSKU;
                existing.name = model.name;
                existing.skuDiscountType = model.skuDiscountType;
                existing.relatedSKU = model.relatedSKU;
                existing.isActive = model.isActive;
                existing.amount = model.amount;

                if (model.skuDiscountType == "REGULAR DISCOUNT")
                {
                    existing.amountType = model.amountType;
                    existing.buyQty = 0;
                    existing.takeQty = 0;
                }
                if (model.skuDiscountType == "BUY&TAKE")
                {
                    existing.amountType = null;
                    existing.buyQty = model.buyQty;
                    existing.takeQty = model.takeQty;
                }                    

                // ADD NEW DISCOUNT HERE IF NEW DISCOUNT IS BEING IMPLEMENTED
                _context.Update(existing);
                LogAudit("Update", nameof(EditDiscount), existing.id.ToString(), $"{existing.name} has been updated by {ViewBag.Username}");
                await _context.SaveChangesAsync();

                TempData["ToastMessage"] = "Discount updated successfully.";
                return RedirectToAction(nameof(DiscountList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discount");
                TempData["ErrorMessage"] = "An error occurred while updating the discount.";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult DeleteDiscountConfirmed(int id)
        {
            var discount = _context.POSDiscount.FirstOrDefault(d => d.id == id);

            if (discount == null)
                return NotFound();

            // Mark as soft deleted
            discount.IsDeleted = true;

            // Optional: Track when it was deleted
            discount.DeletedAt = DateTime.UtcNow;
            discount.DeletedBy = ViewBag.UserName ?? "System";

            LogAudit("Delete", nameof(DeleteDiscountConfirmed), discount.id.ToString(), $"{discount.name} has been updated by {ViewBag.Username}");
            _context.SaveChanges();
            TempData["ToastMessage"] = $"Discount {discount.name} was deleted successfully.";
            return RedirectToAction(nameof(DiscountList));
        }

        #endregion
        
        #region POS Transactions

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult TransactionList(string search, bool showVoided = false, int page = 1, int pageSize = 10)
        {
            var query = _context.POSTransactionHeaders
                .Where(d => !d.IsDeleted)
                .AsQueryable();

            // 🔹 Filter by search (ORNumber or CashierName)
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.ORNumber.Contains(search) ||
                    t.CashierName.Contains(search));
            }

            // 🔹 Hide voided unless checkbox checked
            if (!showVoided)
            {
                query = query.Where(t => !t.IsVoided);
            }

            // 🔹 Pagination
            int totalItems = query.Count();
            var transactions = query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 🔹 Pass values to view
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search;
            ViewBag.ShowVoided = showVoided;

            // Stats (only for displayed records)
            ViewBag.TotalTransactions = totalItems;
            ViewBag.TotalVoided = query.Count(t => t.IsVoided);
            ViewBag.TotalAmount = query.Sum(t => t.TotalAmount);

            return View(transactions);
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public IActionResult TransactionDetails(int id)
        {
            var transaction = _context.POSTransactionHeaders
                .FirstOrDefault(t => t.TransactionHeaderId == id && !t.IsDeleted);

            if (transaction == null)
                return NotFound();

            return Json(new
            {
                transaction.TransactionHeaderId,
                transaction.ORNumber,
                transaction.TransactionDate,
                transaction.CashierName,
                transaction.PaymentMethod,
                transaction.RegularDiscount,
                transaction.StatutoryDiscount,
                transaction.VATIncluded,
                transaction.VATExcluded,
                transaction.TotalAmount,
                transaction.AmountTendered,
                transaction.ChangeAmount,
                transaction.Cart // still JSON string, will parse in JS
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> VoidTransactionConfirmed(int id)
        {
            var transaction = _context.POSTransactionHeaders
                .Include(t => t.TransactionDetails)
                .FirstOrDefault(t => t.TransactionHeaderId == id);

            // do not allow voiding if there is an existing credit memo
            var creditMemoExists = _context.CreditMemos
                .Where(d => d.TransactionOrNumber == transaction.ORNumber && !d.IsVoided)
                .FirstOrDefault();

            if(creditMemoExists != null)
                {
                TempData["ErrorMessage"] = $"Cannot void transaction with existing credit memo {creditMemoExists.CreditMemoNumber}.";
                return RedirectToAction(nameof(TransactionList));
            }   

            if (transaction == null)
                return NotFound();

            if (transaction.IsVoided)
            {
                TempData["ErrorMessage"] = "Transaction already voided.";
                return RedirectToAction(nameof(TransactionList));
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        foreach (var detail in transaction.TransactionDetails)
                        {
                            // Notice: no nested transaction here
                            await _inventoryService.VoidTransactionDetail(detail.TransactionDetailId, ViewBag.Username ?? "System");
                        }

                        transaction.IsVoided = true;

                        LogAudit("Update", nameof(VoidTransactionConfirmed), transaction.TransactionHeaderId.ToString(), $"{transaction.ORNumber} has been void by {ViewBag.Username}");
                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        TempData["ToastMessage"] = "Transaction successfully voided.";
                    }
                    catch (Exception ex)
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogError(ex, "Error voiding transaction.");
                        TempData["ErrorMessage"] = "An error occurred while voiding the transaction. Please try again.";
                        throw;
                    }
                });
            }
            catch
            {
                return RedirectToAction(nameof(TransactionList));
            }

            return RedirectToAction(nameof(TransactionList));
        }
        #endregion

        #region POS Audit logs
        [HttpGet]
        [Authorize(Roles = RoleConstants.PosRoles)]
        public async Task<IActionResult> AuditLogs(string filter, DateTime? startDate, DateTime? endDate,int page = 1, int pageSize = 10)
        {
            var start = startDate?.Date ?? DateTime.Today;
            var end = (endDate?.Date ?? DateTime.Today).AddDays(1);

            var query = _context.POSAuditLogs
                .Where(a => !a.IsDeleted);

            // Filter by User or OR Number
            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(a => a.PerformedBy.Contains(filter) || a.TransactionId.Contains(filter));
            }

            query = query.Where(a => a.Timestamp >= start && a.Timestamp < end);

            var totalItems = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    Timestamp = a.Timestamp,
                    TransactionId = a.TransactionId,
                    Action = a.Action,
                    PerformedBy = a.PerformedBy,
                    Reason = a.Reason,
                    OldDiscountType = a.OldDiscountType != null ? a.OldDiscountType.Name : null,
                    NewDiscountType = a.NewDiscountType != null ? a.NewDiscountType.Name : null
                })
                .ToListAsync();

            ViewBag.PerformedByFilter = filter;
            ViewBag.StartDateFilter = start.ToString("yyyy-MM-dd");
            ViewBag.EndDateFilter = end.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = page;

            return View(logs);
        }

        #endregion
    }
}
