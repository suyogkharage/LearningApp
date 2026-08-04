# Instructions

- Do not use EF Core scaffolding or database-first generators.
- Do not add migrations unless the project owner explicitly requests them.
- Add table changes first in SQL scripts, then mirror them in entity and configuration classes.
- Keep protected pages behind Blazor authorization and attach JWT tokens through `AuthHeaderHandler`.
- Use environment configuration for secrets and connection strings.
