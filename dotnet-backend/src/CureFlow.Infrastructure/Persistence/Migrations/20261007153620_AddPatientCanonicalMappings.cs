using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientCanonicalMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_PatientIdentityReviewVersions_TenantId_Id",
                table: "PatientIdentityReviewVersions",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "PatientMerges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SurvivorPatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePatientTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SurvivorPatientTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PatientMerges", x => x.Id);
                    table.UniqueConstraint("AK_PatientMerges_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PatientMerges_PatientIdentityReviewVersions_TenantId_Review~",
                        columns: x => new { x.TenantId, x.ReviewVersionId },
                        principalTable: "PatientIdentityReviewVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMerges_Patients_TenantId_SourcePatientId",
                        columns: x => new { x.TenantId, x.SourcePatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMerges_Patients_TenantId_SurvivorPatientId",
                        columns: x => new { x.TenantId, x.SurvivorPatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientLinkJournals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MergeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AfterValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicationHold = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_PatientLinkJournals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientLinkJournals_PatientMerges_TenantId_MergeId",
                        columns: x => new { x.TenantId, x.MergeId },
                        principalTable: "PatientMerges",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientLinkJournals_TenantId_MergeId_Operation",
                table: "PatientLinkJournals",
                columns: new[] { "TenantId", "MergeId", "Operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientMerges_TenantId_ReviewVersionId",
                table: "PatientMerges",
                columns: new[] { "TenantId", "ReviewVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientMerges_TenantId_SourcePatientId",
                table: "PatientMerges",
                columns: new[] { "TenantId", "SourcePatientId" },
                unique: true,
                filter: "\"ReversedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMerges_TenantId_SurvivorPatientId",
                table: "PatientMerges",
                columns: new[] { "TenantId", "SurvivorPatientId" });
            migrationBuilder.Sql("""
                ALTER TABLE "PatientMerges" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientMerges" FORCE ROW LEVEL SECURITY;
                ALTER TABLE "PatientLinkJournals" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientLinkJournals" FORCE ROW LEVEL SECURITY;
                CREATE POLICY patient_merge_tenant ON "PatientMerges" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY patient_link_journal_tenant ON "PatientLinkJournals" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "PatientMerges" ADD CONSTRAINT patient_merge_valid CHECK (
                  "SourcePatientId"<>"SurvivorPatientId" AND "Revision"=1 AND "ReversedAt" IS NULL AND "ReversalReason" IS NULL
                  AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                ALTER TABLE "PatientLinkJournals" ADD CONSTRAINT patient_link_valid CHECK (
                  "Operation"='merge' AND "EntityType"='PatientIdentityMapping' AND NOT "PublicationHold"
                  AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                CREATE FUNCTION validate_patient_merge_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE patient_count bigint; evidence record; ancestor_depth integer; first_id uuid; second_id uuid;
                BEGIN
                  PERFORM pg_advisory_xact_lock(hashtextextended('patient-mapping:'||NEW."TenantId"::text,0));
                  first_id=LEAST(NEW."SourcePatientId",NEW."SurvivorPatientId");
                  second_id=GREATEST(NEW."SourcePatientId",NEW."SurvivorPatientId");
                  PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||first_id::text||':'||second_id::text,0));
                  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id" IN (first_id,second_id)
                    AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE;
                  GET DIAGNOSTICS patient_count=ROW_COUNT;
                  IF patient_count<>2 THEN RAISE EXCEPTION 'Two active scoped identities required' USING ERRCODE='23514'; END IF;
                  SELECT * INTO evidence FROM "PatientIdentityReviewVersions"
                    WHERE "TenantId"=NEW."TenantId" AND "FirstPatientId"=first_id AND "SecondPatientId"=second_id
                    ORDER BY "Revision" DESC LIMIT 1 FOR SHARE;
                  IF evidence."Id" IS DISTINCT FROM NEW."ReviewVersionId" OR evidence."Decision"<>'potential_same'
                    OR evidence."CreatedBy" IS NULL OR evidence."CreatedBy"=NEW."CreatedBy"
                    OR evidence."FirstPatientTag" IS DISTINCT FROM (CASE WHEN first_id=NEW."SourcePatientId" THEN NEW."SourcePatientTag" ELSE NEW."SurvivorPatientTag" END)
                    OR evidence."SecondPatientTag" IS DISTINCT FROM (CASE WHEN second_id=NEW."SourcePatientId" THEN NEW."SourcePatientTag" ELSE NEW."SurvivorPatientTag" END) THEN
                    RAISE EXCEPTION 'Current distinct review evidence required' USING ERRCODE='23514';
                  END IF;
                  IF EXISTS(SELECT 1 FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId"
                    AND "SourcePatientId" IN (first_id,second_id) AND "ReversedAt" IS NULL) THEN
                    RAISE EXCEPTION 'Explicit canonical endpoints required' USING ERRCODE='23514';
                  END IF;
                  WITH RECURSIVE ancestors(id,depth) AS (
                    SELECT NEW."SourcePatientId",0 UNION ALL SELECT m."SourcePatientId",a.depth+1 FROM "PatientMerges" m JOIN ancestors a ON m."SurvivorPatientId"=a.id
                    WHERE m."TenantId"=NEW."TenantId" AND m."ReversedAt" IS NULL AND a.depth<32)
                    SELECT COALESCE(MAX(depth),0) INTO ancestor_depth FROM ancestors;
                  IF ancestor_depth>=31 THEN RAISE EXCEPTION 'Mapping depth requires review' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_merge_insert BEFORE INSERT ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION validate_patient_merge_insert();
                CREATE FUNCTION prevent_patient_mapping_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Patient mapping evidence is retained' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER patient_merge_history BEFORE UPDATE OR DELETE ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION prevent_patient_mapping_rewrite();
                CREATE TRIGGER patient_link_history BEFORE UPDATE OR DELETE ON "PatientLinkJournals" FOR EACH ROW EXECUTE FUNCTION prevent_patient_mapping_rewrite();
                CREATE FUNCTION validate_patient_link_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE mapping record;
                BEGIN
                  SELECT * INTO mapping FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."MergeId";
                  IF mapping."Id" IS NULL OR NEW."EntityId"<>mapping."SourcePatientId" OR NEW."CreatedBy"<>mapping."CreatedBy"
                     OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
                     OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text THEN
                    RAISE EXCEPTION 'Exact retained mapping journal required' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_link_insert BEFORE INSERT ON "PatientLinkJournals" FOR EACH ROW EXECUTE FUNCTION validate_patient_link_insert();
                INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
                  VALUES ('d06f1860-b8fb-4138-b8be-b807fa51e21d','IdentityMerge.View','Resolve explicit local identity mappings','No automatic chart aggregation',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
                         ('d4f147f7-6835-4f59-b94f-3fcf3816941d','IdentityMerge.Manage','Apply reviewed local identity mappings','Distinct review and retained source IDs',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
                  ON CONFLICT ("Code") DO NOTHING;
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT ON "PatientMerges","PatientLinkJournals" TO cureflow_tenant;
                  REVOKE UPDATE,DELETE ON "PatientMerges","PatientLinkJournals" FROM cureflow_tenant;
                END IF; END $grant$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientLinkJournals");

            migrationBuilder.DropTable(
                name: "PatientMerges");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PatientIdentityReviewVersions_TenantId_Id",
                table: "PatientIdentityReviewVersions");
            migrationBuilder.Sql("DROP FUNCTION validate_patient_link_insert(); DROP FUNCTION validate_patient_merge_insert(); DROP FUNCTION prevent_patient_mapping_rewrite();");
        }
    }
}
