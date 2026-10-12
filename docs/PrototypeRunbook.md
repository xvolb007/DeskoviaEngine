# Deskovia: functional prototype setup and acceptance

## Implemented scope

One ABC organisation, local registration without email verification, first-account Owner and subsequent Employee roles, Cookie Authentication, revocable eight-hour sessions, current roles/activity checks, antiforgery and registration/sign-in rate limits. Normal backend startup automatically prepares active ABC after migrations, without a setup command or payment. The first successfully registered account becomes Owner automatically; later registrations create Employee. No Owner setup command is required.

Workflow: offices → rooms → layouts and desks → availability for individual dates → atomic batch → confirmation/cancellation/completion → personal and administrator history. Administrators manage schedules, closures with confirmed previews, archiving and member disabling. Owner manages administrators, the demo directory, test mailbox and payment simulation.

Entity and mapping changes are prepared. The developer did not create a database, generate/apply migrations or edit existing migrations/snapshots. No Tests project was added. UI, API messages, notifications and documentation use English. Application culture is en-GB; office time zones continue to define booking dates and times.

## Configuration and user-applied migration

Requirements: .NET SDK 10, a running SQL Server instance (SSMS manages that instance) and a trusted local HTTPS certificate. Use a new SQL Server database. The user removed the previous database and migrations; no data transfer is required. Run from DeskoviaEngine in Windows PowerShell:

~~~powershell
dotnet dev-certs https --trust
.\scripts\Configure-Database.ps1 -ServerName 'YOUR_SSMS_SERVER_NAME'
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef migrations add SqlServerInitial --project Infrastructure --startup-project WebApi --output-dir Migrations -- --environment Development
~~~

The configuration script uses Windows Authentication by default and stores ConnectionStrings:DeskoviaSqlServer in WebApi User Secrets. Pass -Username for SQL Server Authentication; the password is prompted securely. The default database name is Deskovia; override with -Database. For a local development server with a self-signed certificate, explicitly pass -TrustServerCertificate if needed. Production must validate the server certificate. No password belongs in appsettings.json or Git.

Review the newly generated SQL Server migration, then run:

~~~powershell
dotnet ef database update --project Infrastructure --startup-project WebApi -- --environment Development
dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project WebApi -- --environment Development
~~~

In Visual Studio Package Manager Console, select Infrastructure as default project and WebApi as startup project:

~~~powershell
Add-Migration SqlServerInitial -OutputDir Migrations
Update-Database
~~~

SQL Server uses one DeskoviaDbContext, dbo, binary collation Latin1_General_100_BIN2, datetimeoffset timestamps and byte[] rowversion concurrency tokens. Migration history uses dbo.__EFMigrationsHistory. No PostgreSQL provider or second context is retained; -Context/--context is unnecessary.

The ConnectionStrings:DeskoviaSqlServer key is visible in WebApi/appsettings.json. Edit the local connection in Visual Studio by right-clicking WebApi and choosing Manage User Secrets, or use Configure-Database.ps1 above. The design-time factory explicitly loads User Secrets even when Visual Studio uses its default environment. Example for Windows Authentication, with local-only certificate trust:

~~~json
{
  "ConnectionStrings": {
    "DeskoviaSqlServer": "Server=YOUR_SSMS_SERVER_NAME;Database=Deskovia;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
  }
}
~~~

Replace the server name with the value from SSMS. JSON needs a doubled backslash for a named instance, such as SERVER\SQLEXPRESS. Production must validate the certificate. Keep passwords outside tracked files.

No database or migration was created/applied by the developer. SSMS manages a running SQL Server instance; the configured login needs access and sufficient migration permissions. Recreating a missing database also needs server permission. Once an initial migration exists, a deliberate database reset requires only Update-Database; do not generate Init again for every reset.

Once migrations are applied, simply start both applications normally. Backend startup creates ABC before starting background jobs or accepting HTTP requests. Repeated starts do not duplicate ABC or change existing organisation data. The default Billing.Enabled=false requires no payment or subscription activation; no trial subscription is created. If the existing organisation was deliberately archived, startup preserves that status.

