using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientIdentityReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientIdentityReviewVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstPatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondPatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstPatientTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SecondPatientTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExactNameSignal = table.Column<bool>(type: "boolean", nullable: false),
                    SharedPhoneSignal = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_PatientIdentityReviewVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientIdentityReviewVersions_Patients_TenantId_FirstPatien~",
                        columns: x => new { x.TenantId, x.FirstPatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientIdentityReviewVersions_Patients_TenantId_SecondPatie~",
                        columns: x => new { x.TenantId, x.SecondPatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentityReviewVersions_TenantId_FirstPatientId_Secon~",
                table: "PatientIdentityReviewVersions",
                columns: new[] { "TenantId", "FirstPatientId", "SecondPatientId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentityReviewVersions_TenantId_ReviewId_Revision",
                table: "PatientIdentityReviewVersions",
                columns: new[] { "TenantId", "ReviewId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentityReviewVersions_TenantId_SecondPatientId",
                table: "PatientIdentityReviewVersions",
                columns: new[] { "TenantId", "SecondPatientId" });
            migrationBuilder.Sql("""
                ALTER TABLE "PatientIdentityReviewVersions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientIdentityReviewVersions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY identity_review_tenant ON "PatientIdentityReviewVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "PatientIdentityReviewVersions" ADD CONSTRAINT identity_review_valid CHECK (
                  "FirstPatientId"<"SecondPatientId" AND "Revision">0 AND "Decision" IN ('open','different','potential_same','inconclusive')
                  AND length(btrim("EvidenceReference"))>0 AND length(btrim("Reason"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                CREATE FUNCTION validate_identity_review_append() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE previous_revision bigint; previous_review uuid; patient_count bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||NEW."FirstPatientId"::text||':'||NEW."SecondPatientId"::text,0));
                  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id" IN (NEW."FirstPatientId",NEW."SecondPatientId") AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE;
                  GET DIAGNOSTICS patient_count=ROW_COUNT;
                  IF patient_count<>2 THEN RAISE EXCEPTION 'Two active scoped identities required' USING ERRCODE='23514'; END IF;
                  SELECT "Revision","ReviewId" INTO previous_revision,previous_review FROM "PatientIdentityReviewVersions"
                    WHERE "TenantId"=NEW."TenantId" AND "FirstPatientId"=NEW."FirstPatientId" AND "SecondPatientId"=NEW."SecondPatientId" ORDER BY "Revision" DESC LIMIT 1;
                  IF previous_revision IS NULL THEN
                    IF NEW."Revision"<>1 OR NEW."Decision"<>'open' THEN RAISE EXCEPTION 'New pair reviews begin open' USING ERRCODE='23514'; END IF;
                  ELSIF NEW."Revision"<>previous_revision+1 OR NEW."ReviewId"<>previous_review THEN
                    RAISE EXCEPTION 'Review identity and consecutive evidence must be retained' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER identity_review_append BEFORE INSERT ON "PatientIdentityReviewVersions" FOR EACH ROW EXECUTE FUNCTION validate_identity_review_append();
                CREATE FUNCTION prevent_identity_review_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Identity review evidence is append-only' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER identity_review_history BEFORE UPDATE OR DELETE ON "PatientIdentityReviewVersions" FOR EACH ROW EXECUTE FUNCTION prevent_identity_review_rewrite();
                INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
                  VALUES ('849fbf4f-cae0-470d-9011-2c4956a36d39','IdentityReview.View','Read local identity reviews','Minimum fields and protected review evidence',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
                         ('d7114e87-3a89-43be-98e4-b20bfd182c87','IdentityReview.Manage','Manage local identity reviews','Explicit candidate review without merge authority',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
                  ON CONFLICT ("Code") DO NOTHING;
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT ON "PatientIdentityReviewVersions" TO cureflow_tenant;
                  REVOKE UPDATE,DELETE ON "PatientIdentityReviewVersions" FROM cureflow_tenant;
                END IF; END $grant$;
                -- Catalogue permissions only: no reviewer role assignments or identity merge authority are invented.
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientIdentityReviewVersions");
            migrationBuilder.Sql("DROP FUNCTION validate_identity_review_append(); DROP FUNCTION prevent_identity_review_rewrite();");
        }
    }
}
