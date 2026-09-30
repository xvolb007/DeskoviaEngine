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

This is the project skeleton. Database access, authentication, domain models
and integrations are not implemented yet. No test project is included.