### Automatic Owner account

Register normally: the first account in ABC receives Owner and can manage offices, roles and demo tools immediately. Later accounts receive Employee. The backend selects the role inside the same locked transaction as registration, so concurrent first registrations cannot produce two Owners.

If you already registered accounts under the earlier version and no Owner exists, restart the updated backend: it promotes the earliest active local member automatically (JoinedAtUtc, then Id). Existing passwords/sessions are preserved; refresh the UI or sign in again to display the updated role. Disabled accounts are never reactivated, archived ABC is preserved, and an existing Owner (even inactive) is never replaced automatically.

The following optional command is only for creating an Owner before anyone registers. It is unnecessary for normal setup:

~~~powershell
.\scripts\Initialize-Owner.ps1 -Email "owner@example.test"
~~~

Owner initialisation prompts for a password without echo, passes it to the child process through a temporary environment variable and restores the environment. It does not store the password in Git. Repeating it for the same Owner preserves the password and role. A different occupied email or an existing other Owner is rejected. It creates no Microsoft connection. If ABC already exists under a different Id, explicitly set Prototype:OrganizationId to that Id before bootstrap; the application does not provide organisation registration.

## Microsoft Entra ID sign-in

The email-only demo flow has been removed. See [EntraLogin.md](EntraLogin.md) for real single-tenant sign-in, local secret configuration and account identity rules. No new migration is required.

## Running the applications

First terminal, DeskoviaEngine:

~~~powershell
dotnet build
dotnet run --project WebApi --launch-profile https
~~~

Second terminal, DeskoviaWebClient:

~~~powershell
dotnet build
dotnet run --project DeskoviaWebClient --launch-profile https
~~~

Open https://localhost:7178. API: https://localhost:7095. Development OpenAPI: https://localhost:7095/openapi/v1.json.

Public API snapshot with DTOs and error schemas: [OpenApi.json](OpenApi.json). The live document is generated from current controllers and Shared.

The frontend sends credentials and X-CSRF-TOKEN directly to the API. Authenticated pages execute in WebAssembly without prerender. Client references the shared contract through `../../DeskoviaEngine/Shared/Shared.csproj`; keep the repositories adjacent or adjust this reference when moving them.

Configuration:

| Setting | Purpose |
| --- | --- |
| ConnectionStrings:DeskoviaSqlServer | Secret: User Secrets/environment only, never the frontend |
| Prototype:OrganizationId | Single ABC; default abc00000-0000-0000-0000-000000000001 |
| Prototype:FrontendOrigin | Exact HTTPS origin; default https://localhost:7178 |
| Prototype:DemoDirectoryEnabled | Enables the fake directory; default true |
| ApiBaseUrl (Client/wwwroot/appsettings.json) | HTTPS API URL; default https://localhost:7095/ |
| Billing:Enabled | false technically disables subscription enforcement; this is not a trial |
| Billing:DemoMonthlyPrice | Demonstration value 499 |
| Billing:Currency | Demonstration value CZK |

With Billing.Enabled=true, new intervals must fit entirely inside an active paid period, which must also be active now. Existing reservations remain manageable without a subscription. Owner pays for ABC through a clearly labelled local success/failure simulator. An operation key returns the same payment even if retried with a different requested outcome. Success extends the period once; failure does not activate a subscription. No card data or real charges are involved.

For deployment, use one origin/reverse proxy or compatible HTTPS subdomains; CORS has no wildcard. Multiple API instances require a shared ASP.NET Data Protection key ring and identical application name configured by hosting. SQL Server application locking is already shared between instances, but cookie keys must be shared separately. The `--verify-host` switch disables both automatic ABC initialisation and the worker solely for HTTP host verification without a database; normal startup does not use it.

## Automated checks without a database

~~~powershell
dotnet run --file scripts/Verify-Prototype.cs
.\scripts\Check-TextFormat.ps1
~~~

