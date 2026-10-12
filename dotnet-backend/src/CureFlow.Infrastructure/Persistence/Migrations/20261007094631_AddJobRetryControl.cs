using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRetryControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperatorRetries",
                table: "OutboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "OutboxEvents",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);
            migrationBuilder.Sql("""
                ALTER TABLE "OutboxEvents" ADD CONSTRAINT outbox_operator_budget CHECK ("OperatorRetries" BETWEEN 0 AND 3 AND "Revision">0);
                CREATE FUNCTION enforce_outbox_retry_version() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Attempts"<OLD."Attempts" OR NEW."OperatorRetries"<OLD."OperatorRetries"
                       OR NEW."OperatorRetries">OLD."OperatorRetries"+1 THEN
                        RAISE EXCEPTION 'Outbox retry provenance cannot be reset' USING ERRCODE='23514';
                    END IF;
                    IF NEW."OperatorRetries">OLD."OperatorRetries" AND
                       (OLD."State"<>'failed' OR NEW."State"<>'pending' OR OLD."LeaseOwner" IS NOT NULL OR OLD."LeaseToken" IS NOT NULL OR OLD."LeaseUntil" IS NOT NULL)
                    THEN RAISE EXCEPTION 'Outbox manual retry requires failed unleased work' USING ERRCODE='23514'; END IF;
                    NEW."Revision":=OLD."Revision"+1;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER outbox_retry_version BEFORE UPDATE ON "OutboxEvents"
                    FOR EACH ROW EXECUTE FUNCTION enforce_outbox_retry_version();
                INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
                    VALUES ('8c78f486-827b-4ba6-845c-8587b723aa37','Job.View','Inspect failed jobs','Restricted technical summaries only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
                           ('4244f084-45d6-4510-8581-e236b7834385','Job.Retry','Retry failed jobs','Bounded immutable version-safe retry',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
                    ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER outbox_retry_version ON "OutboxEvents";
                DROP FUNCTION enforce_outbox_retry_version();
                ALTER TABLE "OutboxEvents" DROP CONSTRAINT outbox_operator_budget;
                -- Permission catalogue/role assignments are deliberately retained on rollback.
                """);

            migrationBuilder.DropColumn(
                name: "OperatorRetries",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "OutboxEvents");
        }
    }
}

