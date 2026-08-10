using CookieLearning.AuthorizationServer.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CookieLearning.AuthorizationServer.Pages;

public sealed class DiagnosticsModel(
    DiagnosticEventStore events,
    IHostEnvironment environment) : PageModel
{
    public IReadOnlyList<DiagnosticEvent> Events { get; private set; } = [];

    public IActionResult OnGet()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        Events = events.GetEvents();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        events.Clear();
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToPage();
    }
}
