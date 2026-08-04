# BFF Server-Side Authentication Guide

This document explains the current Microsoft Entra ID authentication design for LearningApp after moving from browser-token SPA auth to a BFF-style server-side auth flow.

BFF means Backend for Frontend. In this project, the Blazor WebAssembly app still runs in the browser, but it no longer talks directly to Microsoft Entra ID for access tokens and no longer sends bearer tokens directly to `LearningApp.Api`. Instead, the UI server signs the user in, keeps the authentication session on the server side of the application boundary, and proxies API calls.

## Current Project Shape

| Project | Role |
| --- | --- |
| `LearningApp.UI.Client` | Blazor WebAssembly browser UI. It asks the BFF server who the current user is and calls `/bff/api/...` endpoints. |
| `LearningApp.UI.Server` | Hosts the Blazor WASM files, performs OpenID Connect login/logout, owns the auth cookie, stores tokens in the protected auth ticket, and proxies API calls. |
| `LearningApp.UI.Shared` | Shared DTOs used by client and server, including the BFF user DTO. |
| `LearningApp.Api` | Protected ASP.NET Core Web API. It still validates JWT bearer access tokens from Microsoft Entra ID. |
| Microsoft Entra ID | Identity provider. It signs users in and issues tokens. |

## Current IDs And URLs

| Purpose | Value |
| --- | --- |
| UI app/client ID | `71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462` |
| API app/client ID | `2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| API Application ID URI | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| Delegated API scope | `access_as_user` |
| Full API scope | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user` |
| Local API base URL | `http://localhost:5013/` |
| Local UI HTTP URL | `http://localhost:5068` |
| Local UI HTTPS URL | `https://localhost:7024` |
| BFF login endpoint | `/bff/login` |
| BFF logout endpoint | `/bff/logout` |
| BFF user endpoint | `/bff/user` |
| BFF API proxy prefix | `/bff/api/...` |
| OpenID Connect callback | `/signin-oidc` |
| OpenID Connect logout callback | `/signout-callback-oidc` |

## Required Azure Portal Configuration

This BFF flow uses a confidential web app configuration. That is different from the old SPA-only setup.

### 1. UI App Registration

Open Microsoft Entra ID -> App registrations -> the UI app registration.

Use the app registration with this client ID:

```text
71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462
```

In Authentication, add a Web platform if it does not already exist.

Add these Web redirect URIs for local development:

```text
http://localhost:5068/signin-oidc
http://localhost:5068/signout-callback-oidc
```

If you run the UI server through HTTPS, also add:

```text
https://localhost:7024/signin-oidc
https://localhost:7024/signout-callback-oidc
```

The old SPA redirect URIs such as `/authentication/login-callback` and `/authentication/logout-callback` are no longer used by the BFF flow. They can remain during transition, but the BFF flow depends on the Web redirect URIs above.

### 2. Supported Account Types

The code currently uses this authority:

```text
https://login.microsoftonline.com/consumers/v2.0
```

That means the app is intended for personal Microsoft accounts using the Microsoft identity platform v2.0 endpoint. In the app registration, the supported account type must allow personal Microsoft accounts.

If the app should support work/school accounts instead, the authority and Azure app registration account type must both be changed together. Do not change only one side.

### 3. Client Secret

Because the UI server is now a confidential web app, it needs a client secret.

In the UI app registration:

1. Go to Certificates & secrets.
2. Create a new client secret.
3. Copy the secret value immediately.
4. Store it outside source control.

For local development, use .NET user secrets. The server project already has a `UserSecretsId`, so setting the secret is enough:

```powershell
dotnet user-secrets set "AzureAd:ClientSecret" "<secret-value>" --project LearningApp.UI\LearningApp.UI.Server\LearningApp.UI.Server.csproj
```

Do not put the real secret in `appsettings.json`.

For deployed environments, store the value in the hosting platform secret store or environment variables, for example:

```text
AzureAd__ClientSecret=<secret-value>
```

### 4. API App Registration

Open the API app registration with this app/client ID:

```text
2664ec3d-e5b6-4a59-a133-429d815b1b77
```

In Expose an API, confirm:

