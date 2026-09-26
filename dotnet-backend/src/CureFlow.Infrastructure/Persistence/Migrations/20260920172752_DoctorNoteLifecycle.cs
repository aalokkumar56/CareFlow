using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoctorNoteLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditableUntil",
                table: "ClinicalNotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "ClinicalNotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAutoSavedAt",
                table: "ClinicalNotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "ClinicalNotes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "ClinicalNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Existing records were saved clinical notes, never recoverable drafts.
            // Their original language and finalization time were not recorded.
            migrationBuilder.Sql("""
                DO $backfill$
                DECLARE previous_scope text := current_setting('app.platform_admin', true);
                BEGIN
                    PERFORM set_config('app.platform_admin', 'true', true);
                    UPDATE "ClinicalNotes" SET "OriginalLanguage" = 'und', "Revision" = 1,
                        "FinalizedAt" = "CreatedAt",
                        "EditableUntil" = LEAST("CreatedAt" + INTERVAL '24 hours', CURRENT_TIMESTAMP);
                    PERFORM set_config('app.platform_admin', COALESCE(previous_scope, 'false'), true);
                END;
                $backfill$;

                CREATE FUNCTION protect_doctor_note() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD."FinalizedAt" IS NOT NULL THEN
                            RAISE EXCEPTION 'Finalized Doctor Notes cannot be deleted';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF NEW."PatientId" IS DISTINCT FROM OLD."PatientId"
                        OR NEW."VisitId" IS DISTINCT FROM OLD."VisitId"
                        OR NEW."AppointmentId" IS DISTINCT FROM OLD."AppointmentId"
                        OR NEW."AuthorUserId" IS DISTINCT FROM OLD."AuthorUserId"
                        OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId"
                        OR NEW."AuthorName" IS DISTINCT FROM OLD."AuthorName" THEN
                        RAISE EXCEPTION 'Doctor Note association and authorship are immutable';
                    END IF;
                    IF OLD."FinalizedAt" IS NOT NULL THEN
                        IF NEW."IsDeleted" OR NOT NEW."IsActive"
                            OR NEW."FinalizedAt" IS DISTINCT FROM OLD."FinalizedAt"
                            OR NEW."EditableUntil" IS DISTINCT FROM OLD."EditableUntil" THEN
                            RAISE EXCEPTION 'Finalized Doctor Notes cannot be removed or reopened';
                        END IF;
                        IF OLD."EditableUntil" IS NULL OR clock_timestamp() >= OLD."EditableUntil" THEN
                            RAISE EXCEPTION 'Doctor Note editing window has ended';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_doctor_note BEFORE UPDATE OR DELETE ON "ClinicalNotes"
                    FOR EACH ROW EXECUTE FUNCTION protect_doctor_note();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS protect_doctor_note ON "ClinicalNotes";
                DROP FUNCTION IF EXISTS protect_doctor_note();
                """);
            migrationBuilder.DropColumn(
                name: "EditableUntil",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "LastAutoSavedAt",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ClinicalNotes");
        }
    }
}
