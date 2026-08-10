using CookieLearning.AuthorizationServer.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace CookieLearning.AuthorizationServer.Tests;

public sealed class CookieDiagnosticsTests
{
    [Fact]
    public async Task Middleware_records_attributes_without_cookie_value()
    {
        var events = new DiagnosticEventStore();
        var middleware = new CookieDiagnosticsMiddleware(
            context =>
            {
                context.Response.Cookies.Append("sample", "sensitive-value", new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

                return Task.CompletedTask;
            },
            events,
            new DevelopmentEnvironment());
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        var diagnostic = Assert.Single(events.GetEvents());
        Assert.Equal("sample", diagnostic.Details["name"]);
        Assert.Equal("True", diagnostic.Details["httpOnly"]);
        Assert.DoesNotContain("sensitive-value", string.Join(" ", diagnostic.Details.Values));
    }

    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = nameof(CookieDiagnosticsTests);
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
