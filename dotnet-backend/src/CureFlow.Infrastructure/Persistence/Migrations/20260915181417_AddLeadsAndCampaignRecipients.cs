using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadsAndCampaignRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceLeadId",
                table: "Patients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PatientId",
                table: "CampaignRecipients",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "LeadId",
                table: "CampaignRecipients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Age = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    ConvertedPatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_SourceLeadId",
                table: "Patients",
                column: "SourceLeadId",
                unique: true,
                filter: "\"SourceLeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_LeadId",
                table: "CampaignRecipients",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_ConvertedAt",
                table: "Leads",
                columns: new[] { "TenantId", "ConvertedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_Phone",
                table: "Leads",
                columns: new[] { "TenantId", "Phone" },
                unique: true,
                filter: "\"IsDeleted\" = false");
            migrationBuilder.Sql("""
                ALTER TABLE "Leads" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Leads" FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON "Leads"
                USING (current_setting('app.platform_admin', true) = 'true'
                    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (current_setting('app.platform_admin', true) = 'true'
                    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Patients_SourceLeadId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_CampaignRecipients_LeadId",
                table: "CampaignRecipients");

            migrationBuilder.DropColumn(
                name: "SourceLeadId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "CampaignRecipients");

            migrationBuilder.AlterColumn<Guid>(
                name: "PatientId",
                table: "CampaignRecipients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
