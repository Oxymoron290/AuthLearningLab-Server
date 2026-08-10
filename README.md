# CookieLearning authorization server

This subproject is the local OpenID Connect authorization server for the cookie-learning lab. It uses ASP.NET Core Identity for the server login session and OpenIddict for protocol behavior.

## Run

```powershell
Set-Location .\server
dotnet tool restore
dotnet run --project .\src\CookieLearning.AuthorizationServer --launch-profile https
```

Open `https://localhost:7001/`.

The first run creates and migrates `app.db`, seeds the development user, and registers both learning clients.

## Authentication credentials

These values are intentionally local and must not be reused outside this learning lab.

| Fixture | Value |
| --- | --- |
| Login email | `alice@example.test` |
| Password | `Passw0rd!` |
| PKCE client ID | `cookie-learning-pkce` |
| PKCE redirect URI | `https://localhost:7101/signin-oidc` |
| Katana client ID | `cookie-learning-katana` |
| Katana client secret | `development-only-secret` |
| Katana redirect URI | `https://localhost:44300/signin-oidc` |

Use the login email and password on the authorization server's login page. The Katana client ID and secret are application credentials used by a future OIDC client, not credentials entered by the user.

## Endpoints

| Purpose | URL |
| --- | --- |
| Discovery | `https://localhost:7001/.well-known/openid-configuration` |
| Authorization | `https://localhost:7001/connect/authorize` |
| Token | `https://localhost:7001/connect/token` |
| User info | `https://localhost:7001/connect/userinfo` |
| Logout | `https://localhost:7001/connect/logout` |
| Diagnostics | `https://localhost:7001/Diagnostics` |

## First exercises

1. Open the home page in a private browser window and inspect the browser cookie store. No login cookie should exist yet.
2. Select **Login**, sign in with the development user, and inspect the `__Host-CookieLearning.Server` cookie. This is the authorization server's ASP.NET Core Identity session cookie.
3. Open **Diagnostics** and compare the cookie's browser-visible attributes with the sanitized create/receive events.
4. Open the discovery endpoint and identify the supported authorization endpoints, response types, response modes, and `S256` PKCE support.
5. Select **Logout**, then observe the cookie deletion event and the browser cookie store.

The diagnostics page records cookie names and attributes, never cookie values, authorization codes, tokens, passwords, secrets, keys, or sensitive headers.

## Server cookies versus future Katana cookies

This project currently owns only the authorization-server login session. A future .NET Framework client will introduce separate cookies for:

- the client's own application session;
- OpenID Connect nonce validation;
- correlation/state handling;
- chunked cookie fragments when a ticket exceeds a single-cookie size.

Those client-side cookies are where Katana's default `ICookieManager`, `SystemWebCookieManager`, and chunking/System.Web interactions will be compared. Keeping the server in this standalone directory lets future clients run as sibling subprojects against an unchanged issuer.

## Tests

```powershell
Set-Location .\server
dotnet test .\CookieLearning.Server.slnx
```
