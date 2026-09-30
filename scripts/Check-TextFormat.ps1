$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$extensions = @('.cs', '.csproj', '.sln', '.slnx', '.razor', '.css', '.js', '.ts', '.json', '.xml', '.props', '.targets', '.md', '.txt', '.http', '.ps1', '.yml', '.yaml', '.html', '.svg', '.config', '.resx', '.ruleset')
$specialNames = @('.editorconfig', '.gitattributes', '.gitignore', 'Dockerfile')
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$failures = [System.Collections.Generic.List[string]]::new()
$count = 0
Push-Location $repositoryRoot
try {
    $gitOutput = git -c core.quotepath=false ls-files -z --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate repository files.' }
    $paths = ($gitOutput -join "`n").Split([char]0, [System.StringSplitOptions]::RemoveEmptyEntries) | Sort-Object -Unique
    foreach ($relativePath in $paths) {
        if ($relativePath -match '(^|/)(bin|obj|node_modules|\.git|\.vs)/|(^|/)wwwroot/lib/') { continue }
        $path = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        if ([System.IO.Path]::GetExtension($path) -notin $extensions -and [System.IO.Path]::GetFileName($path) -notin $specialNames) { continue }
        $count++
        $bytes = [System.IO.File]::ReadAllBytes($path)
        if ($bytes.Length -lt 3 -or $bytes[0] -ne 239 -or $bytes[1] -ne 187 -or $bytes[2] -ne 191) {
            $failures.Add("$relativePath : missing UTF-8 BOM")
        }
        try { $content = $strictUtf8.GetString($bytes) }
        catch { $failures.Add("$relativePath : invalid UTF-8"); continue }
        if ($content -match '(?<!\r)\n|\r(?!\n)') { $failures.Add("$relativePath : line endings must be CRLF") }
        if ($content -match '(?m)^[ \t]*\t') { $failures.Add("$relativePath : indentation contains tabs") }
        if (-not $content.EndsWith("`r`n")) { $failures.Add("$relativePath : missing final CRLF") }
    }
} finally {
    Pop-Location
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Output $_ }
    exit 1
}
Write-Output "Text format OK: $count files (spaces, CRLF, UTF-8 BOM)."