| Setting | Value |
| --- | --- |
| Application ID URI | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| Delegated scope name | `access_as_user` |
| Full scope | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user` |

The API app registration represents `LearningApp.Api`. It tells Microsoft Entra ID which access token audience and scopes are valid for the API.

### 5. API Permission On UI App

In the UI app registration, go to API permissions.

Add permission:

```text
LearningApp.Api -> Delegated permissions -> access_as_user
```

For a single-user personal account setup, consent can happen interactively at login. In organizational tenants, an admin may need to grant consent.

## Required Code Configuration

### UI Server Settings

`LearningApp.UI.Server/appsettings.json` and `appsettings.Development.json` contain non-secret auth settings:

```json
{
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/consumers/v2.0",
    "ClientId": "71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462",
    "ClientSecret": "",
    "ApiScope": "api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "Api": {
    "BaseUrl": "http://localhost:5013/"
  }
}
```

`ClientSecret` is intentionally empty in source-controlled settings. Use user secrets or environment variables.

### UI Server Authentication

`LearningApp.UI.Server/Program.cs` configures two auth pieces:

| Piece | Purpose |
| --- | --- |
| Cookie auth | Keeps the user's logged-in session between browser requests. |
| OpenID Connect | Redirects the user to Microsoft login and receives tokens after login. |

Important behavior:

- Default auth scheme is the cookie.
- Default challenge scheme is OpenID Connect.
- `ResponseType = code` uses the authorization code flow.
- `SaveTokens = true` allows the BFF proxy to retrieve the access token when calling the API.
- `GetClaimsFromUserInfoEndpoint = false` is intentional. The access token requested here is for `LearningApp.Api`, so it should not be sent to Microsoft's UserInfo endpoint.
- The requested API scope is `AzureAd:ApiScope`.

### BFF Auth Controller

`LearningApp.UI.Server/Controllers/BffAuthController.cs` exposes:

| Endpoint | Purpose |
| --- | --- |
| `GET /bff/login` | Starts Microsoft login. |
| `GET /bff/logout` | Signs out locally and at Microsoft. |
| `GET /bff/user` | Returns the current user's browser-safe auth state. |

The login endpoint accepts `returnUrl`, but only local URLs are allowed. That prevents open redirect attacks.

### BFF API Proxy Controller

`LearningApp.UI.Server/Controllers/BffApiProxyController.cs` exposes:

```text
/bff/api/{**path}
```

For example:

```text
Browser calls: /bff/api/categories
BFF calls:    http://localhost:5013/api/categories
```

The browser sends only the BFF cookie. The server retrieves the saved access token and sends this to `LearningApp.Api`:

```text
Authorization: Bearer <access-token>
```

### Blazor WASM Client

`LearningApp.UI.Client` no longer uses MSAL or browser token storage.

Important changes:

| File | Change |
| --- | --- |
| `Program.cs` | Registers normal `HttpClient` with app base address. Registers `BffAuthenticationStateProvider`. |
| `Auth/BffAuthenticationStateProvider.cs` | Calls `/bff/user` and converts the response into Blazor claims. |
| `Services/ApiClient.cs` | Rewrites API calls through `/bff/...`. |
| `Pages/Login.razor` | Redirects to `/bff/login`. |
| `Layout/NavMenu.razor` | Redirects to `/bff/logout`. |
| `Pages/Authentication.razor` | Legacy compatibility page that redirects old auth routes to BFF endpoints. |
| `wwwroot/appsettings.json` | Empty because the browser no longer needs Entra IDs/scopes. |
| `wwwroot/index.html` | MSAL JavaScript script removed. |

## Login Flow

This is the full login flow in simple terms:

1. User opens the Blazor app.
2. Blazor asks `/bff/user` whether the user is authenticated.
3. If not authenticated, the user clicks login or reaches a protected route.
4. Browser navigates to `/bff/login`.
5. `LearningApp.UI.Server` creates an OpenID Connect challenge.
6. Browser is redirected to Microsoft Entra ID.
7. User signs in with Microsoft.
8. Microsoft redirects back to `/signin-oidc` on `LearningApp.UI.Server`.
9. The UI server validates the login response.
10. The UI server requests tokens from Microsoft using the authorization code and client secret.
11. The UI server creates an auth cookie for the browser.
12. Browser returns to the original page.
13. Blazor calls `/bff/user` again and receives authenticated user info.
14. Dashboard and protected UI can render.

The key difference from SPA auth is step 10. The token exchange happens on the server, not in browser JavaScript.

## API Call Flow

This is the flow for loading dashboard data:

1. Dashboard asks `CategoryApiService` for categories.
2. `ApiClient` rewrites `api/categories` to `/bff/api/categories`.
3. Browser sends a same-origin request to the UI server.
4. Browser includes the `LearningApp.Bff` auth cookie automatically.
5. `BffApiProxyController` checks `[Authorize]`.
6. The controller reads the saved access token with `HttpContext.GetTokenAsync("access_token")`.
7. The controller calls `http://localhost:5013/api/categories`.
8. The controller adds `Authorization: Bearer <access-token>`.
9. `LearningApp.Api` validates the JWT bearer token.
10. API returns JSON to the BFF proxy.
11. BFF proxy returns the JSON to Blazor.
12. Blazor renders the dashboard.

