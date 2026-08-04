# Architecture

## API

`Controllers` expose REST endpoints. `Services` hold business logic, validation orchestration, mapping, and transaction boundaries. `Data` contains the EF Core DbContext. `Configurations` map entities to the manually authored SQL schema. `Middleware` centralizes exception handling.

## UI

`Client` contains authenticated Blazor pages, reusable components, API services, and JWT authentication state. `Server` hosts the WebAssembly assets and exposes health/configuration endpoints. `Shared` contains DTOs and common API response contracts.
