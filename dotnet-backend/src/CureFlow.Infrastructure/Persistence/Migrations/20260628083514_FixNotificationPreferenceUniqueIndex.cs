using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixNotificationPreferenceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~",
                table: "NotificationPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~",
                table: "NotificationPreferences",
                columns: new[] { "TenantId", "UserId", "NotificationType", "Channel" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~",
                table: "NotificationPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~",
                table: "NotificationPreferences",
                columns: new[] { "TenantId", "UserId", "NotificationType", "Channel" },
                unique: true);
        }
    }
}
