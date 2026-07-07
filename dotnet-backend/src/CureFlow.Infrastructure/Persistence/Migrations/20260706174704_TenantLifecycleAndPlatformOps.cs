using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantLifecycleAndPlatformOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByPlatformUserId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "OnboardingComplete",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantOnboardingStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileComplete = table.Column<bool>(type: "boolean", nullable: false),
                    WhatsAppConnected = table.Column<bool>(type: "boolean", nullable: false),
                    TeamInvited = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantOnboardingStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_LifecycleStatus",
                table: "Tenants",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Email",
                table: "PlatformUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantOnboardingStates_TenantId",
                table: "TenantOnboardingStates",
                column: "TenantId",
                unique: true);

            // Existing seeded/demo hospitals were already live — mark Active + onboarding complete.
            migrationBuilder.Sql("""
                UPDATE "Tenants"
                SET "LifecycleStatus" = 1,
                    "OnboardingComplete" = true,
                    "ApprovedAt" = COALESCE("ApprovedAt", NOW())
                WHERE "IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAuditLogs");

            migrationBuilder.DropTable(
                name: "PlatformUsers");

            migrationBuilder.DropTable(
                name: "TenantOnboardingStates");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_LifecycleStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApprovedByPlatformUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OnboardingComplete",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Tenants");
        }
    }
}
