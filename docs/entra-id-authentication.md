# Microsoft Entra ID Authentication Guide

> Note: this document describes the previous Blazor WebAssembly SPA/MSAL browser-token flow. The current project has moved to BFF/server-side authentication. For the current architecture, Azure portal setup, code flow, and troubleshooting guide, see [bff-server-side-authentication.md](bff-server-side-authentication.md).

This document explains how LearningApp integrates Microsoft Entra ID authentication between:

- `LearningApp.UI.Client`: Blazor WebAssembly single-page app.
- `LearningApp.Api`: ASP.NET Core Web API protected by JWT bearer tokens.
- Microsoft Entra ID app registrations: one client app registration for the UI and one API app registration for the protected API.

It is written for future maintainers and AI agents. It includes required Azure configuration, required code wiring, login/logout flow, concepts, and the troubleshooting path used to fix the issues encountered during setup.

## Current IDs

The current local configuration uses two app registrations.

| Purpose | Value |
| --- | --- |
| UI SPA client app ID | `71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462` |
| API app ID | `2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| API Application ID URI | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| API delegated scope | `access_as_user` |
| Full API scope requested by UI | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user` |
| Local API URL | `http://localhost:5013/` |
| Local UI HTTP URL | `http://localhost:5068` |
| Local UI HTTPS URL | `https://localhost:7024` |

## Core Concepts

### App Registration

An app registration is the Microsoft Entra ID configuration record for an application. It tells Microsoft identity:

- Which app is requesting login.
- Which redirect URLs are trusted.
- Which account types can sign in.
- Which API permissions the app may request.
- Which custom API scopes exist.

### UI App Registration

The UI app registration represents the Blazor WebAssembly SPA. Its main job is to let the browser app sign users in and request access tokens.

In this project, the UI app registration client ID is configured in:

```json
{
  "AzureAd": {
    "ClientId": "71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462"
  }
}
```

File:

```text
LearningApp.UI/LearningApp.UI.Client/wwwroot/appsettings.json
```

### API App Registration

The API app registration represents `LearningApp.Api`. It exposes a delegated permission scope that client apps can request.

In this project, the API app registration client ID and audience are configured in:

```json
{
  "AzureAd": {
    "ClientId": "2664ec3d-e5b6-4a59-a133-429d815b1b77",
    "Audience": "api://2664ec3d-e5b6-4a59-a133-429d815b1b77"
  }
}
```

Files:

```text
LearningApp.Api/appsettings.json
LearningApp.Api/appsettings.Development.json
```

### Scope

A scope is a named delegated permission. The UI asks Microsoft identity for this scope when it needs to call the API as the signed-in user.

The full scope is:

```text
api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user
```

This is composed of:

```text
Application ID URI: api://2664ec3d-e5b6-4a59-a133-429d815b1b77
Scope name: access_as_user
```

### Access Token

An access token is a JWT issued by Microsoft identity. The UI sends it to the API in the HTTP `Authorization` header:

```text
Authorization: Bearer <token>
```

The API validates the token issuer, audience, lifetime, signature, and other claims before allowing the request.

### Audience

The audience, or `aud` claim, says which API the token is meant for. The API must reject tokens whose audience is not one of its expected values.

In this setup, Microsoft can issue the `aud` claim in either of these valid forms:

```text
2664ec3d-e5b6-4a59-a133-429d815b1b77
api://2664ec3d-e5b6-4a59-a133-429d815b1b77
```

The API code accepts both.

### Redirect URI

A redirect URI is where Microsoft sends the browser after login or logout. For Blazor WebAssembly remote auth, these callback routes are handled by `RemoteAuthenticatorView`.

Examples:

```text
http://localhost:5068/authentication/login-callback
http://localhost:5068/authentication/logout-callback
https://localhost:7024/authentication/login-callback
https://localhost:7024/authentication/logout-callback
```

These must be registered under the Single-page application platform in the UI app registration.

## Required Azure Configuration

### 1. UI SPA App Registration

Create or update the app registration used by the Blazor UI.

Required settings:

| Setting | Required value |
| --- | --- |
| Application/client ID | `71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462` |
| Platform type | Single-page application |
| Supported account types | Must match the authority in UI config |
| Redirect URI | `http://localhost:5068/authentication/login-callback` |
| Redirect URI | `http://localhost:5068/authentication/logout-callback` |
| Redirect URI, if using HTTPS profile | `https://localhost:7024/authentication/login-callback` |
| Redirect URI, if using HTTPS profile | `https://localhost:7024/authentication/logout-callback` |

