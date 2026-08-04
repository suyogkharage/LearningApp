# LearningApp Azure Deployment Plan

Status: Planning

## Objective

Deploy the existing LearningApp solution to Azure, starting with the `LearningApp.Api` ASP.NET Core Web API on Azure App Service.

## Current State

- Azure SQL Database has been created.
- Database schema and seed scripts are available under `Database/`.
- Local API can connect to Azure SQL and `/health/ready` returns healthy.
- Local UI Server/BFF and API are running successfully.
- API App Service has been created: `https://learningappapi-etcfd2ekaxc0bqbq.westus2-01.azurewebsites.net`.
- API App Service has been deployed and both `/health/live` and `/health/ready` are healthy.
- UI App Service has been created: `https://learningappui-ahhce3dqggd5crgy.westus2-01.azurewebsites.net`.

## Target Architecture

- `LearningApp.Api` -> Azure App Service for the API.
- `LearningApp.UI.Server` -> Azure App Service for the hosted Blazor/BFF app.
- Data -> Azure SQL Database.
- Authentication -> Microsoft Entra app registrations with UI/BFF OpenID Connect and API JWT bearer validation.
- Secrets/configuration -> Azure App Service environment variables.

## API App Service Plan

1. Create an Azure App Service for `LearningApp.Api`.
2. Configure .NET 8 runtime.
3. Add API application settings.
4. Add Azure SQL connection string.
5. Add UI origin to API CORS after UI App Service is created.
6. Publish `LearningApp.Api` to the API App Service.
7. Verify `/health/live`, `/health/ready`, and authenticated API access.

## UI App Service Plan

1. Add production redirect URIs to the UI/BFF Entra app registration.
2. Configure UI App Service settings for API base URL and OpenID Connect.
3. Add the UI App Service origin to API CORS settings.
4. Publish `LearningApp.UI.Server` to the UI App Service.
5. Verify login, `/bff/user`, and dashboard API proxy calls.

## Pending Decisions

- Resource group name.
- Azure region.
- App Service plan SKU.
- Deployment method: Visual Studio, Azure CLI zip deploy, or Azure Developer CLI.
