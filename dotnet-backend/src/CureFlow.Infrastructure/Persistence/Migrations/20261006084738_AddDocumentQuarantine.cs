using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentQuarantine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScanEvidenceId",
                table: "PatientDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanStatus",
                table: "PatientDocuments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "unresolved");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScannedAt",
                table: "PatientDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentScanEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Engine = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_DocumentScanEvidence", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_ScanEvidenceId",
                table: "PatientDocuments",
                column: "ScanEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_TenantId_ScanStatus",
                table: "PatientDocuments",
                columns: new[] { "TenantId", "ScanStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentScanEvidence_TenantId_DocumentId_Version",
                table: "DocumentScanEvidence",
                columns: new[] { "TenantId", "DocumentId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientDocuments_DocumentScanEvidence_ScanEvidenceId",
                table: "PatientDocuments",
                column: "ScanEvidenceId",
                principalTable: "DocumentScanEvidence",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE "DocumentScanEvidence" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "DocumentScanEvidence" FORCE ROW LEVEL SECURITY;
                CREATE POLICY document_scan_tenant ON "DocumentScanEvidence"
                    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "PatientDocuments" ADD CONSTRAINT document_scan_status CHECK ("ScanStatus" IN ('unresolved','staged','quarantined','available','rejected','failed'));
                CREATE FUNCTION prevent_document_scan_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Document scan evidence is append-only' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER document_scan_append_only BEFORE UPDATE OR DELETE ON "DocumentScanEvidence"
                    FOR EACH ROW EXECUTE FUNCTION prevent_document_scan_rewrite();
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                        GRANT SELECT,INSERT ON "DocumentScanEvidence" TO cureflow_tenant;
                        REVOKE UPDATE,DELETE ON "DocumentScanEvidence" FROM cureflow_tenant;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientDocuments_DocumentScanEvidence_ScanEvidenceId",
                table: "PatientDocuments");

            migrationBuilder.DropTable(
                name: "DocumentScanEvidence");
            migrationBuilder.Sql("DROP FUNCTION prevent_document_scan_rewrite();");

            migrationBuilder.DropIndex(
                name: "IX_PatientDocuments_ScanEvidenceId",
                table: "PatientDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PatientDocuments_TenantId_ScanStatus",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "ScanEvidenceId",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "ScannedAt",
                table: "PatientDocuments");
        }
    }
}

