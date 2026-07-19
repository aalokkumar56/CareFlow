import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(__dirname, "..", "..", "assets", "referral-crm-verify.png");

const BASE = process.env.APP_URL || "http://localhost:3000";
const VIEWPORT = { width: 1440, height: 900 };

async function login(page) {
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle", timeout: 60000 });
  const email = page.locator('[data-testid="login-email"]');
  if (await email.count()) {
    await email.fill("admin@cureflow.in");
    await page.locator('[data-testid="login-password"]').fill("admin123");
    await page.locator('[data-testid="login-form"]').evaluate((form) => form.requestSubmit());
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 15000 }).catch(() => {});
  }
}

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: VIEWPORT });

await login(page);
if (page.url().includes("/login")) {
  console.error("FAIL: Could not log in.");
  await browser.close();
  process.exit(1);
}

await page.goto(`${BASE}/doctors`, { waitUntil: "networkidle", timeout: 60000 });
await page.waitForSelector('[data-testid="referral-kanban"], [data-testid="doctors-search"]', { timeout: 15000 });
await page.waitForTimeout(1500);

const metrics = await page.evaluate(({ vw }) => {
  const kanban = document.querySelector('[data-testid="referral-kanban"]');
  const text = document.body.innerText;
  return {
    title: text.includes("Referral CRM"),
    hasPipelineFunnel: text.includes("Pipeline Funnel"),
    hasSidebar: text.includes("Top Referrers"),
    hasKanban: !!kanban,
    kanbanHeight: kanban?.getBoundingClientRect().height ?? 0,
    columnCount: document.querySelectorAll('[data-testid="referral-kanban"] .xl\\:grid-cols-4 > div, [data-testid="referral-kanban"] .grid > div.min-w-0').length,
    cardCount: document.querySelectorAll('[data-testid^="doctor-row-"]').length,
    searchInHeader: !!document.querySelector("header [data-testid='doctors-search']"),
    hasStatCards: !!document.querySelector('[class*="StatCard"]') || text.includes("Referring Doctors"),
    hasNotification: !!document.querySelector('header [aria-label="Notifications"]'),
    pageOverflowPx: document.documentElement.scrollHeight - document.documentElement.clientHeight,
    hasHorizontalScroll: document.documentElement.scrollWidth > vw + 2,
    hasDropZones: (text.match(/\+ Drop here/g) || []).length,
    hasTipFooter: text.includes("Drag and drop doctors between stages"),
    sidebarWidth: document.querySelector('[data-testid="referral-sidebar"]')?.getBoundingClientRect().width ?? 0,
    kanbanWidth: kanban?.getBoundingClientRect().width ?? 0,
  };
}, { vw: VIEWPORT.width });

console.log(JSON.stringify(metrics, null, 2));
await page.screenshot({ path: out, fullPage: false });
console.log("Saved:", out);

await browser.close();

const failures = [];
if (!metrics.title) failures.push("Page title should be Referral CRM");
if (!metrics.hasPipelineFunnel) failures.push("Pipeline Funnel toolbar missing");
if (!metrics.hasSidebar) failures.push("Top Referrers sidebar missing");
if (!metrics.searchInHeader) failures.push("Search should be in header");
if (metrics.hasStatCards) failures.push("Old stat cards should be removed");
if (metrics.hasNotification) failures.push("Notifications should be hidden");
if (metrics.pageOverflowPx > 8) failures.push(`Page overflow: ${metrics.pageOverflowPx}px`);
if (metrics.hasHorizontalScroll) failures.push("Horizontal scroll detected");
if (metrics.hasTipFooter) failures.push("Footer tip should be removed");
if (metrics.sidebarWidth > 0 && metrics.sidebarWidth < 210) failures.push(`Sidebar too narrow: ${metrics.sidebarWidth}px`);
if (metrics.sidebarWidth > 240) failures.push(`Sidebar wrong width: ${metrics.sidebarWidth}px`);
if (metrics.kanbanWidth < 800) failures.push(`Kanban too narrow: ${metrics.kanbanWidth}px`);

if (failures.length) {
  console.error("FAIL:\n" + failures.map((f) => `  - ${f}`).join("\n"));
  process.exit(1);
}

console.log("PASS: Referral CRM layout verified");
