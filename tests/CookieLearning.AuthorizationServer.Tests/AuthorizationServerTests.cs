using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CookieLearning.AuthorizationServer.Tests;

public sealed class AuthorizationServerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationServerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Discovery_exposes_modern_and_legacy_capabilities()
    {
        var document = await _client.GetFromJsonAsync<DiscoveryDocument>("/.well-known/openid-configuration");

        Assert.NotNull(document);
        Assert.Equal("https://localhost:7001/", document.Issuer);
        Assert.Contains("code", document.ResponseTypesSupported);
        Assert.Contains("id_token", document.ResponseTypesSupported);
        Assert.Contains("code id_token", document.ResponseTypesSupported);
        Assert.Contains("S256", document.CodeChallengeMethodsSupported);
    }

    [Fact]
    public async Task Unknown_client_is_rejected_without_reaching_login()
    {
        var response = await _client.GetAsync(
            "/connect/authorize?client_id=unknown&redirect_uri=https%3A%2F%2Flocalhost%2Fcallback" +
            "&response_type=code&scope=openid&code_challenge=test&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pkce_client_requires_a_code_challenge()
    {
        var response = await _client.GetAsync(
            "/connect/authorize?client_id=cookie-learning-pkce" +
            "&redirect_uri=https%3A%2F%2Flocalhost%3A7101%2Fsignin-oidc" +
            "&response_type=code&scope=openid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_id_token_form_post_request_reaches_login()
    {
        var response = await _client.GetAsync(
            "/connect/authorize?client_id=cookie-learning-katana" +
            "&redirect_uri=https%3A%2F%2Flocalhost%3A44300%2Fsignin-oidc" +
            "&response_type=id_token&response_mode=form_post&scope=openid&nonce=test");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Identity/Account/Login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Login_and_diagnostics_reset_create_then_delete_server_cookie()
    {
        var loginPage = await _client.GetStringAsync("/Identity/Account/Login");
        var loginToken = GetAntiforgeryToken(loginPage);

        var login = await _client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "alice@workforce.example.test",
            ["Input.Password"] = "Passw0rd!",
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = loginToken
        }));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(
            login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-CookieLearning.Workforce=", StringComparison.Ordinal));

        var diagnosticsPage = await _client.GetStringAsync("/Diagnostics");
        var diagnosticsToken = GetAntiforgeryToken(diagnosticsPage);
        var reset = await _client.PostAsync("/Diagnostics", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = diagnosticsToken
        }));

        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Contains(
            reset.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-CookieLearning.Workforce=;", StringComparison.Ordinal));
    }

    private static string GetAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "The antiforgery token was not present in the response.");
        return HttpUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed record DiscoveryDocument(
        [property: JsonPropertyName("issuer")]
        string Issuer,
        [property: JsonPropertyName("response_types_supported")]
        string[] ResponseTypesSupported,
        [property: JsonPropertyName("code_challenge_methods_supported")]
        string[] CodeChallengeMethodsSupported);
}
