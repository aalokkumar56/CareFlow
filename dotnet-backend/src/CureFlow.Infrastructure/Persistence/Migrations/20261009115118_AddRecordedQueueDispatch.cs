using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedQueueDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_QueueEntries_TenantId_Id",
                table: "QueueEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "QueuePriorityOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QueueEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SortRank = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QueuePriorityOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueuePriorityOverrides_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueuePriorityOverrides_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueuePriorityOverrides_QueueEntries_TenantId_QueueEntryId",
                        columns: x => new { x.TenantId, x.QueueEntryId },
                        principalTable: "QueueEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueueStarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QueueEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SortRank = table.Column<int>(type: "integer", nullable: false),
                    DefaultRank = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QueueStarts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueStarts_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueueStarts_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueueStarts_QueueEntries_TenantId_QueueEntryId",
                        columns: x => new { x.TenantId, x.QueueEntryId },
                        principalTable: "QueueEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueuePriorityOverrides_TenantId_FacilityId_ArrivalDate",
                table: "QueuePriorityOverrides",
                columns: new[] { "TenantId", "FacilityId", "ArrivalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QueuePriorityOverrides_TenantId_PatientId",
                table: "QueuePriorityOverrides",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_QueuePriorityOverrides_TenantId_QueueEntryId_Revision",
                table: "QueuePriorityOverrides",
                columns: new[] { "TenantId", "QueueEntryId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueueStarts_TenantId_FacilityId_ArrivalDate",
                table: "QueueStarts",
                columns: new[] { "TenantId", "FacilityId", "ArrivalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueStarts_TenantId_PatientId",
                table: "QueueStarts",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueStarts_TenantId_QueueEntryId",
                table: "QueueStarts",
                columns: new[] { "TenantId", "QueueEntryId" },
                unique: true);
            migrationBuilder.Sql("""
DO $scope$ DECLARE tab text; BEGIN
 FOREACH tab IN ARRAY ARRAY['QueuePriorityOverrides','QueueStarts'] LOOP
 EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tab);
 EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tab);
 EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tab);
 EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING (public.cureflow_patient_read_allowed("TenantId","PatientId"))',tab);
 EXECUTE format('ALTER TABLE %I ADD CONSTRAINT dispatch_shape CHECK ("Revision">1 AND "SortRank" BETWEEN 0 AND 1000 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted")',tab);
 END LOOP;
END $scope$;
ALTER TABLE "QueuePriorityOverrides" ADD CONSTRAINT queue_override_reason CHECK (length(btrim("Reason")) BETWEEN 3 AND 500);
ALTER TABLE "QueueStarts" ADD CONSTRAINT queue_default_rank CHECK ("DefaultRank" BETWEEN 0 AND 1000);
CREATE FUNCTION public.guard_queue_dispatch() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry public."QueueEntries"; prior_revision bigint; prior_rank integer; first_id uuid;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Queue decisions are attributable retained evidence' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
 OR NOT EXISTS(SELECT 1 FROM public."Users" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."CreatedBy" AND "IsActive" AND NOT "IsDeleted")
 OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
 OR EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" WHERE "TenantId"=NEW."TenantId" AND "Restricted"
 AND NOT public.cureflow_patient_read_allowed("TenantId","PatientId")) THEN
 RAISE EXCEPTION 'Current complete hospital queue authority is required' USING ERRCODE='42501'; END IF;
 SELECT * INTO entry FROM public."QueueEntries" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."QueueEntryId";
 IF NOT FOUND OR entry."FacilityId"<>NEW."FacilityId" OR entry."PatientId"<>NEW."PatientId" OR entry."ArrivalDate"<>NEW."ArrivalDate"
 OR NOT EXISTS(SELECT 1 FROM public."Appointments" WHERE "TenantId"=NEW."TenantId" AND "Id"=entry."AppointmentId"
 AND "PatientId"=entry."PatientId" AND "FacilityId"=entry."FacilityId" AND "CheckedInAt"=entry."ArrivedAtUtc"
 AND "Status" IN (0,1) AND "IsActive" AND NOT "IsDeleted") THEN
 RAISE EXCEPTION 'Current exact pending arrival is required' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM public."QueueStarts" WHERE "TenantId"=NEW."TenantId" AND "QueueEntryId"=entry."Id") THEN
 RAISE EXCEPTION 'The queue item has already started' USING ERRCODE='23514'; END IF;
 SELECT "Revision","SortRank" INTO prior_revision,prior_rank FROM public."QueuePriorityOverrides"
 WHERE "TenantId"=NEW."TenantId" AND "QueueEntryId"=entry."Id" ORDER BY "Revision" DESC LIMIT 1;
 IF NEW."Revision"<>COALESCE(prior_revision,1)+1 THEN RAISE EXCEPTION 'The queue revision changed' USING ERRCODE='23514'; END IF;
 IF TG_TABLE_NAME='QueueStarts' THEN
 IF NEW."SortRank"<>COALESCE(prior_rank,NEW."DefaultRank") THEN RAISE EXCEPTION 'Start must retain current priority' USING ERRCODE='23514'; END IF;
 SELECT q."Id" INTO first_id FROM public."QueueEntries" q
 LEFT JOIN LATERAL(SELECT p."SortRank" FROM public."QueuePriorityOverrides" p WHERE p."TenantId"=q."TenantId" AND p."QueueEntryId"=q."Id" ORDER BY p."Revision" DESC LIMIT 1) p ON true
 WHERE q."TenantId"=NEW."TenantId" AND q."FacilityId"=NEW."FacilityId" AND q."ArrivalDate"=NEW."ArrivalDate"
 AND NOT EXISTS(SELECT 1 FROM public."QueueStarts" s WHERE s."TenantId"=q."TenantId" AND s."QueueEntryId"=q."Id")
 ORDER BY COALESCE(p."SortRank",NEW."DefaultRank"),q."Token" LIMIT 1;
 IF first_id IS DISTINCT FROM entry."Id" THEN RAISE EXCEPTION 'Only the current first queue item may start' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER queue_priority_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueuePriorityOverrides" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_dispatch();
CREATE TRIGGER queue_start_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueueStarts" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_dispatch();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "QueuePriorityOverrides","QueueStarts" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "QueuePriorityOverrides","QueueStarts" FROM cureflow_tenant;
END IF; END $grant$;

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Attributable queue history requires a reviewed forward migration.");
        }
    }
}
