# Deskovia — prototype implementation specification

Date: 6 October 2026. Status at the time of writing: development brief; the functionality described here had not yet been implemented. See Verification.md for current implementation and verification status.

## 1. Purpose and scope

Deskovia is a web application for booking shared workspaces in the offices of one organisation, ABC. Members select a workspace and individual dates, create reservations, confirm arrival and, when necessary, release the workspace early. Administrators manage offices, room layouts, desks and reservations.

The current version has one organisation and one SQL Server database. Company self-registration, switching organisations, individual branding and managing separate company databases are outside scope. Keep the existing Organization and OrganizationMember entities; their presence does not require a multi-company service.

All workspaces are shared. Do not add assigned desks or private offices. This is a reservation system rather than a time tracking system: arrival confirmation preserves a reservation and is not used to calculate pay or working hours.

Confirmed prototype decision: anyone can register using a name, email address and password, without email verification. The first successful registration creates Owner in the single ABC organisation; all later registrations create Employee. Microsoft Entra is not required for the application to work.

Sections labelled “Proposed solution” clarify technical or product details that were not yet agreed when this brief was written. Distinguish these from confirmed requirements. All product UI, API messages, notifications, comments and documentation must be in English.

## 2. Initial state and development constraints

Backend: `D:\mendelu\MVIS\Deskovia\DeskoviaEngine`, ASP.NET Core / EF Core 10, SQL Server, dbo schema. Projects: WebApi, Application, Domain, Infrastructure, Shared.

Frontend: `D:\mendelu\MVIS\Deskovia\DeskoviaWebClient`, Blazor Web App with Interactive WebAssembly, a server project and a Client project.

At the time of writing, entities and EF mappings existed for offices, users, reservations, notifications, audit and test payments. User-owned Init and Second migrations existed. These provided persistence foundations rather than complete business workflows. The reviewed backend Program had no authentication configuration, and the frontend had basic Blazor configuration.

Development rules:

- Preserve the project structure. Keep controllers thin; workflows and validation belong in Application, EF and external adapters in Infrastructure, entities and states in Domain, and public DTOs in Shared.
- Do not add a Tests project. The acceptance checks below are required and may be performed manually and through reproducible HTTP scenarios without a separate project.
- Do not create a database or generate/apply migrations without a separate explicit request from the project owner. Prepare the model and explain which migration is needed; the owner runs it.
- Do not manually modify existing migrations or the snapshot to bypass this agreement.
- All created text files must use UTF-8 with BOM, CRLF and spaces for indentation, normally four. Check with `pwsh -NoProfile -File scripts/Check-TextFormat.ps1`.
- Store secrets in User Secrets or environment variables, never in source files or the frontend.

The original `docs/DataModel.md` described Microsoft-only users and a mandatory subscription. Follow this specification for local authentication and current scope, and synchronise the model documentation before implementing the corresponding stage.

## 3. Registration and sign-in

### 3.1. User behaviour

Registration requires a display name, email, password and password confirmation on the form. No verification email is sent. There is no giacom.com domain restriction. The backend ignores client-supplied roles, MemberId and OrganizationId: membership is always created in configured ABC. Under the transaction lock, the first membership receives Owner and all subsequent memberships receive Employee; clients cannot choose a role.

Create the user and membership atomically. Reject duplicate registration of a normalised email; a database uniqueness constraint must also handle concurrent registrations. Normalise email consistently for registration and sign-in, without stripping dots or applying provider-specific address rules.

Registration signs the user into the application. Sign-in checks the password and whether both the user and membership are active. Use one generic failure message for invalid credentials or an unavailable account. Never log passwords or tokens.

Proposed solution: passwords contain 12–128 characters and allow spaces and Unicode; names contain 1–100 characters after trimming. Rate-limit registration and sign-in, returning 429 when exceeded. Use ASP.NET Core Identity PasswordHasher rather than custom password hashing, and support rehashing after successful sign-in.

Email is a login identifier whose ownership has not been verified. This registration is intended for the demonstration ABC organisation. Future real company access must depend on corporate identity rather than a claimed email address.

