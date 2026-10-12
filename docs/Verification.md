# Implementation verification

Checks performed on 6 October 2026.

## Original functional prototype checks

- Backend and frontend `dotnet build --no-restore`: passed, zero errors and warnings before the language correction.
- `dotnet run --file scripts/Verify-Prototype.cs`: 61 passing EF model and Application checks using a transactional in-memory store and controlled time. No PostgreSQL connections, DDL or migrations were executed.
- Backend startup in `--verify-host` mode with an intentionally unavailable connection and disabled worker: passed. Frontend HTTPS startup: passed.
- HTTP checks: anonymous antiforgery, Secure/HttpOnly antiforgery cookie, protected-route 401 without redirects, rejection of login without CSRF, field errors with a stable code, credentialed CORS for the exact origin only, and 429 after the limit. Live OpenAPI contains 36 paths, DTOs and ApiProblem; a snapshot is saved in docs/OpenApi.json. The frontend serves the Blazor shell.
- UTF-8 BOM/CRLF/spaces checks in both repositories: passed.
- Accept-Prototype.ps1 syntax parsing: passed; SQL acceptance was not executed.
- The UI tool could not attach a browser tab (attach timeout; no available tabs). Visual and interactive browser verification is therefore not claimed.

## English language correction

- UI labels, API/business errors, notification content, demo diagnostics, specification, runbook and frontend README were translated to English. Both AGENTS.md files now require English project content.
- HTML uses lang="en". API and WebAssembly application cultures are explicitly en-GB. Office time zones still define booking boundaries. Displayed reservation times and calendar labels use English month names. Full WebAssembly globalisation data is included so English culture is available regardless of browser language. Unicode names/passwords remain supported.
- Both solutions built successfully with zero warnings/errors using separate temporary output directories. Ordinary output was locked by the user's running API/frontend and Visual Studio; those processes were not stopped.
- Commands used for isolated compilation:
  ~~~powershell
  dotnet build --no-restore "-p:OutDir=$env:TEMP\Deskovia-English-Backend\"
  dotnet build --no-restore "-p:OutDir=$env:TEMP\Deskovia-English-Frontend\"
  ~~~
  Run the first command from DeskoviaEngine and the second from DeskoviaWebClient.
- The file-based runner was rerun after translation: all 61 checks passed.
- Freshly built hosts were started on separate temporary HTTPS ports, with API worker disabled and an intentionally unavailable database connection. Three HTTP assertions passed: English CSRF error, English validation title and required-field messages, and a 200 frontend shell containing lang="en". These temporary hosts were stopped afterwards.
- Source/documentation scanning found no Cyrillic text outside generated output, Git metadata and third-party libraries. This includes user-owned migration sources, which were inspected by the scan but not modified.
- Text-format checks were rerun successfully for both repositories.
- Restart/rebuild the applications after stopping your active debug sessions to load the new text. Existing user-entered data and persisted historical messages were not rewritten.

## Automatic ABC startup (previous behaviour; Owner policy superseded below)

- Normal startup now initialises active ABC before the worker and HTTP server start. The same PostgreSQL transaction advisory lock protects concurrent initialisation.
- Missing ABC is inserted; existing name, creation time and archive status are preserved. No default password, Owner, payment or trial subscription is created. Default Billing.Enabled=false requires no activation.
- At this stage, registration was independent of Owner bootstrap and assigned Employee to the first registrant. The later first-account Owner change below supersedes that policy.
- Backend isolated build passed with zero warnings/errors.
- The file-based runner passed 67 checks, including six additional checks for automatic initialisation, idempotency, no credentials/subscription, registration before Owner and preservation of existing organisation state.
- The user reports applying the migrations. This change does not generate/apply migrations or modify entities/mappings; no additional migration is required.
- Automatic startup against the user's live PostgreSQL database was not executed by the developer.

## First-account Owner policy

