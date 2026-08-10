using CookieLearning.AuthorizationServer.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CookieLearning.AuthorizationServer.Pages;

public class IndexModel(ProviderSettings provider, IConfiguration configuration) : PageModel
{
    public string DisplayName => provider.DisplayName;
    public string Issuer => provider.Issuer;
    public string UserEmail => configuration["Seed:UserEmail"]
        ?? throw new InvalidOperationException("Seed:UserEmail is required.");

    public void OnGet()
    {
    }
}
