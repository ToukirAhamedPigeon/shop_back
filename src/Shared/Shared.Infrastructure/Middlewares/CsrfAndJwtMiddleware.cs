using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Middlewares
{
    public class CsrfAndJwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAntiforgery _antiforgery;

        public CsrfAndJwtMiddleware(RequestDelegate next, IAntiforgery antiforgery)
        {
            _next = next;
            _antiforgery = antiforgery;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            var hasJwt = !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ");

            // Only check CSRF if no JWT token exists AND it's an unsafe method
            if (
                !hasJwt &&
                (HttpMethods.IsPost(context.Request.Method) ||
                HttpMethods.IsPut(context.Request.Method) ||
                HttpMethods.IsDelete(context.Request.Method) ||
                HttpMethods.IsPatch(context.Request.Method)))
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch (Exception ex)
                {
                    // Console.WriteLine("CSRF validation failed!");
                    // Console.WriteLine($"Message: {ex.Message}");
                    // Console.WriteLine($"StackTrace: {ex.StackTrace}");

                    foreach (var header in context.Request.Headers)
                        // Console.WriteLine($"Header: {header.Key} = {header.Value}");

                    foreach (var cookie in context.Request.Cookies)
                        // Console.WriteLine($"Cookie: {cookie.Key} = {cookie.Value}");

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("CSRF validation failed.");
                    return;
                }
            }

            // For authenticated endpoints, check JWT if needed
            if (!context.Request.Path.StartsWithSegments("/api/csrf/token") &&
                !context.Request.Path.StartsWithSegments("/api/translations/get") &&
                !context.Request.Path.StartsWithSegments("/api/auth/login") &&
                !context.Request.Path.StartsWithSegments("/api/auth/refresh") &&
                !hasJwt)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized.");
                return;
            }

            await _next(context);
        }
    }
}