## Logout Flow

1. User clicks logout.
2. Browser navigates to `/bff/logout`.
3. UI server clears the local auth cookie.
4. UI server starts Microsoft OpenID Connect sign-out.
5. Microsoft redirects back to `/signout-callback-oidc`.
6. User lands on `/login`.
7. `/bff/user` returns anonymous state.

## Important Concepts For Beginners

### Authentication

Authentication answers: who is the user?

Microsoft login proves the user's identity. After login, the app knows the user name and claims.

### Authorization

Authorization answers: what is the user allowed to do?

In this app:

- The UI uses Blazor authorization state to decide what to show.
- The BFF uses `[Authorize]` before proxying API calls.
- The API validates JWT bearer tokens before returning protected data.

### OpenID Connect

OpenID Connect is the login protocol built on OAuth 2.0.

In this app, it is used between `LearningApp.UI.Server` and Microsoft Entra ID.

### OAuth 2.0 Access Token

An access token is used to call an API.

In this app, the access token is meant for `LearningApp.Api`, not for the Blazor UI. The API checks the token audience and scope before trusting it.

### ID Token

An ID token describes the signed-in user. It is for the client app that performed login.

The API should not use the ID token as an API access token.

### Auth Cookie

The auth cookie is the browser's session key for the BFF.

In this app:

- The cookie is `HttpOnly`, so JavaScript cannot read it.
- In production, the cookie should be `Secure`, so it is sent only over HTTPS.
- `SameSite=Lax` helps reduce cross-site request forgery risk for normal navigation.

### Token Storage In This Implementation

The browser no longer stores access tokens in `localStorage` or `sessionStorage`.

With `SaveTokens = true`, ASP.NET Core stores tokens inside the protected authentication ticket. With the default cookie ticket store, that protected ticket is encrypted and signed using ASP.NET Core Data Protection and sent as an `HttpOnly` cookie.

That means:

- Browser JavaScript cannot read the access token.
- A person using DevTools cannot directly see the raw token in storage.
- The encrypted cookie may still contain the protected ticket.
- If a browser is fully compromised, the active session can still be abused.

For higher-security production BFF designs, store tokens in a server-side distributed token cache instead of inside the cookie ticket. Then the browser cookie contains only a session reference, and the access/refresh tokens stay entirely server-side.

### Why BFF Is Safer Than SPA Token Storage

Old SPA flow:

```text
Browser JavaScript obtains access token -> browser stores token -> browser sends bearer token to API
```

BFF flow:

```text
Server obtains access token -> browser receives HttpOnly cookie -> server sends bearer token to API
```

This reduces the impact of XSS because injected JavaScript cannot simply read the access token. It does not remove every browser security risk. If malicious JavaScript runs on the page, it may still call same-origin BFF endpoints while the user is logged in. That is why content security policy, dependency hygiene, antiforgery for state-changing operations, and careful input handling still matter.

## Issues Encountered Earlier And How They Were Fixed

### `AuthenticationService` Was Undefined

Old error:

```text
Could not find 'AuthenticationService.init'
```

Cause:

The Blazor WASM app was configured for MSAL auth, but the MSAL JavaScript file was missing from `index.html`.

SPA fix:

The MSAL script was added.