This is an SDK 10 C# file referencing Infrastructure, **not a separate Tests project**. PublishAot=false avoids unnecessary NativeAOT downloads. It checks the EF model without connections or executing DDL and Application workflows using a transactional in-memory store and controlled TimeProvider. Checks cover automatic ABC initialisation, first-account Owner assignment, concurrent registration under an in-memory transaction lock, automatic upgrade of existing accounts without Owner, preservation of existing organisation state, registration, roles, bootstrap, session revocation, individual dates/BatchId, atomic batches, adjacent intervals, horizon, exact time boundaries, early release, closures, directory checks, outbox and idempotent payments. It also checks real Microsoft identity creation, email collision handling, stable identity despite email changes, disabled accounts, session expiry/revocation and concurrent duplicate sign-ins. It does not prove SQL concurrency or browser cookie behaviour.

For HTTP verification without a database, start the API with an intentionally unavailable connection and worker disabled:

~~~powershell
dotnet run --project WebApi --launch-profile https -- "--ConnectionStrings:DeskoviaSqlServer=Server=127.0.0.1,1;Database=unused;Integrated Security=true;Encrypt=true;Connect Timeout=1" --verify-host
~~~

With the frontend running:

~~~powershell
pwsh -NoProfile -File scripts/Verify-Host.ps1
~~~

This checks anonymous antiforgery, Secure/HttpOnly, 401 without redirects, CSRF, validation ProblemDetails, CORS, live OpenAPI, 429 and the Blazor shell. Stop this API afterwards and start normally with the real connection. Rate limits are per-instance in memory; do not run sign-in acceptance immediately after exhausting the limit (wait a minute or restart the demonstration API).

Also run from the frontend repository:

~~~powershell
.\scripts\Check-TextFormat.ps1
~~~

## HTTP acceptance after your migration

Use a separate demonstration database. The following script creates timestamp-labelled accounts/offices/reservations, changes roles, runs demo sync and simulates payments. Data and history are retained; other data is not deleted. It uses current ABC demo records, so Disabled/Deleted records may be disabled during the manual check.

~~~powershell
pwsh -NoProfile -Command '& ./scripts/Accept-Prototype.ps1 -OwnerCredential (Get-Credential -Message "Existing ABC Owner")'
~~~

The scenario checks A–F through the API, including a real race between two HTTP requests: exactly 200 + 409 is expected. It also checks SQL Server batch atomicity, adjacent intervals, 403/404, database-backed roles, closures with refreshed previews, directory processing, payment idempotency and logout. The developer does not run it before your migration. Use the additional scenarios below for real SQL Server confirm/NoShow races and lease/restart checks.

## Manual UI and background-job checklist

