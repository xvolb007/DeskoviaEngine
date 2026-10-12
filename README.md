# Deskovia Engine

ASP.NET Core backend on .NET 10. Open `DeskoviaEngine.slnx` in Visual Studio and select `WebApi` as the startup project.

## Projects

| Project | Responsibility |
| --- | --- |
| WebApi | HTTP controllers, request handling and application composition |
| Application | Use cases, application services and interfaces for external capabilities |
| Domain | Business entities, value objects and business rules |
| Infrastructure | Persistence and implementations of identity, notification and payment integrations |
| Shared | API DTOs and contracts suitable for sharing with clients |

Dependency direction:

```text
WebApi -> Application, Infrastructure, Shared
Infrastructure -> Application, Domain
Application -> Domain, Shared
Domain -> no project dependencies
Shared -> no project dependencies
```

Domain must not depend on API DTOs, EF Core or authentication providers.
Shared must not contain database entities, secrets or server implementations.
External adapters implement interfaces defined in Application.
Only WebApi is executable; the other projects are class libraries.

## Run

```powershell
dotnet build DeskoviaEngine.slnx
dotnet run --project WebApi/WebApi.csproj --launch-profile https
```

Development URLs: HTTPS port 7095, HTTP port 5144.
The OpenAPI document is available at `/openapi/v1.json` in Development.
An interactive Swagger UI has not been added.

## Package restore

Repository NuGet.Config uses only nuget.org for remote packages/audits and clears inherited source settings. The frontend has the same configuration because its Shared reference resides in this backend repository. Global corporate feeds, including an HTTP Unionstreet source, are not required by Deskovia. User/Visual Studio NuGet settings were not changed. Local .NET SDK library packs may still appear in restore metadata.

Run dotnet restore and dotnet build normally; no --source workaround is required. Restart/reload Visual Studio solutions if their cached restore errors remain. [NuGet configuration precedence and clear](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file).

## SQL Server database (SSMS)

Configure a running SQL Server instance using its SSMS Server name. Windows Authentication is the default; SQL Server Authentication is available with -Username and a secure password prompt.

~~~powershell
.\scripts\Configure-Database.ps1 -ServerName 'YOUR_SSMS_SERVER_NAME'
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef migrations add SqlServerInitial --project Infrastructure --startup-project WebApi --output-dir Migrations -- --environment Development
dotnet ef database update --project Infrastructure --startup-project WebApi -- --environment Development
~~~

Run these migration commands yourself after reviewing the generated migration. Database/migration creation is never automatic at API startup. For a self-signed local development server certificate, the configuration script accepts -TrustServerCertificate. Use validated certificates in production.

Local runtime settings, including ConnectionStrings:DeskoviaSqlServer and Entra:ClientSecret, are stored in WebApi/appsettings.json at the user's request. Production can supply ConnectionStrings__DeskoviaSqlServer through deployment secrets. WebApi/appsettings.json contains the connection key. Configure a Windows Authentication development connection there or use User Secrets; keep SQL passwords outside tracked files.

The runtime uses one DeskoviaDbContext, SQL Server dbo, rowversion concurrency tokens and transaction-owned sp_getapplock locking. There is no PostgreSQL provider or secondary context. The user removed the old database/migrations and starts with a fresh SQL Server database.

In Visual Studio, choose WebApi as startup project and Infrastructure as the Package Manager Console default project, then run Add-Migration Init and Update-Database. No -Context parameter is needed. EF design-time tooling loads WebApi User Secrets even when no environment argument is provided.

To edit the local connection, open WebApi/appsettings.json and change ConnectionStrings:DeskoviaSqlServer. Replace YOUR_SSMS_SERVER_NAME with the exact server name from SSMS:

~~~json
{
  "ConnectionStrings": {
    "DeskoviaSqlServer": "Server=YOUR_SSMS_SERVER_NAME;Database=Deskovia;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
  }
}
~~~

This example uses Windows Authentication and trusts the server certificate for local development only. In JSON, a named instance needs an escaped backslash, for example SERVER\SQLEXPRESS. For SQL Authentication use the configuration script with -Username; it prompts for the password. Do not put passwords in tracked configuration. User Secrets are stored outside the repository and are not a production vault.

## Functional prototype A–F

After your SQL Server migration, normal startup prepares active ABC automatically. The first registered account becomes Owner and later accounts become Employee. No separate ABC activation, Owner command or payment is required with default settings.

Setup, current migration instructions and acceptance scenarios: [PrototypeRunbook.md](docs/PrototypeRunbook.md). Model and integrity rules: [DataModel.md](docs/DataModel.md). Completed checks and unverified integrations: [Verification.md](docs/Verification.md). No database or migration was created/applied by the implementation agent; no Tests project was added.

## Microsoft Entra ID sign-in

Real single-tenant Microsoft sign-in replaces email-only demo authentication. Configuration and acceptance scenarios: [EntraLogin.md](docs/EntraLogin.md). Existing identity fields support this change without a new migration. Local password sign-in remains available.
