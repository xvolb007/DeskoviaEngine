# Data model

These are EF Core model definitions for the agreed first version, using the default
SQL Server dbo schema. This change does not create tables, scaffold a migration,
modify the existing Init migration, or connect to a database.

## Tables

| Table | Purpose |
| --- | --- |
| Organizations | Company and archival flag |
| Users | Unique normalized local email, nullable genuine Microsoft identity, display name and activity |
| LocalCredentials | Framework password hash and changed-at timestamp; PK/FK UserId |
| DemoDirectoryRecords | Explicitly connected local demo members: Active/Disabled/Deleted/Unavailable |
| DirectoryRuns | Durable time and result of the latest daily demonstration check |
| OrganizationMembers | User participation, Employee/Administrator/Owner role and activation |
| OrganizationIdentityConnections | Reserved for future genuine corporate Entra integration; no fake local connection |
| UserSessions | Revocable eight-hour cookie sessions; ticket protected by ASP.NET, no TokenHash |
| Offices | Organization, timezone, booking horizon and archival flag |
| Rooms | Office, room placement and dimensions on the office map |
| Desks | Room, label, position, rotation and archival flag |
| OfficeWorkingHours | One opening interval per weekday, expressed in office local time |
| OfficeClosures | UTC interval closing an office or one of its rooms, reason and author |
| Reservations | One desk and one employee for one dated interval, lifecycle timestamps |
| ReservationEvents | Append-only history of creation, check-in, completion and cancellation |
| EmailNotifications | Email outbox with schedule, deduplication key, retry state and worker lease |
| OrganizationSubscriptions | One monthly subscription per organization, price and paid period, no trial |
| Payments | Test payment attempts, period, amount/currency snapshot and provider identifiers |
| PaymentWebhookEvents | Provider event deduplication and processing state |
| AuditEntries | Administrative actions and their author; sanitized details only |

## Relationships and isolation

- Local registration normalizes trimmed email with ToUpperInvariant, stores an Identity
  PasswordHasher credential and creates membership atomically in the sole ABC. The first
  account receives Owner under the transaction lock; all later accounts receive Employee.
  Mail ownership is not verified. Both external GUIDs are null for local accounts.
- Future genuine Entra identity uses tenant ID + object ID, not an unverified email;
  automatic linking by matching local email is prohibited. Roles belong to membership.
- Membership has a unique organization/user pair. A filtered unique index permits at
  most one active Owner per organization. ABC is created automatically at normal startup. The first registration establishes
  Owner atomically; the unique index alone does not implement this workflow. An existing
  ABC with no Owner promotes its earliest active local member at startup (JoinedAtUtc,
  then Id). Existing Owners are never replaced, and disabled accounts are not reactivated.
  Optional local bootstrap is retained for creating Owner before normal registrations.
  Owner cannot be disabled/demoted; ownership transfer is not exposed.
- Reservations include organization, office, room and desk identifiers. Composite foreign
  keys ensure the full location chain is consistent and the member belongs to that
  organization. These are validated references, not independent choices.
- Composite foreign keys also keep closure authors, reservation events, notifications
  and payments in their organization. A null actor means a system action.
- Foreign keys restrict deletion to preserve history. Use activation, archival and
  cancellation fields rather than deleting records referenced by history.
- These constraints do not replace authorization. Services must scope all reads and
  writes to the authenticated membership and enforce Owner/Administrator permissions.

## Booking semantics

- StartsAtUtc and EndsAtUtc preserve the original booking interval [start, end).
  CompletedAtUtc records early departure or normal completion without overwriting it.
- Each selected day is a separate reservation. BatchId groups one submission without
  imposing a continuous date range. Validate and save the entire batch atomically.
- Full-day booking resolves that date's office schedule into concrete UTC timestamps.
  Changes to working hours must not silently change existing reservations.
- A missing working-hours row means closed. DayOfWeek follows .NET: Sunday = 0.
  One interval within a day is supported; overnight schedules are outside this version.
- Office timezone and calendar date determine the 30-day default horizon, including
  today + 30 days. Services validate timezone identifiers, DST, 15-minute selection
  steps and a minimum scheduled duration of 30 minutes.
- Check-in is allowed from start - 1 hour until strictly before start + 5 minutes.
  Unconfirmed reservations are cancelled at the deadline. Confirmation does not permit
  occupying a desk earlier than the scheduled start.