### 3.2. Sessions — proposed technical solution

Use ASP.NET Core Cookie Authentication. The session cookie is HttpOnly, Secure, SameSite=Lax and expires after eight hours without sliding renewal. Do not store passwords or authentication tokens in localStorage/sessionStorage. The cookie contains a protected framework ticket identifying UserSession; check current roles and activity on the server.

Every authenticated request verifies that the session exists, is not revoked or expired, and that the user and membership are active. Logout revokes the database session and clears the cookie. Role changes apply on the next request; disabling an account revokes all its sessions. The API returns 401/403 rather than HTML redirects to sign-in.

Enable antiforgery validation for all state-changing API requests, including registration, sign-in and logout. The client obtains a request token through a dedicated endpoint and sends it in a header; obtain a fresh token after sign-in. Do not rely solely on SameSite.

Proposed hosting setup: the browser calls the API directly with credentials; CORS allows only the exact configured frontend origin and credentials. Both applications use HTTPS localhost locally. For deployment, choose one origin through a reverse proxy or one site with subdomains; unrelated domains require a separate cookie strategy. Never combine wildcard CORS with credentials.

Use WebAssembly without server prerender for authenticated pages, or implement explicit authentication forwarding during prerender. Do not assume the frontend server has the separate API's session. Load UI identity through `/api/auth/me`; hiding a button does not replace API authorisation.

### 3.3. Model changes for local authentication

| Entity | Change |
| --- | --- |
| AppUser | Add NormalizedEmail with a unique index for the local prototype. Make MicrosoftTenantId and MicrosoftObjectId nullable. Require both to be present or both absent; enforce pair uniqueness only for present values. Never populate fictional GUIDs. |
| LocalCredential — new entity | UserId is a PK/FK to AppUser, with PasswordHash and PasswordChangedAtUtc. One local password per user. Never expose this entity through the API. |
| UserSession | Keep Id, MemberId, CreatedAtUtc, ExpiresAtUtc and RevokedAtUtc. If adopting cookie authentication, remove mandatory TokenHash: a protected framework ticket replaces a custom bearer token, while the session record provides revocation. Verify that no other consumers use TokenHash before migration. |
| OrganizationIdentityConnection | Do not create a fictional Microsoft connection for local ABC. |

User registration never creates organisations. After the user applies migrations, normal backend startup automatically creates the active ABC organisation if it is missing. This is idempotent and does not change an existing organisation, create a subscription or create privileged accounts. The first successfully registered account becomes Owner automatically, without an additional command. For an existing prototype with members but no Owner, normal startup promotes the earliest active local member (JoinedAtUtc, then Id); disabled accounts are never reactivated and an existing Owner is never replaced. The optional administrative bootstrap command may create Owner before any registration, using a locally supplied secret password with no default; it is not required for normal use. Do not reset passwords or permissions on every application start.

Email password recovery and email changes are outside the first stage. Do not create an insecure reset endpoint without identity verification. A known test account or a new registration is sufficient for demonstration.

### 3.4. Entra sign-in and daily directory checks

Separate local password validation from external identity acquisition and daily directory checks. Reservations use MemberId and do not depend on the sign-in method.

Current Entra sign-in uses a verified tenant ID + object ID pair. See EntraLogin.md for the subsequent real-login implementation that supersedes earlier demo-login decisions. Do not automatically link an external account to a local account by matching email: the local email was not verified. Account/history transfer requires a separate verified workflow. A real Microsoft application registration and a server-side secret are now required for Microsoft login. Graph permissions remain outside this change.

Local mode does not call Microsoft Graph or disable local users because they are absent from Microsoft. Administrator disabling of a local account takes effect immediately.

Proposed daily-check demonstration: a separate fake directory adapter with Active, Disabled, Deleted and Unavailable states for explicitly connected test records. It does not impersonate Microsoft sign-in. Once daily, disable only records reliably returned as Disabled/Deleted. Unavailable, timeout or incomplete responses do not indicate dismissal. Allow Owner to manually run the same check in Local/demo mode and persist the last run time and result.

