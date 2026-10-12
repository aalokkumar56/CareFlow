using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnknownPatientIdentityVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityStatus",
                table: "Patients",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "unreviewed");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PatientDemographicVersions_TenantId_PatientId_Id",
                table: "PatientDemographicVersions",
                columns: new[] { "TenantId", "PatientId", "Id" });

            migrationBuilder.CreateTable(
                name: "PatientIdentityVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemographicVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProofReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PatientTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_PatientIdentityVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientIdentityVerifications_PatientDemographicVersions_Ten~",
                        columns: x => new { x.TenantId, x.PatientId, x.DemographicVersionId },
                        principalTable: "PatientDemographicVersions",
                        principalColumns: new[] { "TenantId", "PatientId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemporaryPatientIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_TemporaryPatientIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemporaryPatientIdentities_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemporaryPatientIdentities_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentityVerifications_TenantId_PatientId_CreatedAt",
                table: "PatientIdentityVerifications",
                columns: new[] { "TenantId", "PatientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentityVerifications_TenantId_PatientId_Demographic~",
                table: "PatientIdentityVerifications",
                columns: new[] { "TenantId", "PatientId", "DemographicVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryPatientIdentities_TenantId_FacilityId_Value",
                table: "TemporaryPatientIdentities",
                columns: new[] { "TenantId", "FacilityId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryPatientIdentities_TenantId_PatientId",
                table: "TemporaryPatientIdentities",
                columns: new[] { "TenantId", "PatientId" },
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE "TemporaryPatientIdentities" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "TemporaryPatientIdentities" FORCE ROW LEVEL SECURITY;
                ALTER TABLE "PatientIdentityVerifications" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientIdentityVerifications" FORCE ROW LEVEL SECURITY;
                CREATE POLICY temporary_patient_tenant ON "TemporaryPatientIdentities" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY patient_verification_tenant ON "PatientIdentityVerifications" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "Patients" ADD CONSTRAINT patient_identity_status_valid CHECK ("IdentityStatus" IN ('unknown','unreviewed','verified_local'));
                ALTER TABLE "TemporaryPatientIdentities" ADD CONSTRAINT temporary_patient_valid CHECK (
                  "Value"='TEMP-'||replace("PatientId"::text,'-','') AND length(btrim("Reason"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                ALTER TABLE "PatientIdentityVerifications" ADD CONSTRAINT patient_verification_valid CHECK (
                  length(btrim("ProofReference"))>0 AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
                  AND length("PatientTag") BETWEEN 80 AND 128 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                CREATE FUNCTION validate_temporary_patient_intake() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF NOT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted")
                    OR NOT EXISTS(SELECT 1 FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId"
                      AND "IdentityStatus"='unknown' AND "BirthDatePrecision"='unknown' AND "Phone"='' AND "DateOfBirth" IS NULL AND "Age" IS NULL
                      AND "IsActive" AND NOT "IsDeleted") THEN
                    RAISE EXCEPTION 'Unknown intake requires active local facility/patient without guessed demographics' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER temporary_patient_insert BEFORE INSERT ON "TemporaryPatientIdentities" FOR EACH ROW EXECUTE FUNCTION validate_temporary_patient_intake();
                CREATE FUNCTION validate_patient_local_verification() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE current_version uuid;
                BEGIN
                  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId"
                    AND "IdentityStatus"='verified_local' AND "UpdatedBy"=NEW."CreatedBy" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Current local patient/reviewer attribution required' USING ERRCODE='23514'; END IF;
                  SELECT "Id" INTO current_version FROM "PatientDemographicVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId"
                    ORDER BY "Revision" DESC LIMIT 1;
                  IF current_version IS DISTINCT FROM NEW."DemographicVersionId" THEN
                    RAISE EXCEPTION 'Current demographic source evidence required' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_verification_insert BEFORE INSERT ON "PatientIdentityVerifications" FOR EACH ROW EXECUTE FUNCTION validate_patient_local_verification();
                CREATE FUNCTION prevent_patient_identity_evidence_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Temporary identity and verification evidence are retained' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER temporary_patient_history BEFORE UPDATE OR DELETE ON "TemporaryPatientIdentities"
                  FOR EACH ROW EXECUTE FUNCTION prevent_patient_identity_evidence_rewrite();
                CREATE TRIGGER patient_verification_history BEFORE UPDATE OR DELETE ON "PatientIdentityVerifications"
                  FOR EACH ROW EXECUTE FUNCTION prevent_patient_identity_evidence_rewrite();
                CREATE FUNCTION validate_patient_identity_transition() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP='INSERT' THEN
                    IF NEW."IdentityStatus"='verified_local' THEN RAISE EXCEPTION 'New registrations begin unreviewed' USING ERRCODE='23514'; END IF;
                  ELSIF NEW."IdentityStatus" IS DISTINCT FROM OLD."IdentityStatus" THEN
                    IF NEW."IdentityStatus"<>'verified_local' OR OLD."IdentityStatus" NOT IN ('unknown','unreviewed') OR NEW."UpdatedBy" IS NULL THEN
                      RAISE EXCEPTION 'Identity workflow transition requires reviewed verification' USING ERRCODE='23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_identity_transition BEFORE INSERT OR UPDATE ON "Patients" FOR EACH ROW EXECUTE FUNCTION validate_patient_identity_transition();
                CREATE FUNCTION require_patient_identity_verification() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF NOT EXISTS(SELECT 1 FROM "PatientIdentityVerifications" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."Id"
                    AND "CreatedBy"=NEW."UpdatedBy" AND "CreatedAt">=NEW."UpdatedAt") THEN
                    RAISE EXCEPTION 'Local verification requires evidence in the same transaction' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE CONSTRAINT TRIGGER patient_identity_verification_required AFTER UPDATE ON "Patients"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                  WHEN (NEW."IdentityStatus"='verified_local' AND OLD."IdentityStatus" IS DISTINCT FROM NEW."IdentityStatus")
                  EXECUTE FUNCTION require_patient_identity_verification();
                INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
                  VALUES ('0f79b2ee-67d6-4f55-9b70-f02b3ac4b723','PatientIdentity.Verify','Review local patient identity evidence','Configured accountable local review; no portal/sharing authority',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
                  ON CONFLICT ("Code") DO NOTHING;
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT ON "TemporaryPatientIdentities","PatientIdentityVerifications" TO cureflow_tenant;
                  REVOKE UPDATE,DELETE ON "TemporaryPatientIdentities","PatientIdentityVerifications" FROM cureflow_tenant;
                END IF; END $grant$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Unknown/local verification evidence cannot be downgraded automatically; use reviewed forward repair or protected restore.");

        }
    }
}