The UI config currently uses:

```json
"Authority": "https://login.microsoftonline.com/consumers"
```

That means the app is configured for personal Microsoft accounts. The UI app registration must support personal Microsoft accounts. If using work/school accounts instead, change both the app registration and the `Authority`.

### 2. API App Registration

Create or update the app registration used by the API.

Required settings:

| Setting | Required value |
| --- | --- |
| Application/client ID | `2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| Application ID URI | `api://2664ec3d-e5b6-4a59-a133-429d815b1b77` |
| Exposed scope name | `access_as_user` |
| Scope type | Delegated |
| Scope state | Enabled |
| Who can consent | Admins and users, for local/dev simplicity |

Suggested scope text:

| Field | Suggested value |
| --- | --- |
| Admin consent display name | `Access LearningApp API` |
| Admin consent description | `Allows the app to access the LearningApp API as the signed-in user.` |
| User consent display name | `Access LearningApp API` |
| User consent description | `Allows this app to access the LearningApp API as you.` |

The final scope must be exactly:

```text
api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user
```

### 3. Add API Permission To The UI App

In the UI app registration:

1. Go to `API permissions`.
2. Select `Add a permission`.
3. Select `My APIs`.
4. Select the LearningApp API registration.
5. Select `Delegated permissions`.
6. Choose `access_as_user`.
7. Add the permission.
8. Grant consent when appropriate.

Without this, the UI may sign in successfully but fail when it tries to acquire an access token for the API.

### 4. CORS

The API must allow requests from the UI origin.

Configured in:

```text
LearningApp.Api/appsettings.json
```

```json
"Cors": {
  "AllowedOrigins": [
    "https://localhost:7024",
    "http://localhost:5068"
  ]
}
```

Used in:

```text
LearningApp.Api/Program.cs
```

```csharp
app.UseCors("LearningAppUi");
app.UseAuthentication();
app.UseAuthorization();
```

Middleware order matters: CORS must run before authentication/authorization for browser API calls to behave correctly.

## Required Code Changes

### UI: Packages

The Blazor WebAssembly client needs:

```xml
<PackageReference Include="Microsoft.Authentication.WebAssembly.Msal" Version="8.0.18" />
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" Version="8.0.18" />
```

These provide MSAL authentication and `RemoteAuthenticatorView`.

### UI: appsettings.json

File:

```text
LearningApp.UI/LearningApp.UI.Client/wwwroot/appsettings.json
```

Required shape:

```json
{
  "ApiBaseUrl": "http://localhost:5013/",
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/consumers",
    "ClientId": "71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462",
    "ValidateAuthority": true,
    "ApiScope": "api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user"
  }
}
```

Notes:

- `ClientId` is the UI app registration.
- `ApiScope` is the API app registration scope.
- Do not confuse UI client ID and API client ID.

### UI: Load MSAL AuthenticationService.js

File:

```text
LearningApp.UI/LearningApp.UI.Client/wwwroot/index.html
```

Required scripts:

```html
<script src="_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js"></script>
<script src="_framework/blazor.webassembly.js"></script>
```

The MSAL script must load before Blazor starts. Without it, Blazor cannot find the JavaScript interop object `AuthenticationService`.

### UI: Register MSAL

File:

```text
LearningApp.UI/LearningApp.UI.Client/Program.cs
```

Required registration:

```csharp
builder.Services.AddAuthorizationCore();
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
});
```

Important: the project does not add the API scope to `DefaultAccessTokenScopes` during initial login. Login should only sign the user in. API access token acquisition happens when the API call is made through `AuthorizationMessageHandler`.

### UI: Add Authorized HttpClient Handler

File:

```text
LearningApp.UI/LearningApp.UI.Client/Auth/ApiAuthorizationMessageHandler.cs
```

Purpose:

- Attach bearer tokens to calls going to `ApiBaseUrl`.
- Request the configured `AzureAd:ApiScope` when needed.

Key code:

```csharp
ConfigureHandler(
    authorizedUrls: new[] { apiBaseUrl },
    scopes: new[] { apiScope });
```

Registered in:

```text
LearningApp.UI/LearningApp.UI.Client/Program.cs
```

```csharp
builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
builder.Services.AddHttpClient("LearningApi", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<ApiAuthorizationMessageHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("LearningApi"));
```

### UI: Login Page

File:

```text
LearningApp.UI/LearningApp.UI.Client/Pages/Login.razor
```

The login button calls:

