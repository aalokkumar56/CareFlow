using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacilityResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    OperationalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
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
                    table.PrimaryKey("PK_FacilityResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityResources_FacilityLocations_TenantId_FacilityId_Loc~",
                        columns: x => new { x.TenantId, x.FacilityId, x.LocationId },
                        principalTable: "FacilityLocations",
                        principalColumns: new[] { "TenantId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityResources_TenantId_FacilityId_Code",
                table: "FacilityResources",
                columns: new[] { "TenantId", "FacilityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityResources_TenantId_FacilityId_LocationId",
                table: "FacilityResources",
                columns: new[] { "TenantId", "FacilityId", "LocationId" });
            migrationBuilder.Sql("""
                ALTER TABLE "FacilityResources" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "FacilityResources" FORCE ROW LEVEL SECURITY;
                CREATE POLICY resource_tenant ON "FacilityResources" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "FacilityResources" ADD CONSTRAINT resource_configuration_valid CHECK (
                  "Capacity" BETWEEN 1 AND 100000 AND "Revision">0 AND "Code" ~ '^[A-Z0-9_-]{1,64}$'
                  AND "ResourceType" ~ '^[a-z0-9_-]{1,32}$' AND length(btrim("Name"))>0
                  AND "OperationalStatus" IN ('enabled','disabled','maintenance')
                  AND ("EffectiveUntil" IS NULL OR "EffectiveUntil">"EffectiveFrom"));
                CREATE FUNCTION enforce_facility_resource() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Retained resources cannot be deleted' USING ERRCODE='23514'; END IF;
                  PERFORM pg_advisory_xact_lock(hashtextextended('organization-db:'||NEW."TenantId"::text||':'||NEW."FacilityId"::text,0));
                  IF NEW."IsDeleted" OR (TG_OP='INSERT' AND NEW."Revision"<>1) THEN
                    RAISE EXCEPTION 'Invalid retained resource revision' USING ERRCODE='23514'; END IF;
                  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId" OR NEW."Code"<>OLD."Code"
                    OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR NEW."Revision"<>OLD."Revision"+1) THEN
                    RAISE EXCEPTION 'Resource identity/provenance must be retained' USING ERRCODE='23514'; END IF;
                  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
                  PERFORM "Id" FROM "FacilityLocations" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."LocationId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Active same-facility location required' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER resource_guard BEFORE INSERT OR UPDATE OR DELETE ON "FacilityResources" FOR EACH ROW EXECUTE FUNCTION enforce_facility_resource();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT,INSERT,UPDATE ON "FacilityResources" TO cureflow_tenant;
                  REVOKE DELETE ON "FacilityResources" FROM cureflow_tenant;
                END IF; END $grant$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityResources");
            migrationBuilder.Sql("DROP FUNCTION enforce_facility_resource();");
        }
    }
}
