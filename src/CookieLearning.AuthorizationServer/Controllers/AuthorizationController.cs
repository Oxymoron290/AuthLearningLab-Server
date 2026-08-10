using System.Security.Claims;
using CookieLearning.AuthorizationServer.Diagnostics;
using CookieLearning.AuthorizationServer.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CookieLearning.AuthorizationServer.Controllers;

public sealed class AuthorizationController(
    IOpenIddictApplicationManager applications,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signInManager,
    DiagnosticEventStore events) : Controller
{
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect authorization request is unavailable.");

        events.Add("protocol", "authorization-request", HttpContext.TraceIdentifier, new Dictionary<string, string?>
        {
            ["clientId"] = request.ClientId,
            ["responseType"] = request.ResponseType,
            ["responseMode"] = request.ResponseMode,
            ["scope"] = request.Scope
        });

        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded)
        {
            if (request.HasPromptValue("none"))
            {
                return Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                    }),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                },
                IdentityConstants.ApplicationScheme);
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("The requesting client no longer exists.");

        return View("Authorize", new AuthorizationViewModel(
            await applications.GetLocalizedDisplayNameAsync(application) ?? request.ClientId!,
            request.ClientId!,
            request.Scope ?? string.Empty,
            authentication.Principal?.Identity?.Name));
    }

    [HttpPost("~/connect/authorize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuthorizeDecision(string decision)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect authorization request is unavailable.");

        if (!string.Equals(decision, "accept", StringComparison.Ordinal))
        {
            events.Add("protocol", "authorization-denied", HttpContext.TraceIdentifier, new Dictionary<string, string?>
            {
                ["clientId"] = request.ClientId
            });

            return Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The authorization request was denied."
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var user = await users.GetUserAsync(User)
            ?? throw new InvalidOperationException("The signed-in user no longer exists.");
        var principal = await signInManager.CreateUserPrincipalAsync(user);

        principal.SetClaim(Claims.Subject, await users.GetUserIdAsync(user));
        principal.SetScopes(request.GetScopes());
        principal.SetDestinations(GetDestinations);

        events.Add("protocol", "authorization-approved", HttpContext.TraceIdentifier, new Dictionary<string, string?>
        {
            ["clientId"] = request.ClientId,
            ["responseType"] = request.ResponseType,
            ["scope"] = request.Scope
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect token request is unavailable.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("Only authorization code and refresh token grants are supported.");
        }

        var authentication = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = authentication.Principal
            ?? throw new InvalidOperationException("The token request principal is unavailable.");

        var userId = principal.GetClaim(Claims.Subject)
            ?? throw new InvalidOperationException("The subject claim is unavailable.");
        var user = await users.FindByIdAsync(userId);
        if (user is null || !await signInManager.CanSignInAsync(user))
        {
            return Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        principal.SetClaim(Claims.Name, await users.GetUserNameAsync(user));
        principal.SetClaim(Claims.Email, await users.GetEmailAsync(user));
        principal.SetDestinations(GetDestinations);

        events.Add("protocol", "token-issued", HttpContext.TraceIdentifier, new Dictionary<string, string?>
        {
            ["clientId"] = request.ClientId,
            ["grantType"] = request.GrantType,
            ["scope"] = request.Scope
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo")]
    public async Task<IActionResult> UserInfo()
    {
        var authentication = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var principal = authentication.Principal;
        return Ok(new Dictionary<string, object?>
        {
            [Claims.Subject] = principal.GetClaim(Claims.Subject),
            [Claims.Name] = principal.GetClaim(Claims.Name),
            [Claims.Email] = principal.GetClaim(Claims.Email)
        });
    }

    [HttpGet("~/connect/logout"), HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public IActionResult Logout()
    {
        events.Add("protocol", "logout", HttpContext.TraceIdentifier, new Dictionary<string, string?>
        {
            ["authenticatedUser"] = User.Identity?.Name
        });

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            IdentityConstants.ApplicationScheme,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name when claim.Subject?.HasScope(Scopes.Profile) is true =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when claim.Subject?.HasScope(Scopes.Email) is true =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            "AspNet.Identity.SecurityStamp" => [],
            _ => [Destinations.AccessToken]
        };
    }
}