```csharp
Navigation.NavigateToLogin("authentication/login", new InteractiveRequestOptions
{
    Interaction = InteractionType.SignIn,
    ReturnUrl = returnUrl
});
```

This starts the MSAL sign-in redirect flow.

### UI: Authentication Callback Page

File:

```text
LearningApp.UI/LearningApp.UI.Client/Pages/Authentication.razor
```

Required component:

```razor
@page "/authentication/{action}"
@attribute [AllowAnonymous]

<RemoteAuthenticatorView Action="@Action" />
```

`RemoteAuthenticatorView` handles:

- `/authentication/login`
- `/authentication/login-callback`
- `/authentication/logout`
- `/authentication/logout-callback`
- token callback state
- redirect result processing

### UI: Handle Missing Tokens And Non-JSON API Errors

File:

```text
LearningApp.UI/LearningApp.UI.Client/Services/ApiClient.cs
```

Required behavior:

- Catch `AccessTokenNotAvailableException`.
- Call `exception.Redirect()` so MSAL can request consent or a token.
- Do not parse empty `401` and `403` responses as JSON.

This prevents dashboard/component crashes when the API rejects a token or when additional consent is needed.

### API: Packages

The API needs:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.18" />
<PackageReference Include="Microsoft.Identity.Web" Version="..." />
```

`Microsoft.Identity.Web` provides `AddMicrosoftIdentityWebApi`.

### API: appsettings.json And appsettings.Development.json

Files:

```text
LearningApp.Api/appsettings.json
LearningApp.Api/appsettings.Development.json
```

Required shape:

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "consumers",
  "ClientId": "2664ec3d-e5b6-4a59-a133-429d815b1b77",
  "Audience": "api://2664ec3d-e5b6-4a59-a133-429d815b1b77"
}
```

For local development, keep `appsettings.Development.json` aligned with `appsettings.json`. Local `dotnet run` uses the Development environment by default, so stale values there will override correct base settings.

### API: Register JWT Bearer Validation

File:

```text
LearningApp.Api/Extensions/ServiceCollectionExtensions.cs
```

Required behavior:

```csharp
var azureAdSection = configuration.GetSection("AzureAd");
var azureAdClientId = azureAdSection["ClientId"];
var azureAdAudience = azureAdSection["Audience"];

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        azureAdSection.Bind(options);
        options.TokenValidationParameters.ValidAudiences = new[]
        {
            azureAdAudience,
            azureAdClientId
        }.Where(value => !string.IsNullOrWhiteSpace(value));
    }, options => azureAdSection.Bind(options));
```

Why both audiences are accepted:

- Some tokens may have `aud` as `api://<api-client-id>`.
- Some tokens may have `aud` as `<api-client-id>`.
- Both refer to the same API registration in this app.

### API: Enable Auth Middleware

File:

```text
LearningApp.Api/Program.cs
```

Required order:

```csharp
app.UseCors("LearningAppUi");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

### API: Protect Controllers

Controllers use `[Authorize]` or authorization policies:

```csharp
[Authorize(Policy = "LearnerOrAdmin")]
```

Policies are currently simple authenticated-user checks:

```csharp
options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser());
options.AddPolicy("LearnerOrAdmin", policy => policy.RequireAuthenticatedUser());
```

If role-based authorization is needed later, add role claims to tokens or map app roles/groups from Entra ID.

## Login Flow

This is the end-to-end login flow in plain terms.

1. User opens the Blazor UI.
2. User clicks `Sign in with Microsoft`.
3. `Login.razor` calls `NavigateToLogin("authentication/login", ...)`.
4. Blazor routes to `Authentication.razor`.
5. `RemoteAuthenticatorView` asks MSAL to start login.
6. Browser is redirected to Microsoft identity.
7. User signs in with a Microsoft account.
8. Microsoft redirects browser back to:

```text
http://localhost:5068/authentication/login-callback
```

9. `RemoteAuthenticatorView` processes the response.
10. MSAL stores authentication state in browser storage.
11. Blazor marks the user authenticated.
12. The app navigates back to the original return URL, usually `/`.
13. Dashboard renders.

At this point, login is complete, but the UI may not yet have an API access token.

## API Call Flow

This is what happens when dashboard loads data.

1. Dashboard calls `CategoryApiService.GetAllAsync()`.
2. The service calls `ApiClient.GetAsync("api/categories")`.
3. The named HttpClient uses `ApiAuthorizationMessageHandler`.
4. The handler sees the request is going to the configured `ApiBaseUrl`.
5. The handler asks MSAL for an access token for:

```text
api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user
```

6. If MSAL has a valid token, it adds:

```text
Authorization: Bearer <access-token>
```

7. API receives `GET /api/categories`.
8. API validates the JWT:

- signature
- issuer
- lifetime
- audience
- required authorization policy

9. If valid, the controller runs and returns JSON.
10. UI parses the `ApiResponse<T>` and renders data.

## Logout Flow

1. User clicks logout.
2. Blazor/MSAL starts logout through the authentication route.
3. Browser redirects to Microsoft logout endpoint.
4. Microsoft clears the server-side sign-in session for that app.
5. Browser redirects back to:

```text
http://localhost:5068/authentication/logout-callback
```

6. `RemoteAuthenticatorView` completes local logout state cleanup.
7. UI returns to an unauthenticated state.

The logout callback URI is normal and should be registered as a SPA redirect URI.

## Troubleshooting History

### 1. `AuthenticationService` Was Undefined

Symptom:

```text
Could not find 'AuthenticationService.init' ('AuthenticationService' was undefined)
```

Cause:

The Blazor MSAL JavaScript asset was not loaded before Blazor WebAssembly started.

Fix:

Add this before `_framework/blazor.webassembly.js` in `index.html`:

```html
<script src="_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js"></script>
```

Concept:

Blazor authentication uses JavaScript interop. The .NET authentication code calls browser-side MSAL functions through `window.AuthenticationService`. If the script is missing, the .NET code cannot initialize auth.

### 2. Scope Did Not Exist

Symptom:

```text
The provided value for the input parameter 'scope' is not valid.
The scope 'api://.../access_as_user openid profile offline_access' does not exist.
```

Cause:

The UI requested a custom API scope that was not present in the API app registration, or the wrong app ID was used.

Fix:

In the API app registration:

- Set Application ID URI.
- Add delegated scope `access_as_user`.
- Ensure the full scope exactly matches `AzureAd:ApiScope`.

Also ensure the UI is not using the API client ID as its own client ID unless intentionally using a single app registration.

Concept:

Microsoft identity knows built-in scopes like `openid`, `profile`, and `offline_access`, but custom API scopes must be explicitly exposed by an API app registration.

### 3. Login Error Message Was Empty

Symptom:

```text
There was an error trying to log you in: '
```

Cause:

After app ID and scope changes, MSAL/browser state can be stale. Another possible cause is redirect URI mismatch.

Fix:

- Clear browser site data for the local UI origin.
- Use Incognito/InPrivate.
- Restart UI.
- Verify SPA redirect URIs exactly match the local URL and port.

Concept:

MSAL stores state and cached auth records in browser storage. If app IDs, scopes, or redirect URIs change during development, old state can make the callback fail unclearly.

### 4. Token Endpoint Returned 400

Symptom:

```text
Failed to load resource: the server responded with a status of 400 (Bad Request)
https://login.microsoftonline.com/consumers/oauth2/v2.0/token

