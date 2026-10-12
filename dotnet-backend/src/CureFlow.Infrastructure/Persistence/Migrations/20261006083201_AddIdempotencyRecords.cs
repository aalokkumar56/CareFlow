using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandNamespace = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResponseJson = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_TenantId_ActorId_CommandNamespace_KeyHash",
                table: "IdempotencyRecords",
                columns: new[] { "TenantId", "ActorId", "CommandNamespace", "KeyHash" },
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE "IdempotencyRecords" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "IdempotencyRecords" FORCE ROW LEVEL SECURITY;
                CREATE POLICY idempotent_command_tenant ON "IdempotencyRecords"
                  USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                    GRANT SELECT,INSERT,UPDATE ON "IdempotencyRecords" TO cureflow_tenant;
                    REVOKE DELETE ON "IdempotencyRecords" FROM cureflow_tenant;
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");
        }
    }
}
