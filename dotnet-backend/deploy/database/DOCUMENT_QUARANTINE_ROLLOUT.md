# Document quarantine activation

New uploads return HTTP202 with Id, FileUrl, Title, Status=quarantined and StatusUrl=/api/patient-documents/{id}/status. Status GET returns Id, Status, ScannedAt; its permission and tenant/patient checks match clinical reads. Lists include Status. Download returns409 until available and immutable clean scan evidence matches the current version/hash. Historical rows default unresolved and are never inferred clean; protected private-object migration preserves that unresolved state.

Upload checks declared and actual size and supported PDF/JPEG/PNG signatures against extension and declared MIME. Immutable stored bytes are rehashed on scan/read. Object publication precedes metadata; failed publication/validation/transaction retains objects for BE004 orphan reconciliation without destructive cleanup. Domain metadata plus scan event are committed atomically.

The document.scan consumer performs repeatable read-only scanner preparation outside the local domain transaction, then locks the current document and verifies the exact object version, hash and patient/visit association before recording append-only evidence and state. It uses bounded retries/timeouts. Preparation has no external provider sends or domain writes. Local effects/deduplication/acknowledgement remain atomic under a live lease.

The default IDocumentScanner is UnavailableDocumentScanner: it records failed/unavailable rather than approving bytes. Register an approved scanner adapter with explicit engine/signature version and reliable malware/format verdict before activating Outbox:Enabled. This source intentionally cannot claim clean-file acceptance while no scanner is configured. A transport outage/timeout yields failed state, never available. There is no public rescan/retention-delete endpoint; approved retry/retention workflow remains an operational gate.

Rehearse AddDocumentQuarantine and predecessor migrations/grants on the protected destination before deploying. Review old files separately; no source clinical files or rows were read, imported, reclassified or deleted during preparation. No live migrations or test files were created.

## Optional local ClamAV transport

DocumentScanning:Provider defaults to Unavailable; ClamAvLocal is explicit opt-in. Configure DocumentScanning:ApprovedPolicyVersion, ClamAv:SocketPath (private absolute Unix socket), ClamAv:ApprovedDaemonVersionSha256 (uppercase SHA-256 of the complete VERSION response without its NUL terminator) and optional ClamAv:TimeoutSeconds (1–20, default 18). Protect socket permissions and run an approved maintained daemon/definition updater; the adapter never enables one. Definition changes invalidate the approval hash and fail closed until reviewed updated evidence is configured. The hash is engine/definition identity, not a promise of freshness; retain the actual version string and updater evidence in the approved operational registry. There is no clinical-file network upload over raw TCP. The official protocol explains that clamd TCP is unauthenticated/unencrypted and defines NUL framing plus big-endian INSTREAM chunks: https://docs.clamav.net/manual/Usage/ClamdProtocol.html .

No daemon was contacted during implementation; actual clean/rejected/malformed/timeout deployment acceptance remains pending.

