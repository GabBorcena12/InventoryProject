using InventoryApp.Core.Models;
using InventoryApp.Core.Models.PosModels;
using InventoryApp.MVC.Models.ViewModel;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace InventoryApp.MVC.Service
{
    public class InventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public (bool Success, string Message, int? DisplayItemId) MarkAsDisplayed(int id, int quantityToDisplay, string username = "System")
        {
            var item = _context.RepackItem.FirstOrDefault(r => r.Id == id);
            if (item == null)
                return (Success: false, Message: "Item not found.", null);

            var availableQty = item.InitialQty - item.SoldQty;
            var newTotalDisplayed = item.QuantityDisplayed + quantityToDisplay;

            if (availableQty <= 0 || quantityToDisplay <= 0 || newTotalDisplayed > availableQty)
                return (Success: false, Message: "Unable to mark items as displayed. Please verify that the quantity does not exceed the available stock.", null);

            item.QuantityDisplayedToInventory += quantityToDisplay;
            item.QuantityDisplayed = newTotalDisplayed;

            var displayEntry = new DisplayItem
            {
                RepackItemId = item.Id,
                QuantityDisplayed = quantityToDisplay,
                QuantitySold = 0,
                DisplayedBy = username,
                DisplayedOn = DateTime.Now,
                IsSoldOut = false,
            };

            _context.DisplayItems.Add(displayEntry);

            _context.SaveChanges();
            return (Success: true, Message: $"Marked {quantityToDisplay} unit(s) as Displayed.", DisplayItemId: displayEntry.Id);
        }

        public (bool Success, string Message, int? SaleId) MarkAsReleased(int id, int quantityReleased, string salesChannel, string username = "System", string? reason = null)
        {
            var displayItem = _context.DisplayItems
                .Include(d => d.RepackItem)
                .FirstOrDefault(d => d.Id == id);

            if (displayItem == null)
                return (Success: false, Message: "Display item not found.", null);

            var repackItem = displayItem.RepackItem;
            if (repackItem == null)
                return (Success: false, Message: "Repack item not found.", null);

            if (repackItem.SoldQty + quantityReleased > repackItem.InitialQty)
                return (Success: false, Message: "Cannot sell more than the available repacked quantity.", null);

            var inventory = _context.Inventory.FirstOrDefault(i => i.Id == repackItem.InventoryId);
            if (inventory == null)
                return (Success: false, Message: "Inventory not found.", null);

            int requiredQty = quantityReleased * repackItem.QuantityValue;
            if (inventory.CurrentQty < requiredQty)
                return (Success: false, Message: "Insufficient inventory quantity.", null);

            if (displayItem.QuantityDisplayed < quantityReleased)
                return (Success: false, Message: "Item to be sold is more than displayed items. Please check again.", null);

            // Update values
            displayItem.QuantityDisplayed -= quantityReleased;
            displayItem.QuantitySold += quantityReleased;
            if (displayItem.QuantityDisplayed == 0)
                displayItem.IsSoldOut = true;

            repackItem.QuantityDisplayedToInventory -= quantityReleased;
            repackItem.SoldQty += quantityReleased;
            repackItem.QuantityDisplayed -= quantityReleased;
            inventory.CurrentQty -= requiredQty;

            var product = _context.Products
                .FirstOrDefault(d => d.Id == repackItem.ProductId);

            if (product == null)
                throw new Exception("Unable to identify product volume.");

            // Calculate item capital based on cost per unit and volume
            var itemCapital = (inventory.CostPerUnit / product.Volume) * repackItem.QuantityValue;
            var sale = new Sale
            {
                InventoryId = repackItem.InventoryId,
                RepackItemId = displayItem.RepackItemId,
                DisplayItemId = displayItem.Id,
                Quantity = quantityReleased,
                TotalPrice = quantityReleased * itemCapital, // use cost per unit here to compute total loss
                Reason = reason,
                SalesChannel = salesChannel,
                DateSold = DateTime.Now,
                SoldBy = username
            };

            _context.Sales.Add(sale);
            _context.SaveChanges();
            return (Success: true, Message: $"Item {repackItem.VariantCode} sold successfully.", sale.Id);
        }

        public string GenerateRandomString(int length)
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

        public async Task RefundBrokenItem(TransactionDetail detail, TransactionHeader header, TransactionRepackItem transactionRepackItem, RefundRequestViewModel request, string username = "System")
        {
            int quantityToRefund = request.Quantity;

            if (transactionRepackItem == null)
                throw new Exception("No repack item found for this transaction detail.");

            // Step 1: Revert Inventory Changes
            // Revert changes on RepackItem QuantityDisplayedToPOS , QuantityDisplayed , SoldQty
            // Revert changes on POS product list QtySold & QtyDisplayed
            // Revert changes on Inventory Current Qty
            await VoidTransactionDetailForCreditMemo(detail.TransactionDetailId, quantityToRefund, username);

            // Step 2 :Remove item from POS Product List
            await RemoveItemsFromPosProducts(transactionRepackItem.RepackItem.VariantSku, quantityToRefund);

            // Step 3: Shift the stocks from Displayed POS to Display Inventory
            var repackItem = _context.RepackItem.FirstOrDefault(r => r.Id == transactionRepackItem.RepackItemId);
            if (repackItem != null)
            {
                repackItem.QuantityDisplayed -= quantityToRefund;
                repackItem.QuantityDisplayedToPOS -= quantityToRefund;
                repackItem.QuantityDisplayedToInventory += quantityToRefund;
            }

            // Step 4: Add item to Display Inventory
            var displayResult = MarkAsDisplayed(
                transactionRepackItem.RepackItemId,
                quantityToRefund,
                username
            );

            if (!displayResult.Success || !displayResult.DisplayItemId.HasValue)
                throw new Exception(displayResult.Message);

            // Step 5: Immediately mark it as 'Out Items' since it is not resellable any more
            var soldResult = MarkAsReleased(
                displayResult.DisplayItemId.Value,
                quantityToRefund,
                "Out Items",
                username,
                request.Reason ?? "System generated: Item marked as defective or unsuitable for resale."
            );

            if (!soldResult.Success)
                throw new Exception(soldResult.Message);

            var saleId = soldResult.Item3 ?? null;

            // Step 6: Add credit memo record
            await AddCreditMemo(detail, header, request, username, saleId);
        }

        public async Task RefundStillSellable(TransactionDetail detail, TransactionHeader header, RefundRequestViewModel request, string username = "System")
        {
            int quantityToRefund = request.Quantity;

            // Step 1 : Revert Inventory changes
            // Revert changes on RepackItem QuantityDisplayedToPOS , QuantityDisplayed , SoldQty
            // Revert changes on POS product list QtySold & QtyDisplayed
            // Revert changes on Inventory Current Qty
            await VoidTransactionDetailForCreditMemo(detail.TransactionDetailId, quantityToRefund, username);

            // Step 2: Item stays in POSProduct, no need to revert from POS, since item is fit for resale
            // Step 3: Add record credit memo
            await AddCreditMemo(detail, header, request, username, null);
        }

        public async Task RemoveItemsFromPosProducts(string variantSku, int quantityToRemove)
        {
            var product = _context.POSProduct
                .FirstOrDefault(d => d.Sku == variantSku);
            if (product != null)
            {
                // Subtract refunded items from POS display inventory
                product.QtyDisplayed = Math.Max(0, product.QtyDisplayed - quantityToRemove);

                // Reduce QtySold but prevent negative values
                product.QtySold = Math.Max(0, product.QtySold - quantityToRemove);

                _context.POSProduct.Update(product);
            }
        }

        public async Task AddCreditMemo(TransactionDetail detail, TransactionHeader header, RefundRequestViewModel request, string username = "System", int? saleId = null)
        {
            // Step 1: Get last credit memo
            var lastCreditMemo = await _context.CreditMemos
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastCreditMemo != null && !string.IsNullOrEmpty(lastCreditMemo.CreditMemoNumber))
            {
                string lastNumberPart = lastCreditMemo.CreditMemoNumber.Replace("CM-", "");
                if (int.TryParse(lastNumberPart, out int parsed))
                {
                    nextNumber = parsed + 1;
                }
            }

            // Step 2: Format CM number
            string newCreditMemoNumber = $"CM-{nextNumber:D9}";

            // Step 3: Create memo
            var creditMemo = new CreditMemo
            {
                CreditMemoNumber = newCreditMemoNumber,
                TransactionOrNumber = header.ORNumber,
                TransactionDetailId = detail.TransactionDetailId,
                Sku = detail.Sku,
                ProductName = detail.Name,
                SaleId = saleId,
                Qty = request.Quantity,
                Amount = detail.PricePerKg,
                TotalAmount = detail.PricePerKg * request.Quantity,
                Reason = request.Reason,
                IsBroken = request.IsBroken,
                IssuedBy = username
            };

            _context.CreditMemos.Add(creditMemo);
            await Task.CompletedTask;
        }

        public async Task VoidCreditMemo(int creditMemoId, string username = "System")
        {
            var creditMemo = _context.CreditMemos
                .FirstOrDefault(d => d.Id == creditMemoId && !d.IsVoided);

            if (creditMemo == null)
                throw new Exception("Credit memo not found.");

            // void credit memo record
            creditMemo.IsVoided = true;
            _context.CreditMemos.Update(creditMemo);
        }

        public async Task RevertChangesOnVoidTransactionDetailForCreditMemo(CreditMemo creditMemo, string username = "System")
        {
            var quantityToRevert = (int)creditMemo.Qty;

            var transactionRepackItem = _context.TransactionRepackItems
                .Where(tr => tr.TransactionDetailId == creditMemo.TransactionDetailId && !tr.IsDeleted)
                .ToList();

            if (!transactionRepackItem.Any())
                throw new Exception("transaction not found.");

            foreach (var item in transactionRepackItem)
            {
                // --- Refund quantity validation
                // Get total voided quantity for this transaction detail
                var existingQtyVoided = await _context.CreditMemos
                    .Where(cm => cm.TransactionDetailId == creditMemo.TransactionDetailId && !cm.IsVoided)
                    .SumAsync(cm => (int?)cm.Qty) ?? 0;

                var totalQtyVoided = existingQtyVoided - quantityToRevert;
                if (totalQtyVoided < 0)
                    throw new Exception("Refund quantity exceeds allocated quantity.");

                // --- Step 1: Restore RepackItem stock ---
                var repackItem = _context.RepackItem.FirstOrDefault(r => r.Id == item.RepackItemId);
                if (repackItem != null)
                {
                    repackItem.QuantityDisplayedToPOS -= quantityToRevert;
                    repackItem.QuantityDisplayed -= quantityToRevert;
                    repackItem.SoldQty += quantityToRevert;
                    if (repackItem.SoldQty < 0) repackItem.SoldQty = 0;

                    // --- Step 2: Restore Inventory Current Qty  ---
                    var inventory = _context.Inventory.FirstOrDefault(i => i.Id == repackItem.InventoryId);
                    if (inventory != null)
                    {
                        inventory.CurrentQty -= Math.Max(0, (quantityToRevert * repackItem.QuantityValue));
                    }
                }

                // --- Step 3: Restore POS Product stock ---
                var product = _context.POSProduct.FirstOrDefault(p => p.Sku == repackItem.VariantSku);
                if (product != null)
                {
                    product.QtySold += quantityToRevert; // reduce quantity sold
                    product.QtyDisplayed -= quantityToRevert; // restore quantity displayed
                }

                // --- Step 4: Tag void the allocation record ---
                item.IsVoided = (totalQtyVoided == item.AllocatedQty);
                item.IsVoided = false;
                item.UpdatedBy = username;
                item.UpdatedAt = DateTime.Now;
            }
        }

        public async Task VoidTransactionDetailForCreditMemo(int transactionDetailId, int refundQty, string username = "System")
        {
            // Get all allocations for this transaction detail
            var transactionRepackItem = _context.TransactionRepackItems
                .Where(tr => tr.TransactionDetailId == transactionDetailId && !tr.IsDeleted)
                .ToList();

            if (!transactionRepackItem.Any()) return;

            foreach (var item in transactionRepackItem)
            {
                // --- Refund quantity validation
                // Get total voided quantity for this transaction detail
                var existingQtyVoided = await _context.CreditMemos
                    .Where(cm => cm.TransactionDetailId == transactionDetailId && !cm.IsVoided)
                    .SumAsync(cm => (int?)cm.Qty) ?? 0;


                var totalQtyVoided = existingQtyVoided + refundQty;
                if (totalQtyVoided > item.AllocatedQty)
                    throw new Exception("Refund quantity exceeds allocated quantity.");

                // --- Step 1: Restore RepackItem stock ---
                var repackItem = _context.RepackItem.FirstOrDefault(r => r.Id == item.RepackItemId);
                if (repackItem != null)
                {
                    repackItem.QuantityDisplayedToPOS += refundQty;
                    repackItem.QuantityDisplayed += refundQty;
                    repackItem.SoldQty -= refundQty;
                    if (repackItem.SoldQty < 0) repackItem.SoldQty = 0;

                    // --- Step 2: Restore Inventory Current Qty  ---
                    var inventory = _context.Inventory.FirstOrDefault(i => i.Id == repackItem.InventoryId);
                    if (inventory != null)
                    {
                        inventory.CurrentQty += (refundQty * repackItem.QuantityValue);
                    }
                }

                // --- Step 3: Restore POS Product stock ---
                var product = _context.POSProduct.FirstOrDefault(p => p.Sku == repackItem.VariantSku);
                if (product != null)
                {
                    product.QtySold -= refundQty; // reduce quantity sold
                    product.QtyDisplayed += refundQty; // restore quantity displayed
                }

                // --- Step 4: Tag void the allocation record ---                
                item.IsVoided = (totalQtyVoided == item.AllocatedQty);
                item.UpdatedBy = username;
                item.UpdatedAt = DateTime.Now;
            }
        }

        public async Task VoidTransactionDetail(int transactionDetailId, string username = "System")
        {
            // Get all allocations for this transaction detail
            var transactionRepackItem = _context.TransactionRepackItems
                .Where(tr => tr.TransactionDetailId == transactionDetailId && !tr.IsDeleted)
                .ToList();

            if (!transactionRepackItem.Any()) return;

            foreach (var item in transactionRepackItem)
            {
                // --- Step 1: Restore RepackItem stock ---
                var repackItem = _context.RepackItem.FirstOrDefault(r => r.Id == item.RepackItemId);
                if (repackItem != null)
                {
                    repackItem.QuantityDisplayedToPOS += item.AllocatedQty;
                    repackItem.QuantityDisplayed += item.AllocatedQty;
                    repackItem.SoldQty -= item.AllocatedQty;
                    if (repackItem.SoldQty < 0) repackItem.SoldQty = 0;


                    // --- Step 2: Restore Inventory Current Qty  ---
                    var inventory = _context.Inventory.FirstOrDefault(i => i.Id == repackItem.InventoryId);
                    if (inventory != null)
                    {
                        inventory.CurrentQty += (item.AllocatedQty * repackItem.QuantityValue);
                    }
                }

                // --- Step 3: Restore POS Product stock ---
                var product = _context.POSProduct.FirstOrDefault(p => p.Sku == repackItem.VariantSku);
                if (product != null)
                {
                    product.QtySold -= item.AllocatedQty; // reduce quantity sold
                    product.QtyDisplayed += item.AllocatedQty; // restore quantity displayed
                }

                // --- Step 4: Tag void the allocation record ---
                item.IsVoided = true;
                item.UpdatedBy = username;
                item.UpdatedAt = DateTime.Now;
            }
        }
                       
        public async Task AllocateFifoToTransaction(int transactionDetailId, string variantSku, int requestedQty, string username = "System")
        {
            // Get available RepackItems for the SKU, ordered by DateCreated (FIFO)
            var availableItems = _context.RepackItem
                .Where(r => r.VariantSku == variantSku
                        && r.QuantityDisplayedToPOS > 0 // ensure item is added to pos
                        && (r.InitialQty - r.SoldQty) > 0 // ensure item is not sold out
                        && !r.IsDeleted)
                .OrderBy(r => r.DateCreated)
                .ThenBy(r => r.Id)
                .ToList();

            int remainingQty = requestedQty;

            foreach (var item in availableItems)
            {
                var availableQty = Math.Min(item.QuantityDisplayedToPOS, item.QuantityDisplayed);
                if (availableQty <= 0) continue;

                // Update Repack Item Table
                var allocateQty = Math.Min(availableQty, remainingQty);
                item.QuantityDisplayedToPOS -= allocateQty;
                item.QuantityDisplayed -= allocateQty;
                item.SoldQty += allocateQty;
                item.UpdateAt = DateTime.Now;
                item.UpdateBy = username;

                // Update POS Product table
                var product = await _context.POSProduct.FirstOrDefaultAsync(p => p.Sku == variantSku);
                if (product != null)
                {
                    product.QtySold += allocateQty;
                    product.QtyDisplayed -= allocateQty;
                }

                // Create a new TransactionRepackItem record
                var transactionRepack = new TransactionRepackItem
                {
                    TransactionDetailId = transactionDetailId,
                    RepackItemId = item.Id,
                    AllocatedQty = allocateQty,
                    CreatedAt = DateTime.Now,
                    CreatedBy = username
                };

                _context.TransactionRepackItems.Add(transactionRepack);

                var inventory = _context.Inventory.FirstOrDefault(i => i.Id == item.InventoryId);
                if (inventory != null)
                {
                    inventory.CurrentQty -= (allocateQty * item.QuantityValue);
                }

                remainingQty -= allocateQty;
                if (remainingQty <= 0) break;
            }

            if (remainingQty > 0)
            {
                throw new Exception($"Not enough stock for SKU {variantSku}. Remaining qty: {remainingQty}");
            }
        }
    }
}
