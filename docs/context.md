# LearningApp Project Context

This document is a compact handoff for agents and developers working on LearningApp. It summarizes what the app does, how it is structured, how data moves through the system, and what deployment state is documented in this repository.

For deeper implementation notes, use these source documents:

- `docs/bff-server-side-authentication.md` for the current Microsoft Entra ID and BFF authentication design.
- `docs/entra-id-authentication.md` for Entra setup background and troubleshooting.
- `docs/global-error-response-structure.md` for API success/error response conventions.
- `.azure/deployment-plan.md` for Azure deployment status and pending decisions.

## What This App Is

LearningApp is an enterprise-style .NET 8 learning management CRUD application. It lets authenticated users manage and view:

- Users
- Learning categories
- Courses
- Course enrollments
- Learning progress
- A dashboard with aggregate counts and average progress

The project is intentionally structured as a realistic full-stack .NET app: ASP.NET Core Web API, hosted Blazor WebAssembly UI, SQL Server storage, Microsoft Entra ID authentication, health checks, logging, and Azure App Service deployment planning.

## Repository Layout

```text
LearningApp
|-- Database
|   |-- 001_create_schema.sql
|   `-- 002_seed_data.sql
|-- LearningApp.Api
|   |-- Authentication
|   |-- Configurations
|   |-- Controllers
|   |-- Data
|   |-- Entities
|   |-- Exceptions
|   |-- Extensions
|   |-- HealthChecks
|   |-- Middleware
|   |-- Repositories
|   `-- Services
|-- LearningApp.UI
|   |-- LearningApp.UI.Client
|   |-- LearningApp.UI.Server
|   `-- LearningApp.UI.Shared
|-- docs
`-- .azure
```

Important solution files:

- `LearningApp.Api.sln` includes `LearningApp.Api` and `LearningApp.UI.Shared`.
- `LearningApp.UI/LearningApp.UI.sln` includes `LearningApp.UI.Client`, `LearningApp.UI.Server`, and `LearningApp.UI.Shared`.

## Tech Stack

- .NET 8
- ASP.NET Core Web API
- Hosted Blazor WebAssembly
- ASP.NET Core BFF-style server with cookie + OpenID Connect authentication
- Microsoft Entra ID / Microsoft identity platform
- JWT bearer validation with `Microsoft.Identity.Web`
- Entity Framework Core 8 with SQL Server
- Manual SQL schema and seed scripts
- Serilog with Console and Seq sinks
- Swagger/OpenAPI with bearer token support in development
- Azure App Service and Azure SQL Database for the planned/deployed Azure environment

Main package references:

- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.Identity.Web`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Authentication.OpenIdConnect`
- `Microsoft.AspNetCore.Components.WebAssembly`
- `Microsoft.AspNetCore.Components.WebAssembly.Server`
- `Serilog.AspNetCore`
- `Serilog.Sinks.Seq`
- `Swashbuckle.AspNetCore`

## High-Level Architecture

```text
Browser
  |
  | same-origin requests, auth cookie only
  v
LearningApp.UI.Server
  - Hosts Blazor WASM static assets
  - Handles Microsoft login/logout with OpenID Connect
  - Owns the HttpOnly BFF auth cookie
  - Retrieves saved access tokens
  - Proxies /bff/api/... calls to the API
  |
  | server-to-server HTTP with Authorization: Bearer <access_token>
  v
LearningApp.Api
  - Validates Microsoft Entra JWT bearer tokens
  - Runs controllers/services/repositories
  - Uses EF Core DbContext
  |
  v
SQL Server / Azure SQL Database
```

The browser does not store or send raw Microsoft access tokens. The Blazor client calls same-origin BFF routes, and the UI server adds bearer tokens when calling `LearningApp.Api`.

## Projects And Responsibilities

### `LearningApp.Api`

Backend REST API. It contains:

- Controllers for `Auth`, `Categories`, `Courses`, `Enrollments`, `LearningProgress`, and `Users`.
- Service layer for business rules and DTO mapping.
- Generic EF repository.
- EF Core `LearningAppDbContext`.
- Entity type configuration classes using Fluent API.
- Global exception middleware.
- SQL database health check.
- JWT bearer authentication and authorization configuration.
- CORS policy for allowed UI origins.
- Dashboard read rate limiter.

### `LearningApp.UI.Server`

Hosted Blazor/BFF server. It contains:

