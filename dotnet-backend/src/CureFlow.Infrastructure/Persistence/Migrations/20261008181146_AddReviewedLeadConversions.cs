using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewedLeadConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ReferringDoctors_TenantId_Id",
                table: "ReferringDoctors",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Leads_TenantId_Id",
                table: "Leads",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "LeadConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceLeadTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalLeadJson = table.Column<string>(type: "text", nullable: false),
                    CandidateReviewTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SelectedPatientTag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferralSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferringDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_LeadConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadConversions_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadConversions_Leads_TenantId_LeadId",
                        columns: x => new { x.TenantId, x.LeadId },
                        principalTable: "Leads",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadConversions_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadConversions_ReferringDoctors_TenantId_ReferringDoctorId",
                        columns: x => new { x.TenantId, x.ReferringDoctorId },
                        principalTable: "ReferringDoctors",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadConversions_TenantId_FacilityId",
                table: "LeadConversions",
                columns: new[] { "TenantId", "FacilityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadConversions_TenantId_LeadId",
                table: "LeadConversions",
                columns: new[] { "TenantId", "LeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadConversions_TenantId_PatientId",
                table: "LeadConversions",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadConversions_TenantId_ReferringDoctorId",
                table: "LeadConversions",
                columns: new[] { "TenantId", "ReferringDoctorId" });
            migrationBuilder.Sql("""
                ALTER TABLE "Leads" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Leads" FORCE ROW LEVEL SECURITY;
                DO $policy$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_policy WHERE polrelid='public."Leads"'::regclass AND polname='conversion_tenant_scope') THEN
                 CREATE POLICY conversion_tenant_scope ON "Leads" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                END IF; END $policy$;
                CREATE POLICY conversion_tenant_guard ON "Leads" AS RESTRICTIVE FOR ALL USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY converted_patient_confidentiality ON "Leads" AS RESTRICTIVE FOR SELECT USING ("ConvertedPatientId" IS NULL OR public.cureflow_patient_read_allowed("TenantId","ConvertedPatientId"));
                ALTER TABLE "LeadConversions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "LeadConversions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_scope ON "LeadConversions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY patient_confidentiality_read ON "LeadConversions" AS RESTRICTIVE FOR SELECT USING (public.cureflow_patient_read_allowed("TenantId","PatientId"));
                ALTER TABLE "LeadConversions" ADD CONSTRAINT lead_conversion_shape CHECK (COALESCE(
                 "Decision" IN ('create_new','link_existing') AND "SourceLeadTag" ~ '^[0-9A-F]{64}$' AND "CandidateReviewTag" ~ '^[0-9A-F]{64}$'
                 AND jsonb_typeof("OriginalLeadJson"::jsonb)='object' AND length("OriginalLeadJson")<=32768
                 AND length(btrim("EvidenceReference")) BETWEEN 1 AND 200 AND length(btrim("Reason")) BETWEEN 1 AND 500
                 AND length("PolicyVersion") BETWEEN 1 AND 100 AND ("Decision"='link_existing')=("SelectedPatientTag" IS NOT NULL)
                 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted",false));
                CREATE FUNCTION public.guard_lead_conversion() RETURNS trigger LANGUAGE plpgsql AS $guard$
                DECLARE source public."Leads"%ROWTYPE;
                BEGIN
                 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Lead conversion provenance is immutable' USING ERRCODE='23514'; END IF;
                 SELECT * INTO source FROM public."Leads" l WHERE l."TenantId"=NEW."TenantId" AND l."Id"=NEW."LeadId" FOR UPDATE;
                 IF NOT FOUND OR source."ConvertedAt" IS NOT NULL OR NOT source."IsActive" OR source."IsDeleted" THEN
                  RAISE EXCEPTION 'Conversion requires current active unconverted lead' USING ERRCODE='23514';
                 END IF;
                 IF NEW."OriginalLeadJson"::jsonb->>'Id' IS DISTINCT FROM NEW."LeadId"::text
                  OR NEW."OriginalLeadJson"::jsonb->>'TenantId' IS DISTINCT FROM NEW."TenantId"::text
                  OR NEW."OriginalLeadJson"::jsonb->>'Name' IS DISTINCT FROM source."Name"
                  OR NEW."OriginalLeadJson"::jsonb->>'Phone' IS DISTINCT FROM source."Phone"
                  OR (NEW."OriginalLeadJson"::jsonb->>'Age')::integer IS DISTINCT FROM source."Age"
                  OR (NEW."OriginalLeadJson"::jsonb->>'Gender')::integer IS DISTINCT FROM source."Gender" THEN
                  RAISE EXCEPTION 'Original lead identity evidence differs' USING ERRCODE='23514';
                 END IF;
                 IF public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
                  OR NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=NEW."PatientId" AND p."IsActive" AND NOT p."IsDeleted")
                  OR (NEW."ReferringDoctorId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM public."ReferringDoctors" d WHERE d."TenantId"=NEW."TenantId" AND d."Id"=NEW."ReferringDoctorId" AND d."IsActive" AND NOT d."IsDeleted")) THEN
                  RAISE EXCEPTION 'Conversion target/facility/referral scope is not current' USING ERRCODE='23514';
                 END IF;
                 RETURN NEW;
                END $guard$;
                CREATE TRIGGER lead_conversion_guard BEFORE INSERT OR UPDATE OR DELETE ON "LeadConversions" FOR EACH ROW EXECUTE FUNCTION public.guard_lead_conversion();
                CREATE FUNCTION public.require_lead_conversion_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                 IF NOT EXISTS(SELECT 1 FROM public."Leads" l WHERE l."TenantId"=NEW."TenantId" AND l."Id"=NEW."LeadId"
                   AND l."ConvertedPatientId"=NEW."PatientId" AND l."ConvertedAt" IS NOT NULL AND NOT l."IsActive") THEN
                  RAISE EXCEPTION 'Conversion source/target mapping must commit together' USING ERRCODE='23514';
                 END IF;
                 RETURN NEW;
                END $guard$;
                CREATE CONSTRAINT TRIGGER lead_conversion_commit AFTER INSERT ON "LeadConversions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_lead_conversion_commit();
                CREATE FUNCTION public.guard_retained_lead_conversion() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                 IF OLD."ConvertedAt" IS NOT NULL AND (TG_OP='DELETE' OR
                  (to_jsonb(OLD)-ARRAY['UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt','UpdatedBy'])) THEN
                  RAISE EXCEPTION 'Converted lead source and mapping are retained' USING ERRCODE='23514';
                 END IF;
                 IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                 RETURN NEW;
                END $guard$;
                CREATE TRIGGER retained_lead_conversion BEFORE UPDATE OR DELETE ON "Leads" FOR EACH ROW EXECUTE FUNCTION public.guard_retained_lead_conversion();
                CREATE FUNCTION public.require_new_lead_mapping() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                 IF OLD."ConvertedAt" IS NULL AND NEW."ConvertedAt" IS NOT NULL AND NOT EXISTS(
                  SELECT 1 FROM public."LeadConversions" c WHERE c."TenantId"=NEW."TenantId" AND c."LeadId"=NEW."Id" AND c."PatientId"=NEW."ConvertedPatientId") THEN
                  RAISE EXCEPTION 'New conversion requires retained reviewed mapping' USING ERRCODE='23514';
                 END IF;
                 RETURN NEW;
                END $guard$;
                CREATE CONSTRAINT TRIGGER new_lead_mapping AFTER UPDATE ON "Leads" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_new_lead_mapping();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                 GRANT SELECT,INSERT ON "LeadConversions" TO cureflow_tenant;
                 REVOKE UPDATE,DELETE ON "LeadConversions" FROM cureflow_tenant;
                END IF; END $grant$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Retained lead source/attribution/conversion mappings require reviewed forward repair or protected restore; automatic downgrade is forbidden.");
        }
    }
}