- The user changed the role policy: the first successful registration in ABC now receives Owner automatically. Later registrations receive Employee; request payloads cannot select roles.
- Role selection occurs after acquiring the existing database transaction advisory lock and is committed with user, credential, membership, session and audit. This prevents concurrent registrations from both selecting Owner.
- Normal startup upgrades existing ABC accounts when there is no Owner: the earliest active local member (JoinedAtUtc, then Id) becomes Owner. Existing passwords and sessions remain valid; roles are read from the database on each request.
- Existing Owners, including inactive ones, are never replaced. Disabled accounts are not reactivated; archived organisation state is preserved. Promotion audit is idempotent.
- Both solutions built successfully with zero warnings/errors in temporary output directories.
- The file-based runner passed 74 checks, covering first-account Owner, rejected invalid first registration, later Employee accounts, existing-account promotion, current session roles, repeated startup, inactive Owner preservation, and two concurrent registrations using the in-memory transaction lock.
- Source, UI registration text, specification and runbooks were updated. Text-format and diff checks passed in both repositories.
- No entity/mapping changes or migrations are required. The live user database and real PostgreSQL registration race were not executed during this change.

## Compact Room/Desk layout editor

- Room and Desk forms now appear to the left of their respective live maps. The responsive layout stacks each map below its form on narrow screens.
- Numeric position, room size and desk rotation fields share a reusable SliderField component. Each number has its slider directly underneath, and both emit changes on input.
- Office previews include a new unsaved room, reflect the selected room's draft coordinates/dimensions, show origin guides and preserve saved geometry for other rooms. Room previews display existing desks plus the current desk draft with rotation.
- Existing map items support mouse selection and Enter/Space. Selection is blocked while a save is in progress; save handlers restore the returned selection directly.
- Frontend isolated build passed with zero warnings/errors. Text-format and diff checks passed; backend logic and schema were not changed.
- Browser inventory exposed only empty in-app browser/MCP surfaces, without an authenticated Deskovia tab. Interactive/visual browser verification of the editor was not performed. A manual checklist is included in the frontend README.

## SQL Server conversion, 8 October 2026

- Runtime persistence now uses Microsoft.EntityFrameworkCore.SqlServer, dbo, datetimeoffset and byte[] rowversion. Check constraints, filtered indexes and unique-conflict handling were adapted to SQL Server. All ABC mutations use one transaction-owned sp_getapplock resource.
- SqlServerDeskoviaDbContext isolates SQL Server migrations from the historical PostgreSQL context. The old migration and snapshot SHA-256 values were compared before/after and are unchanged. Npgsql is retained only for historical migration source compilation.
- SQL Server configuration is separate: ConnectionStrings:DeskoviaSqlServer. Windows PowerShell setup supports Windows Authentication or a securely prompted SQL login. No connection secret was written by the developer, and the user's PostgreSQL configuration/database was not changed.
- Backend and frontend isolated builds passed with zero warnings/errors. Backend WebApi also built with its project references in the separate SqlServerVerification configuration. This avoids files locked by the user's debug sessions.
- The file-based runner passed 82 assertions: existing business scenarios plus SQL Server model validation, rowversion metadata, exact collation, migration isolation, SQL constraint/index rendering and first-Owner/lifecycle/outbox query compilation. Generated SQL was inspected in memory only; no DDL or query was executed against a database.
- EF dbcontext info with an intentionally unavailable connection selected SqlServerDeskoviaDbContext, Microsoft.EntityFrameworkCore.SqlServer and dbo.__EFMigrationsHistorySqlServer without initialising ABC or starting workers. No migration was generated/applied.
- Fresh temporary HTTPS hosts passed all 12 Verify-Host.ps1 assertions: secure antiforgery bootstrap, four protected-route 401 responses, CSRF rejection, validation errors, allowed/denied CORS, live OpenAPI, rate limiting and frontend shell. The API used --verify-host and an intentionally unavailable SQL connection. Temporary hosts were stopped afterwards; the user's debug sessions were preserved.
- Both repositories passed UTF-8 BOM/CRLF/spaces and git diff --check. Modified PowerShell scripts passed syntax parsing.
- A new user-generated SQL Server initial migration is required even if PostgreSQL migrations were already applied. Existing PostgreSQL data export/import is not implemented. Neither a SQL Server instance nor live database operations were configured or tested by the developer.

## One SQL Server context, 11 October 2026

