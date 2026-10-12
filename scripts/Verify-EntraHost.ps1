param([string]$ApiUrl = 'https://localhost:7095', [string]$FrontendUrl = 'https://localhost:7178')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.CookieContainer = [Net.CookieContainer]::new()
$client = [Net.Http.HttpClient]::new($handler)
function Assert($ok, $name) { if (-not $ok) { throw "FAIL: $name" }; Write-Output "PASS: $name" }
function Form($values) {
    $fields = [Collections.Generic.Dictionary[string,string]]::new()
    foreach ($key in $values.Keys) { $fields.Add($key, [string]$values[$key]) }
    return [Net.Http.FormUrlEncodedContent]::new($fields)
}
function Post($path, $values) { return $client.PostAsync("$ApiUrl/$path", (Form $values)).GetAwaiter().GetResult() }
function NoSession($response) {
    return -not ($response.Headers.Contains('Set-Cookie') -and (($response.Headers.GetValues('Set-Cookie') -join ' ') -match '__Host-Deskovia.Session='))
}
try {
    $options = $client.GetStringAsync("$ApiUrl/api/auth/options").GetAwaiter().GetResult() | ConvertFrom-Json
    Assert ($options.microsoftEnabled -and $options.microsoftConfigured) 'real Microsoft sign-in is configured'
    $token = ($client.GetStringAsync("$ApiUrl/api/auth/antiforgery").GetAwaiter().GetResult() | ConvertFrom-Json).token
    $response = Post 'api/auth/microsoft' @{returnUrl='/'}
    Assert ([int]$response.StatusCode -eq 400 -and (NoSession $response)) 'Microsoft challenge rejects missing antiforgery token'
    $response.Dispose()
    $response = Post 'api/auth/microsoft' @{returnUrl='/reservations'; __RequestVerificationToken=$token}
    $target = $response.Headers.Location
    Assert ([int]$response.StatusCode -eq 302 -and $target.Host -eq 'login.microsoftonline.com') 'challenge redirects to real Microsoft authorization endpoint'
    Assert ($target.AbsolutePath -eq '/0fc379c2-9aac-4a1c-9fbd-f4dc20999e9c/oauth2/v2.0/authorize') 'challenge uses the configured single tenant'
    $values = @{}
    foreach ($pair in $target.Query.TrimStart('?').Split('&')) {
        $parts = $pair.Split('=',2)
        $values[[uri]::UnescapeDataString($parts[0])] = [uri]::UnescapeDataString($parts[1].Replace('+',' '))
    }
    Assert ($values.client_id -eq '5f93d70d-3656-4603-9ad7-144d6bd1e74c' -and $values.response_type -eq 'code' -and (-not $values.ContainsKey('response_mode') -or $values.response_mode -eq 'query')) 'configured client uses authorization code flow'
    Assert ($values.redirect_uri -eq "$ApiUrl/signin-oidc") 'callback belongs to the backend'
    Assert ($values.code_challenge_method -eq 'S256' -and $values.code_challenge -and $values.state -and $values.nonce) 'PKCE, protected state and nonce included'
    Assert (-not $values.ContainsKey('client_secret') -and (NoSession $response)) 'challenge exposes no secret and issues no Deskovia session'
    $response.Dispose()
    $response = $client.GetAsync("$ApiUrl/signin-oidc?code=forged&state=forged").GetAwaiter().GetResult()
    Assert ([int]$response.StatusCode -eq 302 -and $response.Headers.Location.AbsoluteUri -eq "$FrontendUrl/login?authError=microsoft_signin_failed" -and (NoSession $response)) 'forged callback rejected without issuing a session'
    $response.Dispose()
    $response = Post 'signin-oidc' @{error='access_denied'; state=$values.state}
    Assert ([int]$response.StatusCode -eq 302 -and $response.Headers.Location.AbsoluteUri -eq "$FrontendUrl/login?authError=microsoft_signin_failed" -and (NoSession $response)) 'cancelled Microsoft login with genuine state cannot authenticate'
    $response.Dispose()
    Write-Output 'Entra host checks passed. No successful Microsoft authentication or database operations were performed.'
}
finally { $client.Dispose(); $handler.Dispose() }
