# LearningApp

Enterprise-style .NET 8 CRUD application with:

- ASP.NET Core Web API
- Hosted Blazor WebAssembly UI
- SQL Server manual schema scripts
- EF Core Fluent API mappings
- JWT authentication and role authorization
- Serilog and Seq logging
- Swagger/OpenAPI with Bearer token support

## Structure

```text
LearningApp
|-- Database
|   |-- 001_create_schema.sql
|   `-- 002_seed_data.sql
|-- LearningApp.Api
|   |-- Controllers
|   |-- Services
|   |-- Repositories
|   |-- Entities
|   |-- Configurations
|   |-- Data
|   |-- Middleware
|   |-- Authentication
|   `-- Extensions
`-- LearningApp.UI
    |-- LearningApp.UI.Client
    |-- LearningApp.UI.Server
    |-- LearningApp.UI.Shared
    `-- .Agent
```

## Database Setup

Open SQL Server Management Studio or Azure Data Studio and run:

```sql
:r Database/001_create_schema.sql
:r Database/002_seed_data.sql
:r Database/003_external_logins.sql
```

The seed script creates sample categories and courses only. It does not create demo login users. Register a learner from the UI, or create/administer privileged users through a controlled setup process.

## Run

```powershell
dotnet restore .\LearningApp.Api.sln
dotnet build .\LearningApp.Api.sln
dotnet run --project .\LearningApp.Api\LearningApp.Api.csproj

dotnet restore .\LearningApp.UI\LearningApp.UI.sln
dotnet build .\LearningApp.UI\LearningApp.UI.sln
dotnet run --project .\LearningApp.UI\LearningApp.UI.Server\LearningApp.UI.Server.csproj
```

Swagger is available from the API development URL at `/swagger`.

Seq is expected at `http://localhost:5341`; the app still runs if Seq is unavailable, but log shipping will not succeed until Seq is running.

## Authentication

Microsoft Entra ID setup, code wiring, login/logout flow, and troubleshooting notes are documented in:

```text
docs/entra-id-authentication.md
```

Current single-tenant Entra roles, local user linking, and Azure deployment steps are in
[docs/entra-role-authorization.md](docs/entra-role-authorization.md).
