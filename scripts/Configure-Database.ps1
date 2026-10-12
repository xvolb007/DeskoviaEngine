param(
    [string]$ServerName,
    [string]$Database = 'Deskovia',
    [string]$Username,
    [switch]$TrustServerCertificate
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ServerName)) {
    $ServerName = Read-Host 'SQL Server instance (the Server name shown in SSMS)'
}
if ([string]::IsNullOrWhiteSpace($ServerName) -or [string]::IsNullOrWhiteSpace($Database)) {
    throw 'SQL Server instance and database are required.'
}
$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'WebApi/WebApi.csproj'
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $ServerName
$connection['Database'] = $Database
$connection['Encrypt'] = $true
$connection['TrustServerCertificate'] = $TrustServerCertificate.IsPresent
$password = $null
try {
    if ([string]::IsNullOrWhiteSpace($Username)) {
        $connection['Integrated Security'] = $true
    } else {
        $connection['User ID'] = $Username
        $password = Read-Host 'SQL Server password (saved locally in User Secrets)' -AsSecureString
        $connection['Password'] = [System.Net.NetworkCredential]::new('', $password).Password
    }
    @{ 'ConnectionStrings:DeskoviaSqlServer' = $connection.ConnectionString } |
        ConvertTo-Json -Compress |
        dotnet user-secrets set --project $projectPath
    if ($LASTEXITCODE -ne 0) { throw 'Failed to save the SQL Server connection string.' }
    Write-Output 'SQL Server connection saved locally. No database or migrations were created.'
} finally {
    $connection.Clear()
    if ($null -ne $password) { $password.Dispose() }
}
