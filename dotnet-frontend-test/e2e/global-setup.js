const fs = require("fs");
const path = require("path");

const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";
const DEPT_CACHE_PATH = path.join(__dirname, "helpers/.e2e-departments.json");
const DEPT_FALLBACK = ["Cardiology", "Orthopedics"];

async function seedMultiHospitalsIfNeeded(apiUp) {
  if (!apiUp) return;
  try {
    const res = await fetch(`${apiURL}/api/dev/seed-multi-hospitals`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
    });
    if (res.ok) {
      console.log("[E2E] Multi-hospital seed confirmed via /api/dev/seed-multi-hospitals");
    } else {
      console.warn(`[E2E] seed-multi-hospitals returned ${res.status}`);
    }
  } catch (err) {
    console.warn(`[E2E] Could not seed multi-hospitals: ${err.message}`);
  }
}

async function cacheDepartmentFilters(apiUp) {
  if (!apiUp) {
    fs.writeFileSync(DEPT_CACHE_PATH, JSON.stringify(DEPT_FALLBACK));
    return;
  }
  try {
    const { apiLogin, getDepartments } = require("./helpers/api");
    const { HOSPITAL_A } = require("./helpers/multi-hospital");
    // Use multi-hospital admin instead of demo admin to avoid extra login pressure.
    const { accessToken } = await apiLogin(HOSPITAL_A.adminEmail, HOSPITAL_A.password);
    const depts = await getDepartments(accessToken);
    const list = Array.isArray(depts) && depts.length ? depts : DEPT_FALLBACK;
    fs.mkdirSync(path.dirname(DEPT_CACHE_PATH), { recursive: true });
    fs.writeFileSync(DEPT_CACHE_PATH, JSON.stringify(list));
    console.log(`[E2E] Department filters cached (${list.length}): ${list.join(", ")}`);
  } catch (err) {
    console.warn(`[E2E] Could not fetch departments, using fallback: ${err.message}`);
    fs.writeFileSync(DEPT_CACHE_PATH, JSON.stringify(DEPT_FALLBACK));
  }
}

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

  await seedMultiHospitalsIfNeeded(apiUp);
  await cacheDepartmentFilters(apiUp);

  if (!apiUp || !feUp) {
    console.warn(
      "\n[E2E] Backend and/or frontend are not reachable.\n" +
        `  API:      ${apiURL} (${apiUp ? "ok" : "down"})\n` +
        `  Frontend: ${baseURL} (${feUp ? "ok" : "down"})\n` +
        "  Start them before running tests:\n" +
        "    cd dotnet-backend/src/CureFlow.Api && dotnet run\n" +
        "    cd dotnet-frontend && npm run dev\n" +
        "  Then from the test app:\n" +
        "    cd dotnet-frontend-test && npm run test:e2e\n"
    );
  }
}

module.exports = globalSetup;
