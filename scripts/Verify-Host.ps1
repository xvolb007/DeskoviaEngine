param([string]$ApiUrl = 'https://localhost:7095', [string]$FrontendUrl = 'https://localhost:7178')
$ErrorActionPreference = 'Stop'
function Parse($r) {
    if ($r.Content -is [byte[]]) { return [Text.Encoding]::UTF8.GetString($r.Content) | ConvertFrom-Json }
    return $r.Content | ConvertFrom-Json
}
function Assert($ok, $name) { if (-not $ok) { throw "FAIL: $name" }; Write-Output "PASS: $name" }
$s = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$settings = Invoke-RestMethod "$ApiUrl/api/auth/options"
Assert ($null -ne $settings.microsoftEnabled -and $null -ne $settings.microsoftConfigured) 'Microsoft sign-in configuration advertised'
$t = Invoke-RestMethod "$ApiUrl/api/auth/antiforgery" -WebSession $s
$cookie = $s.Cookies.GetCookies([uri]$ApiUrl) | Where-Object Name -eq '__Host-Deskovia.Antiforgery'
Assert ($cookie.HttpOnly -and $cookie.Secure) 'anonymous CSRF bootstrap uses Secure/HttpOnly cookie'
foreach ($path in @('api/auth/me','api/offices','api/admin/members','api/owner/billing')) {
    $r = Invoke-WebRequest "$ApiUrl/$path" -SkipHttpErrorCheck
    Assert ($r.StatusCode -eq 401 -and -not $r.Headers.ContainsKey('Location')) "$path returns 401 without redirect"
}
$r = Invoke-WebRequest "$ApiUrl/api/auth/login" -Method Post -ContentType application/json -Body '{"email":"check@example.test","password":"long enough password"}' -SkipHttpErrorCheck
Assert ($r.StatusCode -eq 400 -and (Parse $r).code -eq 'antiforgery_failed') 'login requires CSRF token'
$r = Invoke-WebRequest "$ApiUrl/api/auth/login" -Method Post -ContentType application/json -Body '{}' -WebSession $s -Headers @{'X-CSRF-TOKEN'=$t.token} -SkipHttpErrorCheck
Assert ($r.StatusCode -eq 400 -and (Parse $r).code -eq 'validation_failed') 'field validation ProblemDetails'
$r = Invoke-WebRequest "$ApiUrl/api/auth/microsoft" -Method Post -ContentType application/x-www-form-urlencoded -Body 'returnUrl=/' -SkipHttpErrorCheck
Assert ($r.StatusCode -eq 400 -and (Parse $r).code -eq 'antiforgery_failed') 'Microsoft challenge requires CSRF token'
$r = Invoke-WebRequest "$ApiUrl/api/auth/demo-login" -Method Post -ContentType application/json -Body '{"email":"demo-check@example.test"}' -WebSession $s -Headers @{'X-CSRF-TOKEN'=$t.token} -SkipHttpErrorCheck
Assert ($r.StatusCode -eq 404) 'email-only demo endpoint removed'
$r = Invoke-WebRequest "$ApiUrl/api/auth/antiforgery" -Headers @{Origin=$FrontendUrl}
Assert ($r.Headers['Access-Control-Allow-Origin'] -eq $FrontendUrl -and $r.Headers['Access-Control-Allow-Credentials'] -eq 'true') 'exact origin with credentials'
$r = Invoke-WebRequest "$ApiUrl/api/auth/antiforgery" -Headers @{Origin='https://example.invalid'}
Assert (-not $r.Headers.ContainsKey('Access-Control-Allow-Origin')) 'unrelated origin denied'
$o = Invoke-RestMethod "$ApiUrl/openapi/v1.json"
Assert (@($o.paths.PSObject.Properties).Count -ge 38) 'live OpenAPI routes'
for ($i=0; $i -lt 12; $i++) {
    $r = Invoke-WebRequest "$ApiUrl/api/auth/login" -Method Post -ContentType application/json -Body '{}' -WebSession $s -Headers @{'X-CSRF-TOKEN'=$t.token} -SkipHttpErrorCheck
}
Assert ($r.StatusCode -eq 429) 'login rate limit'
$r = Invoke-WebRequest "$FrontendUrl/login"
Assert ($r.StatusCode -eq 200 -and $r.Content -match 'blazor.web.js') 'frontend Blazor shell'