- Blazor WASM static file hosting.
- Cookie authentication and OpenID Connect challenge flow.
- `/bff/login`, `/bff/logout`, and `/bff/user` endpoints.
- `/bff/api/{**path}` proxy endpoint for API calls.
- HTTP client named `LearningApi` pointed at `Api:BaseUrl`.
- Razor error page and health/configuration controllers.

### `LearningApp.UI.Client`

Blazor WebAssembly browser UI. It contains:

- Pages for dashboard, login, categories, courses, enrollments, progress, users, and legacy auth-route compatibility.
- `BffAuthenticationStateProvider`, which reads auth state from `/bff/user`.
- `ApiClient`, which rewrites API paths through `/bff/...`.
- Entity-specific API service wrappers.
- Shared components such as `ErrorAlert` and `LoadingIndicator`.

### `LearningApp.UI.Shared`

Shared DTO and response contract library used by API and UI:

- `ApiResponse<T>`
- `ApiErrorResponse`
- Learning DTOs and request models
- BFF/auth DTOs

## Data Model

Database schema is created manually with `Database/001_create_schema.sql`. The schema name is `learning`.

Tables:

- `learning.Users`
  - `UserId`, `Email`, `FullName`, `PasswordHash`, `Role`, `IsActive`, timestamps
  - Unique index on `Email`
- `learning.Categories`
  - `CategoryId`, `Name`, `Description`, `IsActive`, `CreatedUtc`
  - Unique index on `Name`
- `learning.Courses`
  - Belongs to a category
  - Includes title, description, difficulty, duration, published flag, timestamps
- `learning.Enrollments`
  - Joins user and course
  - Tracks enrollment status and completion date
  - Unique index on `(UserId, CourseId)`
- `learning.LearningProgress`
  - One progress row per enrollment
  - Tracks percent complete, last accessed date, notes, update timestamp
  - Cascades delete from enrollment

Seed data is in `Database/002_seed_data.sql`. It creates sample categories and courses only. It does not create demo login users, enrollments, or progress rows.

The app supports both Microsoft sign-in and local email/password sign-in through the BFF. Local users should be created through registration or a controlled admin/setup process when the API has a local JWT signing key configured.

## API Surface

Base controller route pattern is `/api/[controller]`.

Endpoints:

- `POST /api/auth/login`
  - Local email/password login used by the BFF. Returns an API token to the BFF server, not directly to Blazor UI code.
- `POST /api/auth/register`
  - Creates a local `Learner` account and returns an API token to the BFF server.
- `/api/categories`
  - Authenticated reads.
  - Create/update/delete additionally decorated with `AdminOnly`.
- `/api/courses`
  - Authenticated reads.
  - Create/update/delete additionally decorated with `AdminOnly`.
- `/api/enrollments`
  - Authenticated reads and create.
  - Update/delete additionally decorated with `AdminOnly`.
- `/api/learningprogress`
  - Authenticated read, upsert, and delete.
- `/api/users`
  - Decorated with `AdminOnly`.

Authorization policies are role-based in the API: `AdminOnly` requires `Admin`, and `LearnerOrAdmin` requires `Learner` or `Admin`. For Microsoft sign-in, Entra access-token roles are authoritative; `Users.Role` never grants permissions or provides a Learner fallback. Validated `(iss, sub)` links Microsoft identities to database users through `learning.ExternalLogins` for enrollments and progress; local active status is still checked. Local password users continue to use database roles via local JWTs. Run `Database/003_external_logins.sql` if not already applied. See [entra-role-authorization.md](entra-role-authorization.md).

## UI Features And Routes

- `/`
  - Dashboard. Shows category count, course count, enrollment count, and average progress.
- `/login`
  - Login entry point that redirects into the BFF login flow.
- `/categories`
  - Category list, create, edit, delete.
- `/courses`
  - Course list, create, edit, delete. Requires categories for the course form.
- `/enrollments`
  - Enrollment list and create. Loads users and courses for selection.
- `/progress`
  - Learning progress list, upsert, delete. Loads enrollments for selection.
- `/users`
  - Admin UI route. User list, create, delete.

The navigation menu only shows protected app routes after authentication. It shows the Users link only for users with an Admin role claim.

## Request And Data Flow

Typical dashboard load:

