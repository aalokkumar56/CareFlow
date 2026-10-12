using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindTenantSessionAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecurityRevision",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SecurityRevision",
                table: "RefreshSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_FamilyId_UserId_SecurityRevision",
                table: "RefreshSessions",
                columns: new[] { "FamilyId", "UserId", "SecurityRevision" });
            migrationBuilder.Sql("""
                CREATE FUNCTION enforce_user_security_revision() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
                BEGIN
                    IF ROW(OLD."PasswordHash",OLD."Role",OLD."IsActive",OLD."IsDeleted",OLD."TenantId")
                       IS DISTINCT FROM ROW(NEW."PasswordHash",NEW."Role",NEW."IsActive",NEW."IsDeleted",NEW."TenantId")
                       OR NEW."SecurityRevision">OLD."SecurityRevision"
                    THEN
                        NEW."SecurityRevision":=GREATEST(OLD."SecurityRevision"+1,NEW."SecurityRevision");
                        UPDATE public."RefreshSessions" SET "Revoked"=true WHERE "UserId"=OLD."Id";
                    ELSE NEW."SecurityRevision":=OLD."SecurityRevision";
                    END IF;
                    RETURN NEW;
                END $$;
                REVOKE ALL ON FUNCTION enforce_user_security_revision() FROM PUBLIC;
                CREATE TRIGGER user_security_revision BEFORE UPDATE ON "Users"
                    FOR EACH ROW EXECUTE FUNCTION enforce_user_security_revision();
                CREATE FUNCTION revoke_role_assignment_sessions() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        UPDATE public."Users" SET "SecurityRevision"="SecurityRevision"+1 WHERE "Id"=OLD."UserId" AND "TenantId"=OLD."TenantId";
                    END IF;
                    IF TG_OP <> 'DELETE' AND (TG_OP='INSERT' OR ROW(NEW."UserId",NEW."TenantId") IS DISTINCT FROM ROW(OLD."UserId",OLD."TenantId")) THEN
                        UPDATE public."Users" SET "SecurityRevision"="SecurityRevision"+1 WHERE "Id"=NEW."UserId" AND "TenantId"=NEW."TenantId";
                    END IF;
                    RETURN NULL;
                END $$;
                REVOKE ALL ON FUNCTION revoke_role_assignment_sessions() FROM PUBLIC;
                CREATE TRIGGER role_assignment_sessions AFTER INSERT OR UPDATE OR DELETE ON "UserRoleAssignments"
                    FOR EACH ROW EXECUTE FUNCTION revoke_role_assignment_sessions();
                CREATE FUNCTION revoke_role_permission_sessions() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        UPDATE public."Users" u SET "SecurityRevision"=u."SecurityRevision"+1 WHERE EXISTS
                         (SELECT 1 FROM public."UserRoleAssignments" a WHERE a."RoleId"=OLD."RoleId" AND a."UserId"=u."Id" AND a."TenantId"=u."TenantId" AND NOT a."IsDeleted");
                    END IF;
                    IF TG_OP <> 'DELETE' AND (TG_OP='INSERT' OR NEW."RoleId" IS DISTINCT FROM OLD."RoleId") THEN
                        UPDATE public."Users" u SET "SecurityRevision"=u."SecurityRevision"+1 WHERE EXISTS
                         (SELECT 1 FROM public."UserRoleAssignments" a WHERE a."RoleId"=NEW."RoleId" AND a."UserId"=u."Id" AND a."TenantId"=u."TenantId" AND NOT a."IsDeleted");
                    END IF;
                    RETURN NULL;
                END $$;
                REVOKE ALL ON FUNCTION revoke_role_permission_sessions() FROM PUBLIC;
                CREATE TRIGGER role_permission_sessions AFTER INSERT OR UPDATE OR DELETE ON "RolePermissions"
                    FOR EACH ROW EXECUTE FUNCTION revoke_role_permission_sessions();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER role_permission_sessions ON "RolePermissions";
                DROP TRIGGER role_assignment_sessions ON "UserRoleAssignments";
                DROP TRIGGER user_security_revision ON "Users";
                DROP FUNCTION revoke_role_permission_sessions();
                DROP FUNCTION revoke_role_assignment_sessions();
                DROP FUNCTION enforce_user_security_revision();
                """);

            migrationBuilder.DropIndex(
                name: "IX_RefreshSessions_FamilyId_UserId_SecurityRevision",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "SecurityRevision",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityRevision",
                table: "RefreshSessions");
        }
    }
}

