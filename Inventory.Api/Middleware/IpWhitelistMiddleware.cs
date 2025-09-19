using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Inventory.Api.Middleware
{
    public class IpWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string[] _whitelistedIPs;

        public IpWhitelistMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _whitelistedIPs = config.GetSection("ApiSettings:WhitelistedIPs").Get<string[]>() ?? new string[0];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp != null && !_whitelistedIPs.Contains(remoteIp.ToString()))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync($"Your IP {remoteIp.ToString()} is not allowed.");
                return;
            }

            await _next(context);
        }
    }
}