On disabling: deactivate the user/membership, revoke sessions, cancel future reservations with AccountDisabled and complete current confirmed occupancy at detection time. Save changes, events and notifications consistently. Repeated runs are idempotent. Reactivating an account does not restore cancelled reservations.

## 4. Roles and access

| Action | Employee | Administrator | Owner |
| --- | --- | --- | --- |
| Offices, layouts, occupancy and colleagues' names today and in the future | Yes | Yes | Yes |
| Create, confirm, cancel and complete own reservations | Yes | Yes | Yes |
| Own history | Yes | Yes | Yes |
| All members' history | No | Yes | Yes |
| Manage offices, rooms, desks and closures | No | Yes | Yes |
| Cancel another member's reservation with a reason | No | Yes | Yes |
| Disable an ordinary Employee | No | Yes | Yes |
| Assign/remove Administrator and manage administrators | No | No | Yes |

Administrators cannot promote themselves or disable Owner or another administrator. The single active Owner cannot be disabled. If ownership transfer is implemented, it must be a separate atomic operation that never leaves the organisation without an owner.

All members can access all active ABC offices. The client cannot choose a reservation owner: the API takes MemberId from the current user. Creating reservations on behalf of another member is outside this stage.

Ordinary members see the occupant's name and interval on the map, but not other members' emails, history, session data or external identifiers. Restrict historical queries on the server.

## 5. Offices and maps

Hierarchy: Office → Room → Desk. Administrators create multiple offices, add rooms and place desks on a simple two-dimensional layout. Store room position/dimensions and desk coordinates within the room plus rotation. CAD, floor-plan uploads, 3D and per-desk equipment descriptions are not required.

Office stores a name, time zone, booking horizon in days (default 30) and a weekly schedule. Each day has one interval within that day; no interval means closed. Overnight shifts are outside scope. All UI dates use the selected office time zone; the server stores instants in UTC.

Desks must fit within their room. Desk labels are unique within a room. Archiving preserves history and excludes the resource from new reservations. Historical reservations retain access to their workspace and status.

Proposed solution: coordinate changes are allowed regardless of reservations. Block moves to another room/office when current or future reservations exist; never break the stored Office/Room/Desk chain. Time zone and schedule changes require checking existing reservations. Reject conflicting changes with affected reservations until explicit cancellation/closure. Never silently recalculate old intervals.

A closure applies to an office or one room and has an interval and reason. Preview affected reservations before confirmation. Saving a closure and resolving conflicts is one transaction: cancel future reservations and complete current confirmed occupancy when the closure takes effect immediately. For future closures, cancel overlapping reservations in full with an explicit warning. Removing a closure never automatically restores cancelled reservations.

## 6. Reservations

### 6.1. Selection and creation

A member selects an office, room, desk and individual calendar dates. These form a set of days rather than a continuous range between the first and last date. Show unavailable dates and the reasons for the selected desk.

Default to “full working day”: use that office's entire working interval on each selected date. Alternatively, select hours within working hours. Always persist StartsAtUtc and EndsAtUtc, plus IsFullDay. Different daily schedules result in corresponding different full-day intervals.

Proposed form solution: one submission uses one desk and one time mode for all selected dates; different desks or hours require separate submissions. Use 15-minute increments and a minimum planned duration of 30 minutes, matching the prepared model.

Horizon: from today through today + BookingHorizonDays inclusive in the office time zone. “30 days” means 30 days, not a calendar month. Past times cannot be booked. Proposed solution: once today's office has opened, offer hours starting at the next 15-minute boundary; do not backdate a full-day reservation or introduce a hidden “start now” exception.

Hourly conflicts use the selected interval. A desk occupied 10:00–11:00 is available 14:00–15:00 the same day. Any overlap blocks a full-day selection. Adjacent intervals 10:00–11:00 and 11:00–12:00 are allowed.

A member cannot hold overlapping active reservations, even across offices. A desk cannot have overlapping active reservations. Closures, archiving, non-working hours, inactive accounts and the horizon also prevent creation.

