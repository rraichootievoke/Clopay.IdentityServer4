using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Clopay.IDPAdminUI.Core.Middleware
{
    /// <summary>
    /// Adds the standard security headers to every response. Headers are set inside OnStarting so they are written once,
    /// after the rest of the pipeline has produced the response.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        // The Admin screens use inline scripts and styles (jQuery/Knockout pages), so 'unsafe-inline' is still required.
        private const string ContentSecurityPolicy =
            "default-src 'self'; " +
            "img-src 'self' data:; " +
            "font-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "frame-ancestors 'self'; base-uri 'self'; form-action 'self'";

        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "SAMEORIGIN";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Content-Security-Policy"] = ContentSecurityPolicy;

                // Pages (not CSS/JS/images) must never be cached: they contain client details and one-time secrets.
                var contentType = context.Response.ContentType;
                if (contentType != null && contentType.StartsWith("text/html", System.StringComparison.OrdinalIgnoreCase) && !headers.ContainsKey("Cache-Control"))
                {
                    headers["Cache-Control"] = "no-store";
                }
                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}
