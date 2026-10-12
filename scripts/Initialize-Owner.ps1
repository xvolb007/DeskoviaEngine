param([Parameter(Mandatory)][string]$Email, [string]$Name = 'ABC Owner')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$securePassword = Read-Host 'Owner password (12–128 characters; never stored in repository)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$previousPassword = $env:Bootstrap__Password
$previousEmail = $env:Bootstrap__Email
$previousName = $env:Bootstrap__Name
try {
    $env:Bootstrap__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $env:Bootstrap__Email = $Email
    $env:Bootstrap__Name = $Name
    dotnet run --project (Join-Path $repositoryRoot 'WebApi') --no-launch-profile -- --environment Development --bootstrap-owner
    if ($LASTEXITCODE -ne 0) { throw 'Owner initialization failed. Check applied migration and database configuration.' }
} finally {
    $env:Bootstrap__Password = $previousPassword
    $env:Bootstrap__Email = $previousEmail
    $env:Bootstrap__Name = $previousName
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $securePassword.Dispose()
}
