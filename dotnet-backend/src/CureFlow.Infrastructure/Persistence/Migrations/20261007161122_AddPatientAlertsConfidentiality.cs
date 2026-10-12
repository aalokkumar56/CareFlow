using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientAlertsConfidentiality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientAlertVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AlertIsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportedDeathDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_PatientAlertVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAlertVersions_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientConfidentialityStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Restricted = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_PatientConfidentialityStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientConfidentialityStates_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAlertVersions_TenantId_AlertId_Revision",
                table: "PatientAlertVersions",
                columns: new[] { "TenantId", "AlertId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientAlertVersions_TenantId_PatientId_Category_Key_Revisi~",
                table: "PatientAlertVersions",
                columns: new[] { "TenantId", "PatientId", "Category", "Key", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientConfidentialityStates_TenantId_PatientId",
                table: "PatientConfidentialityStates",
                columns: new[] { "TenantId", "PatientId" },
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE "PatientConfidentialityStates" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientConfidentialityStates" FORCE ROW LEVEL SECURITY;
                ALTER TABLE "PatientAlertVersions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientAlertVersions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY patient_confidentiality_tenant ON "PatientConfidentialityStates" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY patient_alert_tenant ON "PatientAlertVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "PatientConfidentialityStates" ADD CONSTRAINT patient_confidentiality_revision_valid CHECK ("Revision">0 AND "IsActive" AND NOT "IsDeleted");
                ALTER TABLE "PatientAlertVersions" ADD CONSTRAINT patient_alert_valid CHECK (
                  "Revision">0 AND "Category" IN ('identity','clinical','confidentiality','deceased_reported') AND "Severity" IN ('info','warning','critical')
                  AND "SourceType" IN ('local_application','patient_reported','guardian_reported','document_reported','staff_observed','import')
                  AND length(btrim("Title"))>0 AND length(btrim("Key"))>0 AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
                  AND ("Category"<>'confidentiality' OR "Key"='chart') AND ("Category"<>'deceased_reported' OR "Key"='status')
                  AND ("ReportedDeathDate" IS NULL OR "Category"='deceased_reported') AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                CREATE FUNCTION validate_patient_alert_append() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE prior record; head_count bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(hashtextextended('patient-alerts:'||NEW."TenantId"::text||':'||NEW."PatientId"::text,0));
                  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active scoped patient required' USING ERRCODE='23514'; END IF;
                  SELECT "AlertId","Revision" INTO prior FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId"
                    AND "PatientId"=NEW."PatientId" AND "Category"=NEW."Category" AND "Key"=NEW."Key" ORDER BY "Revision" DESC LIMIT 1;
                  IF prior."Revision" IS NULL THEN
                    IF NEW."Revision"<>1 THEN RAISE EXCEPTION 'New alert starts at revision one' USING ERRCODE='23514'; END IF;
                    SELECT COUNT(*) INTO head_count FROM (SELECT DISTINCT "Category","Key" FROM "PatientAlertVersions"
                      WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId") heads;
                    IF head_count>=100 THEN RAISE EXCEPTION 'Patient alert count requires reconciliation' USING ERRCODE='23514'; END IF;
                  ELSIF NEW."AlertId"<>prior."AlertId" OR NEW."Revision"<>prior."Revision"+1 THEN
                    RAISE EXCEPTION 'Stable alert identity/consecutive evidence required' USING ERRCODE='23514';
                  END IF;
                  IF EXISTS(SELECT 1 FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "AlertId"=NEW."AlertId"
                    AND ("PatientId"<>NEW."PatientId" OR "Category"<>NEW."Category" OR "Key"<>NEW."Key")) THEN
                    RAISE EXCEPTION 'Alert ownership/type/key cannot change' USING ERRCODE='23514';
                  END IF;
                  NEW."EffectiveFrom"=clock_timestamp();
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_alert_append BEFORE INSERT ON "PatientAlertVersions" FOR EACH ROW EXECUTE FUNCTION validate_patient_alert_append();
                CREATE FUNCTION prevent_patient_alert_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Patient alert evidence is append-only' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER patient_alert_history BEFORE UPDATE OR DELETE ON "PatientAlertVersions" FOR EACH ROW EXECUTE FUNCTION prevent_patient_alert_rewrite();
                CREATE FUNCTION validate_patient_confidentiality_state() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE evidence record;
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Chart confidentiality state is retained' USING ERRCODE='23514'; END IF;
                  SELECT * INTO evidence FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId"
                    AND "Category"='confidentiality' AND "Key"='chart' ORDER BY "Revision" DESC LIMIT 1;
                  IF evidence."Id" IS NULL OR evidence."Revision"<>NEW."Revision" OR evidence."AlertIsActive"<>NEW."Restricted" THEN
                    RAISE EXCEPTION 'Chart restriction must match latest reviewed alert evidence' USING ERRCODE='23514';
                  END IF;
                  IF TG_OP='INSERT' THEN
                    IF NEW."Revision"<>1 THEN RAISE EXCEPTION 'Initial chart restriction requires first evidence' USING ERRCODE='23514'; END IF;
                  ELSE
                    IF NEW."Revision"<>OLD."Revision"+1 OR (to_jsonb(NEW)-ARRAY['Restricted','Revision','UpdatedAt','UpdatedBy'])
                      IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Restricted','Revision','UpdatedAt','UpdatedBy']) THEN
                      RAISE EXCEPTION 'Chart ownership and consecutive history must be retained' USING ERRCODE='23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_confidentiality_state_change BEFORE INSERT OR UPDATE OR DELETE ON "PatientConfidentialityStates"
                  FOR EACH ROW EXECUTE FUNCTION validate_patient_confidentiality_state();
                CREATE FUNCTION require_patient_confidentiality_state() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF NEW."Category"='confidentiality' AND NOT EXISTS(SELECT 1 FROM "PatientConfidentialityStates"
                    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "Revision">=NEW."Revision"
                      AND "Revision"=(SELECT MAX("Revision") FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "Category"='confidentiality' AND "Key"='chart')
                      AND "Restricted"=(SELECT "AlertIsActive" FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "Category"='confidentiality' AND "Key"='chart' ORDER BY "Revision" DESC LIMIT 1)) THEN
                    RAISE EXCEPTION 'Confidentiality evidence and effective state must commit together' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE CONSTRAINT TRIGGER patient_confidentiality_state_required AFTER INSERT ON "PatientAlertVersions"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_patient_confidentiality_state();
                INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
                  VALUES ('8a717e93-adb7-4ef3-9a1c-7a7dc032d6e9','PatientAlerts.View','Read local patient alerts','Current scope and sourced reported evidence',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
                         ('630cfda1-f72c-4d7e-9e65-b30c7961c616','PatientAlerts.Manage','Manage sourced local patient alerts','Versioned audited reasons and review policy',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
                         ('064e15f7-1f38-4283-9200-6c02bdbe512c','PatientRestricted.View','Read confidential local charts','Explicit reviewed grant; no family/emergency sharing',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
                  ON CONFLICT ("Code") DO NOTHING;
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT ON "PatientAlertVersions" TO cureflow_tenant;
                  REVOKE UPDATE,DELETE ON "PatientAlertVersions" FROM cureflow_tenant;
                  GRANT SELECT,INSERT,UPDATE ON "PatientConfidentialityStates" TO cureflow_tenant;
                  REVOKE DELETE ON "PatientConfidentialityStates" FROM cureflow_tenant;
                END IF; END $grant$;
                """);
            migrationBuilder.Sql("""
                -- Install after confidentiality state creation. Predicate reads current persisted
                -- authority, never JWT permission strings or caller-supplied clearance booleans.
                CREATE FUNCTION public.cureflow_patient_read_allowed(scope_tenant uuid, scope_patient uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER
                SET search_path=pg_catalog,public,pg_temp AS $body$
                 SELECT scope_tenant= NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
                   AND (NOT EXISTS (SELECT 1 FROM public."PatientConfidentialityStates" s
                     WHERE s."TenantId"=scope_tenant AND s."PatientId"=scope_patient AND s."Restricted" AND s."IsActive" AND NOT s."IsDeleted")
                   OR EXISTS (
                     SELECT 1 FROM public."Users" u
                       JOIN public."UserRoleAssignments" a ON a."TenantId"=u."TenantId" AND a."UserId"=u."Id" AND a."IsActive" AND NOT a."IsDeleted"
                       JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
                       JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
                       JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
                     WHERE u."TenantId"=scope_tenant AND u."Id"=NULLIF(current_setting('app.current_user_id',true),'')::uuid
                       AND u."IsActive" AND NOT u."IsDeleted" AND p."Code"='PatientRestricted.View'));
                $body$;
                REVOKE ALL ON FUNCTION public.cureflow_patient_read_allowed(uuid,uuid) FROM PUBLIC;
                
                -- Restrictive SELECT policies also protect search/count/raw child projections.
                -- State itself contains only the minimum binary scope data, preventing recursion.
                DO $guard$
                DECLARE tbl record; expression text; col text;
                BEGIN
                 FOR tbl IN SELECT c.oid,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='public' AND c.relkind IN ('r','p')
                     AND c.relname<>'PatientConfidentialityStates'
                     AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='TenantId' AND NOT a.attisdropped)
                 LOOP
                  expression=NULL;
                  IF tbl.relname='Patients' THEN
                   expression='public.cureflow_patient_read_allowed("TenantId","Id")';
                  ELSE
                   FOREACH col IN ARRAY ARRAY['PatientId','FirstPatientId','SecondPatientId','SourcePatientId','SurvivorPatientId'] LOOP
                    IF EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname=col AND NOT a.attisdropped) THEN
                     expression=concat_ws(' AND ',expression,format('(%1$I IS NULL OR public.cureflow_patient_read_allowed("TenantId",%1$I))',col));
                    END IF;
                   END LOOP;
                   IF expression IS NULL AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='VisitId' AND NOT a.attisdropped) THEN
                    expression=format('EXISTS(SELECT 1 FROM public."Visits" v WHERE v."TenantId"=%1$I."TenantId" AND v."Id"=%1$I."VisitId")',tbl.relname);
                   ELSIF expression IS NULL AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='PrescriptionId' AND NOT a.attisdropped) THEN
                    expression=format('EXISTS(SELECT 1 FROM public."Prescriptions" p WHERE p."TenantId"=%1$I."TenantId" AND p."Id"=%1$I."PrescriptionId")',tbl.relname);
                   ELSIF expression IS NULL AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='MergeId' AND NOT a.attisdropped) THEN
                    expression=format('EXISTS(SELECT 1 FROM public."PatientMerges" m WHERE m."TenantId"=%1$I."TenantId" AND m."Id"=%1$I."MergeId")',tbl.relname);
                   ELSIF expression IS NULL AND tbl.relname='DocumentScanEvidence' THEN
                    expression='EXISTS(SELECT 1 FROM public."PatientDocuments" d WHERE d."TenantId"="DocumentScanEvidence"."TenantId" AND d."Id"="DocumentScanEvidence"."DocumentId")';
                   ELSIF expression IS NULL AND tbl.relname='PrivateDocumentVersions' THEN
                    expression='("DocumentKind"=''patient_document'' AND EXISTS(SELECT 1 FROM public."PatientDocuments" d WHERE d."TenantId"="PrivateDocumentVersions"."TenantId" AND d."Id"="PrivateDocumentVersions"."DocumentId"))
                      OR ("DocumentKind"=''lab_report'' AND EXISTS(SELECT 1 FROM public."LabReports" l WHERE l."TenantId"="PrivateDocumentVersions"."TenantId" AND l."Id"="PrivateDocumentVersions"."DocumentId"))';
                   END IF;
                  END IF;
                  IF expression IS NOT NULL THEN
                   EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',tbl.relname);
                   EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY',tbl.relname);
                   EXECUTE format('CREATE POLICY patient_confidentiality_read ON public.%I AS RESTRICTIVE FOR SELECT USING (%s)',tbl.relname,expression);
                  END IF;
                 END LOOP;
                 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT EXECUTE ON FUNCTION public.cureflow_patient_read_allowed(uuid,uuid) TO cureflow_tenant;
                 END IF;
                END $guard$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Confidentiality/alert evidence requires protected restore or reviewed forward repair; automatic downgrade is forbidden.");
        }
    }
}