The frontend prevents selecting conflicting days. The backend repeats every check when saving. If any selected date develops a conflict, create none of the submitted reservations; return 409 with dates and conflict reasons. The member adjusts the selection and resubmits. Never automatically save only the available subset.

Each day creates one Reservation. BatchId links one submission. Proposed limit: at most 31 dates per submission; a larger configured horizon does not imply an unlimited request size.

### 6.2. States and time

| Operation | Condition | Result |
| --- | --- | --- |
| Create | All checks succeed | Reserved |
| Confirm arrival | Reservation owner, start − 1 hour ≤ now < start + 5 minutes | Confirmed with ConfirmedAtUtc |
| No-show | Reserved and now ≥ start + 5 minutes | Cancelled with NoShow reason |
| Cancel | Own active reservation before confirmed occupancy, or an authorised administrator action | Cancelled with actor, time and reason |
| Complete early | Confirmed, start ≤ now < end | Completed with CompletedAtUtc; workspace released |
| Complete as scheduled | Confirmed and now ≥ end | Completed; CompletedAtUtc equals scheduled end |

A 07:00 reservation may be confirmed at 06:00. It cannot be confirmed at 07:05. Early confirmation does not move the occupancy start. “Complete” is unavailable before the start; use cancellation instead.

StartsAtUtc/EndsAtUtc retain the original interval. Record early departure separately in CompletedAtUtc and preserve history. After completion or cancellation, the released future interval can be booked again.

Once a member's confirmed reservation has started, offer “Complete” rather than “Delete”. Administrators may cancel an active reservation with a reason; completed and cancelled records are not rewritten into another state. Repeating the same completed action creates no duplicate events or notifications.

Separate editing of the date, workspace or time of an active reservation is unnecessary in the first stage: cancel and create a new reservation. This is a proposed simplification; do not add editing without conflict and history rules.

### 6.3. Concurrency

Availability checking and saving must be protected from concurrent requests. SQL Server rowversion protects row updates but does not prevent two new overlapping reservations.

Accepted implementation: every ABC mutation acquires the same transaction-owned SQL Server sp_getapplock resource Deskovia:ABC:mutations:v1. After acquiring it, clear tracked state, re-read availability and save the entire date set in one transaction. Closures, archiving, schedules, registration and workers follow the same protocol. This deliberately coarse serialization prioritizes correctness for one organisation; a plain SELECT before INSERT is insufficient.

Resolve confirmation/NoShow races atomically with fresh status and server-time checks. Worker delays never extend the confirmation window. Process expired Reserved records for affected resources before availability checks so a delayed worker cannot retain a workspace indefinitely.

Create ReservationEvent and outbox entries in the same transaction. Never hold that transaction open during email delivery or calls to external services.

## 7. Notifications and background jobs

Events: creation, reminder two hours before start, and cancellation by a member/administrator/system. Messages include office, room, desk, date, time, confirmation rule and a reservation link. Late reservations created less than two hours before start receive one creation message including the reminder, without a separate overdue reminder.

Proposed solution: early confirmation cancels an unsent confirmation reminder. Cancellation/completion also cancels obsolete pending reminders. One creation email listing a batch is acceptable, but each reminder concerns a specific reservation.

Local registration does not prove email ownership. Local mode therefore defaults to a test mailbox/outbox rather than sending to arbitrary supplied addresses. The demo must expose message content and delivery status. Real SMTP requires separate configuration; registration emails remain unnecessary.

Background jobs:

- Confirmation expiry: check at least every 30 seconds; server time defines the business deadline, not worker frequency.
- Automatically complete confirmed reservations after EndsAtUtc.
- Deliver due outbox entries by ScheduledAtUtc using claims, leases, retries and error diagnostics.
- Run daily directory checks only in the selected integration/simulation mode.

Workers survive application restarts without duplicating business events. DeduplicationKey prevents duplicate enqueueing. Email delivery is normally at-least-once: a failure between delivery and recording Sent can produce a duplicate unless the provider supports idempotency. Do not promise exactly-once delivery.

