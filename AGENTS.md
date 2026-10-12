# Repository rules

- Use spaces for indentation (4 spaces by default), CRLF line endings, and UTF-8 with BOM for all authored text files.
- Preserve this format after every edit, including source code, configuration, documentation and scripts.
- Do not reformat generated output, third-party libraries or binary assets.
- Run `pwsh -NoProfile -File scripts/Check-TextFormat.ps1` before completing changes. The same command can run in CI.
- Prefer codebase-memory-mcp tools for code discovery; index this repository before discovery when necessary.

- The user runs database migrations themselves. Do not generate or apply migrations, or create a database, unless explicitly asked to execute that specific step. Preparing entities, mappings and connection configuration is allowed within the requested task.

- Use SQL Server with the default dbo schema. Use one DeskoviaDbContext and SQL Server migrations in Migrations. The user removed PostgreSQL and its migrations for a fresh database; do not restore them.

- The project targets English-speaking users. Write all UI text, API messages, notifications, comments, documentation and other authored project content in English. Continue to support Unicode user input.
