# Repository rules

- Use spaces for indentation (4 spaces by default), CRLF line endings, and UTF-8 with BOM for all authored text files.
- Preserve this format after every edit, including source code, configuration, documentation and scripts.
- Do not reformat generated output, third-party libraries or binary assets.
- Run `pwsh -NoProfile -File scripts/Check-TextFormat.ps1` before completing changes. The same command can run in CI.
- Prefer codebase-memory-mcp tools for code discovery; index this repository before discovery when necessary.
