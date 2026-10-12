using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalPatientContactVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientContactVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ContactType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Usage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Relationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Preferred = table.Column<bool>(type: "boolean", nullable: false),
                    ContactIsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PatientContactVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientContactVersions_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientContactVersions_TenantId_PatientId_ContactId_Revision",
                table: "PatientContactVersions",
                columns: new[] { "TenantId", "PatientId", "ContactId", "Revision" },
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE "PatientContactVersions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientContactVersions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY contact_tenant ON "PatientContactVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "PatientContactVersions" ADD CONSTRAINT local_contact_valid CHECK (
                  "Revision">0 AND "ContactType" IN ('phone','email','person') AND "Usage" IN ('normal','emergency','guardian')
                  AND "VerificationStatus"='unverified' AND NOT "IsDeleted" AND "IsActive" AND length(btrim("DisplayName"))>0
                  AND ("EffectiveUntil" IS NULL OR "EffectiveUntil">"EffectiveFrom")
                  AND ("Usage"<>'guardian' OR COALESCE(length(btrim("Relationship")),0)>0)
                  AND ("ContactType"<>'phone' OR COALESCE("Value" ~ '^\+?[0-9]{5,20}$' AND length("Value")<=20,false))
                  AND ("ContactType"<>'email' OR COALESCE(length(btrim("Value")),0)>0)
                  AND ("ContactType"<>'person' OR COALESCE(length(btrim("Value")),0)=0));
                CREATE FUNCTION validate_local_contact_append() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE previous_revision bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(hashtextextended('patient-contacts:'||NEW."TenantId"::text||':'||NEW."PatientId"::text,0));
                  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active scoped patient required for contact update' USING ERRCODE='23514'; END IF;
                  SELECT COALESCE(max("Revision"),0) INTO previous_revision FROM "PatientContactVersions"
                    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "ContactId"=NEW."ContactId";
                  IF NEW."Revision"<>previous_revision+1 THEN RAISE EXCEPTION 'Contact history requires a consecutive revision' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER local_contact_append BEFORE INSERT ON "PatientContactVersions" FOR EACH ROW EXECUTE FUNCTION validate_local_contact_append();
                CREATE FUNCTION prevent_local_contact_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Local contact history is append-only' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER local_contact_history BEFORE UPDATE OR DELETE ON "PatientContactVersions" FOR EACH ROW EXECUTE FUNCTION prevent_local_contact_rewrite();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT ON "PatientContactVersions" TO cureflow_tenant;
                  REVOKE UPDATE,DELETE ON "PatientContactVersions" FROM cureflow_tenant;
                END IF; END $grant$;
                -- No backfill, identity verification, portal proxy or global guardian authority.
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientContactVersions");
            migrationBuilder.Sql("DROP FUNCTION validate_local_contact_append(); DROP FUNCTION prevent_local_contact_rewrite();");
        }
    }
}