1. Owner signs in and creates two offices with different schedules, rooms and desks. Edit geometry and sliders, save and reload: coordinates match. Employee sees the list and map; other names are escaped text. Desks can also be selected from a list.
2. Employee selects three individual working dates. The summary shows three office-local intervals converted from UTC. Creation produces three Reservations sharing one BatchId. After a concurrent conflict, the UI shows 409 and refreshed availability without partial success.
3. Today's full working day becomes unavailable after opening. Select the next 15-minute boundaries with at least 30 minutes. Check today+30 and today+31 in the office time zone, closed days and different working hours. Invalid/ambiguous DST times are rejected.
4. For 07:00 office-local start: 05:59 fails, 06:00 succeeds, 07:04:59 succeeds, 07:05 fails. The C# runner already checks boundaries with controlled time. For integration races, run confirmation and worker operations at test time by replacing TimeProvider in the test host's DI; there is no public time-changing endpoint. Under real time the worker checks no-shows every 15 seconds, regardless of when confirmation is clicked.
5. Confirmed reservations before start can be cancelled but not completed early. After start, Employee completes rather than deletes the reservation. The original interval remains and CompletedAt records departure. Booking the remainder is allowed subject to 15-minute increments and current time.
6. Early creation schedules a reminder for start−2h. Late creation includes the reminder in the creation message and produces no separate reminder. Early confirmation/cancellation/completion cancels unsent reminders. Owner sees content and status in the test mailbox. Nothing is sent to unverified email addresses.
7. Stop and restart the normal API: ReservationEvent records are not duplicated and pending outbox processing resumes. For lease recovery in a test database, stop after claim; Processing records with expired LockedUntil are reclaimed. The local sink sends nothing externally; a real provider may deliver twice if a failure occurs between sending and recording Sent (at-least-once).
8. Closures show a preview. A new overlapping reservation between preview/save produces 409 and a refreshed list. Immediate closures complete current Confirmed occupancy; future closures cancel entire overlapping intervals. Removing a closure restores no reservations. Archiving preserves history and resolves active reservations.
9. Administrator manages Employee but cannot manage Owner/Administrator. Owner assigns/removes Administrator. Disabling revokes sessions, cancels future reservations and completes current confirmed occupancy. Demo Unavailable/incomplete responses do not disable anyone; ordinary local accounts are not included in the directory. Manual reactivation restores no reservations.
10. Enable Billing.Enabled on the backend and restart. New reservations are unavailable without payment. Failure activates no period; success activates one; repeating a key does not extend it twice. Existing reservations remain manageable after expiry. The UI explicitly labels the simulator.
11. Stop the API: forms show network errors; empty offices/reservations/notifications show empty states; loading prevents double submissions. A 401 navigates to sign-in with a safe local return URL. Check browser registration/sign-in/logout, cookies and absence of passwords/tokens in localStorage/sessionStorage.

## Accepted architecture decisions

- Preserve WebApi/Application/Domain/Infrastructure/Shared. Controllers are thin. Application uses IWorkspaceStore; EF/mappings/PasswordHasher/adapters live in Infrastructure. Prototype IQueryable reads are synchronous; transactions and writes are asynchronous.
- All ABC mutations (registration, reservations, closures, schedules, roles, workers and leases) use one stable SQL Server transaction-owned application lock via sp_getapplock, Deskovia:ABC:mutations:v1. Clear the change tracker after acquiring the lock and re-read checks. This is correct but deliberately coarse serialisation for one organisation, with limited throughput. Direct SQL writes outside the application must follow this protocol.
- Intervals use [start,end), 15-minute increments, a 30-minute minimum and at most 31 selected dates. The calendar uses office.Today rather than the browser's date.
- Desks are visually 12-unit squares rotated around their centres. The backend checks rotated boundaries too. Existing composite alternate keys for Office/Room/Desk prevent moving existing desks between rooms; archive and create a new desk instead. This preserves the full history chain. CAD/drag-and-drop is unnecessary; the editor has coordinates, dimensions, rotation, sliders and a live preview.
- Schedule/time zone changes never recalculate old intervals; conflicting changes return affected reservations. Room shrinking cannot leave active desks outside boundaries. Outbox deduplication uses reservation/kind; durable events and audit are saved with business changes.
- LocalSimulator is an explicitly local IPaymentAdapter. Simulated events are stored in PaymentWebhookEvents under a separate provider. Redirects do not confirm payment; no public endpoint impersonates an external provider webhook.
- Real Entra sign-in is implemented; account linking is removed; Graph, automatic email linking, password reset and ownership transfer are not implemented. The single Owner is protected from disabling and demotion.
- Product language is English. Application culture is explicitly en-GB for consistent framework messages and display; office time-zone calculations remain independent of browser culture. Unicode names and passwords remain supported.

## External integrations and unverified conditions

Apply your migration and start the applications normally; ABC initialisation needs no separate command. The first registered account becomes Owner; no separate administrative setup command is required. Real SQL Server concurrency/persistence, EF SaveChanges against the new schema, end-to-end cookie sessions and the full authenticated UI are not claimed verified until then.

A real payment sandbox remains an external integration: select a provider, obtain keys, implement the replaceable adapter and server-side webhook signature/provider-event verification. Real Entra sign-in is configured as described in EntraLogin.md. Microsoft Graph and SMTP remain disconnected; email-only account linking is deliberately unavailable.

See Verification.md for completed developer checks.
