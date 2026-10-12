# Controlled database initialization and deployment

These source manifests prepare separated environments; they have not been deployed or accepted. BE-009 recovery evidence and BE-002 restricted database authority must pass before an existing hospital environment is changed.

## Runtime initialization

Only the Development environment may migrate or seed during API startup. Production, Staging, Testing and other environments reject any enabled `Database:AutoMigrate`, `Database:SeedCatalog`, `Database:Seed`, `Database:SeedPlatformUser` or `WhatsApp:UseTestPhone` flag. With these flags disabled, startup does not create the initialization scope or run catalog/demo seeders. Existing catalog rows remain intact.

Local automatic migration also requires an explicit `ConnectionStrings:Migration`; Development seeders use the separately configured `ConnectionStrings:Provisioning` session. Runtime credentials are never a fallback for either operation or design-time migration tooling. Provide them through the process environment (`ConnectionStrings__Migration`, `ConnectionStrings__Provisioning`) or approved private configuration; do not put credential values in command arguments, checked-in files or logs. Development catalog seeding is controlled by `Database:SeedCatalog` (default true only in Development); demo seeding remains an explicit opt-in. The `/api/dev/seed-multi-hospitals` route already denies non-Development environments.

Previously deployed settings enabling production AutoMigrate must explicitly set it false before upgrading. Do not bypass the guard by labelling a production host Development.

Remote PostgreSQL connections default to `SSL Mode=VerifyFull` and reject explicitly weaker modes. All non-Development runtime connections require VerifyFull, including locally named database services. Install the approved database CA/root certificate where required; do not replace hostname or certificate checks with a trust override. Rotate connection secrets through the approved secret mechanism and reconcile the BE-002 role grants before restarting. These guards do not establish an approved secret store or prove a real certificate rotation/recovery exercise.

## Separate environments

`compose.staging.yaml` and `compose.production.yaml` use distinct project names, connection/secret variables and private volumes. Neither runtime receives migration or provisioning credentials. Each runtime explicitly requires separate Default (tenant), Identity, Catalogue, PlatformIdentity and ControlPlane connections from BE-002. Pin the API image to an immutable reviewed tag or digest. Supply private values through an approved secret-injection mechanism. The manifests are templates, not evidence that supplied connection values target different databases.

The staging network is internal and deliberately has no Internet egress. Attach an explicitly isolated staging PostgreSQL service to it; do not connect it to a production network or enable live messaging/callbacks. A cloud-hosted staging database needs a separately reviewed network/egress configuration before this template can be used. The API ports bind loopback for a reviewed TLS reverse proxy. Private documents use `/var/lib/cureflow/private-files/documents`, outside the API content root. Separate environment-specific volumes cover the existing default WhatsApp media/log paths; they do not certify tenant/quarantine/retention controls for messaging attachments. Configure any overridden media/log paths into reviewed private mounts; the read-only filesystem exposes writes to unconfigured paths. Provision the private document root and its restricted permissions before readiness probing; a probe never creates it.

## Reviewed migration job

Use the existing EF migration tooling from the backend root or API/Infrastructure project directory, with an explicit environment and separately operated `ConnectionStrings:Migration`. The design-time factory loads only that environment's configuration and refuses a missing Migration connection. It no longer falls back to Development configuration or the Default runtime connection. Generate/review an idempotent SQL migration script or a pinned migration bundle as a build artifact; do not run a migration automatically in an API container.

Before application, record the operator, source environment/schema identity, approved destination, actual role grants, backup identifier/hash and recoverable key reference. Restore to a protected isolated copy first. Review the SQL for data preservation and run the upgrade there; reconcile existing identifiers, associations, source document hashes and committed financial/clinical history. Prevent external delivery from the restored environment. Record compatibility with both the old and new API and the rollback/forward-repair decision.

Apply to the intended environment only after that evidence is accepted. Stop unsafe writes before any incompatible step. Never reset/reseed an existing database, use a destructive Down migration as an assumed rollback, delete retained files or grant a runtime role schema ownership. After the controlled schema/catalog provisioning job completes, launch the API with initialization flags false and verify the current schema and role boundaries. A build or generated script does not complete BE-021's protected upgrade rehearsal or operational sign-off.
