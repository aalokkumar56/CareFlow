using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalPatientIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Patients_TenantId_Id",
                table: "Patients",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "PatientIdentifierCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Namespace = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrentValue = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_PatientIdentifierCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientIdentifierCounters_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientIdentifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Namespace = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_PatientIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientIdentifiers_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientIdentifiers_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifierCounters_TenantId_FacilityId_Namespace",
                table: "PatientIdentifierCounters",
                columns: new[] { "TenantId", "FacilityId", "Namespace" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_TenantId_FacilityId_Namespace_PatientId",
                table: "PatientIdentifiers",
                columns: new[] { "TenantId", "FacilityId", "Namespace", "PatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_TenantId_FacilityId_Namespace_Value",
                table: "PatientIdentifiers",
                columns: new[] { "TenantId", "FacilityId", "Namespace", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_TenantId_PatientId",
                table: "PatientIdentifiers",
                columns: new[] { "TenantId", "PatientId" });
            migrationBuilder.Sql("""
                DO $rls$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['PatientIdentifiers','PatientIdentifierCounters'] LOOP
                  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
                  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',t);
                  EXECUTE format('CREATE POLICY identifier_tenant ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',t);
                END LOOP; END $rls$;
                ALTER TABLE "PatientIdentifierCounters" ADD CONSTRAINT local_counter_valid CHECK ("Namespace"='local-mrn' AND "CurrentValue">0 AND NOT "IsDeleted");
                ALTER TABLE "PatientIdentifiers" ADD CONSTRAINT local_identifier_valid CHECK ("Namespace"='local-mrn' AND "Sequence">0 AND NOT "IsDeleted"
                  AND "Value"='MRN-'||CASE WHEN length("Sequence"::text)<10 THEN lpad("Sequence"::text,10,'0') ELSE "Sequence"::text END);
                CREATE FUNCTION enforce_identifier_counter() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Identifier counters cannot be deleted' USING ERRCODE='23514'; END IF;
                  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required for namespace counter' USING ERRCODE='23514'; END IF;
                  IF NOT NEW."IsActive" THEN RAISE EXCEPTION 'Namespace counter cannot be disabled/reset' USING ERRCODE='23514'; END IF;
                  IF TG_OP='INSERT' AND NEW."CurrentValue"<>1 THEN RAISE EXCEPTION 'Counter must begin at one' USING ERRCODE='23514'; END IF;
                  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId" OR NEW."Namespace"<>OLD."Namespace"
                    OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR NEW."CurrentValue"<>OLD."CurrentValue"+1) THEN
                    RAISE EXCEPTION 'Identifier counter cannot reset or change namespace' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER identifier_counter_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientIdentifierCounters" FOR EACH ROW EXECUTE FUNCTION enforce_identifier_counter();
                CREATE FUNCTION prevent_identifier_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Issued identifiers are immutable and cannot be reused' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER identifier_immutable BEFORE UPDATE OR DELETE ON "PatientIdentifiers" FOR EACH ROW EXECUTE FUNCTION prevent_identifier_rewrite();
                CREATE FUNCTION validate_identifier_allocation() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE current_value bigint;
                BEGIN
                  SELECT "CurrentValue" INTO current_value FROM "PatientIdentifierCounters" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Namespace"=NEW."Namespace" FOR SHARE;
                  IF current_value IS DISTINCT FROM NEW."Sequence" THEN RAISE EXCEPTION 'Identifier requires current allocated counter value' USING ERRCODE='23514'; END IF;
                  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active patient required' USING ERRCODE='23514'; END IF;
                  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER identifier_allocation_guard BEFORE INSERT ON "PatientIdentifiers" FOR EACH ROW EXECUTE FUNCTION validate_identifier_allocation();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT ON "PatientIdentifiers" TO cureflow_tenant;
                  REVOKE UPDATE,DELETE ON "PatientIdentifiers" FROM cureflow_tenant;
                  GRANT SELECT,INSERT,UPDATE ON "PatientIdentifierCounters" TO cureflow_tenant;
                  REVOKE DELETE ON "PatientIdentifierCounters" FROM cureflow_tenant;
                END IF; END $grant$;
                -- No retroactive MRNs, source identifier copying, resets or clinical-row identity replacement.
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientIdentifierCounters");

            migrationBuilder.DropTable(
                name: "PatientIdentifiers");

            migrationBuilder.Sql("DROP FUNCTION enforce_identifier_counter(); DROP FUNCTION prevent_identifier_rewrite(); DROP FUNCTION validate_identifier_allocation();");
            migrationBuilder.DropUniqueConstraint(
                name: "AK_Patients_TenantId_Id",
                table: "Patients");
        }
    }
}
