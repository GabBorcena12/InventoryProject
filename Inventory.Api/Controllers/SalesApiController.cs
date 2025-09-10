using Inventory.Api.Dtos;
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

        // GET: api/SalesApi/all
        [HttpGet("all")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<TransactionSalesDto>>> GetAllSales()
        {
            var sales = from a in _context.POSTransactionHeaders
                        join b in _context.POSTransactionDetails on a.TransactionHeaderId equals b.TransactionHeaderId
                        where !a.IsDeleted
                            && !b.IsDeleted
                            && !a.IsVoided
                        orderby a.TransactionDate descending
                        select new
                        {
                            a.ORNumber,
                            a.TotalAmount,
                            a.TransactionDate,
                            a.CreatedBy,
                            a.PaymentMethod
                        };

            var dtoList = await sales.Select(s => new TransactionSalesDto
            {
                ORNo = s.ORNumber,
                TransactionDate = s.TransactionDate,
                CashierName = s.CreatedBy,
                PaymentMethod = s.PaymentMethod,
                TotalAmount = s.TotalAmount
            }).ToListAsync();

            return Ok(dtoList);
        }

        // GET: api/SalesApi/bydaterange?fromDate=2025-09-01&toDate=2025-09-10
        [HttpGet("bydaterange")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<TransactionSalesDto>>> GetSalesByDateRange(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            var sales = from a in _context.POSTransactionHeaders
                        join b in _context.POSTransactionDetails on a.TransactionHeaderId equals b.TransactionHeaderId
                        where !a.IsDeleted
                              && !b.IsDeleted
                              && !a.IsVoided
                              && a.TransactionDate.Date >= fromDate.Date
                              && a.TransactionDate.Date <= toDate.Date
                        orderby a.TransactionDate descending
                        select new
                        {
                            a.ORNumber,
                            a.TotalAmount,
                            a.TransactionDate,
                            a.CreatedBy,
                            a.PaymentMethod
                        };

            var dtoList = await sales.Select(s => new TransactionSalesDto
            {
                ORNo = s.ORNumber,
                TransactionDate = s.TransactionDate,
                CashierName = s.CreatedBy,
                PaymentMethod = s.PaymentMethod,
                TotalAmount = s.TotalAmount
            }).ToListAsync();

            return Ok(dtoList);
        }

        // GET: api/SalesApi/or/OR12345
        [HttpGet("or/{orNumber}")]
        [Authorize]
        public async Task<ActionResult<TransactionSalesDto>> GetSaleByORNumber(string orNumber)
        {
            var sale = await (from a in _context.POSTransactionHeaders
                              join b in _context.POSTransactionDetails on a.TransactionHeaderId equals b.TransactionHeaderId
                              where !a.IsDeleted
                                    && !b.IsDeleted
                                    && !a.IsVoided
                                    && a.ORNumber == orNumber
                              select new TransactionSalesDto
                              {
                                  ORNo = a.ORNumber,
                                  TransactionDate = a.TransactionDate,
                                  CashierName = a.CreatedBy,
                                  PaymentMethod = a.PaymentMethod,
                                  TotalAmount = a.TotalAmount
                              }).FirstOrDefaultAsync();

            if (sale == null)
                return NotFound($"Sale with OR Number '{orNumber}' not found.");

            return Ok(sale);
        }


    }
}
