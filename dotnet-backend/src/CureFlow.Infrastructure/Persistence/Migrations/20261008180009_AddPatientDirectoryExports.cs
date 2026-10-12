using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientDirectoryExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientIdsJson = table.Column<string>(type: "text", nullable: false),
                    PatientTagsJson = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ObjectVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Length = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_PatientExportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientExportJobs_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientExportJobs_RequesterId",
                table: "PatientExportJobs",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientExportJobs_TenantId_RequesterId_CreatedAt",
                table: "PatientExportJobs",
                columns: new[] { "TenantId", "RequesterId", "CreatedAt" });
            migrationBuilder.Sql("""
                ALTER TABLE "PatientExportJobs" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientExportJobs" FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_scope ON "PatientExportJobs" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY export_owner ON "PatientExportJobs" AS RESTRICTIVE FOR SELECT USING (
                 NULLIF(current_setting('app.current_user_id',true),'') IS NULL OR "RequesterId"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
                CREATE POLICY patient_confidentiality_read ON "PatientExportJobs" AS RESTRICTIVE FOR SELECT USING (
                 NOT EXISTS(SELECT 1 FROM jsonb_array_elements_text("PatientIdsJson"::jsonb) id WHERE NOT public.cureflow_patient_read_allowed("TenantId",id.value::uuid)));
                ALTER TABLE "PatientExportJobs" ADD CONSTRAINT export_shape CHECK (COALESCE(
                 jsonb_typeof("PatientIdsJson"::jsonb)='array' AND jsonb_array_length("PatientIdsJson"::jsonb) BETWEEN 1 AND 200
                 AND jsonb_typeof("PatientTagsJson"::jsonb)='object' AND jsonb_typeof("FieldsJson"::jsonb)='array' AND jsonb_array_length("FieldsJson"::jsonb) BETWEEN 1 AND 6
                 AND length("PatientIdsJson")<=10000 AND length("PatientTagsJson")<=40000 AND length("FieldsJson")<=200
                 AND length(btrim("Purpose")) BETWEEN 1 AND 500 AND length("PolicyVersion") BETWEEN 1 AND 100
                 AND "CreatedBy"="RequesterId" AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '24 hours' AND "IsActive" AND NOT "IsDeleted"
                 AND (("State"='pending' AND "Revision"=1 AND "ObjectKey" IS NULL AND "ObjectVersion" IS NULL AND "Sha256" IS NULL AND "Length" IS NULL)
                  OR ("State"='ready' AND "Revision"=2 AND length("ObjectKey") BETWEEN 1 AND 500 AND "ObjectVersion" IS NOT NULL AND "Sha256" ~ '^[0-9A-F]{64}$' AND "Length" BETWEEN 1 AND 2097152)),false));
                CREATE FUNCTION public.guard_patient_export_job() RETURNS trigger LANGUAGE plpgsql AS $guard$
                DECLARE selected uuid; field text;
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Export receipt/provenance is retained' USING ERRCODE='23514'; END IF;
                 IF NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."RequesterId" AND u."IsActive" AND NOT u."IsDeleted")
                  OR NEW."ExpiresAt"<=clock_timestamp() THEN RAISE EXCEPTION 'Export requester/review window invalid' USING ERRCODE='23514'; END IF;
                 IF TG_OP='INSERT' THEN
                  IF NEW."State"<>'pending' OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
                   RAISE EXCEPTION 'Export requires authenticated requester and pending state' USING ERRCODE='23514';
                  END IF;
                 ELSE
                  IF OLD."State"<>'pending' OR NEW."State"<>'ready' OR NEW."Revision"<>2
                   OR (to_jsonb(OLD)-ARRAY['State','Revision','ObjectKey','ObjectVersion','Sha256','Length','UpdatedAt'])
                      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','ObjectKey','ObjectVersion','Sha256','Length','UpdatedAt']) THEN
                   RAISE EXCEPTION 'Export source is immutable and artifact commit is terminal' USING ERRCODE='23514';
                  END IF;
                 END IF;
                 IF (SELECT count(*) FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb))<>
                    (SELECT count(DISTINCT value) FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb)) THEN
                  RAISE EXCEPTION 'Export scope cannot repeat patients' USING ERRCODE='23514';
                 END IF;
                 FOR selected IN SELECT value::uuid FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb) LOOP
                  IF NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=selected AND p."IsActive" AND NOT p."IsDeleted"
                    AND NOT EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" s WHERE s."TenantId"=p."TenantId" AND s."PatientId"=p."Id" AND s."Restricted"))
                    OR NOT (NEW."PatientTagsJson"::jsonb ? selected::text) THEN
                   RAISE EXCEPTION 'Export patient scope is not current unrestricted scope' USING ERRCODE='23514';
                  END IF;
                 END LOOP;
                 FOR field IN SELECT value FROM jsonb_array_elements_text(NEW."FieldsJson"::jsonb) LOOP
                  IF field NOT IN ('patient_id','name','phone','email','age','gender') THEN RAISE EXCEPTION 'Unsupported directory export field' USING ERRCODE='23514'; END IF;
                 END LOOP;
                 RETURN NEW;
                END $guard$;
                CREATE TRIGGER patient_export_job_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientExportJobs" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_export_job();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                 GRANT SELECT,INSERT ON "PatientExportJobs" TO cureflow_tenant;
                 REVOKE UPDATE,DELETE ON "PatientExportJobs" FROM cureflow_tenant;
                 GRANT UPDATE ("State","Revision","ObjectKey","ObjectVersion","Sha256","Length","UpdatedAt") ON "PatientExportJobs" TO cureflow_tenant;
                END IF; END $grant$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Export provenance/artifacts require reviewed forward repair or protected restore; automatic downgrade is forbidden.");
        }
    }
}
