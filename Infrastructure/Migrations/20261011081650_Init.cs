using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deskovia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MicrosoftTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MicrosoftObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSignInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_ExternalIdentity", "([MicrosoftTenantId] IS NULL AND [MicrosoftObjectId] IS NULL) OR ([MicrosoftTenantId] IS NOT NULL AND [MicrosoftObjectId] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "DirectoryRuns",
                schema: "dbo",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryRuns", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_DirectoryRuns_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Offices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BookingHorizonDays = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offices", x => x.Id);
                    table.UniqueConstraint("AK_Offices_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Offices_BookingHorizonDays", "[BookingHorizonDays] >= 0");
                    table.ForeignKey(
                        name: "FK_Offices_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationIdentityConnections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MicrosoftTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ConnectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSuccessfulSyncAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextSyncAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeltaLink = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationIdentityConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationIdentityConnections_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSubscriptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentPeriodStartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentPeriodEndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProviderCustomerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSubscriptions", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationSubscriptions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_OrganizationSubscriptions_ActivePeriod", "[Status] <> 'Active' OR ([CurrentPeriodStartsAtUtc] IS NOT NULL AND [CurrentPeriodEndsAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_OrganizationSubscriptions_Currency", "LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
                    table.CheckConstraint("CK_OrganizationSubscriptions_Period", "([CurrentPeriodStartsAtUtc] IS NULL AND [CurrentPeriodEndsAtUtc] IS NULL) OR ([CurrentPeriodStartsAtUtc] IS NOT NULL AND [CurrentPeriodEndsAtUtc] IS NOT NULL AND [CurrentPeriodEndsAtUtc] > [CurrentPeriodStartsAtUtc])");
                    table.CheckConstraint("CK_OrganizationSubscriptions_Price", "[MonthlyPrice] >= 0");
                    table.CheckConstraint("CK_OrganizationSubscriptions_Status", "[Status] IN ('PendingPayment','Active','Expired','Cancelled')");
                    table.ForeignKey(
                        name: "FK_OrganizationSubscriptions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocalCredentials",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_LocalCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMembers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMembers", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationMembers_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_OrganizationMembers_Role", "[Role] IN ('Employee','Administrator','Owner')");
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeWorkingHours",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeWorkingHours", x => x.Id);
                    table.CheckConstraint("CK_OfficeWorkingHours_Day", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_OfficeWorkingHours_Hours", "[ClosesAt] > [OpensAt]");
                    table.ForeignKey(
                        name: "FK_OfficeWorkingHours_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalSchema: "dbo",
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PositionX = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PositionY = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Width = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 100m),
                    Height = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 100m),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.UniqueConstraint("AK_Rooms_OfficeId_Id", x => new { x.OfficeId, x.Id });
                    table.CheckConstraint("CK_Rooms_Dimensions", "[Width] > 0 AND [Height] > 0");
                    table.ForeignKey(
                        name: "FK_Rooms_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalSchema: "dbo",
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderPaymentId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsTest = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PeriodStartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.UniqueConstraint("AK_Payments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Payments_Currency", "LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
                    table.CheckConstraint("CK_Payments_Paid", "[Status] NOT IN ('Succeeded','Refunded') OR [PaidAtUtc] IS NOT NULL");
                    table.CheckConstraint("CK_Payments_Period", "[PeriodEndsAtUtc] > [PeriodStartsAtUtc]");
                    table.CheckConstraint("CK_Payments_Status", "[Status] IN ('Pending','Succeeded','Failed','Refunded')");
                    table.ForeignKey(
                        name: "FK_Payments_OrganizationSubscriptions_OrganizationId_SubscriptionId",
                        columns: x => new { x.OrganizationId, x.SubscriptionId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationSubscriptions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_OrganizationMembers_OrganizationId_ActorMemberId",
                        columns: x => new { x.OrganizationId, x.ActorMemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEntries_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DemoDirectoryRecords",
                schema: "dbo",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoDirectoryRecords", x => x.MemberId);
                    table.CheckConstraint("CK_DemoDirectory_State", "[State] IN ('Active','Disabled','Deleted','Unavailable')");
                    table.ForeignKey(
                        name: "FK_DemoDirectoryRecords_OrganizationMembers_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.CheckConstraint("CK_UserSessions_Lifetime", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_UserSessions_OrganizationMembers_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Desks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PositionX = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PositionY = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RotationDegrees = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Desks", x => x.Id);
                    table.UniqueConstraint("AK_Desks_RoomId_Id", x => new { x.RoomId, x.Id });
                    table.CheckConstraint("CK_Desks_Rotation", "[RotationDegrees] >= 0 AND [RotationDegrees] < 360");
                    table.ForeignKey(
                        name: "FK_Desks_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeClosures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeClosures", x => x.Id);
                    table.CheckConstraint("CK_OfficeClosures_Interval", "[EndsAtUtc] > [StartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_OfficeClosures_Offices_OrganizationId_OfficeId",
                        columns: x => new { x.OrganizationId, x.OfficeId },
                        principalSchema: "dbo",
                        principalTable: "Offices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeClosures_OrganizationMembers_OrganizationId_CreatedByMemberId",
                        columns: x => new { x.OrganizationId, x.CreatedByMemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeClosures_Rooms_OfficeId_RoomId",
                        columns: x => new { x.OfficeId, x.RoomId },
                        principalSchema: "dbo",
                        principalTable: "Rooms",
                        principalColumns: new[] { "OfficeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                    table.CheckConstraint("CK_PaymentWebhookEvents_Status", "[Status] IN ('Pending','Processed','Failed')");
                    table.ForeignKey(
                        name: "FK_PaymentWebhookEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "dbo",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentWebhookEvents_Payments_OrganizationId_PaymentId",
                        columns: x => new { x.OrganizationId, x.PaymentId },
                        principalSchema: "dbo",
                        principalTable: "Payments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsFullDay = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.UniqueConstraint("AK_Reservations_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Reservations_Cancellation", "([Status] = 'Cancelled' AND [CancelledAtUtc] IS NOT NULL AND [CancellationReason] IS NOT NULL) OR ([Status] <> 'Cancelled' AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL)");
                    table.CheckConstraint("CK_Reservations_CancellationReason", "[CancellationReason] IS NULL OR [CancellationReason] IN ('Employee','Administrator','NoShow','OfficeClosed','RoomClosed','DeskArchived','AccountDisabled')");
                    table.CheckConstraint("CK_Reservations_Completion", "([Status] = 'Completed' AND [CompletedAtUtc] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [StartsAtUtc] AND [CompletedAtUtc] <= [EndsAtUtc]) OR ([Status] <> 'Completed' AND [CompletedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_Reservations_Confirmation", "([Status] <> 'Confirmed' OR [ConfirmedAtUtc] IS NOT NULL) AND ([Status] <> 'Reserved' OR [ConfirmedAtUtc] IS NULL) AND ([ConfirmedAtUtc] IS NULL OR ([ConfirmedAtUtc] >= DATEADD(hour, -1, [StartsAtUtc]) AND [ConfirmedAtUtc] < DATEADD(minute, 5, [StartsAtUtc])))");
                    table.CheckConstraint("CK_Reservations_Interval", "[EndsAtUtc] > [StartsAtUtc]");
                    table.CheckConstraint("CK_Reservations_MinimumDuration", "[EndsAtUtc] >= DATEADD(minute, 30, [StartsAtUtc])");
                    table.CheckConstraint("CK_Reservations_Status", "[Status] IN ('Reserved','Confirmed','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Reservations_Desks_RoomId_DeskId",
                        columns: x => new { x.RoomId, x.DeskId },
                        principalSchema: "dbo",
                        principalTable: "Desks",
                        principalColumns: new[] { "RoomId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Offices_OrganizationId_OfficeId",
                        columns: x => new { x.OrganizationId, x.OfficeId },
                        principalSchema: "dbo",
                        principalTable: "Offices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_OrganizationMembers_OrganizationId_MemberId",
                        columns: x => new { x.OrganizationId, x.MemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Rooms_OfficeId_RoomId",
                        columns: x => new { x.OfficeId, x.RoomId },
                        principalSchema: "dbo",
                        principalTable: "Rooms",
                        principalColumns: new[] { "OfficeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailNotifications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotifications", x => x.Id);
                    table.CheckConstraint("CK_EmailNotifications_Attempts", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_EmailNotifications_Kind", "[Kind] IN ('ReservationCreated','ReservationReminder','ReservationCancelled')");
                    table.CheckConstraint("CK_EmailNotifications_Sent", "([Status] = 'Sent' AND [SentAtUtc] IS NOT NULL) OR ([Status] <> 'Sent' AND [SentAtUtc] IS NULL)");
                    table.CheckConstraint("CK_EmailNotifications_Status", "[Status] IN ('Pending','Processing','Sent','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_EmailNotifications_OrganizationMembers_OrganizationId_MemberId",
                        columns: x => new { x.OrganizationId, x.MemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailNotifications_Reservations_OrganizationId_ReservationId",
                        columns: x => new { x.OrganizationId, x.ReservationId },
                        principalSchema: "dbo",
                        principalTable: "Reservations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationEvents", x => x.Id);
                    table.CheckConstraint("CK_ReservationEvents_EventType", "[EventType] IN ('Created','Confirmed','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_ReservationEvents_OrganizationMembers_OrganizationId_ActorMemberId",
                        columns: x => new { x.OrganizationId, x.ActorMemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationEvents_Reservations_OrganizationId_ReservationId",
                        columns: x => new { x.OrganizationId, x.ReservationId },
                        principalSchema: "dbo",
                        principalTable: "Reservations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OrganizationId_ActorMemberId",
                schema: "dbo",
                table: "AuditEntries",
                columns: new[] { "OrganizationId", "ActorMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OrganizationId_OccurredAtUtc",
                schema: "dbo",
                table: "AuditEntries",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Desks_RoomId_Label",
                schema: "dbo",
                table: "Desks",
                columns: new[] { "RoomId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_OrganizationId_DeduplicationKey",
                schema: "dbo",
                table: "EmailNotifications",
                columns: new[] { "OrganizationId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_OrganizationId_MemberId",
                schema: "dbo",
                table: "EmailNotifications",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_OrganizationId_ReservationId",
                schema: "dbo",
                table: "EmailNotifications",
                columns: new[] { "OrganizationId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotifications_Status_ScheduledAtUtc",
                schema: "dbo",
                table: "EmailNotifications",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeClosures_OfficeId_RoomId",
                schema: "dbo",
                table: "OfficeClosures",
                columns: new[] { "OfficeId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeClosures_OfficeId_StartsAtUtc_EndsAtUtc",
                schema: "dbo",
                table: "OfficeClosures",
                columns: new[] { "OfficeId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeClosures_OrganizationId_CreatedByMemberId",
                schema: "dbo",
                table: "OfficeClosures",
                columns: new[] { "OrganizationId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeClosures_OrganizationId_OfficeId",
                schema: "dbo",
                table: "OfficeClosures",
                columns: new[] { "OrganizationId", "OfficeId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeWorkingHours_OfficeId_DayOfWeek",
                schema: "dbo",
                table: "OfficeWorkingHours",
                columns: new[] { "OfficeId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationIdentityConnections_MicrosoftTenantId",
                schema: "dbo",
                table: "OrganizationIdentityConnections",
                column: "MicrosoftTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationIdentityConnections_OrganizationId",
                schema: "dbo",
                table: "OrganizationIdentityConnections",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_OrganizationId",
                schema: "dbo",
                table: "OrganizationMembers",
                column: "OrganizationId",
                unique: true,
                filter: "[Role] = 'Owner' AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_OrganizationId_UserId",
                schema: "dbo",
                table: "OrganizationMembers",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_UserId",
                schema: "dbo",
                table: "OrganizationMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_OrganizationId",
                schema: "dbo",
                table: "OrganizationSubscriptions",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                schema: "dbo",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_SubscriptionId",
                schema: "dbo",
                table: "Payments",
                columns: new[] { "OrganizationId", "SubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderPaymentId",
                schema: "dbo",
                table: "Payments",
                columns: new[] { "Provider", "ProviderPaymentId" },
                unique: true,
                filter: "[ProviderPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_OrganizationId_PaymentId",
                schema: "dbo",
                table: "PaymentWebhookEvents",
                columns: new[] { "OrganizationId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_ProviderEventId",
                schema: "dbo",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_OrganizationId_ActorMemberId",
                schema: "dbo",
                table: "ReservationEvents",
                columns: new[] { "OrganizationId", "ActorMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_OrganizationId_ReservationId",
                schema: "dbo",
                table: "ReservationEvents",
                columns: new[] { "OrganizationId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_ReservationId_OccurredAtUtc",
                schema: "dbo",
                table: "ReservationEvents",
                columns: new[] { "ReservationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BatchId",
                schema: "dbo",
                table: "Reservations",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DeskId_StartsAtUtc_EndsAtUtc",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "DeskId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MemberId_StartsAtUtc_EndsAtUtc",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "MemberId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OfficeId_RoomId",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "OfficeId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OrganizationId_MemberId",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "OrganizationId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OrganizationId_OfficeId",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "OrganizationId", "OfficeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RoomId_DeskId",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "RoomId", "DeskId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Status_StartsAtUtc",
                schema: "dbo",
                table: "Reservations",
                columns: new[] { "Status", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_MicrosoftTenantId_MicrosoftObjectId",
                schema: "dbo",
                table: "Users",
                columns: new[] { "MicrosoftTenantId", "MicrosoftObjectId" },
                unique: true,
                filter: "[MicrosoftTenantId] IS NOT NULL AND [MicrosoftObjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                schema: "dbo",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_MemberId_ExpiresAtUtc",
                schema: "dbo",
                table: "UserSessions",
                columns: new[] { "MemberId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DemoDirectoryRecords",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DirectoryRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmailNotifications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LocalCredentials",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OfficeClosures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OfficeWorkingHours",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrganizationIdentityConnections",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReservationEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserSessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Reservations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrganizationSubscriptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Desks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrganizationMembers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Offices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "dbo");
        }
    }
}
