// @ts-check
const { defineConfig, devices } = require("@playwright/test");
const path = require("path");

const baseURL = process.env.PLAYWRIGHT_BASE_URL || "http://localhost:3000";
const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";

// Screenshots go to the canonical project-wide store, timestamped per run.
// D:\Projects\Sarvik\Care-Flow\screenshots\<YYYY-MM-DD_HH-mm>\
const RUN_TS = new Date()
  .toISOString()
  .slice(0, 16)
  .replace("T", "_")
  .replace(":", "-");
const SCREENSHOT_BASE = path.join(
  "D:\\Projects\\Sarvik\\Care-Flow\\screenshots",
  RUN_TS
);

module.exports = defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [["list"], ["html", { open: "never" }]],
  timeout: 120_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL,
    trace: "on-first-retry",
    // Always capture screenshots so we can compare runs; failures are obvious
    // because Playwright names them with the test title.
    screenshot: "on",
    screenshotsPath: SCREENSHOT_BASE,
    video: "retain-on-failure",
    ...devices["Desktop Chrome"],
  },
  // Playwright's own temp output (videos, traces) stays local; screenshots copy
  // to SCREENSHOT_BASE via the globalTeardown helper.
  outputDir: "e2e/test-results",
  globalSetup: require.resolve("./e2e/global-setup.js"),
  globalTeardown: require.resolve("./e2e/global-teardown.js"),
  metadata: { apiURL, screenshotBase: SCREENSHOT_BASE },
  webServer: process.env.PLAYWRIGHT_SKIP_WEBSERVER
    ? undefined
    : {
        command: "npm run dev",
        url: `${baseURL}/login`,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
        cwd: __dirname,
        stdout: "pipe",
        stderr: "pipe",
      },
});