- Reserved -> Confirmed -> Completed, or Reserved/Confirmed -> Cancelled.
  Store the cancellation reason and create a history event in the same transaction.
- Early completion releases the remainder; at the scheduled end the worker completes
  confirmed reservations automatically. Before start, use cancellation, not completion.
- Reminders are scheduled at start - 2 hours. For late-created bookings send a single
  creation message instead. Cancel pending reminders when their reservation is cancelled.
- Administrative disable immediately revokes sessions and resolves active reservations.
  Daily fake-directory checks only explicitly connected demo members. Disabled/Deleted
  disable; Unavailable, failed or incomplete responses do not. Confirmed ongoing bookings
  end at detection. Ordinary local users are not queried against Microsoft. Restoring
  an account never restores cancelled bookings.
- Room/office closure creation and desk archival must resolve affected reservations,
  append events and enqueue notifications atomically.

## What the database checks, and what is still service work

Configurations enforce referential integrity, tenant-consistent references, unique desk
labels per room, member/identity uniqueness, enum values, positive time intervals,
reservation timestamp consistency, nonnegative amounts and valid currency format.
Row versions protect updates of memberships, reservations, subscriptions, payments and
notification jobs against lost updates. They do NOT prevent overlapping inserts.

Application now checks both desk/member overlaps after a stable SQL Server transaction-owned
sp_getapplock application lock shared by every ABC mutation (including schedule, closures and workers).
The whole selected-date batch commits atomically. This deliberately coarse organization
serialization favors correctness over throughput. byte[] rowversion protects updates additionally;
direct SQL changes must follow the same protocol. Working hours, closures, entitlement,
membership and lifecycle transitions are revalidated inside the locked transaction.

The implemented worker checks lifecycle every 15 seconds, dispatches local outbox with
a two-minute durable lease and up to five retries, and runs daily fake-directory sync.
No Microsoft Graph/SMTP calls are made in Local mode. Deduplication keys and row versions
protect durable work. Local simulated payment events use a clearly separate provider;
real sandbox webhook signature verification remains an external integration.
Mail bodies, audit details and errors must not contain passwords or access tokens.
DeltaLink is internal synchronization state and must never be exposed by public DTOs.

Subscriptions start PendingPayment. Paid periods are half-open intervals; new bookings
require an active paid period. Existing bookings remain manageable after expiry.
Billing.Enabled=false is technical entitlement bypass, not a trial. Configurable demo
MonthlyPrice=499 and Currency=CZK are explicitly demonstration values. Owner controls
the LocalSimulator success/failure operation with durable idempotency. IsTest=true;
real charging is outside this project.
Validate webhook signatures and deduplicate provider events before changing payment
or subscription state. Do not trust a browser redirect as proof of payment.

## User-run migration

Runtime persistence uses DeskoviaDbContext, dbo and Latin1_General_100_BIN2
collation for exact normalized identifier comparisons. DateTimeOffset values map to
datetimeoffset; membership, reservation, subscription, payment and outbox concurrency
tokens map to SQL Server rowversion. Constraint expressions and filtered indexes use
SQL Server syntax. The common application-lock resource is Deskovia:ABC:mutations:v1.

There is one SQL Server context and the standard dbo.__EFMigrationsHistory table. The user removed PostgreSQL and its old migrations to start fresh. Generate/apply migrations yourself; no context selector or data transfer is needed.

In Visual Studio use WebApi as startup project and Infrastructure as default project:

~~~powershell
Add-Migration SqlServerInitial -OutputDir Migrations
Update-Database
~~~

No migration, database creation/update or data transfer was executed by the developer.
Exact connection instructions and acceptance: [PrototypeRunbook.md](PrototypeRunbook.md).

## Real Entra sign-in

AppUser.MicrosoftTenantId and MicrosoftObjectId now hold verified Entra claims after server-side OpenID Connect authentication. Their existing pair constraint and filtered unique index remain unchanged; no extra migration is required. New Entra users have no LocalCredential. Account linking is removed. Existing Microsoft identities continue to work, with no email-only matching or Microsoft-derived Deskovia roles. See EntraLogin.md for configuration and recovery boundaries.

## Legacy payment storage

OrganizationSubscription, Payment and PaymentWebhookEvent mappings are retained only for compatibility with existing applied migrations. The application no longer reads or changes these records for billing and exposes no payment workflow. Their presence does not restrict booking. No schema change or data deletion accompanies payment removal.
