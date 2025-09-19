public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _apiKeys;
    private readonly string _apiClientName;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _apiKeys = config.GetSection("ApiSettings:Keys").Get<string[]>() ?? new string[0];
        _apiClientName = config.GetValue<string>("ApiSettings:ClientName") ?? "UnknownClient";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate API key only if header exists (optional)
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var extractedKey))
        {
            if (!_apiKeys.Contains(extractedKey.ToString()))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid API Key");
                return;
            }

            // Add X-Api-Client header
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Add("X-Api-Client", _apiClientName);
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}
