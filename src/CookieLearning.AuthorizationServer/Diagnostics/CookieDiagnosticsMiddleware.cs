using Microsoft.Net.Http.Headers;

namespace CookieLearning.AuthorizationServer.Diagnostics;

public sealed class CookieDiagnosticsMiddleware(
    RequestDelegate next,
    DiagnosticEventStore events,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!environment.IsDevelopment())
        {
            await next(context);
            return;
        }

        foreach (var name in context.Request.Cookies.Keys.Order())
        {
            events.Add("cookie", "received", context.TraceIdentifier, new Dictionary<string, string?>
            {
                ["name"] = name,
                ["path"] = context.Request.Path
            });
        }

        await next(context);

        foreach (var header in context.Response.Headers.SetCookie)
        {
            if (!string.IsNullOrEmpty(header))
            {
                RecordSetCookie(header);
            }
        }

        void RecordSetCookie(string header)
        {
            var parsed = SetCookieHeaderValue.Parse(header);
            var action = parsed.Expires <= DateTimeOffset.UnixEpoch || parsed.MaxAge <= TimeSpan.Zero
                ? "deleted"
                : "created";

            events.Add("cookie", action, context.TraceIdentifier, new Dictionary<string, string?>
            {
                ["name"] = parsed.Name.ToString(),
                ["path"] = parsed.Path.ToString(),
                ["domain"] = parsed.Domain.ToString(),
                ["secure"] = parsed.Secure.ToString(),
                ["httpOnly"] = parsed.HttpOnly.ToString(),
                ["sameSite"] = parsed.SameSite.ToString(),
                ["expires"] = parsed.Expires?.ToString("O"),
                ["maxAge"] = parsed.MaxAge?.ToString()
            });
        }
    }
}