- The user removed PostgreSQL and all existing migrations for a clean SQL Server database. Those deletions were preserved; no migration/snapshot was restored, edited, generated or applied.
- The runtime now has one sealed DeskoviaDbContext. The secondary context and legacy design-time factory were removed, as was the Npgsql package. SQL Server uses the standard dbo.__EFMigrationsHistory.
- The factory explicitly loads WebApi User Secrets even with the default Production environment used by migration tooling. Environment and command-line values retain precedence. This follows the EF [design-time factory pattern](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation).
- The empty connection key is visible in WebApi/appsettings.json; actual local settings belong in WebApi > Manage User Secrets or the configuration script. The SQL Server key was checked for presence only and was not configured; no secret values were printed.
- Backend and frontend compilations passed with zero warnings/errors. Both repositories passed UTF-8 BOM/CRLF/spaces and git diff --check. The file runner passed all 82 assertions, including exactly one concrete DbContext and SQL Server mappings/query rendering.
- EF dbcontext list returned only DeskoviaDbContext. EF dbcontext info without --context and without an environment argument selected Microsoft.EntityFrameworkCore.SqlServer using an intentionally unavailable command-line connection. No database was accessed or created.
- Live SQL Server operations and Visual Studio Package Manager Console execution remain user-run checks after local connection setup.

## Repository package sources and demo email sign-in, 11 October 2026

- The Unionstreet HTTP source was found in the user's global NuGet.Config, where it was already marked disabled. Deskovia itself has no Unionstreet dependency. Per-repository NuGet.Config now clears inherited package sources, disabled-source settings, source mappings and audit sources and selects nuget.org. Both repositories need this because Shared is referenced across their directory boundary. Global/Visual Studio settings were not changed.
- dotnet restore --force passed in both repositories without --source. Fresh Shared/frontend restore metadata has nuget.org as its only remote source; local .NET SDK library-packs remains present. Backend and frontend compilation passed with zero warnings/errors after fixing intermediate DTO-name/Razor parsing errors.
- Development enables explicitly labelled email-only demo sign-in. The base option is false. A new email creates a passwordless account, existing identities/roles are retained, inactive users/members are rejected and the first-member Owner policy remains protected by the common SQL transaction lock. No Microsoft credential, identity token or callback is consumed.
- The SDK file runner passed 99 assertions. New checks use a deterministic TimeProvider timer to prove no account/session before five seconds, no transaction lock while waiting, first Owner/later Employee, normalized email reuse, current roles, absent password/Microsoft identity, revocable eight-hour session, disabled mode/accounts, malformed email, cancellation, archived ABC and concurrent same-email creation.
- Updated Verify-Host.ps1 passed 15 assertions with demo enabled and 15 with it disabled, including CSRF, demo options, malformed email or disabled-mode rejection, authorization, CORS, OpenAPI, rate limiting and frontend shell. Temporary API hosts used --verify-host and an intentionally unavailable SQL Server connection; no database was created/accessed successfully or migrated.
- A valid demo request against that unavailable SQL Server waited 6.97 seconds before returning server_error/500 rather than a successful session. This verifies the real backend delay and error response; it is not evidence of successful SQL persistence.
- The popup JavaScript module passed mocked Node checks for a single event-handler attachment, synchronous Microsoft portal opening with noopener/noreferrer, invalid/disabled form guards and cleanup. The fresh frontend served that module with HTTP 200. An authenticated interactive browser flow and actual popup permissions are not claimed verified.
- Live OpenAPI was refreshed with auth/options, auth/demo-login and their DTOs. Both repositories passed BOM/CRLF/spaces and diff checks. The three current user-generated migration/snapshot SHA-256 hashes were compared before/after and remain identical.
- No entity/model change or new migration is required. Successful browser sign-in with SQL Server storage remains a user-run acceptance scenario; see PrototypeRunbook.md. All temporary verification hosts were stopped afterwards.

## Microsoft popup correction