1. Browser loads the hosted Blazor app from `LearningApp.UI.Server`.
2. `BffAuthenticationStateProvider` calls `/bff/user`.
3. If authenticated, `Dashboard.razor` asks entity services for categories, courses, enrollments, and progress.
4. `ApiClient` rewrites calls such as `api/categories` to `/bff/api/categories`.
5. Browser sends the same-origin request with the `LearningApp.Bff` cookie.
6. `BffApiProxyController` requires authentication and reads the saved `access_token`.
7. BFF sends the request to `LearningApp.Api` using `Authorization: Bearer <token>`.
8. API validates the Microsoft Entra JWT.
9. Controller calls service.
10. Service uses repository and EF Core `LearningAppDbContext`.
11. SQL Server returns data.
12. API wraps success in `ApiResponse<T>`.
13. BFF returns the API response body to Blazor.
14. UI renders the data or displays an error through `ErrorAlert`.

## Authentication Design

Current design is BFF server-side authentication:

- `LearningApp.UI.Server` is a confidential web app for Microsoft sign-in and the local-login host for email/password sign-in.
- Microsoft login uses OpenID Connect authorization code flow.
- Local login validates email/password through the API and signs the browser in with the BFF cookie.
- The browser receives an HttpOnly cookie named `LearningApp.Bff`.
- `SaveTokens = true` stores tokens in the protected auth ticket.
- The BFF proxy retrieves the access token with `HttpContext.GetTokenAsync("access_token")`.
- `LearningApp.Api` validates JWT bearer access tokens from Microsoft Entra ID and local JWTs issued for BFF-to-API calls.
- The Blazor WASM client does not use MSAL and does not store access tokens in browser storage.

Current local auth/config values are documented in `docs/bff-server-side-authentication.md`. Key IDs from checked-in config:

- UI app/client ID: `71bf9fbe-f5c8-4d2c-9d9a-b1e413de5462`
- API app/client ID: `2664ec3d-e5b6-4a59-a133-429d815b1b77`
- API Application ID URI: `api://2664ec3d-e5b6-4a59-a133-429d815b1b77`
- API delegated scope: `api://2664ec3d-e5b6-4a59-a133-429d815b1b77/access_as_user`
- Required single-tenant Authority override: `https://login.microsoftonline.com/<tenant-id>/v2.0`; override the API `AzureAd:TenantId` with the same tenant GUID. Checked-in `consumers` values are legacy defaults and must be overridden for Entra app roles.

Do not commit real `AzureAd:ClientSecret` values. For local development, use .NET user secrets for `LearningApp.UI.Server`.

Do not commit production `Jwt:SigningKey` values. Development config contains a dev-only local auth signing key. In Azure, set `Jwt__SigningKey` through App Service settings or a secret store.

Local email/password flow:

1. Browser posts credentials to `/bff/local-login` or `/bff/register`.
2. `LearningApp.UI.Server` calls `LearningApp.Api` server-to-server.
3. API validates or creates the user and returns a local API token to the BFF.
4. BFF stores that token in the protected auth ticket and creates the HttpOnly auth cookie.
5. Browser sees only the BFF cookie, never the JWT.

## Error Handling And Response Contracts

Success responses use:

```csharp
ApiResponse<T>
```

Error responses use:

```csharp
ApiErrorResponse
```

API error responses include:

- `success`
- `statusCode`
- `message`
- `errors`
- `traceId`
- `correlationId`
- `timestamp`

Global error behavior:

- `ValidationAppException` maps to 400 with detailed errors.
- `NotFoundException` maps to 404.
- `ConflictException` maps to 409.
- Unexpected exceptions map to safe 500 responses.
- `[ApiController]` model validation is customized to return `ApiErrorResponse`.
- JWT 401 and 403 responses are customized to return `ApiErrorResponse`.

Avoid controller-level try/catch just to format errors. Let services throw controlled app exceptions and let middleware format responses.

## Logging, Health Checks, And Rate Limiting

Logging:

- API and UI server both use Serilog.
- Console logging is configured.
- Seq sink points to `http://localhost:5341`.
- The app still runs if Seq is unavailable, but log shipping will fail until Seq is running.

Health checks:

- API exposes `/health/live`.
- API exposes `/health/ready`.
- API exposes `/health`.
- Readiness checks SQL connectivity through `SqlDatabaseHealthCheck`.

Rate limiting:

- API has a named fixed-window policy `dashboard-reads`.
- It currently applies to `GetAll` actions on Categories, Courses, Enrollments, and LearningProgress.
- Limit is 500 requests per minute per authenticated user ID or remote IP fallback.
- Rejections return HTTP 429 with a JSON error body.

## Local Development

Database setup:

