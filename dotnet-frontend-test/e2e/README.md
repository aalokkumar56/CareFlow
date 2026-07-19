# Cure-Flow Playwright E2E

## Prerequisites

1. **PostgreSQL** running with Cure-Flow schema migrated.
2. **Backend** on `http://localhost:5180`:
   ```bash
   cd dotnet-backend/src/CureFlow.Api
   dotnet run
   ```
3. **Frontend** on `http://localhost:3000` (optional — Playwright `webServer` can start it):
   ```bash
   cd dotnet-frontend
   npm run dev
   ```

Default login: `admin@cureflow.in` / `admin123` (tenant `cureandcare`).

## Run tests

```bash
cd dotnet-frontend-test
npm install
npx playwright install chromium
npx playwright test
```

Optional env:

- `PLAYWRIGHT_BASE_URL` — frontend URL (default `http://localhost:3000`)
- `PLAYWRIGHT_API_URL` — API origin (default `http://localhost:5180`)

View HTML report:

```bash
npx playwright show-report
```
