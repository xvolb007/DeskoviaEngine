$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet tool restore --tool-manifest dotnet-tools.json
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    dotnet ef database update --project Infrastructure --startup-project WebApi -- --environment Development
    if ($LASTEXITCODE -ne 0) { throw 'Database update failed. Check SQL Server, the SQL Server migration and ConnectionStrings:DeskoviaSqlServer in User Secrets.' }
} finally {
    Pop-Location
}
