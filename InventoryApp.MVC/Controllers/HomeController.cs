using InventoryApp.Core.Models;
using InventoryApp.DbContext;
using InventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Controllers
{
    [Authorize(Roles = "Admin,User")]
    public class HomeController : BaseController
    {

        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context) : base(context)
        {
            _logger = logger;
            _context = context;
        }
        
        #region Public
        public IActionResult Index()
        {
            var username = HttpContext.Session.GetString("_Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");
            return View();
        }
        public async Task<IActionResult> Dashboard()
        {
            var data = await GetDashboardData();

            ViewBag.TopPerKg = data.TopPerKg;
            ViewBag.MonthlySales = data.MonthlySales;
            ViewBag.NoStock = data.NoStock;
            ViewBag.SlowMoving = data.SlowMoving;

            return View();
        }
        #endregion

        #region Private

        private async Task<(
            Dictionary<string, double> TopPerKg,
            Dictionary<string, int> MonthlySales,
            List<object> NoStock,
            List<KeyValuePair<string, double>> SlowMoving)>
        GetDashboardData()
        {
            // Sales per product
            var salesData = await _context.Sales
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Where(s => s.DateSold.Year == DateTime.Now.Year)
                .ToListAsync();

            var productSales = new Dictionary<string, double>();
            var productSalesCount = new Dictionary<string, int>();

            foreach (var sale in salesData)
            {
                var product = sale.Inventory?.product ?? sale.RepackItem?.product;
                if (product == null) continue;

                string name = product.ProductName;
                double qty = sale.Quantity;

                if (!productSales.ContainsKey(name))
                    productSales[name] = 0;
                if (!productSalesCount.ContainsKey(name))
                    productSalesCount[name] = 0;

                productSales[name] += qty;
                productSalesCount[name] += (int)Math.Ceiling(qty);
            }

            // Top 5 selling products
            var topPerKg = productSales
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToDictionary(x => x.Key, x => x.Value);

            // Monthly sales for current year
            var monthlySales = await _context.Sales
                .Where(s => s.DateSold.Year == DateTime.Now.Year)
                .GroupBy(s => s.DateSold.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    TotalSales = g.Sum(x => x.TotalPrice)
                })
                .ToListAsync();

            var monthlySalesDict = monthlySales.ToDictionary(
                x => new DateTime(DateTime.Now.Year, x.Month, 1).ToString("MMM yyyy"),
                x => (int)x.TotalSales);

            // Inventory + products
            var inventoryStocks = await _context.Inventory
                .Include(i => i.product)
                .ToListAsync();

            // Low Stock Inventory
            var noStock = inventoryStocks
                .Where(i => i.product != null)
                .GroupBy(i => new
                {
                    i.product.ProductName,
                    i.product.RestockThreshold,
                    i.product.UnitOfMeasure
                })
                .Select(g => new
                {
                    ProductName = g.Key.ProductName,
                    TotalStock = g.Sum(x => x.CurrentQty),
                    Threshold = g.Key.RestockThreshold,
                    UnitOfMeasure = g.Key.UnitOfMeasure.ToString()
                })
                .Where(i => i.TotalStock <= i.Threshold)
                .Select(i => new
                {
                    i.ProductName,
                    i.TotalStock,
                    i.Threshold,
                    i.UnitOfMeasure,
                    DisplayStock = $"{i.TotalStock} {i.UnitOfMeasure.ToLower()}"
                })
                .Cast<object>()
                .ToList();

            // Slow Moving Products
            var slowMovingRaw = new List<KeyValuePair<string, double>>();

            foreach (var stock in inventoryStocks)
            {
                var product = stock.product;
                if (!productSales.ContainsKey(product.ProductName)) continue;

                var totalSold = productSales[product.ProductName];
                var totalCount = productSalesCount[product.ProductName];

                if (product.UnitOfMeasure == UnitOfMeasure.Grams && totalSold < 20)
                {
                    slowMovingRaw.Add(new KeyValuePair<string, double>(product.ProductName, totalSold));
                }
                else if (product.UnitOfMeasure == UnitOfMeasure.Piece && totalCount <= 3)
                {
                    slowMovingRaw.Add(new KeyValuePair<string, double>(product.ProductName, totalCount));
                }
            }

            var slowMoving = slowMovingRaw
                .GroupBy(x => x.Key)
                .Select(g => new KeyValuePair<string, double>(g.Key, g.Min(x => x.Value)))
                .ToList();

            return (topPerKg, monthlySalesDict, noStock, slowMoving);
        }
        #endregion

        #region audit logs
        [HttpGet]
        public IActionResult ViewAuditLogs(DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            const int pageSize = 20;

            var today = DateTime.Today;
            fromDate ??= today;
            toDate ??= today;

            var query = _context.AuditLogs.AsQueryable();

            // Filter by date range
            if (fromDate.HasValue && toDate.HasValue)
            {
                var from = fromDate.Value.Date;
                var to = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(log => log.Timestamp >= from && log.Timestamp <= to);
            }

            var totalLogs = query.Count();
            var totalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);

            var logs = query
                .OrderByDescending(log => log.Timestamp) // Latest first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

            return View(logs);
        }
        #endregion

    }
}
