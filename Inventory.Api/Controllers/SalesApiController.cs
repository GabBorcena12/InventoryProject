using Inventory.Api.Dtos;
using Inventory.API.Dtos;
using InventoryApp.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Controllers
{
    [ApiController]
    [Authorize(AuthenticationSchemes = "Auth0")]
    [Authorize(AuthenticationSchemes = "LocalJwt")]
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
        [HttpGet("GetAllSales")]
        public async Task<ActionResult<IEnumerable<TransactionSalesDto>>> GetAllSales(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20) // defaults if not provided
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;

            var salesQuery = from a in _context.POSTransactionHeaders
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

            // Total count for metadata
            var totalRecords = await salesQuery.CountAsync();

            // Apply pagination
            var pagedSales = await salesQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new TransactionSalesDto
                {
                    ORNo = s.ORNumber,
                    TransactionDate = s.TransactionDate,
                    CashierName = s.CreatedBy,
                    PaymentMethod = s.PaymentMethod,
                    TotalAmount = s.TotalAmount
                })
                .ToListAsync();

            // Optional: return metadata along with data
            var response = new
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = pagedSales
            };

            return Ok(response);
        }
        // GET: api/SalesApi/GetSalesByDateRange
        [HttpGet("GetSalesByDateRange")]
        public async Task<ActionResult<IEnumerable<TransactionSalesDto>>> GetSalesByDateRange(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20) // defaults if not provided
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;

            var salesQuery = from a in _context.POSTransactionHeaders
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

            // Total count for metadata
            var totalRecords = await salesQuery.CountAsync();

            // Apply pagination
            var pagedSales = await salesQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new TransactionSalesDto
                {
                    ORNo = s.ORNumber,
                    TransactionDate = s.TransactionDate,
                    CashierName = s.CreatedBy,
                    PaymentMethod = s.PaymentMethod,
                    TotalAmount = s.TotalAmount
                })
                .ToListAsync();

            // Optional: return metadata along with data
            var response = new
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = pagedSales
            };

            return Ok(response);
        }


        // GET: api/SalesApi/or/OR12345
        [HttpGet("or/{orNumber}")]
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