BFF result:

MSAL browser auth is no longer used. The script has been removed because login is now handled by `LearningApp.UI.Server`.

### Invalid Scope

Old error:

```text
The scope 'api://.../access_as_user openid profile offline_access' does not exist.
```

Cause:

The UI app was requesting a scope for the wrong API app registration, or the API app registration did not expose the exact scope.

Fix:

The API Application ID URI and delegated scope were corrected:

```text
api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user
```

BFF result:

The same API scope is still required, but the UI server requests it instead of browser JavaScript.

### Token Request 400 From Microsoft

Old error:

```text
https://login.microsoftonline.com/consumers/oauth2/v2.0/token returned 400
```

Cause:

In the SPA setup, this usually pointed to redirect URI, scope, client ID, or authority mismatch.

BFF result:

The token request now happens on `LearningApp.UI.Server`. If it fails, check:

- Web redirect URI exactly matches `/signin-oidc`.
- Client secret is present and not expired.
- Authority matches supported account types.
- API scope exists and is permitted.

### API Returned 401

Old error:

```text
GET http://localhost:5013/api/categories 401 Unauthorized
```

Cause:

The API rejected the access token. The `WWW-Authenticate` header showed the useful reason.

Fix:

The API configuration was updated to accept the correct audience values for the API app registration.

BFF result:

The API still validates JWT bearer tokens exactly like before. The difference is that the BFF server sends the bearer token to the API.

### Invalid Audience

Old header:

```text
Bearer error="invalid_token", error_description="The audience '2664ec3d-e5b6-4a59-a133-429d815b1b77' is invalid"
```

Cause:

Microsoft Entra ID can emit the API audience as either the API client ID or the Application ID URI, depending on configuration and token version. The API was only accepting one form.

Fix:

`LearningApp.Api` was configured with valid audiences that include both:

```text
2664ec3d-e5b6-4a59-a133-429d815b1b77
api://2664ec3d-e5b6-4a59-a133-429d815b1b77
```

### Empty JSON Parse Error After 401

Old error:

```text
The input does not contain any JSON tokens.
```

Cause:

The UI tried to parse a failed 401 response as JSON.

Fix:

`ApiClient` now checks HTTP status before reading JSON and throws a clearer error for failed responses.

## Local Development Checklist

1. Make sure `LearningApp.Api` runs on `http://localhost:5013/`.
2. Make sure `LearningApp.UI.Server` runs on the same UI URL registered in Azure.
3. Add Web redirect URI in Azure:

```text
http://localhost:5068/signin-oidc
http://localhost:5068/signout-callback-oidc
```

4. Store the client secret:

```powershell
dotnet user-secrets set "AzureAd:ClientSecret" "<secret-value>" --project LearningApp.UI\LearningApp.UI.Server\LearningApp.UI.Server.csproj
```

5. Run the API.
6. Run the UI server.
7. Browse to the UI app.
8. Click login.
9. After login, check DevTools:

- Application storage should not contain raw access tokens created by this app.
- Requests to `/bff/api/...` should use cookies to the BFF.
- Requests from BFF to `LearningApp.Api` are server-to-server and not visible as browser bearer-token API calls.

## Production Notes

For production, do these before release:

- Use HTTPS only.
- Use `CookieSecurePolicy.Always`.
- Store the client secret in a secure secret store.
- Consider certificate credentials instead of client secrets.
- Configure a durable ASP.NET Core Data Protection key ring.
- Consider a server-side token cache instead of storing saved tokens in the cookie auth ticket.
- Add antiforgery protection for state-changing BFF endpoints.
- Add a content security policy.
- Remove old SPA redirect URIs when the migration is complete.
- Configure exact production redirect URIs:

```text
https://<production-host>/signin-oidc
https://<production-host>/signout-callback-oidc
```

## Quick Mental Model

Remember the BFF split like this:

```text
Browser:
  Has UI files and an HttpOnly app cookie.
  Does not handle Microsoft access tokens.

UI Server / BFF:
  Handles login/logout.
  Holds the authenticated session.
  Gets access tokens.
  Proxies API requests.

API:
  Trusts only valid JWT bearer tokens for its audience and scope.
```

That is the current authentication architecture of this project.
