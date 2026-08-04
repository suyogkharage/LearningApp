# Agent Skills

- Read SQL scripts before modifying EF entities or fluent mappings.
- Keep DTO contracts in `LearningApp.UI.Shared` so API and UI use the same request and response shapes.
- Prefer service-layer changes for business rules and keep controllers thin.
- Validate all request models before persistence.
- Preserve JWT, Serilog, Seq, Swagger, and global exception handling when adding features.
