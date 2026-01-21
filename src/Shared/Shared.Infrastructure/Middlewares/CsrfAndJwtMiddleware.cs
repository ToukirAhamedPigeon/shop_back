using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
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
            // Check if Authorization header has Bearer token
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            var hasJwt = !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ");

            // Only validate CSRF if no JWT AND HTTP method is unsafe
            if (!hasJwt &&
                (HttpMethods.IsPost(context.Request.Method) ||
                 HttpMethods.IsPut(context.Request.Method) ||
                 HttpMethods.IsDelete(context.Request.Method) ||
                 HttpMethods.IsPatch(context.Request.Method)))
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("CSRF validation failed.");
                    return;
                }
            }

            // Define public endpoints that do NOT require JWT
            var path = context.Request.Path;
            var isPublicEndpoint =
                path.StartsWithSegments("/api/csrf/token") ||
                path.StartsWithSegments("/api/translations/get") ||
                path.StartsWithSegments("/swagger") ||
                path.StartsWithSegments("/api/auth/login") ||
                path.StartsWithSegments("/api/auth/password-reset") ||
                path.StartsWithSegments("/api/auth/refresh");

            // Return 401 if JWT missing for protected endpoints
            if (!isPublicEndpoint && !hasJwt)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized.");
                return;
            }

            await _next(context);
        }
    }
}
