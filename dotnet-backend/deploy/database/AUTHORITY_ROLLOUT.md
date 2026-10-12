# Database authority rollout

This preparation is data preserving and is not applied by the application. Back up and rehearse before applying `authority-boundaries.sql` using the migration owner. Inspect all existing tenant policies, PUBLIC grants, function grants and ownership first. The script leaves unrelated role grants unchanged; remove conflicting rights separately after review.

Provision separate login roles with NOSUPERUSER NOBYPASSRLS INHERIT, each a member of exactly one NOLOGIN grant group. They must own no application table and have no membership in the migration owner or another authority group. Configure secrets externally:

- `ConnectionStrings:Default`: cureflow_tenant membership, hospital runtime only.
- `ConnectionStrings:Identity`: cureflow_identity membership, login/registration/refresh only.
- `ConnectionStrings:Catalogue`: cureflow_catalogue membership, tenant routing and worker enumeration only.
- `ConnectionStrings:ControlPlane`: cureflow_control_plane membership, platform identity and authorized management only.
- `ConnectionStrings:Provisioning`: operator-owned initialization connection, Development initialization only; never expose through controllers or tenant services.
- `ConnectionStrings:Migration`: operator-owned migration connection, explicit migration/rehearsal only.

No runtime connection falls back to the operator connection. Each runtime transaction validates role membership, non-superuser/non-BYPASSRLS status, no table ownership and absence of protected-table grants for non-tenant authority. A missing configuration or unsafe role fails closed. The existing Default superuser connection must be replaced before login/clinical endpoints are usable with this boundary.

The restrictive RLS guard protects clinical rows even if a tenant sets `app.platform_admin`. Control-plane usage reads only the fixed aggregate function. Grant function ownership only to the reviewed migration owner, revoke PUBLIC execute, and inspect other SECURITY DEFINER routines for alternate clinical access. Verify role grants and policies in a protected rehearsal before rollout; these operational acceptance checks have not been executed here. No live migration or clinical data change is authorized by this source preparation.
Platform login/bootstrap also require `ConnectionStrings:PlatformIdentity`, a separately provisioned cureflow_platform_identity member with only PlatformUsers/PlatformSessions SELECT/INSERT and PlatformAuditLogs INSERT; never reuse ControlPlane credentials. Apply session grants only after the additive PlatformSessions migration.
The SQL file has explicit deployment phases (role/policy preparation, bounded webhook routing, platform-session grants, private-version grants, clinical-audit grants). It is not an all-or-nothing deployment across these commits. Complete every applicable phase against rehearsed migrations before enabling endpoints; partial phase deployment remains blocked. Tenants now has a restrictive Id/authority guard so additional permissive policies cannot widen access. Runtime role checks also reject unexpected executable public SECURITY DEFINER routines.
