using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePlatformAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TenantType" integer NOT NULL DEFAULT 0;""");
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "TenantEntitlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "PlatformUsers",
                type: "text",
                nullable: false,
                defaultValue: "owner");

            migrationBuilder.CreateTable(
                name: "PlatformCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PermissionsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantEntitlements_PlatformModuleId",
                table: "TenantEntitlements",
                column: "PlatformModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantEntitlements_TenantId_PlatformModuleId",
                table: "TenantEntitlements",
                columns: new[] { "TenantId", "PlatformModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSupportSessions_PlatformUserId",
                table: "PlatformSupportSessions",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSupportSessions_TenantId",
                table: "PlatformSupportSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformModules_Key",
                table: "PlatformModules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_TenantId",
                table: "Facilities",
                column: "TenantId",
                unique: true,
                filter: "\"IsPrimary\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformCommands_ActorId_RequestId",
                table: "PlatformCommands",
                columns: new[] { "ActorId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRoles_Key",
                table: "PlatformRoles",
                column: "Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Tenants_TenantId",
                table: "Facilities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformSupportSessions_PlatformUsers_PlatformUserId",
                table: "PlatformSupportSessions",
                column: "PlatformUserId",
                principalTable: "PlatformUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformSupportSessions_Tenants_TenantId",
                table: "PlatformSupportSessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantEntitlements_PlatformModules_PlatformModuleId",
                table: "TenantEntitlements",
                column: "PlatformModuleId",
                principalTable: "PlatformModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantEntitlements_Tenants_TenantId",
                table: "TenantEntitlements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Tenants_TenantId",
                table: "Facilities");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformSupportSessions_PlatformUsers_PlatformUserId",
                table: "PlatformSupportSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformSupportSessions_Tenants_TenantId",
                table: "PlatformSupportSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantEntitlements_PlatformModules_PlatformModuleId",
                table: "TenantEntitlements");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantEntitlements_Tenants_TenantId",
                table: "TenantEntitlements");

            migrationBuilder.DropTable(
                name: "PlatformCommands");

            migrationBuilder.DropTable(
                name: "PlatformRoles");

            migrationBuilder.DropIndex(
                name: "IX_TenantEntitlements_PlatformModuleId",
                table: "TenantEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_TenantEntitlements_TenantId_PlatformModuleId",
                table: "TenantEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_PlatformSupportSessions_PlatformUserId",
                table: "PlatformSupportSessions");

            migrationBuilder.DropIndex(
                name: "IX_PlatformSupportSessions_TenantId",
                table: "PlatformSupportSessions");

            migrationBuilder.DropIndex(
                name: "IX_PlatformModules_Key",
                table: "PlatformModules");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_TenantId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "TenantEntitlements");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "PlatformUsers");
        }
    }
}
