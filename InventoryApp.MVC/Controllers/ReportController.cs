using InventoryApp.Core.Authorizations;
using InventoryApp.Core.Models;
using InventoryApp.MVC.Models.ViewModel.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = RoleConstants.AdminsOnly)]
    public class ReportController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Main Report landing page
        /// </summary>
        public IActionResult Report()
        {
            return View();
        }

        /// <summary>
        /// Generates a detailed monthly sales report
        /// </summary>
        public IActionResult VoidedSalesTransaction(DateTime? fromDate, DateTime? toDate)
        {
            // If no range is selected, default to current month
            var startDate = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = toDate ?? startDate.AddMonths(1).AddDays(-1);

            // 🔹 POS TransactionRepackItems
            var posQuery =
                from f in _context.POSTransactionHeaders
                where f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == true
                select new TransactionSalesDto
                {
                    ORNo = f.ORNumber,
                    TransactionDate = f.TransactionDate,
                    CashierName =  f.CashierName,
                    PaymentMethod = f.PaymentMethod,
                    TotalAmount = f.TotalAmount,

                };

            var voidedSales = posQuery.ToList();


            ViewBag.ReportTitle = $"Voided Transaction Sales";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;

            ViewBag.ReportColumns = new List<string>
            {
                "OR Number",
                "Transaction Date",
                "Cashier Name",
                "Payment Method",
                "Total Amount",
            };

            ViewBag.ReportData = voidedSales.Select(ms => new List<string>
            {
                ms.ORNo,
                ms.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                ms.CashierName,
                ms.PaymentMethod,
                $"₱{ms.TotalAmount:N2}",
            }).ToList();

            ViewBag.Count = voidedSales.Count();
            ViewBag.TotalAmount = voidedSales.Sum(ms => ms.TotalAmount);
            ViewBag.Action = "VoidedSalesTransaction";

            return View("Report");
        }

        /// <summary>
        /// Generates a detailed monthly sales report
        /// </summary>
        public IActionResult DetailedSalesReport(DateTime? fromDate, DateTime? toDate, string salesChannel = "All")
        {
            // If no range is selected, default to current month
            var startDate = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = toDate ?? startDate.AddMonths(1).AddDays(-1);

            // 🔹 POS TransactionRepackItems
            var posQuery =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join c in _context.Inventory on b.InventoryId equals c.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == false
                select new MonthlySalesDto
                {
                    DateSold = f.TransactionDate,
                    BatchNo = b.BatchNo ?? "—",
                    RepackDescription = b.VariantCode ?? "—",
                    Quantity = a.AllocatedQty,
                    CapitalPrice = (c.CostPerUnit / d.Volume) * b.QuantityValue,
                    SellingPrice = b.PricePerUnit,
                    TotalPrice = a.AllocatedQty * b.PricePerUnit,
                    TotalProfit = (a.AllocatedQty * b.PricePerUnit) - (((c.CostPerUnit / d.Volume) * b.QuantityValue) * a.AllocatedQty),
                    SalesChannel = "POS System"
                };

            var monthlySales = posQuery.ToList();

            // 🔹 Apply channel filter if set
            if (!string.IsNullOrEmpty(salesChannel) && salesChannel != "All")
            {
                monthlySales = monthlySales
                    .Where(ms => ms.SalesChannel == salesChannel)
                    .ToList();
            }

            // 📊 Report Metadata
            var channel = salesChannel == "All"
                ? ""
                : (salesChannel == "Inventory System" ? "Inventory" : "POS");

            ViewBag.ReportTitle = $"{(string.IsNullOrEmpty(channel) ? "" : channel + " ")} Detailed Sales Report";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.EnableDateFilter = true;
            ViewBag.SelectedChannel = salesChannel;
            ViewBag.EnableChannelFilter = false;

            ViewBag.ReportColumns = new List<string>
            {
                "Date Sold",
                "Batch No",
                "Product Name",
                "Quantity",
                "Cost Price",
                "Total Price",
                "Total Profit",
                "Sales Channel"
            };

            ViewBag.ReportData = monthlySales.Select(ms => new List<string>
            {
                ms.DateSold.ToString("yyyy-MM-dd HH:mm:ss"),
                ms.BatchNo,
                ms.RepackDescription,
                $"{ms.Quantity} Piece{(ms.Quantity > 1 ? "s" : "")}",
                $"₱{ms.CapitalPrice:N2}",
                $"₱{ms.TotalPrice:N2}",
                $"₱{ms.TotalProfit:N2}",
                ms.SalesChannel
            }).ToList();

            ViewBag.TotalQuantity = monthlySales.Sum(ms => ms.Quantity);
            ViewBag.TotalAmount = monthlySales.Sum(ms => ms.TotalPrice);
            ViewBag.TotalProfit = monthlySales.Sum(ms => ms.TotalProfit);
            ViewBag.Action = "DetailedSalesReport";

            return View("Report");
        }

        /// <summary>
        /// Generates a detailed monthly sales report
        /// </summary>
        public IActionResult ReleasedItems(DateTime? fromDate, DateTime? toDate)
        {
            // If no range is selected, default to current month
            var startDate = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = toDate ?? startDate.AddMonths(1).AddDays(-1);

            // 🔹 Regular Sales
            var salesQuery =
                from a in _context.Sales
                join c in _context.RepackItem on a.RepackItemId equals c.Id
                join b in _context.Inventory on c.InventoryId equals b.Id
                join d in _context.Products on c.ProductId equals d.Id
                where a.SalesChannel == "Out Items"
                      && a.DateSold >= startDate
                      && a.DateSold <= endDate
                select new MonthlySalesDto
                {
                    DateSold = a.DateSold,
                    BatchNo = c.BatchNo ?? "—",
                    RepackDescription = c.VariantCode ?? "—",
                    Quantity = a.Quantity,
                    CostPriceUnit = (b.CostPerUnit / d.Volume) * c.QuantityValue,
                    TotalCost = ((b.CostPerUnit / d.Volume) * c.QuantityValue) * a.Quantity
                };


            var monthlySalesLoss = salesQuery.ToList();


            ViewBag.ReportTitle = $"Released Items";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;

            ViewBag.ReportColumns = new List<string>
            {
                "Released Date",
                "Batch No",
                "Product Name",
                "Quantity",
                "Cost Per Unit",
                "Total Amount Loss",
            };

            ViewBag.ReportData = monthlySalesLoss.Select(ms => new List<string>
            {
                ms.DateSold.ToString("yyyy-MM-dd HH:mm:ss"),
                ms.BatchNo,
                ms.RepackDescription,
                $"{ms.Quantity} Piece{(ms.Quantity > 1 ? "s" : "")}",
                $"₱{ms.CostPriceUnit:N2}",
                $"₱{ms.TotalCost:N2}"
            }).ToList();

            ViewBag.TotalQuantity =  monthlySalesLoss.Sum(ms => ms.Quantity);
            ViewBag.TotalAmount =  monthlySalesLoss.Sum(ms => ms.TotalCost);
            ViewBag.Action = "ReleasedItems";
            return View("Report");
        }

        /// <summary>
        /// Shows the top-selling products by revenue
        /// </summary>
        public IActionResult TopProductsReport(DateTime? fromDate, DateTime? toDate, string salesChannel = "All", int topCount = 10)
        {
            var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = (toDate?.Date ?? DateTime.Now).AddDays(1).AddTicks(-1);

            // 🔹 POS Sales
            var posSales =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == false
                select new
                {
                    d.ProductName,
                    Quantity = a.AllocatedQty,
                    TotalSales = a.AllocatedQty * b.PricePerUnit,
                    SalesChannel = "POS System"
                };

            // 🔹 Combine sales
            var combinedSales = posSales;

            // Apply filter
            if (salesChannel != "All")
            {
                combinedSales = combinedSales.Where(x => x.SalesChannel == salesChannel);
            }

            // 🔹 Rank by total sales
            var topProducts = combinedSales
                .GroupBy(x => x.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.TotalSales)
                })
                .OrderByDescending(x => x.TotalSales)
                .Take(topCount)
                .ToList();

            // 📊 Metadata
            ViewBag.ReportTitle = "Top Products Report";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.Action = "TopProductsReport";

            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;

            // Table setup
            ViewBag.ReportColumns = new List<string> { "Product Name", "Total Quantity Sold", "Total Sales" };
            ViewBag.ReportData = topProducts.Select(x => new List<string>
            {
                x.ProductName,
                $"{x.TotalQuantity} pcs",
                $"₱{x.TotalSales:N2}"
            }).ToList();

            return View("Report");
        }

        /// <summary>
        /// Shows products with low or no sales
        /// </summary>
        public IActionResult LowOrNoSalesReport(DateTime? fromDate, DateTime? toDate, string salesChannel = "All")
        {
            var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = (toDate?.Date ?? DateTime.Now).AddDays(1).AddTicks(-1);

            // 🔹 All products
            var allProducts = _context.Products.Select(p => p.ProductName).ToList();

            var posSales =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == false
                select new { d.ProductName, Quantity = a.AllocatedQty, SalesChannel = "POS System" };

            var combinedSales = posSales;

            if (salesChannel != "All")
            {
                combinedSales = combinedSales.Where(x => x.SalesChannel == salesChannel);
            }

            // 🔹 Group by product with total qty
            var soldProducts = combinedSales
                .GroupBy(x => x.ProductName)
                .Select(g => new { ProductName = g.Key, TotalQuantity = g.Sum(x => x.Quantity) })
                .ToList();

            // 🔹 Find products not sold or low sales
            var lowOrNoSales = allProducts
                .GroupJoin(soldProducts, p => p, s => s.ProductName, (p, s) => new
                {
                    ProductName = p,
                    TotalQuantity = s.FirstOrDefault()?.TotalQuantity ?? 0
                })
                .Where(x => x.TotalQuantity == 0 || x.TotalQuantity <= 5) // configurable threshold
                .ToList();

            // 📊 Metadata
            ViewBag.ReportTitle = "Low/No Sales Products Report";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.Action = "LowOrNoSalesReport";
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;

            // Table setup
            ViewBag.ReportColumns = new List<string> { "Product Name", "Total Quantity Sold" };
            ViewBag.ReportData = lowOrNoSales.Select(x => new List<string>
            {
                x.ProductName,
                $"{x.TotalQuantity} pcs"
            }).ToList();

            return View("Report");
        }

        /// <summary>
        /// Product Performance Report
        /// Displays each product with total sold items, sales, and profit
        /// </summary>
        public IActionResult ProductPerformanceReport(DateTime? fromDate, DateTime? toDate, string salesChannel = "All")
        {
            // 📌 Default range: current month
            var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = (toDate?.Date ?? DateTime.Now).AddDays(1).AddTicks(-1);

            // 🔹 POS Sales
            var posSales =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join c in _context.Inventory on b.InventoryId equals c.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == false
                select new
                {
                    ProductName = d.ProductName,
                    Quantity = a.AllocatedQty,
                    TotalSales = a.AllocatedQty * b.PricePerUnit,
                    Cost = ((c.CostPerUnit / d.Volume) * b.QuantityValue) * a.AllocatedQty,
                    SalesChannel = "POS System"
                };

            // 🔹 Combine both
            var combinedSales = posSales;

            // 🔹 Apply filter
            if (salesChannel != "All")
            {
                combinedSales = combinedSales.Where(x => x.SalesChannel == salesChannel);
            }

            // 🔹 Group by product
            var productPerformance = combinedSales
                .GroupBy(x => x.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.TotalSales),
                    TotalCost = g.Sum(x => x.Cost),
                    Profit = g.Sum(x => x.TotalSales) - g.Sum(x => x.Cost)
                })
                .OrderByDescending(g => g.TotalSales)
                .ToList();

            // 📊 Report Metadata
            ViewBag.ReportTitle = "Product Performance Report";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.Action = "ProductPerformanceReport";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;
            ViewBag.SelectedChannel = salesChannel;

            // Table Columns
            ViewBag.ReportColumns = new List<string>
            {
                "Product Name",
                "Total Quantity Sold",
                "Total Sales",
                "Profit"
            };

            // Table Data
            ViewBag.ReportData = productPerformance.Select(p => new List<string>
            {
                p.ProductName,
                $"{p.TotalQuantity} pcs",
                $"₱{p.TotalSales:N2}",
                $"₱{p.Profit:N2}"
            }).ToList();

            // Totals
            ViewBag.TotalQuantity = productPerformance.Sum(p => p.TotalQuantity);
            ViewBag.TotalAmount = productPerformance.Sum(p => p.TotalSales);
            ViewBag.TotalProfit = productPerformance.Sum(p => p.Profit);

            return View("Report");
        }

        /// <summary>
        /// Cost Variance Report
        /// Displays product purchase prices by supplier and batch
        /// </summary>
        public IActionResult CostVarianceReport(DateTime? fromDate, DateTime? toDate)
        {
            // 📌 Default range: current year
            var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, 1, 1);
            var endDate = (toDate?.Date ?? DateTime.Now).AddDays(1).AddTicks(-1);

            var purchases =
                from a in _context.Inventory
                join b in _context.Suppliers on a.SupplierId equals b.Id
                join c in _context.Products on a.ProductId equals c.Id
                where a.DateCreated >= startDate && a.DateCreated <= endDate
                select new
                {
                    a.BatchNo,
                    a.SKU,
                    ProductName = c.ProductName,
                    SupplierName = b.Name,
                    PurchasePrice = a.CostPerUnit,
                    PurchasedDate = a.DateCreated,
                    Quantity = a.InitialQuantity / c.Volume,
                    TotalCost = a.CostPerUnit * (a.InitialQuantity / c.Volume)
                };

            var results = purchases
                .OrderBy(r => r.ProductName)
                .ThenBy(r => r.SupplierName)
                .ThenBy(r => r.PurchasedDate)
                .ToList();

            // 📊 Metadata
            ViewBag.ReportTitle = "Cost Variance Report";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.Action = "CostVarianceReport";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.EnableDateFilter = true;

            // Table Columns
            ViewBag.ReportColumns = new List<string>
            {
                "Batch No",
                "SKU",
                "Product Name",
                "Supplier",
                "Purchase Price",
                "Purchased Date",
                "Quantity",
                "Total Cost"
            };

            // Table Data
            ViewBag.ReportData = results.Select(r => new List<string>
            {
                r.BatchNo,
                r.SKU,
                r.ProductName,
                r.SupplierName,
                $"₱{r.PurchasePrice:N2}",
                r.PurchasedDate.ToString("yyyy-MM-dd"),
                $"{r.Quantity}",
                $"₱{r.TotalCost:N2}"
            }).ToList();

            // Totals
            ViewBag.TotalQuantity = results.Sum(r => r.Quantity);
            ViewBag.TotalAmount = results.Sum(r => r.TotalCost);

            return View("Report");
        }

        /// <summary>
        /// Generates a summarized monthly sales report
        /// </summary>
        public IActionResult SummarySalesReport(DateTime? fromDate, DateTime? toDate, string salesChannel = "All")
        {
            // 📌 Default range: current month if not provided
            var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = (toDate?.Date ?? DateTime.Now).AddDays(1).AddTicks(-1);

            // 🔹 POS Sales
            var posQuery =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join c in _context.Inventory on b.InventoryId equals c.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == false
                select new
                {
                    ProductName = d.ProductName,
                    Quantity = a.AllocatedQty,
                    TotalPrice = a.AllocatedQty * b.PricePerUnit,
                    Cost = ((c.CostPerUnit / d.Volume) * b.QuantityValue) * a.AllocatedQty,
                    SalesChannel = "POS System"
                };

            // 🔹 Combine both
            var combinedQuery = posQuery;

            // 🔹 Apply Channel Filter if not "All"
            if (salesChannel != "All")
            {
                combinedQuery = combinedQuery.Where(x => x.SalesChannel == salesChannel);
            }

            // 🔹 Group by ProductName
            var groupedSales = combinedQuery
                .GroupBy(x => x.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.TotalPrice),
                    TotalCost = g.Sum(x => x.Cost),
                    Profit = g.Sum(x => x.TotalPrice) - g.Sum(x => x.Cost)
                })
                .ToList();

            // 📊 Report Metadata
            var channel = salesChannel == "All"
                ? ""
                : (salesChannel == "Inventory System" ? "Inventory" : "POS");

            ViewBag.ReportTitle = $"{(string.IsNullOrEmpty(channel) ? "" : channel + " ")} Summary Sales Report";

            // Show date range
            ViewBag.ReportDate = $"{startDate:MMMM dd, yyyy} - {endDate:MMMM dd, yyyy}";
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedChannel = salesChannel;
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false; // ✅ enable channel dropdown
            ViewBag.Action = "SummarySalesReport";

            // Table Columns
            ViewBag.ReportColumns = new List<string>
            {
                "Product Name",
                "Total Quantity Sold",
                "Total Sales",
                "Profit"
            };

            // Table Data
            ViewBag.ReportData = groupedSales.Select(s => new List<string>
            {
                s.ProductName,
                $"{s.TotalQuantity} Piece{(s.TotalQuantity > 1 ? "s" : "")}",
                $"₱{s.TotalSales:N2}",
                $"₱{s.Profit:N2}"
            }).ToList();

            // Totals
            ViewBag.TotalQuantity = groupedSales.Sum(s => s.TotalQuantity);
            ViewBag.TotalAmount = groupedSales.Sum(s => s.TotalSales);
            ViewBag.TotalProfit = groupedSales.Sum(s => s.Profit);

            return View("Report");
        }

        /// <summary>
        /// Displays yearly sales overview
        /// </summary>
        public IActionResult SalesOverview(int? year, string salesChannel = "All")
        {
            int selectedYear = year ?? DateTime.Now.Year;

            // 🔹 POS Sales
            var posQuery =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join c in _context.Inventory on b.InventoryId equals c.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate.Year == selectedYear
                      && f.IsVoided == false
                select new
                {
                    Month = f.TransactionDate.Month,
                    TotalPrice = a.AllocatedQty * b.PricePerUnit,
                    Cost = ((c.CostPerUnit / d.Volume) * b.QuantityValue) * a.AllocatedQty,
                    SalesChannel = "POS System"
                };

            // 🔹 Combine both
            var combinedQuery = posQuery;

            // 🔹 Apply Channel Filter
            if (salesChannel != "All")
            {
                combinedQuery = combinedQuery.Where(x => x.SalesChannel == salesChannel);
            }

            // 🔹 Group by Month
            var monthlySales = combinedQuery
                .GroupBy(x => x.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    TotalSales = g.Sum(x => x.TotalPrice),
                    TotalProfit = g.Sum(x => x.TotalPrice) - g.Sum(x => x.Cost)
                })
                .OrderBy(g => g.Month)
                .ToList();

            // 📊 Report Metadata
            var channel = salesChannel == "All"
                ? ""
                : (salesChannel == "Inventory System" ? "Inventory" : "POS");
            ViewBag.ReportTitle = $"{(string.IsNullOrEmpty(channel) ? "" : channel + " ")} Monthly Sales Overview";
            ViewBag.ReportDate = selectedYear.ToString();
            ViewBag.SelectedYear = selectedYear;
            ViewBag.SelectedChannel = salesChannel;
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false; // ✅ enable channel filter
            ViewBag.Action = "SalesOverview";

            ViewBag.ReportColumns = new List<string> { "Month", "Total Sales", "Total Profit" };

            ViewBag.ReportData = monthlySales.Select(ms => new List<string>
            {
                new DateTime(selectedYear, ms.Month, 1).ToString("MMMM"),
                $"₱{ms.TotalSales:N2}",
                $"₱{ms.TotalProfit:N2}"
            }).ToList();

            ViewBag.TotalAmount = monthlySales.Sum(s => s.TotalSales);
            ViewBag.TotalProfit = monthlySales.Sum(s => s.TotalProfit);

            return View("Report");
        }


        /// <summary>
        /// Lists items that are below restock threshold
        /// </summary>
        public IActionResult ItemsToOrder()
        {
            ViewBag.ReportTitle = "Restock Recommendations";
            ViewBag.ReportDate = DateTime.Now.ToString("MMMM yyyy");
            ViewBag.ReportColumns = new List<string>
            {
                "Item", "Current Stock", "Threshold", "Recommended Order", "Status"
            };

            // Step 1: run query fully in SQL
            var query = from inv in _context.Inventory
                        join prod in _context.Products on inv.ProductId equals prod.Id
                        group inv by new
                        {
                            prod.ProductName,
                            prod.UnitOfMeasure,
                            prod.RestockThreshold
                        }
                        into g
                        select new
                        {
                            Item = g.Key.ProductName,
                            UOM = g.Key.UnitOfMeasure,
                            Threshold = g.Key.RestockThreshold,
                            CurrentStock = g.Sum(x => x.CurrentQty)
                        };

            // Step 2: Apply HAVING condition
            var itemsToOrder = query
                .Where(i => i.CurrentStock < i.Threshold)  // HAVING SUM <= Threshold
                .AsEnumerable() // switch to client side for extra calculations
                .Select(i => new
                {
                    i.Item,
                    i.UOM,
                    i.CurrentStock,
                    i.Threshold,
                    RecommendedOrder = i.Threshold - i.CurrentStock,
                    Status = i.CurrentStock == 0 ? "Out of Stock" : "Low Stock"
                })
                .ToList();

            // Step 3: Send to view
            ViewBag.ReportData = itemsToOrder
                .Select(i => new List<string>
                {
            i.Item,
            $"{i.CurrentStock} {i.UOM}",
            $"{i.Threshold} {i.UOM}",
            $"{i.RecommendedOrder} {i.UOM}",
            i.Status
                })
                .ToList();

            ViewBag.ItemCount = itemsToOrder.Count();
            return View("Report");
        }


        /// <summary>
        /// Financial summary report for selected month and year
        /// </summary>
        [HttpGet]
        public IActionResult FinancialSummaryReport(DateTime? fromDate, DateTime? toDate, string salesChannel = "All")
        {
            // ✅ Ensure date range is valid
            var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = (toDate?.Date ?? DateTime.Now).AddDays(1).AddTicks(-1);

            // 🔹 Expenses within range
            var expenses = _context.OperatingExpenses
                .Where(e => e.Date >= startDate && e.Date <= endDate)
                .ToList();

            // 🔹 POS Sales
            var posSales =
                from a in _context.TransactionRepackItems
                join b in _context.RepackItem on a.RepackItemId equals b.Id
                join c in _context.Inventory on b.InventoryId equals c.Id
                join d in _context.Products on b.ProductId equals d.Id
                join e in _context.POSTransactionDetails on a.TransactionDetailId equals e.TransactionDetailId
                join f in _context.POSTransactionHeaders on e.TransactionHeaderId equals f.TransactionHeaderId
                where a.IsVoided == false
                      && f.TransactionDate >= startDate
                      && f.TransactionDate <= endDate
                      && f.IsVoided == false
                select new
                {
                    Cost = ((c.CostPerUnit / d.Volume) * b.QuantityValue) * a.AllocatedQty,
                    SalesChannel = "POS System"
                };

            // 🔹 Combine & Filter by channel
            var combinedSales = posSales;

            if (salesChannel != "All")
            {
                combinedSales = combinedSales.Where(x => x.SalesChannel == salesChannel);
            }

            // 🔹 Compute totals
            decimal totalCOGS = combinedSales.Sum(s => s.Cost);
            decimal totalExpenses = expenses.Sum(e => e.Amount);

            // 🔹 Report data
            var reportData = new List<List<string>>();

            foreach (var exp in expenses)
            {
                reportData.Add(new List<string>
                {
                    exp.Date.ToString("yyyy-MM-dd HH:mm"),
                    exp.Description,
                    exp.Category.ToString(),
                    $"₱{exp.Amount:N2}",
                    exp.Notes ?? "-"
                });
            }

            reportData.Add(new List<string> { "—", "—", "—", "—", "—" });

            reportData.Add(new List<string>
            {
                $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}",
                "Cost of Goods Sold (COGS)",
                "COGS",
                $"₱{totalCOGS:N2}",
                "Computed from sales in range"
            });

            // 🔹 Metadata
            ViewBag.ReportColumns = new List<string>
            {
                "Date", "Description", "Category", "Amount", "Notes"
            };

            var channel = salesChannel == "All"
                ? ""
                : (salesChannel == "Inventory System" ? "Inventory" : "POS");

            ViewBag.ReportTitle = $"{(string.IsNullOrEmpty(channel) ? "" : channel + " ")} Financial Summary Report";
            ViewBag.ReportDate = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
            ViewBag.Action = "FinancialSummaryReport";
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;
            ViewBag.SelectedChannel = salesChannel;
            ViewBag.SelectedFromDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.ReportData = reportData;
            ViewBag.TotalAmount = totalExpenses + totalCOGS;
            ViewBag.ItemCount = expenses.Count + 1;

            return View("Report");
        }

        public async Task<IActionResult> IssuedCreditMemo(DateTime? startDate, DateTime? endDate)
        {
            // Default date range: last 30 days
            var from = startDate ?? DateTime.UtcNow.AddDays(-30);
            var to = endDate ?? DateTime.UtcNow;

            var creditMemos = await _context.CreditMemos
                .Include(c => c.TransactionDetail)
                    .ThenInclude(td => td.TransactionHeader) // for OR details
                .Include(c => c.TransactionDetail)
                .Where(c => c.IssuedAt >= from && c.IssuedAt <= to)
                .OrderByDescending(c => c.IssuedAt)
                .ToListAsync();

            // Define columns
            var columns = new List<string>
            {
                "Date",
                "OR Number",
                "Variant Sku",
                "Variant Code",
                "Quantity",
                "Unit Price",
                "Total Amount",
                "Reason",
                "Issued By",
                "Status"
            };

            // Convert data into List<List<string>>
            var data = creditMemos.Select(c => new List<string>
            {
                c.IssuedAt.ToString("yyyy-MM-dd HH:mm"),
                c.TransactionOrNumber,
                c.Sku,
                c.ProductName,
                c.Qty.ToString("N2"),
                c.Amount.ToString("N2"),
                c.TotalAmount.ToString("N2"),
                c.Reason,
                c.IssuedBy,
                c.IsVoided ? "Voided" : "Active"
            }).ToList();

            // ViewBags
            ViewBag.ReportTitle = "Issued Credit Memo Report";
            ViewBag.ReportDate = $"{from:MMM dd, yyyy} - {to:MMM dd, yyyy}";
            ViewBag.Action = "IssuedCreditMemo";
            ViewBag.EnableDateFilter = true;
            ViewBag.EnableChannelFilter = false;
            ViewBag.SelectedFromDate = from.ToString("yyyy-MM-dd");
            ViewBag.SelectedToDate = to.ToString("yyyy-MM-dd");
            ViewBag.ReportColumns = columns;
            ViewBag.ReportData = data;
            ViewBag.TotalAmount = creditMemos.Sum(c => c.TotalAmount);
            ViewBag.ItemCount = creditMemos.Count;

            return View("Report");
        }


    }
}
