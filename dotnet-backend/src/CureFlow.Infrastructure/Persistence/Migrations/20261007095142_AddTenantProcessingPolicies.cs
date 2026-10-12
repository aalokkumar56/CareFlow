using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProcessingPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantProcessingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Allowed = table.Column<bool>(type: "boolean", nullable: false),
                    ActorKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ApprovalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BasisReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BasisRevision = table.Column<int>(type: "integer", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_TenantProcessingPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantProcessingPolicies_TenantId_Provider_Purpose_Revision",
                table: "TenantProcessingPolicies",
                columns: new[] { "TenantId", "Provider", "Purpose", "Revision" },
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE "TenantProcessingPolicies" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "TenantProcessingPolicies" FORCE ROW LEVEL SECURITY;
                CREATE POLICY processing_policy_tenant ON "TenantProcessingPolicies"
                    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "TenantProcessingPolicies" ADD CONSTRAINT processing_policy_evidence
                    CHECK ("Revision">0 AND "BasisRevision">0 AND "ActorKind" IN ('person','service')
                        AND "ExpiresAt">"ApprovedAt" AND length("ApprovalReference")>0 AND length("BasisReference")>0);
                CREATE FUNCTION prevent_processing_policy_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Processing approval history is immutable; append a superseding decision' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER processing_policy_append_only BEFORE UPDATE OR DELETE ON "TenantProcessingPolicies"
                    FOR EACH ROW EXECUTE FUNCTION prevent_processing_policy_rewrite();
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                        GRANT SELECT ON "TenantProcessingPolicies" TO cureflow_tenant;
                        REVOKE INSERT,UPDATE,DELETE ON "TenantProcessingPolicies" FROM cureflow_tenant;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantProcessingPolicies");
            migrationBuilder.Sql("DROP FUNCTION prevent_processing_policy_rewrite();");
        }
    }
}

