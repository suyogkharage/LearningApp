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
```

Seed login:

- Email: `admin@learning.local`
- Password: `Password1!`

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
