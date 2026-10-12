using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateDocumentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "PatientDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "PatientDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageVersion",
                table: "PatientDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StoredLength",
                table: "PatientDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "LabReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "LabReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "LabReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageVersion",
                table: "LabReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoredContentType",
                table: "LabReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StoredLength",
                table: "LabReports",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrivateDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(65)", maxLength: 65, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LegacyFileUrl = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_PrivateDocumentVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_StorageVersion",
                table: "PatientDocuments",
                column: "StorageVersion");

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_StorageVersion",
                table: "LabReports",
                column: "StorageVersion");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateDocumentVersions_TenantId_ObjectKey",
                table: "PrivateDocumentVersions",
                columns: new[] { "TenantId", "ObjectKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LabReports_PrivateDocumentVersions_StorageVersion",
                table: "LabReports",
                column: "StorageVersion",
                principalTable: "PrivateDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientDocuments_PrivateDocumentVersions_StorageVersion",
                table: "PatientDocuments",
                column: "StorageVersion",
                principalTable: "PrivateDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE "PrivateDocumentVersions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PrivateDocumentVersions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY private_document_version_tenant ON "PrivateDocumentVersions"
                    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE FUNCTION prevent_private_document_version_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Retained document versions are immutable' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER private_document_versions_append_only BEFORE UPDATE OR DELETE ON "PrivateDocumentVersions"
                    FOR EACH ROW EXECUTE FUNCTION prevent_private_document_version_rewrite();
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                        GRANT SELECT,INSERT ON "PrivateDocumentVersions" TO cureflow_tenant;
                        REVOKE UPDATE,DELETE ON "PrivateDocumentVersions" FROM cureflow_tenant;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabReports_PrivateDocumentVersions_StorageVersion",
                table: "LabReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientDocuments_PrivateDocumentVersions_StorageVersion",
                table: "PatientDocuments");

            migrationBuilder.DropTable(
                name: "PrivateDocumentVersions");
            migrationBuilder.Sql("DROP FUNCTION prevent_private_document_version_rewrite();");

            migrationBuilder.DropIndex(
                name: "IX_PatientDocuments_StorageVersion",
                table: "PatientDocuments");

            migrationBuilder.DropIndex(
                name: "IX_LabReports_StorageVersion",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "StorageVersion",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "StoredLength",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "StorageVersion",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "StoredContentType",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "StoredLength",
                table: "LabReports");
        }
    }
}
