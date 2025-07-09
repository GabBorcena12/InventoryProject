using InventoryApp.Core.Models;
using InventoryApp.DbContext;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = "Admin,User")]
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
        public IActionResult MonthlySalesDetailed(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var monthlySales = _context.Sales
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Where(s => s.DateSold.Month == selectedMonth && s.DateSold.Year == selectedYear && s.SalesChannel != "Out Items")
                .Select(s => new
                {
                    DateSold = s.DateSold.ToString("yyyy-MM-dd"),
                    BatchNo = s.Inventory.BatchNo ?? "—",
                    RepackDescription = s.RepackItem != null
                        ? $"{s.RepackItem.QuantityValue} {s.RepackItem.product.UnitOfMeasure} {s.RepackItem.product.ProductName}"
                        : s.Inventory.product.ProductName ?? "—",
                    Quantity = s.Quantity,
                    CostPrice = s.RepackItem != null && s.Inventory != null && s.RepackItem.product != null
                        ? ((decimal)s.RepackItem.QuantityValue / s.RepackItem.product.Volume) * s.Inventory.CostPerUnit
                        : s.Inventory != null ? s.Inventory.CostPerUnit * s.Quantity : 0m,
                    TotalPrice = s.TotalPrice,
                    SalesChannel = s.SalesChannel
                })
                .ToList();

            ViewBag.ReportTitle = "Pet Supplies - Monthly Sales (Detailed)";
            ViewBag.ReportDate = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.EnableDateFilter = true;

            ViewBag.ReportColumns = new List<string>
            {
                "Date Sold",
                "Batch No",
                "Product Name",
                "Cost Price Per Unit",
                "Quantity (pcs)",
                "Total Price",
                "Total Profit",
                "Sales Channel"
            };

            ViewBag.ReportData = monthlySales.Select(ms => new List<string>
            {
                ms.DateSold,
                ms.BatchNo,
                ms.RepackDescription,
                $"₱{ms.CostPrice:N2}",
                $"{ms.Quantity} Piece{(ms.Quantity > 1 ? "s" : "")}",
                $"₱{ms.TotalPrice:N2}",
                $"₱{(ms.TotalPrice - (ms.CostPrice * ms.Quantity)):N2}",
                ms.SalesChannel
            }).ToList();

            ViewBag.TotalQuantity = monthlySales.Sum(ms => ms.Quantity);
            ViewBag.TotalAmount = monthlySales.Sum(ms => ms.TotalPrice);
            ViewBag.TotalProfit = monthlySales.Sum(ms => ms.TotalPrice - (ms.CostPrice * ms.Quantity));
            ViewBag.Action = "MonthlySalesDetailed";

            return View("Report");
        }

        /// <summary>
        /// Generates a summarized monthly sales report
        /// </summary>
        public IActionResult MonthlySalesSummary(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var groupedSales = _context.Sales
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Where(s => s.DateSold.Month == selectedMonth
                            && s.DateSold.Year == selectedYear
                            && s.SalesChannel != "Out Items")
                .Select(s => new
                {
                    ProductName = s.RepackItem != null
                        ? s.RepackItem.product.ProductName
                        : s.Inventory.product.ProductName,
                    Quantity = s.Quantity,
                    TotalPrice = s.TotalPrice,
                    Cost = s.RepackItem != null && s.RepackItem.product != null
                        ? ((decimal)s.RepackItem.QuantityValue / s.RepackItem.product.Volume) * s.Inventory.CostPerUnit * s.Quantity
                        : s.Inventory.CostPerUnit * s.Quantity
                })
                .GroupBy(s => s.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.TotalPrice),
                    TotalCost = g.Sum(x => x.Cost),
                    Profit = g.Sum(x => x.TotalPrice) - g.Sum(x => x.Cost)
                })
                .ToList();

            ViewBag.ReportTitle = "Pet Supplies - Monthly Sales Summary";
            ViewBag.ReportDate = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");

            ViewBag.ReportColumns = new List<string>
            {
                "Product Name",
                "Total Quantity Sold",
                "Total Sales",
                "Profit"
            };

            ViewBag.ReportData = groupedSales.Select(s => new List<string>
            {
                s.ProductName,
                $"{s.TotalQuantity} Piece{(s.TotalQuantity > 1 ? "s" : "")}",
                $"₱{s.TotalSales:N2}",
                $"₱{s.Profit:N2}"
            }).ToList();

            ViewBag.TotalQuantity = groupedSales.Sum(s => s.TotalQuantity);
            ViewBag.TotalAmount = groupedSales.Sum(s => s.TotalSales);
            ViewBag.TotalProfit = groupedSales.Sum(s => s.Profit);

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.EnableDateFilter = true;
            ViewBag.Action = "MonthlySalesSummary";

            return View("Report");
        }

        /// <summary>
        /// Displays yearly sales overview
        /// </summary>
        public IActionResult SalesOverview(int? year)
        {
            int selectedYear = year ?? DateTime.Now.Year;

            var sales = _context.Sales
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Where(s => s.SalesChannel != "Out Items" && s.DateSold.Year == selectedYear)
                .ToList();

            var monthlySales = Enumerable.Range(1, 12).Select(month =>
            {
                var salesInMonth = sales.Where(s => s.DateSold.Month == month);

                decimal totalSales = salesInMonth.Sum(s => s.TotalPrice);
                decimal totalCost = salesInMonth.Sum(s =>
                {
                    if (s.RepackItem != null && s.RepackItem.product != null && s.Inventory != null)
                    {
                        return ((decimal)s.RepackItem.QuantityValue / s.RepackItem.product.Volume)
                               * s.Inventory.CostPerUnit * s.Quantity;
                    }
                    else if (s.Inventory != null && s.Inventory.product != null)
                    {
                        return s.Inventory.CostPerUnit * s.Quantity;
                    }
                    else
                    {
                        return 0;
                    }
                });

                return new
                {
                    Month = new DateTime(selectedYear, month, 1).ToString("MMMM"),
                    TotalSales = totalSales,
                    TotalProfit = totalSales - totalCost
                };
            }).ToList();

            ViewBag.ReportTitle = $"Sales Overview for {selectedYear}";
            ViewBag.ReportDate = selectedYear.ToString();
            ViewBag.ReportColumns = new List<string> { "Month", "Total Sales", "Total Profit" };
            ViewBag.SelectedYear = selectedYear;
            ViewBag.EnableDateFilter = true;
            ViewBag.Action = "SalesOverview";

            ViewBag.ReportData = monthlySales.Select(ms => new List<string>
            {
                ms.Month,
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
            ViewBag.ReportTitle = "Items to Reorder";
            ViewBag.ReportDate = DateTime.Now.ToString("MMMM yyyy");
            ViewBag.ReportColumns = new List<string>
            {
                "Item", "Current Stock", "Threshold", "Recommended Order", "Status"
            };

            var groupedItems = _context.Inventory
                .Where(i => i.product != null)
                .GroupBy(i => new
                {
                    i.product.ProductName,
                    i.product.UnitOfMeasure,
                    i.product.RestockThreshold,
                    i.product.Volume
                })
                .Select(g => new
                {
                    Item = g.Key.ProductName,
                    UOM = g.Key.UnitOfMeasure.ToString(),
                    Threshold = g.Key.RestockThreshold,
                    Volume = g.Key.Volume,
                    TotalCurrentStock = g.Sum(x => x.CurrentQty)
                })
                .Where(i => i.TotalCurrentStock <= i.Threshold)
                .Select(i => new
                {
                    i.Item,
                    i.UOM,
                    i.TotalCurrentStock,
                    i.Threshold,
                    RecommendedOrder = i.UOM.ToLower() == "grams"
                        ? i.Volume - i.TotalCurrentStock
                        : i.Threshold - i.TotalCurrentStock,
                    Status = i.TotalCurrentStock == 0 ? "Out of Stock" : "Low Stock"
                })
                .ToList();

            ViewBag.ReportData = groupedItems
                .Select(i => new List<string>
                {
                    i.Item,
                    $"{i.TotalCurrentStock} {i.UOM}",
                    $"{i.Threshold} {i.UOM}",
                    $"{i.RecommendedOrder} {i.UOM}",
                    i.Status
                })
                .ToList();

            ViewBag.ItemCount = groupedItems.Count();
            return View("Report");
        }

        /// <summary>
        /// Financial summary report for selected month and year
        /// </summary>
        [HttpGet]
        public IActionResult FinancialSummaryReport(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            ViewBag.ReportTitle = "Financial Summary Report";
            ViewBag.ReportDate = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");
            ViewBag.Action = "FinancialSummaryReport";
            ViewBag.EnableDateFilter = true;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            var expenses = _context.OperatingExpenses
                .Where(e => e.Date.Month == selectedMonth && e.Date.Year == selectedYear)
                .ToList();

            var sales = _context.Sales
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Where(s => s.DateSold.Month == selectedMonth
                            && s.DateSold.Year == selectedYear
                            && s.SalesChannel != "Out Items")
                .Select(s => new
                {
                    Cost = s.RepackItem != null && s.RepackItem.product != null
                        ? ((decimal)s.RepackItem.QuantityValue / s.RepackItem.product.Volume) * s.Inventory.CostPerUnit * s.Quantity
                        : s.Inventory.CostPerUnit * s.Quantity
                })
                .ToList();

            decimal totalCOGS = sales.Sum(s => s.Cost);
            decimal totalExpenses = expenses.Sum(e => e.Amount);

            var reportData = new List<List<string>>();

            foreach (var exp in expenses)
            {
                reportData.Add(new List<string>
                {
                    exp.Date.ToString("MMM dd, yyyy"),
                    exp.Description,
                    exp.Category.ToString(),
                    $"₱{exp.Amount:N2}",
                    exp.Notes ?? "-"
                });
            }

            reportData.Add(new List<string> { "—", "—", "—", "—", "—" });

            reportData.Add(new List<string>
            {
                new DateTime(selectedYear, selectedMonth, 1).ToString("MMM dd, yyyy"),
                "Cost of Goods Sold (COGS)",
                "COGS",
                $"₱{totalCOGS:N2}",
                "Computed from monthly sales"
            });

            ViewBag.ReportColumns = new List<string>
            {
                "Date", "Description", "Category", "Amount", "Notes"
            };

            ViewBag.ReportData = reportData;
            ViewBag.TotalAmount = totalExpenses + totalCOGS;
            ViewBag.ItemCount = expenses.Count + 1;

            return View("Report");
        }
    }
}
