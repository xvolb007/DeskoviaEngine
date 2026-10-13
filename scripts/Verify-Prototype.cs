#:property PublishAot=false
#:project ../Infrastructure/Infrastructure.csproj

using System.Text.Json;
using Deskovia.Application.Interfaces;
using Deskovia.Application.Services;
using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;
using Deskovia.Infrastructure.Identity;
using Deskovia.Infrastructure.Notifications;
using Deskovia.Infrastructure.Persistence;
using Deskovia.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

// File-based acceptance runner, not a Tests project. No database connections or migrations.
var checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + name);
    checks++; Console.WriteLine("PASS: " + name);
}
async Task Reject(Func<Task> work, int status, string code)
{
    try { await work(); throw new InvalidOperationException("Expected rejection: " + code); }
    catch (BusinessException e) { Check(e.Status == status && e.Code == code, "reject " + code); }
}
using (var db = new DeskoviaDbContext(new DbContextOptionsBuilder<DeskoviaDbContext>().UseSqlServer("Server=127.0.0.1,1;Database=unused;Integrated Security=true;Encrypt=true;Connect Timeout=1").Options))
{
    var model = db.Model;
    Check(model.FindEntityType(typeof(LocalCredential)) != null, "EF model validates and maps local credentials");
    Check(model.GetDefaultSchema() == "dbo", "default dbo schema");
    var userType = model.FindEntityType(typeof(AppUser))!;
    Check(userType.FindProperty(nameof(AppUser.MicrosoftObjectId))!.IsNullable, "nullable real external identity");
    Check(userType.GetIndexes().Any(x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == nameof(AppUser.NormalizedEmail)), "unique normalized login");
    Check(model.FindEntityType(typeof(UserSession))!.FindProperty("TokenHash") == null, "cookie session has no bearer hash");
    var rowVersion = model.FindEntityType(typeof(Reservation))!.FindProperty(nameof(Reservation.Version))!;
    Check(rowVersion.IsConcurrencyToken && rowVersion.ClrType == typeof(byte[]) && rowVersion.GetColumnType() == "rowversion", "SQL Server rowversion updates");
    Check(db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetCollation() == "Latin1_General_100_BIN2", "binary collation preserves exact normalized identifiers");
    Check(typeof(DeskoviaDbContext).Assembly.GetTypes().Count(x => !x.IsAbstract && typeof(DbContext).IsAssignableFrom(x)) == 1, "exactly one concrete database context");
    var createSql = db.Database.GenerateCreateScript();
    Check(createSql.Contains("[dbo].[Reservations]") && createSql.Contains("DATEADD(minute, 30, [StartsAtUtc])"), "SQL Server table and interval constraint rendering without execution");
    Check(!createSql.Contains("INTERVAL '") && !createSql.Contains("timestamp with time zone") && !createSql.Contains(" = TRUE") && !createSql.Contains(" ~ "), "no PostgreSQL-only SQL in current model");
    Check(createSql.Contains("[Role] = 'Owner' AND [IsActive] = 1"), "SQL Server filtered unique active Owner index");
    Check(createSql.Contains("[MicrosoftTenantId] IS NULL AND [MicrosoftObjectId] IS NULL"), "SQL Server nullable external identity constraint");
    Check(createSql.Contains("Latin1_General_100_BIN2 NOT LIKE"), "SQL Server uppercase currency constraint");
    var sampleOrganizationId = Guid.Parse("abc00000-0000-0000-0000-000000000001");
    var ownerSelectionSql = (from member in db.OrganizationMembers
        join user in db.Users on member.UserId equals user.Id
        join credential in db.LocalCredentials on user.Id equals credential.UserId
        where member.OrganizationId == sampleOrganizationId && member.IsActive && user.IsEnabled
        orderby member.JoinedAtUtc, member.Id
        select member).Take(1).ToQueryString();
    Check(ownerSelectionSql.Contains("TOP") && ownerSelectionSql.Contains("[dbo].[LocalCredentials]"), "SQL Server first Owner query compiles without database");
    var sampleNow = new DateTimeOffset(2026, 10, 6, 4, 0, 0, TimeSpan.Zero);
    var lifecycleSql = db.Reservations.Where(x => x.Status == ReservationStatus.Reserved && x.StartsAtUtc.AddMinutes(5) <= sampleNow || x.Status == ReservationStatus.Confirmed && x.EndsAtUtc <= sampleNow).ToQueryString();
    Check(lifecycleSql.Contains("SELECT"), "SQL Server lifecycle query compiles without database");
    var leaseSql = db.EmailNotifications.Where(x => x.AttemptCount < 5 && x.ScheduledAtUtc <= sampleNow && (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Processing && x.LockedUntilUtc <= sampleNow)).OrderBy(x => x.ScheduledAtUtc).Take(25).ToQueryString();
    Check(leaseSql.Contains("TOP"), "SQL Server outbox lease query compiles without database");

}
var startupClock = new ManualClock(new DateTimeOffset(2026, 10, 6, 2, 0, 0, TimeSpan.Zero));
var startupStore = new MemoryStore();
var startupOptions = new PrototypeOptions();
var startupContext = new WorkspaceContext(startupStore, startupClock, startupOptions);
var initializer = new OrganizationService(startupContext);
await initializer.EnsureInitializedAsync(default);
var startupOrganization = startupStore.Query<Organization>().Single();
Check(startupOrganization.Id == startupOptions.OrganizationId && startupOrganization.Name == "ABC" && !startupOrganization.IsArchived, "startup creates active ABC without Owner bootstrap");
var createdAt = startupOrganization.CreatedAtUtc;
startupClock.Current = startupClock.Current.AddDays(1);
await initializer.EnsureInitializedAsync(default);
Check(startupStore.Query<Organization>().Count() == 1 && startupOrganization.CreatedAtUtc == createdAt, "startup initialisation is idempotent");
Check(!startupStore.Query<OrganizationMember>().Any() && !startupStore.Query<LocalCredential>().Any() && !startupStore.Query<OrganizationSubscription>().Any(), "startup creates no default credentials, privileged users or trial subscription");
var startupAuth = new AuthService(startupContext, new LocalPasswordService());
await Reject(() => startupAuth.RegisterAsync(new("Invalid first account", "invalid-first@example.test", "short"), default), 400, "invalid_password");
var firstRegistration = await startupAuth.RegisterAsync(new("First employee", "first@example.test", "first employee password"), default);
Check(firstRegistration.User.Role == "Owner" && startupStore.Query<OrganizationMember>().Count(x => x.Role == MemberRole.Owner) == 1, "first registration becomes Owner without a setup command");
startupClock.Current = startupClock.Current.AddMinutes(1);
var laterRegistration = await startupAuth.RegisterAsync(new("Later employee", "later@example.test", "later employee password"), default);
Check(laterRegistration.User.Role == "Employee", "later registration remains Employee");
var firstMember = startupStore.Query<OrganizationMember>().Single(x => x.Id == firstRegistration.User.MemberId);
firstMember.Role = MemberRole.Employee;
await initializer.EnsureInitializedAsync(default);
Check(firstMember.Role == MemberRole.Owner && startupStore.Query<OrganizationMember>().Count(x => x.Role == MemberRole.Owner) == 1, "startup promotes earliest existing active account when no Owner exists");
Check(startupAuth.ValidateSession(firstRegistration.Session.Id)?.Role == "Owner", "existing session reflects automatic Owner assignment");
var ownerAuditCount = startupStore.Query<AuditEntry>().Count(x => x.Action == "InitializeOwner");
await initializer.EnsureInitializedAsync(default);
Check(startupStore.Query<AuditEntry>().Count(x => x.Action == "InitializeOwner") == ownerAuditCount && firstMember.Role == MemberRole.Owner, "startup preserves Owner and does not duplicate promotion audit");
firstMember.IsActive = false;
await initializer.EnsureInitializedAsync(default);
Check(!firstMember.IsActive && startupStore.Query<OrganizationMember>().Single(x => x.Id == laterRegistration.User.MemberId).Role == MemberRole.Employee, "inactive Owner is neither reactivated nor replaced");
firstMember.IsActive = true;

