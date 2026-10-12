using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientAppointmentQueryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId_PatientId_ScheduledAt_Id",
                table: "Appointments",
                columns: new[] { "TenantId", "PatientId", "ScheduledAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_TenantId_PatientId_ScheduledAt_Id",
                table: "Appointments");
        }
    }
}
