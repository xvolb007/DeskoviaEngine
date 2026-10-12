param(
    [string]$ApiUrl = 'https://localhost:7095',
    [Parameter(Mandatory)][PSCredential]$OwnerCredential
)
$ErrorActionPreference = 'Stop'
# Run only after the owner's migration/bootstrap, against a demonstration database.
# Creates identifiable demonstration data, retains history, never deletes user data.
function Assert($condition, [string]$name) {
    if (-not $condition) { throw "FAIL: $name" }
    Write-Output "PASS: $name"
}
function Parse($response) {
    if (-not $response.Content) { return $null }
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { $response.Content }
    return $text | ConvertFrom-Json
}
function Client { return [Microsoft.PowerShell.Commands.WebRequestSession]::new() }
function Request($session, [string]$method, [string]$path, $body = $null, [int]$expected = 200) {
    $parameters = @{ Uri = "$ApiUrl/$path"; WebSession = $session; Method = $method; SkipHttpErrorCheck = $true }
    if ($method -ne 'GET') {
        $token = Invoke-RestMethod "$ApiUrl/api/auth/antiforgery" -WebSession $session
        $parameters.Headers = @{ 'X-CSRF-TOKEN' = $token.token }
        if ($null -ne $body) { $parameters.Body = ConvertTo-Json $body -Depth 12 -Compress; $parameters.ContentType = 'application/json' }
    }
    $response = Invoke-WebRequest @parameters
    if ($response.StatusCode -ne $expected) {
        $problem = Parse $response
        throw "$method $path expected $expected, got $($response.StatusCode): $($problem.code) $($problem.title)"
    }
    return Parse $response
}
$owner = Client
$password = $OwnerCredential.GetNetworkCredential().Password
try {
    $ownerUser = Request $owner POST 'api/auth/login' @{email=$OwnerCredential.UserName;password=$password}
} finally { $password = $null }
Assert ($ownerUser.role -eq 'Owner') 'owner login'
$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$demoPassword = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$alice = Client; $bob = Client; $carol = Client
$aliceUser = Request $alice POST 'api/auth/register' @{name="Acceptance Alice $stamp";email="alice-$stamp@example.test";password=$demoPassword;role='Owner';organizationId=[guid]::NewGuid()}
$bobUser = Request $bob POST 'api/auth/register' @{name="Acceptance Bob $stamp";email="bob-$stamp@example.test";password=$demoPassword}
$carolUser = Request $carol POST 'api/auth/register' @{name="Acceptance Carol $stamp";email="carol-$stamp@example.test";password=$demoPassword}
Assert ($aliceUser.role -eq 'Employee' -and $aliceUser.organizationId -eq $ownerUser.organizationId) 'client role/organization ignored'
$cookie = $alice.Cookies.GetCookies([uri]$ApiUrl) | Where-Object Name -eq '__Host-Deskovia.Session'
Assert ($cookie.HttpOnly -and $cookie.Secure) 'session cookie flags'
$null = Request $alice POST 'api/auth/register' @{name='Duplicate';email="ALICE-$stamp@example.test";password=$demoPassword} 409
$null = Request $alice GET 'api/admin/members' $null 403
$null = Request $alice GET 'api/owner/billing' $null 403
$hours = 0..6 | ForEach-Object { @{day=$_;opensAt='07:00:00';closesAt='18:00:00'} }
$office = Request $owner POST 'api/admin/offices' @{name="Acceptance Prague $stamp";timeZoneId='Europe/Prague';bookingHorizonDays=30;hours=@($hours)}
$office2 = Request $owner POST 'api/admin/offices' @{name="Acceptance Second $stamp";timeZoneId='Europe/Prague';bookingHorizonDays=30;hours=@(@{day=1;opensAt='09:00:00';closesAt='16:00:00'})}
$room = Request $owner POST 'api/admin/rooms' @{officeId=$office;name='Acceptance Room';x=10;y=10;width=100;height=100}
$desk = Request $owner POST 'api/admin/desks' @{roomId=$room;label='A';x=10;y=10;rotation=0}
$desk2 = Request $owner POST 'api/admin/desks' @{roomId=$room;label='B';x=30;y=10;rotation=0}
$null = Request $alice POST 'api/admin/desks' @{roomId=$room;label='Forbidden';x=60;y=10;rotation=0} 403
$null = Request $owner PUT "api/admin/desks/$desk" @{roomId=$room;label='A';x=15;y=20;rotation=0}
$map = Request $alice GET "api/offices/$office/map"
Assert (($map.desks | Where-Object id -eq $desk).x -eq 15) 'coordinates persist through API reload'
$offices = Request $alice GET 'api/offices'
Assert (@($offices | Where-Object { $_.id -in @($office,$office2) }).Count -eq 2) 'both offices accessible'
$today = [DateOnly]::Parse(($offices | Where-Object id -eq $office).today)
# Local simulated billing must be paid if Billing.Enabled=true.
$paymentKey = "acceptance-$stamp"
$failed = Request $owner POST 'api/owner/billing/simulate' @{idempotencyKey="$paymentKey-failure";succeed=$false}
Assert ($failed.status -eq 'Failed') 'simulated failure recorded'
$paid = Request $owner POST 'api/owner/billing/simulate' @{idempotencyKey=$paymentKey;succeed=$true}
$paidAgain = Request $owner POST 'api/owner/billing/simulate' @{idempotencyKey=$paymentKey;succeed=$true}
Assert ($paid.id -eq $paidAgain.id) 'simulated payment replay returns same payment'
$dates = @(1,2,3 | ForEach-Object { $today.AddDays($_).ToString('yyyy-MM-dd') })
$batch = Request $alice POST 'api/reservations/batch' @{deskId=$desk;dates=$dates;isFullDay=$true}
Assert ($batch.reservations.Count -eq 3 -and @($batch.reservations | Where-Object batchId -ne $batch.batchId).Count -eq 0) 'atomic batch with three dates'
$bobBefore = Request $bob GET 'api/reservations/mine'
$null = Request $bob POST 'api/reservations/batch' @{deskId=$desk;dates=@($today.AddDays(4).ToString('yyyy-MM-dd'),$dates[0]);isFullDay=$true} 409
$bobAfter = Request $bob GET 'api/reservations/mine'
Assert ($bobBefore.total -eq $bobAfter.total) 'conflicting SQL batch has no partial insert'
$null = Request $alice POST 'api/reservations/batch' @{deskId=$desk2;dates=@($dates[0]);isFullDay=$true} 409
Write-Output 'PASS: member overlap rejected across desks'
# Real concurrent requests using distinct sessions and a shared competing desk.
$tokenBob = Invoke-RestMethod "$ApiUrl/api/auth/antiforgery" -WebSession $bob
$tokenCarol = Invoke-RestMethod "$ApiUrl/api/auth/antiforgery" -WebSession $carol
$handlerBob = [Net.Http.HttpClientHandler]::new(); $handlerBob.CookieContainer = $bob.Cookies
$handlerCarol = [Net.Http.HttpClientHandler]::new(); $handlerCarol.CookieContainer = $carol.Cookies
$httpBob = [Net.Http.HttpClient]::new($handlerBob)
$httpCarol = [Net.Http.HttpClient]::new($handlerCarol)
$body = ConvertTo-Json @{deskId=$desk2;dates=@($dates[0]);isFullDay=$true} -Compress
$messageBob = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, "$ApiUrl/api/reservations/batch")
$messageCarol = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, "$ApiUrl/api/reservations/batch")
$messageBob.Content = [Net.Http.StringContent]::new($body,[Text.Encoding]::UTF8,'application/json')
$messageCarol.Content = [Net.Http.StringContent]::new($body,[Text.Encoding]::UTF8,'application/json')
$messageBob.Headers.Add('X-CSRF-TOKEN',$tokenBob.token)
$messageCarol.Headers.Add('X-CSRF-TOKEN',$tokenCarol.token)
try {
    $taskBob = $httpBob.SendAsync($messageBob)
    $taskCarol = $httpCarol.SendAsync($messageCarol)
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($taskBob,$taskCarol))
    $codes = @([int]$taskBob.Result.StatusCode,[int]$taskCarol.Result.StatusCode) | Sort-Object
    Assert (($codes -join ',') -eq '200,409') 'SQL Server application lock permits only one competing reservation'
} finally {
    $httpBob.Dispose(); $httpCarol.Dispose(); $messageBob.Dispose(); $messageCarol.Dispose()
}
$hourDate = $today.AddDays(5).ToString('yyyy-MM-dd')
$null = Request $bob POST 'api/reservations/batch' @{deskId=$desk;dates=@($hourDate);isFullDay=$false;localStart='10:00:00';localEnd='11:00:00'}
$null = Request $bob POST 'api/reservations/batch' @{deskId=$desk;dates=@($hourDate);isFullDay=$false;localStart='11:00:00';localEnd='12:00:00'}
Write-Output 'PASS: adjacent SQL intervals allowed'
$availability = Request $alice POST 'api/reservations/availability' @{deskId=$desk;dates=@($today.AddDays(31).ToString('yyyy-MM-dd'));isFullDay=$true}
Assert ($availability[0].code -eq 'outside_horizon') 'today+31 outside horizon'
$null = Request $alice GET "api/reservations/$((Request $bob GET 'api/reservations/mine').items[0].id)" $null 404
$null = Request $alice GET 'api/admin/reservations' $null 403
$closure = @{officeId=$office;roomId=$room;start=$batch.reservations[0].start;end=$batch.reservations[0].end;reason='Acceptance repair'}
$preview = Request $owner POST 'api/admin/closures/preview' $closure
$closure.acknowledgedReservationIds = @()
$null = Request $owner POST 'api/admin/closures' $closure 409
$closure.acknowledgedReservationIds = @($preview | ForEach-Object id)
$closureId = Request $owner POST 'api/admin/closures' $closure
$null = Request $owner POST "api/admin/closures/$closureId/cancel"
$history = Request $alice GET 'api/reservations/mine?history=true'
Assert (@($history.items | Where-Object cancellationReason -eq 'RoomClosed').Count -gt 0) 'closure cancellation retained after removal'
$null = Request $owner PUT "api/owner/demo-directory/$($bobUser.memberId)" @{state='Unavailable'}
$null = Request $owner POST 'api/owner/demo-directory/sync'
$null = Request $bob GET 'api/auth/me'
$null = Request $owner PUT "api/owner/demo-directory/$($bobUser.memberId)" @{state='Disabled'}
$null = Request $owner POST 'api/owner/demo-directory/sync'
$null = Request $bob GET 'api/auth/me' $null 401
$null = Request $owner POST "api/admin/members/$($bobUser.memberId)/enable"
$null = Request $bob POST 'api/auth/login' @{email=$bobUser.email;password=$demoPassword}
$history = Request $bob GET 'api/reservations/mine?history=true'
Assert (@($history.items | Where-Object cancellationReason -eq 'AccountDisabled').Count -gt 0) 'directory disable processes bookings and enable does not restore'
$null = Request $owner PUT "api/owner/demo-directory/$($bobUser.memberId)" @{state=$null}
$null = Request $owner PUT "api/owner/members/$($aliceUser.memberId)/role" @{role='Administrator'}
$me = Request $alice GET 'api/auth/me'
Assert ($me.role -eq 'Administrator') 'role changes reflected in current session'
$null = Request $alice POST "api/admin/members/$($ownerUser.memberId)/disable" $null 403
$null = Request $owner POST "api/admin/desks/$desk2/archive"
$availability = Request $carol POST 'api/reservations/availability' @{deskId=$desk2;dates=@($dates[1]);isFullDay=$true}
Assert ($availability[0].code -eq 'archived') 'archived desk unavailable'
$notifications = Request $owner GET 'api/owner/notifications'
Assert ($notifications.Count -gt 0) 'outbox visible to Owner'
$null = Request $carol POST 'api/auth/logout' $null 204
$null = Request $carol GET 'api/auth/me' $null 401
$demoPassword = $null
Write-Output "Acceptance complete. Retained demonstration offices/accounts marked $stamp."
Write-Output 'Controlled-time lifecycle boundaries: run scripts/Verify-Prototype.cs; SQL worker restart/outbox lease and full browser flows also need the manual checklist.'
