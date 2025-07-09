using Inventory.API.Dtos;
using InventoryApp.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SalesApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("ping")]
        public ActionResult<string> Ping()
        {
            return Ok("Pong");
        }

        // GET: api/SalesApi
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetAllSales()
        {
            var sales = await _context.Sales
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.DisplayItem)
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.DateSold)
                .ToListAsync();

            var dtoList = sales.Select(s => new SaleDto
            {
                Id = s.Id,
                Quantity = s.Quantity,
                TotalPrice = s.TotalPrice,
                SalesChannel = s.SalesChannel,
                DateSold = s.DateSold
            });

            return Ok(dtoList);
        }

        // GET: api/SalesApi/date?from=2025-06-01&to=2025-06-20
        [HttpGet("date")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSalesByDate(DateTime from, DateTime to)
        {
            var sales = await _context.Sales
                .Include(s => s.RepackItem).ThenInclude(r => r.product)
                .Include(s => s.Inventory).ThenInclude(i => i.product)
                .Include(s => s.DisplayItem)
                .Where(s => !s.IsDeleted && s.DateSold.Date >= from.Date && s.DateSold.Date <= to.Date)
                .OrderByDescending(s => s.DateSold)
                .ToListAsync();

            var dtoList = sales.Select(s => new SaleDto
            {
                Id = s.Id,
                Quantity = s.Quantity,
                TotalPrice = s.TotalPrice,
                SalesChannel = s.SalesChannel,
                DateSold = s.DateSold
            });

            return Ok(dtoList);
        }

        // POST: api/SalesApi
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateSale([FromBody] SaleDto dto)
        {
            if (dto == null || dto.Quantity <= 0)
                return BadRequest("Invalid sale data.");

            var sale = new Sale
            {
                Quantity = dto.Quantity,
                TotalPrice = dto.TotalPrice,
                SalesChannel = dto.SalesChannel,
                DateSold = DateTime.Now,
                CreatedBy = "POS"
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Sale created", id = sale.Id });
        }
    }
}
