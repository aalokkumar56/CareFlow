import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const isMobile = process.argv[2] === "mobile";
const suffix = isMobile ? "-mobile" : "";
const out = path.join(__dirname, "..", "..", "assets", `dashboard-verify${suffix}.png`);

const browser = await chromium.launch();
const page = await browser.newPage({
  viewport: isMobile ? { width: 390, height: 844 } : { width: 1440, height: 900 },
});

await page.goto("http://localhost:3000/login", { waitUntil: "networkidle", timeout: 60000 });

const email = page.locator('input[type="email"], input[name="email"], input[placeholder*="email" i]').first();
const password = page.locator('input[type="password"]').first();

if (await email.count()) {
  await email.fill("admin@cureflow.in");
  await password.fill("admin123");
  await page.locator('button[type="submit"]').first().click();
  await page.waitForURL("**/", { timeout: 30000 }).catch(() => {});
}

await page.goto("http://localhost:3000/", { waitUntil: "networkidle", timeout: 60000 });
await page.waitForTimeout(2500);
await page.screenshot({ path: out, fullPage: isMobile });
console.log("Saved:", out);

const metrics = await page.evaluate(() => {
  const grid = document.querySelector(".dashboard-main-grid");
  const revenue = document.querySelector(".dashboard-cell-bottom-left");
  const chart = document.querySelector(".dashboard-revenue-chart");
  const gridRect = grid?.getBoundingClientRect();
  const revenueRect = revenue?.getBoundingClientRect();
  const chartRect = chart?.getBoundingClientRect();
  return {
    grid: gridRect ? { h: gridRect.height, bottom: gridRect.bottom } : null,
    revenue: revenueRect ? { h: revenueRect.height, bottom: revenueRect.bottom } : null,
    chart: chartRect ? { h: chartRect.height, w: chartRect.width } : null,
  };
});
console.log(JSON.stringify(metrics, null, 2));

await browser.close();
