# Actor and audience isolation rollout

Hospital JWTs use Jwt:TenantAudience, default old Jwt:Audience plus .tenant. Platform JWTs use Jwt:PlatformAudience, default old Jwt:Audience plus .platform. Startup rejects identical/empty audiences. Both audiences are validated cryptographically with configured issuer, signing secret, HMACSHA256 and zero lifetime skew.

OnTokenValidated requires one explicit actor_kind, audience and subject, durable actor-specific session claims and no cross-actor tenant/platform claim mixture. Generic/legacy audience or unbound tokens fail. Live authority checks remain BE003/BE010, not a replacement for signing verification. Hospital and platform route boundaries reject cross-context principals; branded hospital slug must match the signed hospital ID.

CurrentTenant/capability collections are per-request scoped. No static mutable identity context, shared user cache or raw JWT permission-only authorization is introduced. Authentication runs before bounded rate limiting; live tenant authority loads before MVC authorization. Auth/session/logout responses forbid caching. Late-response chart clearing remains UI/BFF scope.

Existing IDs/history are unchanged. Rollout requires fresh sign-in for legacy tokens and coordinated BFF configuration, protected session migrations/role rehearsal, and approved signing-secret delivery. No tests/live deployments/database writes were performed.
