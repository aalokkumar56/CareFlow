# Cure-Flow Frontend UI / E2E Tests

Single Playwright test application for the Cure-Flow Next.js app in `../dotnet-frontend`.

## Prerequisites

1. **PostgreSQL** running with Cure-Flow schema migrated.
2. **Backend** on `http://localhost:5180`:
   ```bash
   cd dotnet-backend/src/CureFlow.Api
   dotnet run
   ```
3. Frontend is started automatically by Playwright `webServer` (`npm run dev` in `../dotnet-frontend`), or start it yourself:
   ```bash
   cd dotnet-frontend
   npm run dev
   ```

Default login: `admin@cureflow.in` / `admin123` (tenant `cureandcare`).

## Install

```bash
cd dotnet-frontend-test
npm install
npx playwright install chromium
```

## Run tests

```bash
cd dotnet-frontend-test
npm run test:e2e
```

Useful scripts:

| Script | Purpose |
|--------|---------|
| `npm run test:e2e` | Full Playwright suite |
| `npm run test:e2e:smoke` | Smoke only |
| `npm run test:e2e:ui` | Playwright UI mode |
| `npm run test:e2e:inputs` | All input-field coverage |
| `npm run test:e2e:walkthrough` | Human walkthrough (admin) |
| `npm run report` | Open HTML report |

Optional env:

- `PLAYWRIGHT_BASE_URL` — frontend URL (default `http://localhost:3000`)
- `PLAYWRIGHT_API_URL` — API origin (default `http://localhost:5180`)
- `PLAYWRIGHT_SKIP_WEBSERVER=1` — do not auto-start `dotnet-frontend`

Screenshots are written to `D:\Projects\Sarvik\Care-Flow\screenshots\<timestamp>\` and compared with the previous run in global teardown.
