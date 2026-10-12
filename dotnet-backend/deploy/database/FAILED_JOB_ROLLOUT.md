# Failed job inspection/retry rollout

GET/api/jobs/failed?limit=50 and GET/api/jobs/{id} require current Job.View. Summaries expose only event ID/type/schema/state, cumulative attempt counts, due time, bounded failure code and revision; no payload, aggregate/patient ID, object path or exception detail. Inspection records protected operational audit before response.

POST/api/jobs/{id}/retry requires current Job.Retry, one Idempotency-Key and resource-specific strong If-Match from GET. Missing version428, malformed400, stale412, active/nonfailed/unknownconsumer/exhausted budget409. Retry queues existing event202 and returns minimal CommandReceipt plus ETag/Idempotency-Replayed. Canonical intent includes target ID and expected revision;24hour command receipt retry window is server-defined, minimal and reauthorized even on replay.

The transaction locks the original event; no event ID, payload, aggregate version, receipt/dedupe key or attempt counter is replaced/reset. Only failed unleased work with exactly one approved schema consumer may become pending. Operator retries are capped3 (config may reduce0..3); each grants at most one extra automatic attempt budget. Provenance trigger prevents resetting attempt/operator counters and increments resource revision on every transition. Concurrent distinct retry keys with same ETag cannot both queue; identical accepted key replays its acknowledgement.

AddJobRetryControl is additive. It adds revision/operator budget fields and catalogue permission rows but no production role grants. Review and explicitly assign operator permissions; viewer defaults exclude them. Parent outbox remains disabled unless explicitly activated. No provider sends/payload editing/delete API exists. Document scanning default failed verdict remains unavailable; retrying completed failed scan is not exposed as a backdoor.

Protected migration/RLS/consumer/lease/concurrency rehearsal and approved operator assignments remain activation gates. No live migration/test files or clinical row modifications occurred.