## 8. Proposed API

All routes other than public authentication and antiforgery bootstrap require authorisation. Owner initialisation is not a public HTTP endpoint. Formalise DTOs in Shared and OpenAPI before connecting screens.

| Route | Purpose |
| --- | --- |
| GET /api/auth/antiforgery | Obtain an antiforgery token |
| POST /api/auth/register | Name, email, password; create first Owner or subsequent Employee and session |
| POST /api/auth/login | Email, password; create a session |
| POST /api/auth/logout | Revoke the current session |
| GET /api/auth/me | Current user, role and organisation |
| GET /api/offices | Available offices and settings |
| GET /api/offices/{id}/map | Rooms and desks |
| GET /api/offices/{id}/availability | Occupancy for a date/interval and a safe list of names |
| POST /api/reservations/availability | Check a desk and individual dates; not a save guarantee |
| POST /api/reservations/batch | Atomically create reservations: deskId, dates, isFullDay, localStart/localEnd for hourly mode |
| GET /api/reservations/mine | Own upcoming reservations/history with pagination |
| GET /api/reservations/{id} | Own reservation or administrator access |
| POST /api/reservations/{id}/confirm | Confirm arrival |
| POST /api/reservations/{id}/cancel | Cancel with an allowed reason |
| POST /api/reservations/{id}/complete | Complete own reservation |
| GET /api/admin/reservations | All reservations/history, filters and pagination |
| POST, PUT /api/admin/offices[...] | Create/update offices and schedules |
| POST, PUT /api/admin/rooms[...] | Create rooms and edit layouts |
| POST, PUT /api/admin/desks[...] | Create/update desks |
| POST /api/admin/{resource}/{id}/archive | Archive an office/room/desk and resolve reservations |
| POST /api/admin/closures/preview | Preview reservations affected by a closure |
| POST /api/admin/closures | Create a closure and resolve conflicts |
| POST /api/admin/closures/{id}/cancel | Remove a closure without restoring reservations |
| GET /api/admin/members | Members and statuses |
| POST /api/admin/members/{id}/disable | Disable after checking role hierarchy |
| POST /api/admin/members/{id}/enable | Enable without restoring old reservations |
| PUT /api/owner/members/{id}/role | Employee/Administrator; Owner only |
| POST /api/owner/demo-directory/sync | Manually run the simulation; Local/demo only |

Admin routes require Administrator or Owner; owner routes require Owner. A preview does not guarantee an unchanged list. Recheck on save; if additional affected reservations appear, return 409 with an updated preview rather than cancelling reservations absent from the confirmed action.

Errors use ProblemDetails with stable codes and field errors. 400: invalid input; 401: no active session; 403: insufficient permission; 404: missing or undisclosable resource; 409: conflict/changed state; 429: rate limit. Never expose SQL, stack traces or secrets.

Take organisation and user identity from the server session. Derive reservation Office/Room from Desk and verify the organisation. DTO selections contain explicit office-local dates; the server returns UTC instants and a time zone for display. Never mix browser and office time zones.

## 9. Frontend screens

1. Sign-in and registration; navigate to offices after sign-in and to sign-in after 401, preserving a safe local return URL.
2. Office, room and date selection; a map showing availability and colleagues' names.
3. Selected-desk panel: day/hour mode, individual-date calendar, unavailable dates with explanations, and a summary before submission.
4. My reservations: upcoming/today plus separate history; confirmation, cancellation and completion only in allowed states.
5. Administration: offices, schedules, layout editor, archiving, closures and previews of affected reservations.
6. Administrator history and members; Owner also manages roles.
7. Owner demo tools: directory simulation status and test notifications. Do not show them to Employee.

Every operation has loading/error/empty states, duplicate-click protection and refreshed data after conflicts. The map must not be the only way to select a desk: provide a workspace list. Render other members' names as text rather than HTML. Never show success before a successful API response.

## 10. Application access without payments

The software is delivered for a one-time project price of CZK 90,000. This is an external project agreement, not an application transaction. Authenticated active ABC members can use the application without a subscription, paid period, trial or payment workflow. Existing role and reservation rules continue to apply.

