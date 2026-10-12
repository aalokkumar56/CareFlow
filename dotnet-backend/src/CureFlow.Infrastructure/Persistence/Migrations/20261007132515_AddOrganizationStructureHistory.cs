using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationStructureHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationStructureVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeCategory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NodeIsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_OrganizationStructureVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationStructureVersions_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationStructureVersions_TenantId_FacilityId_NodeCate~1",
                table: "OrganizationStructureVersions",
                columns: new[] { "TenantId", "FacilityId", "NodeCategory", "NodeId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationStructureVersions_TenantId_FacilityId_NodeCateg~",
                table: "OrganizationStructureVersions",
                columns: new[] { "TenantId", "FacilityId", "NodeCategory", "NodeId", "EffectiveFrom" });
            migrationBuilder.Sql("""
                ALTER TABLE "OrganizationStructureVersions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "OrganizationStructureVersions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY structure_history_tenant ON "OrganizationStructureVersions"
                  USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "OrganizationStructureVersions" ADD CONSTRAINT structure_version_valid CHECK ("Revision">0 AND "NodeCategory" IN ('units','locations'));
                CREATE FUNCTION retain_organization_structure_version() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $body$
                BEGIN
                  INSERT INTO public."OrganizationStructureVersions" ("Id","TenantId","FacilityId","NodeId","NodeCategory","Revision","EffectiveFrom","ParentId","Name","Code","Kind","NodeIsActive","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
                    VALUES (gen_random_uuid(),NEW."TenantId",NEW."FacilityId",NEW."Id",CASE WHEN TG_TABLE_NAME='OrganizationUnits' THEN 'units' ELSE 'locations' END,
                      NEW."Revision",clock_timestamp(),NEW."ParentId",NEW."Name",NEW."Code",NEW."Kind",NEW."IsActive",clock_timestamp(),clock_timestamp(),COALESCE(NEW."UpdatedBy",NEW."CreatedBy"),NULL,true,false);
                  RETURN NEW;
                END $body$;
                REVOKE ALL ON FUNCTION retain_organization_structure_version() FROM PUBLIC;
                CREATE TRIGGER retain_unit_structure AFTER INSERT OR UPDATE ON "OrganizationUnits" FOR EACH ROW EXECUTE FUNCTION retain_organization_structure_version();
                CREATE TRIGGER retain_location_structure AFTER INSERT OR UPDATE ON "FacilityLocations" FOR EACH ROW EXECUTE FUNCTION retain_organization_structure_version();
                CREATE FUNCTION prevent_structure_history_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Organization structure history is immutable' USING ERRCODE='23514'; END $body$;
                CREATE TRIGGER structure_history_immutable BEFORE UPDATE OR DELETE ON "OrganizationStructureVersions" FOR EACH ROW EXECUTE FUNCTION prevent_structure_history_rewrite();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  GRANT SELECT ON "OrganizationStructureVersions" TO cureflow_tenant;
                  REVOKE INSERT,UPDATE,DELETE ON "OrganizationStructureVersions" FROM cureflow_tenant;
                END IF; END $grant$;
                -- No pre-rollout history backfill: effective identities must not be invented from current rows.
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER retain_unit_structure ON \"OrganizationUnits\"; DROP TRIGGER retain_location_structure ON \"FacilityLocations\"; DROP FUNCTION retain_organization_structure_version();");
            migrationBuilder.DropTable(
                name: "OrganizationStructureVersions");
            migrationBuilder.Sql("DROP FUNCTION prevent_structure_history_rewrite();");
        }
    }
}