var concurrentStore = new MemoryStore();
var concurrentContext = new WorkspaceContext(concurrentStore, startupClock, startupOptions);
await new OrganizationService(concurrentContext).EnsureInitializedAsync(default);
var concurrentRegistrations = await Task.WhenAll(
    Task.Run(() => new AuthService(new WorkspaceContext(concurrentStore, startupClock, startupOptions), new LocalPasswordService())
        .RegisterAsync(new("Concurrent one", "concurrent-one@example.test", "concurrent first password"), default)),
    Task.Run(() => new AuthService(new WorkspaceContext(concurrentStore, startupClock, startupOptions), new LocalPasswordService())
        .RegisterAsync(new("Concurrent two", "concurrent-two@example.test", "concurrent second password"), default)));
Check(concurrentRegistrations.Count(x => x.User.Role == "Owner") == 1 && concurrentRegistrations.Count(x => x.User.Role == "Employee") == 1,
    "concurrent first registrations assign exactly one Owner (memory transaction lock)");
startupOrganization.Name = "Existing ABC";
startupOrganization.IsArchived = true;
await initializer.EnsureInitializedAsync(default);
Check(startupOrganization.Name == "Existing ABC" && startupOrganization.IsArchived, "startup preserves existing organisation state");
await Reject(() => startupAuth.RegisterAsync(new("Archived", "archived@example.test", "another long password"), default), 409, "bootstrap_required");
var clock = new ManualClock(new DateTimeOffset(2026, 10, 6, 2, 0, 0, TimeSpan.Zero));
var store = new MemoryStore();
var options = new PrototypeOptions();
var context = new WorkspaceContext(store, clock, options);
var auth = new AuthService(context, new LocalPasswordService());
var reservations = new ReservationService(context, options);
var administration = new AdministrationService(context, reservations);
var operations = new OperationsService(context, administration, reservations, options, new DemoDirectoryAdapter(), new LocalEmailSink());
await auth.BootstrapAsync("Owner", "owner@example.test", "long demonstration password", default);
var owner = (await auth.LoginAsync(new("owner@example.test", "long demonstration password"), default)).User;
await auth.BootstrapAsync("Changed", "OWNER@example.test", "another password value", default);
Check(store.Query<OrganizationMember>().Count(x => x.Role == MemberRole.Owner) == 1, "bootstrap idempotent");
var employee = await auth.RegisterAsync(new("Alice", " Alice@example.test ", "pássword with spaces 123"), default);
var colleague = await auth.RegisterAsync(new("Bob", "bob@example.test", "another long password"), default);
Check(employee.User.Role == "Employee" && store.Query<Organization>().Count() == 1, "registration after Owner exists creates Employee in ABC");
Check(store.Query<AppUser>().All(x => x.MicrosoftObjectId == null && x.MicrosoftTenantId == null), "no fictional Microsoft identity");
await Reject(() => auth.RegisterAsync(new("Duplicate", "ALICE@example.test", "another long password"), default), 409, "email_exists");
await Reject(() => auth.LoginAsync(new("alice@example.test", "wrong password"), default), 401, "invalid_credentials");
await auth.LogoutAsync(employee.Session.Id, default);
Check(auth.ValidateSession(employee.Session.Id) == null, "logout revokes session");
employee = await auth.LoginAsync(new("alice@example.test", "pássword with spaces 123"), default);
var schedule = Enum.GetValues<DayOfWeek>().Where(d => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Select(d => new WorkingHoursInput(d, new(7, 0), new(18, 0))).ToList();
var office = await administration.SaveOfficeAsync(owner.MemberId, null, new("Prague", "Europe/Prague", 30, schedule), default);
var secondOffice = await administration.SaveOfficeAsync(owner.MemberId, null, new("Second", "Europe/Prague", 30, [new(DayOfWeek.Thursday, new(9, 0), new(16, 0))]), default);
var room = await administration.SaveRoomAsync(owner.MemberId, null, new(office, "Room", 10, 10, 100, 100), default);
var desk = await administration.SaveDeskAsync(owner.MemberId, null, new(room, "D1", 10, 10, 0), default);
var otherDesk = await administration.SaveDeskAsync(owner.MemberId, null, new(room, "D2", 30, 10, 0), default);
await Reject(() => administration.SaveDeskAsync(employee.User.MemberId, null, new(room, "Forbidden", 50, 50, 0), default), 403, "forbidden");
await Reject(() => administration.SaveDeskAsync(owner.MemberId, null, new(room, "D1", 50, 50, 0), default), 409, "duplicate_label");
await Reject(() => administration.SaveDeskAsync(owner.MemberId, null, new(room, "Rotated outside", 0, 0, 45), default), 400, "invalid_geometry");
Check(administration.Offices(employee.User.MemberId).Count == 2, "employee sees all active offices");
var today = new DateOnly(2026, 10, 6);
var batch = await reservations.CreateBatchAsync(employee.User.MemberId, new(desk, [today.AddDays(1), today.AddDays(3), today.AddDays(7)], true, null, null), default);
Check(batch.Reservations.Count == 3 && batch.Reservations.All(x => x.BatchId == batch.BatchId), "three separate dates share BatchId");
Check(batch.Reservations.All(x => x.Start.Hour == 5 && x.End.Hour == 16), "full day converted from Prague schedule");
var before = store.Query<Reservation>().Count();
await Reject(() => reservations.CreateBatchAsync(colleague.User.MemberId, new(desk, [today.AddDays(2), today.AddDays(3)], true, null, null), default), 409, "booking_conflict");
Check(store.Query<Reservation>().Count() == before, "conflicting batch has no partial insert (memory transaction)");
var interval = await reservations.CreateBatchAsync(employee.User.MemberId, new(desk, [today.AddDays(2)], false, new(10, 0), new(11, 0)), default);
await reservations.CreateBatchAsync(employee.User.MemberId, new(desk, [today.AddDays(2)], false, new(11, 0), new(12, 0)), default);
await Reject(() => reservations.CreateBatchAsync(employee.User.MemberId, new(otherDesk, [today.AddDays(2)], false, new(10, 15), new(10, 45)), default), 409, "booking_conflict");
Check(store.Query<Reservation>().Count() == before + 2, "adjacent intervals permitted; member overlaps rejected");
var available = await reservations.AvailabilityAsync(employee.User.MemberId, new(otherDesk, [today.AddDays(30), today.AddDays(31)], true, null, null), default);
Check(available[0].Available && available[1].Code == "outside_horizon", "inclusive today+30 and rejected today+31");
var todayBatch = await reservations.CreateBatchAsync(employee.User.MemberId, new(otherDesk, [today], false, new(7, 0), new(8, 0)), default);
var rId = todayBatch.Reservations.Single().Id;
clock.Current = new(2026, 10, 6, 3, 59, 59, TimeSpan.Zero);
await Reject(() => reservations.ChangeAsync(employee.User.MemberId, rId, "confirm", null, default), 409, "invalid_state_or_time");
clock.Current = clock.Current.AddSeconds(1);
await reservations.ChangeAsync(employee.User.MemberId, rId, "confirm", null, default);
Check(store.Query<Reservation>().Single(x => x.Id == rId).ConfirmedAtUtc == clock.Current, "06:00 Prague confirmation accepted for 07:00");
var eventCount = store.Query<ReservationEvent>().Count();
await reservations.ChangeAsync(employee.User.MemberId, rId, "confirm", null, default);
Check(store.Query<ReservationEvent>().Count() == eventCount, "repeat confirmation has no duplicate event");
await Reject(() => reservations.ChangeAsync(employee.User.MemberId, rId, "complete", null, default), 409, "invalid_state_or_time");
clock.Current = new(2026, 10, 6, 5, 30, 0, TimeSpan.Zero);
await reservations.ChangeAsync(employee.User.MemberId, rId, "complete", null, default);
Check(store.Query<Reservation>().Single(x => x.Id == rId).EndsAtUtc.Hour == 6, "early completion preserves planned end");
await reservations.CreateBatchAsync(colleague.User.MemberId, new(otherDesk, [today], false, new(7, 45), new(8, 15)), default);
Check((await reservations.AvailabilityAsync(employee.User.MemberId, new(desk, [today], true, null, null), default))[0].Code == "past_time", "today full day rejected after opening");
var boundary = new Reservation { StartsAtUtc = new(2026, 10, 7, 5, 0, 0, TimeSpan.Zero), EndsAtUtc = new(2026, 10, 7, 6, 0, 0, TimeSpan.Zero) };
Check(BookingRules.CanConfirm(boundary, boundary.StartsAtUtc.AddMinutes(4).AddSeconds(59)), "07:04:59 accepted");
Check(!BookingRules.CanConfirm(boundary, boundary.StartsAtUtc.AddMinutes(5)), "07:05 rejected");
var closureInput = new ClosureRequest(office, room, new(2026, 10, 7, 5, 0, 0, TimeSpan.Zero), new(2026, 10, 7, 6, 0, 0, TimeSpan.Zero), "Repair", null);
var preview = await administration.PreviewAsync(owner.MemberId, closureInput, default);
await Reject(() => administration.CloseAsync(owner.MemberId, closureInput with { AcknowledgedReservationIds = [] }, default), 409, "closure_preview_changed");
var closureId = await administration.CloseAsync(owner.MemberId, closureInput with { AcknowledgedReservationIds = preview.Select(x => x.Id).ToList() }, default);
Check(store.Query<Reservation>().Any(x => x.CancellationReason == CancellationReason.RoomClosed), "room closure records cancellation reason");
await administration.CancelClosureAsync(owner.MemberId, closureId, default);
Check(store.Query<Reservation>().Any(x => x.CancellationReason == CancellationReason.RoomClosed), "removing closure does not restore reservations");
try { reservations.History(employee.User.MemberId, true, true, 1, 20); throw new InvalidOperationException(); }
catch (BusinessException e) { Check(e.Status == 403, "employee cannot read organization history"); }
var unrestricted = await reservations.AvailabilityAsync(colleague.User.MemberId, new(otherDesk, [today.AddDays(3)], true, null, null), default);
Check(unrestricted.Single().Available, "booking availability needs no subscription or payment");
var unpaidBooking = await reservations.CreateBatchAsync(colleague.User.MemberId, new(otherDesk, [today.AddDays(3)], true, null, null), default);
Check(unpaidBooking.Reservations.Count == 1 && !store.Query<OrganizationSubscription>().Any() && !store.Query<Payment>().Any(), "booking succeeds without creating subscriptions or payments");
var legacySubscription = new OrganizationSubscription { OrganizationId = options.OrganizationId, Currency = "CZK", Status = SubscriptionStatus.Active, CurrentPeriodStartsAtUtc = clock.Current.AddDays(-2), CurrentPeriodEndsAtUtc = clock.Current.AddDays(-1) };
store.Add(legacySubscription);
var outsideLegacyPeriod = await reservations.CreateBatchAsync(colleague.User.MemberId, new(otherDesk, [today.AddDays(6)], true, null, null), default);
Check(outsideLegacyPeriod.Reservations.Count == 1, "legacy expired paid period cannot block bookings");
await operations.TickAsync(default);
Check(store.Query<OrganizationSubscription>().Single().Status == SubscriptionStatus.Active, "worker leaves legacy subscription data untouched");
await operations.SetDirectoryAsync(owner.MemberId, colleague.User.MemberId, "Unavailable", default);
await operations.SyncDirectoryAsync(owner.MemberId, true, default);
Check(auth.ValidateSession(colleague.Session.Id) != null, "Unavailable directory leaves account active");
await operations.SetDirectoryAsync(owner.MemberId, colleague.User.MemberId, "Disabled", default);
await operations.SyncDirectoryAsync(owner.MemberId, true, default);
Check(auth.ValidateSession(colleague.Session.Id) == null, "Disabled directory revokes sessions");
Check(store.Query<Reservation>().Where(x => x.MemberId == colleague.User.MemberId).All(x => !BookingRules.Active(x)), "Disabled directory resolves bookings");
Check(auth.ValidateSession(employee.Session.Id) != null, "ordinary local member not connected to directory is unaffected");
await administration.SetEnabledAsync(owner.MemberId, colleague.User.MemberId, true, default);
Check(store.Query<Reservation>().Where(x => x.MemberId == colleague.User.MemberId).All(x => !BookingRules.Active(x)), "enable does not restore cancelled bookings");
await administration.RoleAsync(owner.MemberId, employee.User.MemberId, "Administrator", default);
Check(auth.ValidateSession(employee.Session.Id)!.Role == "Administrator", "session role reflects database change");
await Reject(() => administration.SetEnabledAsync(employee.User.MemberId, owner.MemberId, false, default), 403, "role_hierarchy");
await administration.ArchiveAsync(owner.MemberId, "desks", otherDesk, default);
Check((await reservations.AvailabilityAsync(employee.User.MemberId, new(otherDesk, [today.AddDays(3)], true, null, null), default))[0].Code == "archived", "archived desk unavailable");
var lifecycleDesk = await administration.SaveDeskAsync(owner.MemberId, null, new(room, "Lifecycle", 60, 60, 0), default);
var scheduledEnd = await reservations.CreateBatchAsync(employee.User.MemberId, new(lifecycleDesk, [today.AddDays(2)], false, new(7, 0), new(8, 0)), default);
var noShow = await reservations.CreateBatchAsync(employee.User.MemberId, new(lifecycleDesk, [today.AddDays(2)], false, new(8, 0), new(9, 0)), default);
clock.Current = new(2026, 10, 8, 5, 4, 59, TimeSpan.Zero);
await reservations.ChangeAsync(employee.User.MemberId, scheduledEnd.Reservations[0].Id, "confirm", null, default);
Check(store.Query<EmailNotification>().Where(x => x.ReservationId == scheduledEnd.Reservations[0].Id && x.Kind == NotificationKind.ReservationReminder).All(x => x.Status == NotificationStatus.Cancelled), "confirmation cancels pending reminder");
clock.Current = new(2026, 10, 8, 6, 5, 0, TimeSpan.Zero);
await Reject(() => reservations.ChangeAsync(employee.User.MemberId, noShow.Reservations[0].Id, "confirm", null, default), 409, "invalid_state_or_time");
var completed = store.Query<Reservation>().Single(x => x.Id == scheduledEnd.Reservations[0].Id);
Check(completed.Status == ReservationStatus.Completed && completed.CompletedAtUtc == completed.EndsAtUtc, "automatic completion records planned end");
Check(store.Query<Reservation>().Single(x => x.Id == noShow.Reservations[0].Id).CancellationReason == CancellationReason.NoShow, "confirmation at exact deadline persists NoShow before rejection");
var late = await reservations.CreateBatchAsync(employee.User.MemberId, new(lifecycleDesk, [today.AddDays(2)], false, new(9, 0), new(9, 30)), default);
Check(!store.Query<EmailNotification>().Any(x => x.ReservationId == late.Reservations[0].Id && x.Kind == NotificationKind.ReservationReminder), "late creation has no separate reminder");
clock.Current = new(2026, 10, 8, 8, 5, 0, TimeSpan.Zero);
await operations.TickAsync(default);
Check(store.Query<Reservation>().Where(x => x.StartsAtUtc.AddMinutes(5) <= clock.Current).All(x => x.Status != ReservationStatus.Reserved), "worker expires overdue Reserved");
Check(store.Query<EmailNotification>().Any(x => x.Status == NotificationStatus.Sent), "local mailbox dispatches durable outbox");
eventCount = store.Query<ReservationEvent>().Count();
await operations.TickAsync(default);
Check(store.Query<ReservationEvent>().Count() == eventCount, "worker replay does not duplicate lifecycle events");
Check(auth.ValidateSession(employee.Session.Id) == null, "session expiry remains fixed at eight hours");
var failedNotice = new EmailNotification { OrganizationId = options.OrganizationId, MemberId = owner.MemberId, Kind = NotificationKind.ReservationCreated, RecipientEmail = owner.Email, DeduplicationKey = "retry-fixture", Subject = "Fixture", Body = "Test only", ScheduledAtUtc = clock.Current };
store.Add(failedNotice);
var failingOperations = new OperationsService(context, administration, reservations, options, new DemoDirectoryAdapter(), new FailingEmailSink());
for (var attempt = 0; attempt < 5; attempt++) { await failingOperations.TickAsync(default); clock.Current = clock.Current.AddHours(1); }
Check(store.Query<EmailNotification>().Single(x => x.Id == failedNotice.Id).Status == NotificationStatus.Failed, "outbox failures retry then reach terminal diagnostic state");
var crashed = new EmailNotification { OrganizationId = options.OrganizationId, MemberId = owner.MemberId, Kind = NotificationKind.ReservationCreated, RecipientEmail = owner.Email, DeduplicationKey = "crash-fixture", Subject = "Fixture", Body = "Test only", ScheduledAtUtc = clock.Current, Status = NotificationStatus.Processing, LockedUntilUtc = clock.Current.AddMinutes(-1), AttemptCount = 5 };
store.Add(crashed); await failingOperations.TickAsync(default);
Check(store.Query<EmailNotification>().Single(x => x.Id == crashed.Id).Status == NotificationStatus.Failed, "last-attempt crashed lease never remains stuck Processing");
var microsoftClock = new ManualClock(new DateTimeOffset(2026, 10, 11, 7, 0, 0, TimeSpan.Zero));
var microsoftStore = new MemoryStore();
var microsoftContext = new WorkspaceContext(microsoftStore, microsoftClock, new PrototypeOptions());
await new OrganizationService(microsoftContext).EnsureInitializedAsync(default);
var microsoftAuth = new AuthService(microsoftContext, new LocalPasswordService());
var tenant = Guid.NewGuid();
var oid = Guid.NewGuid();
var microsoftFirst = await microsoftAuth.MicrosoftLoginAsync(tenant, oid, "entra-owner@example.test", "Entra Owner", default);
Check(microsoftFirst.User.Role == "Owner", "first validated Microsoft account becomes Owner");
Check(microsoftStore.Query<AppUser>().Single().MicrosoftObjectId == oid && !microsoftStore.Query<LocalCredential>().Any(), "real Microsoft identity stored without fabricated password");
var microsoftRepeat = await microsoftAuth.MicrosoftLoginAsync(tenant, oid, "changed@example.test", "Renamed User", default);
Check(microsoftRepeat.User.UserId == microsoftFirst.User.UserId && microsoftStore.Query<AppUser>().Count() == 1, "Microsoft identity survives email and name changes");
var microsoftNoEmail = await microsoftAuth.MicrosoftLoginAsync(tenant, oid, null, null, default);
Check(microsoftNoEmail.User.UserId == microsoftFirst.User.UserId, "existing verified identity does not require an email claim");
await Reject(() => microsoftAuth.MicrosoftLoginAsync(Guid.NewGuid(), oid, "entra-owner@example.test", "Other tenant", default), 409, "email_exists");
await Reject(() => microsoftAuth.MicrosoftLoginAsync(tenant, Guid.NewGuid(), "entra-owner@example.test", "Impersonator", default), 409, "email_exists");
await Reject(() => microsoftAuth.MicrosoftLoginAsync(tenant, Guid.NewGuid(), null, null, default), 400, "microsoft_email_unavailable");
await Reject(() => microsoftAuth.MicrosoftLoginAsync(Guid.Empty, oid, "empty@example.test", "Empty", default), 400, "invalid_microsoft_identity");
var localAccount = await microsoftAuth.RegisterAsync(new("Local Employee", "local-account@example.test", "Local password 2026!"), default);
await Reject(() => microsoftAuth.MicrosoftLoginAsync(tenant, Guid.NewGuid(), "local-account@example.test", "Local", default), 409, "email_exists");
Check(microsoftStore.Query<AppUser>().Single(x => x.Id == localAccount.User.UserId).MicrosoftObjectId == null, "email collision never attaches a Microsoft identity to a local account");
var employeeOid = Guid.NewGuid();
var microsoftEmployee = await microsoftAuth.MicrosoftLoginAsync(tenant, employeeOid, "entra-employee@example.test", "Employee", default);
Check(microsoftEmployee.User.Role == "Employee", "later Microsoft accounts receive Employee");
Check(microsoftAuth.ValidateSession(microsoftEmployee.Session.Id)?.UserId == microsoftEmployee.User.UserId && microsoftEmployee.Session.ExpiresAtUtc == microsoftClock.GetUtcNow().AddHours(8), "Microsoft login creates normal eight-hour revocable session");
await microsoftAuth.LogoutAsync(microsoftEmployee.Session.Id, default);
Check(microsoftAuth.ValidateSession(microsoftEmployee.Session.Id) == null, "Microsoft logout revokes its session");
var disabledMicrosoftMember = microsoftStore.Query<OrganizationMember>().Single(x => x.Id == microsoftEmployee.User.MemberId);
disabledMicrosoftMember.IsActive = false;
await Reject(() => microsoftAuth.MicrosoftLoginAsync(tenant, employeeOid, null, null, default), 401, "account_unavailable");
Check(!microsoftStore.Query<OrganizationMember>().Single(x => x.Id == microsoftEmployee.User.MemberId).IsActive, "Microsoft login cannot reactivate disabled membership");
var concurrentOid = Guid.NewGuid();
var concurrentMicrosoft = await Task.WhenAll(microsoftAuth.MicrosoftLoginAsync(tenant, concurrentOid, "concurrent@example.test", "Concurrent", default), microsoftAuth.MicrosoftLoginAsync(tenant, concurrentOid, "concurrent@example.test", "Concurrent", default));
Check(concurrentMicrosoft[0].User.UserId == concurrentMicrosoft[1].User.UserId && microsoftStore.Query<AppUser>().Count(x => x.MicrosoftObjectId == concurrentOid) == 1, "concurrent verified logins produce one Employee account");
microsoftStore.Query<Organization>().Single().IsArchived = true;
await Reject(() => microsoftAuth.MicrosoftLoginAsync(tenant, oid, null, null, default), 409, "bootstrap_required");

Console.WriteLine($"Completed {checks} checks. Database locks, SQL persistence, cookies and browser flows require separate HTTP acceptance.");

sealed class ManualClock(DateTimeOffset initial) : TimeProvider
{
    public DateTimeOffset Current { get; set; } = initial;
    public override DateTimeOffset GetUtcNow() => Current;
}
sealed class MemoryStore : IWorkspaceStore
{
    private Dictionary<Type, List<object>> data = [];
    private Transaction? transaction;
    private readonly SemaphoreSlim gate = new(1, 1);
    public IQueryable<T> Query<T>() where T : class => (data.TryGetValue(typeof(T), out var items) ? items.Cast<T>() : []).AsQueryable();
    public void Add<T>(T entity) where T : class
    {
        if (!data.TryGetValue(typeof(T), out var list)) data[typeof(T)] = list = [];
        list.Add(entity);
    }
    public void Remove<T>(T entity) where T : class => data[typeof(T)].Remove(entity);
    public async Task<IAsyncDisposable> LockAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var snapshot = data.ToDictionary(x => x.Key, x => x.Value.Select(v => JsonSerializer.Deserialize(JsonSerializer.Serialize(v, x.Key), x.Key)!).ToList());
            transaction = new Transaction(this, snapshot);
            return transaction;
        }
        catch
        {
            gate.Release();
            throw;
        }
    }
    public Task CommitAsync(CancellationToken ct = default)
    {
        if (transaction != null) transaction.Committed = true;
        return Task.CompletedTask;
    }
    private sealed class Transaction(MemoryStore owner, Dictionary<Type, List<object>> snapshot) : IAsyncDisposable
    {
        public bool Committed;
        public ValueTask DisposeAsync()
        {
            if (!Committed) owner.data = snapshot;
            owner.transaction = null; owner.gate.Release(); return ValueTask.CompletedTask;
        }
    }
}

sealed class FailingEmailSink : IEmailSink
{
    public Task DeliverAsync(Guid id, string recipient, string subject, string body, CancellationToken ct) => throw new InvalidOperationException("Simulated sink unavailable");
}