- The user requested https://login.microsoftonline.com/ directly instead of the My Apps portal. The automatic popup and manual target=_blank fallback now use that exact URL. Microsoft controls subsequent navigation inside its own tab; Deskovia does not consume the outcome.
- The prior listener was attached to the form. Blazor captures submit at document, so the .NET handler can mark the form Busy/disabled before the form-level listener checks it, skipping window.open. The popup listener now captures submit at window, before the document handler, while the native user activation is active. [Blazor event delegation source](https://github.com/dotnet/aspnetcore/blob/main/src/Components/Web.JS/src/Rendering/Events/EventDelegator.ts).
- Mocked regression checks reproduce window -> document capture order and verify opening the exact Microsoft URL before disabling the form, a single attached handler, invalid/busy/unrelated-form guards, retry and disposal. The module import uses a new cache key so a fresh page does not reuse the old module.
- Frontend build passed with zero warnings/errors; both repositories passed BOM/CRLF/spaces and diff checks. Backend authentication, delay and database model are unchanged.
- Browser popup policy and the full interactive flow remain manual acceptance items. Rebuild/restart the frontend and reload the page to load the new WebAssembly client/module, then submit a valid email and also test Enter and the manual link.

## Conditions not verified

The developer did not create a database or apply migrations. SQL SaveChanges, real reservation request races, real confirm/NoShow races, SQL Server restart/lease recovery, the complete authenticated UI and real external integrations remain unverified. Accept-Prototype.ps1 and the checklist in PrototypeRunbook.md provide reproducible checks after the user applies the schema.

The first original file-based run attempted to restore NativeAOT from an unavailable user NuGet feed. PublishAot=false was added, and subsequent runs succeeded. User NuGet settings were not changed.

## Real Microsoft Entra ID replacement — 11 October 2026

This section supersedes the earlier email-only demo-login checks and Entra-disconnected status. No migrations or snapshots were edited, generated or applied. Existing SQL Server migration hashes were checked unchanged.

- Backend WebApi and all referenced projects built in EntraVerification configuration: zero warnings/errors. Frontend built with isolated temporary output: zero warnings/errors.
- File-based Verify-Prototype.cs: 100 passing checks. The previous 82 EF/business checks are retained; demo-delay checks are replaced by 18 real-identity workflow checks: first Owner/later Employee, real GUID persistence without password, identity stability when email/name change or email is absent, refusal of email-only linking and malformed identity, authenticated linking preserving credentials/role, identity collisions, revoked originating session, fixed lifetime, disabled membership, concurrent duplicate sign-ins and archived ABC.
- API and frontend hosts started on isolated trusted HTTPS ports with --verify-host, disabled initializer/worker and an intentionally unreachable database. No SQL operation was executed.
- Verify-EntraHost.ps1: 11 passing assertions against real Microsoft's metadata/authorization endpoint: configured provider, antiforgery, correct tenant/client/code flow/backend callback, PKCE/state/nonce, secret exclusion, no session before authentication, anonymous linking rejection, forged callback and cancelled login rejection.
- Verify-Host.ps1: 15 passing HTTP checks for antiforgery, protected routes, validation, removed demo endpoint, CORS, OpenAPI, rate limiting and frontend shell. The live OpenAPI snapshot was refreshed.
- No successful real tenant login, token redemption, university consent or SQL-backed account/session persistence is claimed. These require interactive university authentication and the user's configured/applied database. Native form navigation and authenticated linking must be checked in that browser as documented in EntraLogin.md.
- Client secret was configured in local WebApi User Secrets only. Tenant/client IDs are public configuration; no client secret or secret ID is committed. Rotate the credential disclosed in chat.
- Real Graph daily account checks, SMTP and a payment sandbox remain separate integrations.

- An additional isolated host with Entra disabled passed two fail-closed checks (provider unavailable and no Microsoft redirect/session). The configured secret was scanned against both Git-visible repository file sets and was absent. The query-mode callback preserves existing SameSite=Lax sessions for linking; final Microsoft challenge checks assert that response mode.

## Account linking removed — 12 October 2026

The user requested removal of Sign-in settings and all Microsoft account-linking functionality. The navigation entry, linking form/query notices, challenge parameter, protected-state linking branch and Application mutation were removed. Existing verified Microsoft identities, roles and history are preserved. Email collisions reject with email_exists instead of suggesting linking; no automatic email attachment is introduced. Earlier linking acceptance entries are superseded.

Backend and frontend builds passed with zero warnings/errors. The file-based runner passed 99 checks, including preservation of local accounts on email collision, later Microsoft Employee roles and Microsoft-session logout. Verify-EntraHost.ps1 passed 10 HTTP assertions without database access; the old linking assertion was removed. The OpenAPI snapshot and current login documentation were refreshed. No migrations, snapshots or database data were changed. Authenticated browser acceptance is not claimed by these checks.