AccessTokenNotAvailableException:
'api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user'
```

Cause:

MSAL could not acquire an API access token for the requested scope.

Fixes checked:

- API scope exists and is enabled.
- UI app registration has delegated API permission to `access_as_user`.
- Redirect URIs are registered under SPA.
- Browser auth state was cleared.
- `ApiClient` catches `AccessTokenNotAvailableException` and calls `exception.Redirect()`.

Concept:

Signing in and acquiring an API token are related but separate. A user can be logged into the UI while the app still cannot get an access token for the API.

### 5. Dashboard Stayed On Loading

Symptom:

Dashboard showed:

```text
Loading dashboard
```

and console had an unhandled exception.

Cause:

`Dashboard.razor` did not handle exceptions from API calls and did not clear `isLoading` in a `finally` block.

Fix:

- Add an `ErrorAlert`.
- Wrap initialization API calls in `try/catch/finally`.
- Set `isLoading = false` in `finally`.

Concept:

Component lifecycle methods like `OnInitializedAsync` should handle expected API failures. Otherwise a failed task can break rendering and leave loading indicators active.

### 6. 401 Response Caused JSON Parse Error

Symptom:

```text
The input does not contain any JSON tokens.
Expected the input to start with a valid JSON token.
```

Cause:

The API returned `401 Unauthorized` with an empty or non-JSON body. The UI attempted to parse it as `ApiResponse<T>`.

Fix:

`ApiClient.ReadAsync` now checks `response.IsSuccessStatusCode` before JSON parsing and returns a structured failure for `401`, `403`, and other HTTP errors.

Concept:

Do not assume every HTTP response body is JSON. Authentication middleware often returns empty `401` or `403` responses.

### 7. Invalid Audience

Symptom from `WWW-Authenticate` response header:

```text
Bearer error="invalid_token",
error_description="The audience '2664ec3d-e5b6-4a59-a133-429d815b1b77' is invalid"
```

Cause:

The token was valid, but its `aud` claim was the API client ID:

```text
2664ec3d-e5b6-4a59-a133-429d815b1b77
```

The API was only configured to accept:

```text
api://2664ec3d-e5b6-4a59-a133-429d815b1b77
```

Fix:

Configure JWT validation to accept both forms:

```csharp
options.TokenValidationParameters.ValidAudiences = new[]
{
    azureAdAudience,
    azureAdClientId
}.Where(value => !string.IsNullOrWhiteSpace(value));
```

Concept:

The `aud` claim must match what the API accepts. Depending on app registration and scope setup, Microsoft identity may emit either the raw client ID or the Application ID URI form.

### 8. Stale Development Config

Symptom:

Azure and base `appsettings.json` looked correct, but local API still rejected tokens.

Cause:

`appsettings.Development.json` still had old app IDs. In local development, it overrides `appsettings.json`.

Fix:

Keep both files aligned:

```text
LearningApp.Api/appsettings.json
LearningApp.Api/appsettings.Development.json
```

Concept:

ASP.NET Core configuration is layered. Later providers override earlier providers. Environment-specific files override base appsettings.

## Debug Checklist For Future Agents

When auth fails, check in this order.

1. Confirm the UI app loads this script:

```text
_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js
```

2. Confirm UI appsettings:

```text
ClientId = UI app registration
ApiScope = API app registration scope
Authority = account type being used
```

3. Confirm API appsettings and Development appsettings:

```text
ClientId = API app registration
Audience = API Application ID URI
TenantId = matches account type
```

4. Confirm Azure API app registration:

```text
Application ID URI exists
access_as_user scope exists and is enabled
```

5. Confirm Azure UI app registration:

```text
SPA redirect URIs exactly match the browser URL
API permission includes delegated access_as_user
Supported account types match Authority
```

6. Clear browser storage or use Incognito after changing:

```text
ClientId
Authority
ApiScope
Redirect URI
Supported account types
```

7. For API `401`, inspect:

```text
DevTools -> Network -> api request -> Response Headers -> WWW-Authenticate
```

8. Decode the access token at `jwt.ms` during development and inspect:

```text
aud
iss
scp
tid
appid / azp
exp
```

Expected:

```text
scp contains access_as_user
aud is either 2664ec3d-e5b6-4a59-a133-429d815b1b77
or api://2664ec3d-e5b6-4a59-a133-429d815b1b77
```

9. Restart both API and UI after config changes.

10. Rebuild when code changes:

```powershell
dotnet build .\LearningApp.Api\LearningApp.Api.csproj
dotnet build .\LearningApp.UI\LearningApp.UI.Client\LearningApp.UI.Client.csproj
```

## Common Configuration Mistakes

| Mistake | Result | Fix |
| --- | --- | --- |
| UI `ClientId` points to API app | Login or token acquisition behaves unexpectedly | Use UI app ID in UI config |
| API `ClientId` points to UI app | API rejects valid API tokens | Use API app ID in API config |
| `appsettings.Development.json` has old values | Local run ignores correct base config | Update Development config |
| Scope missing in API app registration | Token endpoint returns invalid scope | Expose `access_as_user` |
| UI app lacks API permission | Access token acquisition fails | Add delegated API permission |
| Redirect URI registered as Web, not SPA | MSAL SPA callback fails | Add URI under Single-page application |
| Missing MSAL JS file | `AuthenticationService` undefined | Add script before Blazor |
| API only accepts one audience form | `invalid_token`, invalid audience | Accept both API URI and client ID |
| UI parses empty 401 as JSON | JSON token parse exception | Check status before parsing |

## Security Notes

- Do not store client secrets in the Blazor WebAssembly app. A SPA is a public client.
- Prefer HTTPS redirect URIs for non-local environments.
- Keep scopes least-privilege.
- For production, use tenant-specific authorities instead of broad authorities when possible.
- If roles are needed, implement Entra app roles or group claims and update authorization policies.
- Do not disable issuer or audience validation to make tokens pass. Fix the configuration instead.
