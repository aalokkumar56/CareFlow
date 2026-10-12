# Durable outbox activation

The writer accepts an explicit active source transaction. Callers supply server-defined event/schema/aggregate identity and a bounded minimal typed payload; raw requests, tokens and unnecessary clinical details must not enter the payload. Event IDs are immutable and their identical retry is harmless. Domain rollback rolls the event back.

Outbox:Enabled defaults to false. No existing campaign, reminder or provider delivery is switched. Register an approved ITransactionalOutboxConsumer before enabling. Its handler may perform local database effects only through its provided transaction. The receipt and effects commit with acknowledgement; lease expiry rolls them back. External transport requires a separate provider-specific idempotency/reconciliation adapter; this source does not promise exactly-once external delivery.

Apply the additive AddDurableOutbox migration only in the protected BE009/BE021 rehearsal first. Review grants with authority-boundaries.sql. Rehearse restart, concurrent SKIP LOCKED leasing, expiry, cancellation, local deduplication, rollback and poison events. Configure lease greater than bounded handler deadline. Failed work remains retained with minimal failure code; inspect through an approved tenant-scoped operator workflow, never logging payloads. No delete/replay endpoint is exposed.

The worker uses a fresh tenant scope and named service identity, bounded batches, owner/token checks, lease renewal before each event, bounded exponential backoff and retained terminal failures. The schema protects event provenance and append-only receipts. No database changes or provider messages occurred while preparing these sources.