```powershell
# Run from SQL Server Management Studio or Azure Data Studio.
:r Database/001_create_schema.sql
:r Database/002_seed_data.sql
```

Build and run API:

```powershell
dotnet restore .\LearningApp.Api.sln
dotnet build .\LearningApp.Api.sln
dotnet run --project .\LearningApp.Api\LearningApp.Api.csproj
```

Build and run UI:

```powershell
dotnet restore .\LearningApp.UI\LearningApp.UI.sln
dotnet build .\LearningApp.UI\LearningApp.UI.sln
dotnet run --project .\LearningApp.UI\LearningApp.UI.Server\LearningApp.UI.Server.csproj
```

Expected local URLs from config/docs:

- API base URL: `http://localhost:5013/`
- UI HTTP URL: `http://localhost:5068`
- UI HTTPS URL: `https://localhost:7024`
- Swagger in API development: `/swagger`

For local BFF auth, set the UI client secret with user secrets:

```powershell
dotnet user-secrets set "AzureAd:ClientSecret" "<secret-value>" --project LearningApp.UI\LearningApp.UI.Server\LearningApp.UI.Server.csproj
```

## Azure Deployment Context

The Azure deployment plan is in `.azure/deployment-plan.md`. Current documented status:

- Azure SQL Database has been created.
- Database schema and seed scripts are available under `Database/`.
- Local API can connect to Azure SQL and `/health/ready` returns healthy.
- Local UI Server/BFF and API are running successfully.
- API App Service has been created and deployed:
  - `https://learningappapi-etcfd2ekaxc0bqbq.westus2-01.azurewebsites.net`
  - `/health/live` and `/health/ready` are documented as healthy.
- UI App Service has been created:
  - `https://learningappui-ahhce3dqggd5crgy.westus2-01.azurewebsites.net`

Target Azure architecture:

- `LearningApp.Api` on Azure App Service.
- `LearningApp.UI.Server` on Azure App Service.
- Azure SQL Database for data.
- Microsoft Entra app registrations for UI/BFF login and API JWT validation.
- App Service environment variables for secrets and environment-specific config.

Production settings to configure through App Service settings/environment variables:

- API `ConnectionStrings__LearningAppDb`
- API `AzureAd__TenantId`
- API `AzureAd__ClientId`
- API `AzureAd__Audience`
- API `Cors__AllowedOrigins__0` or equivalent allowed UI origin entries
- UI `Api__BaseUrl`
- UI `AzureAd__Authority`
- UI `AzureAd__ClientId`
- UI `AzureAd__ClientSecret`
- UI `AzureAd__ApiScope`
- API `Jwt__SigningKey`
- API `Jwt__Issuer`
- API `Jwt__Audience`
- UI redirect URIs in the Entra app registration:
  - `https://<ui-host>/signin-oidc`
  - `https://<ui-host>/signout-callback-oidc`

Pending decisions in the deployment plan:

- Resource group name
- Azure region
- App Service plan SKU
- Deployment method: Visual Studio, Azure CLI zip deploy, or Azure Developer CLI

## Patterns To Preserve When Changing Code

- Put shared request/response contracts in `LearningApp.UI.Shared`.
- Keep controllers thin; put business behavior in services.
- Use EF Core configurations for schema mappings.
- Return `ApiResponse<T>` for successful API responses.
- Throw app exceptions for known business errors instead of formatting errors in controllers.
- Send browser API calls through `/bff/api/...`, not directly to `LearningApp.Api`.
- Keep access tokens out of browser JavaScript and browser storage.
- Keep secrets out of `appsettings.json`; use user secrets locally and environment variables in Azure.
- Update `Cors:AllowedOrigins` when adding a new UI host that calls the API directly. In normal BFF operation, the browser calls the UI server same-origin.
- If changing auth, update `docs/bff-server-side-authentication.md` and app registration settings together.
- Preserve API-side role and ownership enforcement; UI hiding is only a convenience.

## Known Gaps And Watch Items

- Microsoft users need Entra role claims or an account-linking flow before they can access role-protected API endpoints.
- BFF token storage uses `SaveTokens = true`; for higher-security production, consider server-side token cache and durable Data Protection key storage.
- State-changing BFF proxy calls should be reviewed for antiforgery protection before production hardening.
- Deployment plan is marked `Planning`, even though the API App Service is documented as deployed.
- No automated test project is visible in the current repository layout.
- Database migrations are not present; schema management is currently manual SQL scripts.
