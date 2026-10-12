using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_OutboxEvents", x => x.Id);
                    table.UniqueConstraint("AK_OutboxEvents_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "OutboxConsumerReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
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
                    table.PrimaryKey("PK_OutboxConsumerReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxConsumerReceipts_OutboxEvents_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "OutboxEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxConsumerReceipts_TenantId_EventId_ConsumerName",
                table: "OutboxConsumerReceipts",
                columns: new[] { "TenantId", "EventId", "ConsumerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_TenantId_State_DueAt_LeaseUntil",
                table: "OutboxEvents",
                columns: new[] { "TenantId", "State", "DueAt", "LeaseUntil" });
            migrationBuilder.Sql("""
                ALTER TABLE "OutboxEvents" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "OutboxEvents" FORCE ROW LEVEL SECURITY;
                ALTER TABLE "OutboxConsumerReceipts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "OutboxConsumerReceipts" FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_tenant ON "OutboxEvents"
                    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY outbox_receipt_tenant ON "OutboxConsumerReceipts"
                    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "OutboxEvents" ADD CONSTRAINT outbox_payload_size CHECK (octet_length("PayloadJson")<=65536),
                    ADD CONSTRAINT outbox_state CHECK ("State" IN ('pending','leased','completed','failed')),
                    ADD CONSTRAINT outbox_attempts CHECK ("Attempts">=0 AND "SchemaVersion">0);
                CREATE FUNCTION prevent_outbox_provenance_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Outbox provenance must be retained' USING ERRCODE='23514'; END IF;
                    IF ROW(OLD."Id",OLD."TenantId",OLD."EventType",OLD."SchemaVersion",OLD."AggregateType",OLD."AggregateId",OLD."AggregateVersion",OLD."PayloadJson",OLD."CreatedAt",OLD."CreatedBy")
                       IS DISTINCT FROM ROW(NEW."Id",NEW."TenantId",NEW."EventType",NEW."SchemaVersion",NEW."AggregateType",NEW."AggregateId",NEW."AggregateVersion",NEW."PayloadJson",NEW."CreatedAt",NEW."CreatedBy")
                    THEN RAISE EXCEPTION 'Outbox provenance is immutable' USING ERRCODE='23514'; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER outbox_provenance BEFORE UPDATE OR DELETE ON "OutboxEvents"
                    FOR EACH ROW EXECUTE FUNCTION prevent_outbox_provenance_rewrite();
                CREATE FUNCTION prevent_outbox_receipt_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Outbox receipts are append-only' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER outbox_receipt_append_only BEFORE UPDATE OR DELETE ON "OutboxConsumerReceipts"
                    FOR EACH ROW EXECUTE FUNCTION prevent_outbox_receipt_rewrite();
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                        GRANT SELECT,INSERT,UPDATE ON "OutboxEvents" TO cureflow_tenant;
                        REVOKE DELETE ON "OutboxEvents" FROM cureflow_tenant;
                        GRANT SELECT,INSERT ON "OutboxConsumerReceipts" TO cureflow_tenant;
                        REVOKE UPDATE,DELETE ON "OutboxConsumerReceipts" FROM cureflow_tenant;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxConsumerReceipts");

            migrationBuilder.DropTable(
                name: "OutboxEvents");
            migrationBuilder.Sql("DROP FUNCTION prevent_outbox_provenance_rewrite(); DROP FUNCTION prevent_outbox_receipt_rewrite();");
        }
    }
}