Remove payment UI, API, adapters, DTOs, configuration and booking restrictions. Retain legacy payment/subscription entities and mappings for compatibility with the user's applied database; do not modify migrations or delete stored records. Background jobs must not process these legacy records. No new migration is needed for this functional removal.

## 11. Implementation sequence and acceptance criteria

### Stage A — local authentication and permissions

Update the local identity model, prepare bootstrap and registration/session APIs, and connect frontend forms. Tell the owner which schema changes are required; the owner runs the migration.

Acceptance: the first registration without email creates Owner in ABC and subsequent registrations create Employee; concurrent first registrations yield exactly one Owner; startup upgrades the earliest active existing account only when no Owner exists; duplicate emails are rejected; a supplied role cannot elevate privileges; logout invalidates the old session; disabled users cannot sign in; Employee receives 403 from admin APIs; JavaScript cannot access the session cookie; state-changing requests without antiforgery tokens are rejected.

### Stage B — offices and maps

Acceptance: an administrator creates two offices, rooms and desks with different schedules; Employee sees both offices but cannot change them through the API. Coordinates survive reload; archived desks cannot be booked.

### Stage C — reservations and concurrency

Acceptance: three selected dates create three reservations with one BatchId; full-day intervals match each day's schedule; a conflict after selection returns 409 without partial persistence; two concurrent requests for one desk yield only one successful reservation; one member cannot occupy two desks simultaneously; adjacent intervals are allowed. Check today, today+30 and today+31 limits and office-local working hours.

### Stage D — lifecycle and notifications

Acceptance with controlled server time (for example, TimeProvider): for a 07:00 reservation, reject confirmation at 05:59, allow it at 06:00 and 07:04:59, and reject it at 07:05. A confirm/NoShow race produces one state. Early departure releases the remainder and preserves history. Schedule reminders two hours before start; late creation produces no second reminder. Restarting workers does not duplicate events.

### Stage E — administration and directory simulation

Acceptance: closing a room cancels only affected reservations with reasons; repeated processing is safe; Employee cannot access other members' history. Disabled in the fake directory revokes sessions and resolves reservations; Unavailable disables nobody; reactivation does not restore cancellations. Daily processing never disables ordinary Local users without a simulated directory record.

### Stage F — verification and handover

Verify that booking works without subscriptions or payment and that legacy paid periods cannot restrict access. Run the acceptance suite and update OpenAPI and setup documentation for the user-applied database, automatic ABC/first-Owner setup, both applications, test mailbox and directory simulation.

## 12. Actions that must not be performed automatically

- Do not present real Entra or real payment charges as mocks.
- Do not create fictional Microsoft identities for local users.
- Do not introduce organisation registration, individual branding or routing between company databases.
- Do not turn arrival confirmation into time tracking.
- Do not generate or apply migrations on behalf of the project owner.
- Do not assume EF mappings alone prevent every business conflict.

## 13. Technical references

Official Microsoft documentation for the chosen mechanisms:

- [Cookie authentication without the full Identity model](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).
- [Blazor authentication and authorisation](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0).
- [PasswordHasher.VerifyHashedPassword](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1.verifyhashedpassword?view=aspnetcore-10.0).
- [CSRF protection in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

## Database decision update, 8 October 2026

At the user request, SQL Server (managed through SSMS) replaces PostgreSQL for runtime persistence. The user subsequently removed PostgreSQL and all old migrations to start with a clean SQL Server database. Runtime and tooling use one DeskoviaDbContext; new migrations belong in Migrations. No data transfer is required. Setup is documented in PrototypeRunbook.md.

## Demo authentication decision update, 11 October 2026

The previously accepted email-only demo has been superseded by genuine single-tenant Microsoft Entra ID authentication. The email-only endpoint and timer are removed. See EntraLogin.md for authorization code flow with PKCE, server-side token validation, new-account roles, email collision handling, secret configuration and acceptance scenarios. Local password login remains available; existing users and history are preserved.
