using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceIdentity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceVersion = table.Column<string>(type: "text", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_ClinicalAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAuditEvents_TenantId_CreatedAt",
                table: "ClinicalAuditEvents",
                columns: new[] { "TenantId", "CreatedAt" });
            migrationBuilder.Sql("""
                ALTER TABLE "ClinicalAuditEvents" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ClinicalAuditEvents" FORCE ROW LEVEL SECURITY;
                CREATE POLICY clinical_audit_tenant ON "ClinicalAuditEvents"
                    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE FUNCTION prevent_clinical_audit_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Clinical audit evidence is append-only' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER clinical_audit_append_only BEFORE UPDATE OR DELETE ON "ClinicalAuditEvents"
                    FOR EACH ROW EXECUTE FUNCTION prevent_clinical_audit_rewrite();
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                        GRANT INSERT ON "ClinicalAuditEvents" TO cureflow_tenant;
                        REVOKE SELECT,UPDATE,DELETE ON "ClinicalAuditEvents" FROM cureflow_tenant;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicalAuditEvents");
            migrationBuilder.Sql("DROP FUNCTION prevent_clinical_audit_rewrite();");
        }
    }
}
