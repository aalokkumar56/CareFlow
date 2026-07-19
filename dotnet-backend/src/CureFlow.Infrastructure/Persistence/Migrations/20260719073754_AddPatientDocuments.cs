using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByName = table.Column<string>(type: "text", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_PatientDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_PatientId_CapturedAt",
                table: "PatientDocuments",
                columns: new[] { "PatientId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_VisitId",
                table: "PatientDocuments",
                column: "VisitId");

            migrationBuilder.Sql("""
                ALTER TABLE "PatientDocuments" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "PatientDocuments" FORCE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON "PatientDocuments";

                CREATE POLICY tenant_isolation ON "PatientDocuments"
                  USING (
                    current_setting('app.platform_admin', true) = 'true'
                    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                  )
                  WITH CHECK (
                    current_setting('app.platform_admin', true) = 'true'
                    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS tenant_isolation ON "PatientDocuments";
                ALTER TABLE "PatientDocuments" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "PatientDocuments" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "PatientDocuments");
        }
    }
}
