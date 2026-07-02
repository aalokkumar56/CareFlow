const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";

async function globalSetup() {
  const { clearBugLog } = require("./helpers/bug-log");
  clearBugLog();
  const healthUrls = [
    `${apiURL}/swagger/index.html`,
    `${apiURL}/api/health`,
    apiURL,
  ];

  let apiUp = false;
  for (const url of healthUrls) {
    try {
      const res = await fetch(url, { signal: AbortSignal.timeout(5000) });
      if (res.ok || res.status === 404) {
        apiUp = true;
        break;
      }
    } catch {
      /* try next */
    }
  }

  const baseURL = process.env.PLAYWRIGHT_BASE_URL || "http://localhost:3000";
  let feUp = false;
  try {
    const res = await fetch(baseURL, { signal: AbortSignal.timeout(5000) });
    feUp = res.ok;
  } catch {
    feUp = false;
  }

  if (!apiUp || !feUp) {
    console.warn(
      "\n[E2E] Backend and/or frontend are not reachable.\n" +
        `  API:      ${apiURL} (${apiUp ? "ok" : "down"})\n` +
        `  Frontend: ${baseURL} (${feUp ? "ok" : "down"})\n` +
        "  Start them before running tests:\n" +
        "    cd dotnet-backend/src/CureFlow.Api && dotnet run\n" +
        "    cd dotnet-frontend && npm start\n"
    );
  }
}

module.exports = globalSetup;
