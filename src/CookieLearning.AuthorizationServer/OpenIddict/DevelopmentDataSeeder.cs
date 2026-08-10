using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CookieLearning.AuthorizationServer.OpenIddict;

public sealed class DevelopmentDataSeeder(
    UserManager<IdentityUser> users,
    IOpenIddictApplicationManager applications,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task SeedAsync()
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var email = configuration["Seed:UserEmail"]
            ?? throw new InvalidOperationException("Seed:UserEmail is required in Development.");
        var password = configuration["Seed:UserPassword"]
            ?? throw new InvalidOperationException("Seed:UserPassword is required in Development.");

        if (await users.FindByEmailAsync(email) is null)
        {
            var result = await users.CreateAsync(new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            }, password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to seed development user: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }

        await CreateClientIfMissingAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "cookie-learning-pkce",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = "Cookie Learning PKCE Client",
            RedirectUris =
            {
                new Uri("https://localhost:7101/signin-oidc")
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://localhost:7101/signout-callback-oidc")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        });

        await CreateClientIfMissingAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "cookie-learning-katana",
            ClientSecret = "development-only-secret",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = "Cookie Learning Katana Client",
            RedirectUris =
            {
                new Uri("https://localhost:44300/signin-oidc")
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://localhost:44300/signout-callback-oidc")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.Implicit,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.ResponseTypes.IdToken,
                Permissions.ResponseTypes.CodeIdToken,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles
            }
        });
    }

    private async Task CreateClientIfMissingAsync(OpenIddictApplicationDescriptor descriptor)
    {
        if (await applications.FindByClientIdAsync(descriptor.ClientId!) is null)
        {
            await applications.CreateAsync(descriptor);
        }
    }
}
