using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLifecycleReviewEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewStartedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewStartedByPlatformUserId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByPlatformUserId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewStartedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReviewStartedByPlatformUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReviewedByPlatformUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Tenants");
        }
    }
}
