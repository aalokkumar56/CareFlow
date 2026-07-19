# Integration tests

**Scenario catalog:** [`TEST_SCENARIOS.md`](./TEST_SCENARIOS.md) — complete `INT-001`… catalog (no bulk implementation yet).

API-host and (later) database / Testcontainers suites for CureFlow.

```
dotnet-backend-test/
├── CureFlow.UnitTest.sln
├── CureFlow.IntegrationTest.sln
├── unit/
│   └── CureFlow.UnitTests/
└── integration/
    └── CureFlow.IntegrationTests/
```

Product code is referenced from `../dotnet-backend/src/` (not copied).

## Prerequisites

- .NET 10 SDK
- For tests that boot the API host: a reachable PostgreSQL connection (same as local API).  
  Set `ConnectionStrings__Default` or use `appsettings` / user secrets on `CureFlow.Api`.  
  The smoke health test starts `WebApplicationFactory` and still runs lightweight startup seed (`RbacSeeder`) against the DB unless a future suite replaces that.

Optional: `CUREFLOW_TEST_CONNECTION` for DB-focused suites moved from unit tests later.

## Build

From the repo root (or `dotnet-backend-test/`):

```powershell
dotnet build CureFlow.IntegrationTest.sln
```

```powershell
cd dotnet-backend-test
dotnet build CureFlow.IntegrationTest.sln
```

## Run tests

```powershell
cd dotnet-backend-test
dotnet test CureFlow.IntegrationTest.sln
```

Filter example:

```powershell
dotnet test CureFlow.IntegrationTest.sln --filter "FullyQualifiedName~HealthEndpointTests"
```

## Scaffold layout

| File | Role |
|---|---|
| `CustomWebApplicationFactory.cs` | `WebApplicationFactory<Program>` host; Testing env; disables migrate/demo seed by default |
| `IntegrationTestBase.cs` | Shared `HttpClient` via `IClassFixture` |
| `HealthEndpointTests.cs` | Smoke: `GET /api/health` → 200 (FluentAssertions) |

Add new test classes under `integration/CureFlow.IntegrationTests/`. Prefer FluentAssertions (`result.StatusCode.Should().Be(...)`).

DB-backed tests that currently live under `unit/CureFlow.UnitTests` (those using `CUREFLOW_TEST_CONNECTION` / `DatabaseIntegration`) can move here when ready.
