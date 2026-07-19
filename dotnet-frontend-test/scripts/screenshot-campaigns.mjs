import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(__dirname, "..", "..", "assets", "campaigns-verify.png");

const BASE = process.env.APP_URL || "http://localhost:3000";
const VIEWPORT = { width: 1440, height: 900 };

async function login(page) {
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle", timeout: 60000 });
  const email = page.locator('[data-testid="login-email"]');
  const password = page.locator('[data-testid="login-password"]');
  if (await email.count()) {
    await email.fill("admin@cureflow.in");
    await password.fill("admin123");
    await page.locator('[data-testid="login-form"]').evaluate((form) => form.requestSubmit());
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 15000 }).catch(() => {});
  }
}

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: VIEWPORT });

await login(page);
if (page.url().includes("/login")) {
  console.error("FAIL: Could not log in — ensure backend API and frontend dev server are running.");
  await page.screenshot({ path: out.replace(".png", "-login-fail.png") });
  await browser.close();
  process.exit(1);
}

await page.goto(`${BASE}/campaigns`, { waitUntil: "networkidle", timeout: 60000 });
await page.waitForSelector('[data-testid="campaigns-kanban"], [data-testid="suggested-drafts-section"]', { timeout: 15000 });
await page.waitForTimeout(1500);

const metrics = await page.evaluate(({ vw, vh }) => {
  const kanban = document.querySelector('[data-testid="campaigns-kanban"]');
  const main = document.querySelector("main");
  const cards = [...document.querySelectorAll('[data-testid="campaigns-kanban"] [draggable="true"]')];
  const kanbanRect = kanban?.getBoundingClientRect();
  const mainRect = main?.getBoundingClientRect();

  let cardOverflow = 0;
  let maxCardHeight = 0;
  cards.forEach((wrapper) => {
    const card = wrapper.firstElementChild;
    if (!card) return;
    const cardRect = card.getBoundingClientRect();
    const colRect = wrapper.closest(".min-w-0")?.getBoundingClientRect();
    maxCardHeight = Math.max(maxCardHeight, cardRect.height);
    if (colRect && cardRect.width > colRect.width + 2) cardOverflow += 1;
  });

  const bodyScrollH = document.documentElement.scrollHeight;
  const bodyClientH = document.documentElement.clientHeight;
  const hasHorizontalScroll = document.documentElement.scrollWidth > vw + 2;

  return {
    hasKanban: !!kanban,
    kanbanHeight: kanbanRect?.height ?? 0,
    kanbanBottom: kanbanRect?.bottom ?? 0,
    mainBottom: mainRect?.bottom ?? 0,
    viewportH: vh,
    pageOverflowPx: bodyScrollH - bodyClientH,
    hasHorizontalScroll,
    cardCount: cards.length,
    cardOverflow,
    maxCardHeight,
    columnCount: document.querySelectorAll('[data-testid="campaigns-kanban"] .grid > .min-w-0').length,
  };
}, { vw: VIEWPORT.width, vh: VIEWPORT.height });

console.log(JSON.stringify(metrics, null, 2));
await page.screenshot({ path: out, fullPage: false });
console.log("Saved:", out);

await browser.close();

const failures = [];
if (!metrics.hasKanban && metrics.cardCount === 0) {
  // empty state is ok without kanban
} else if (!metrics.hasKanban) failures.push("Kanban board missing");
if (metrics.hasKanban && metrics.kanbanHeight < 280) failures.push(`Kanban too short: ${metrics.kanbanHeight}px`);
if (metrics.pageOverflowPx > 8) failures.push(`Page scrolls vertically: overflow ${metrics.pageOverflowPx}px`);
if (metrics.hasHorizontalScroll) failures.push("Page has horizontal scroll");
if (metrics.cardOverflow > 0) failures.push(`${metrics.cardOverflow} card(s) wider than column`);
if (metrics.maxCardHeight > 180) failures.push(`Cards too tall: max ${Math.round(metrics.maxCardHeight)}px (target ≤180px)`);
if (metrics.columnCount > 0 && metrics.columnCount !== 3) failures.push(`Expected 3 columns, got ${metrics.columnCount}`);

if (failures.length) {
  console.error("FAIL:\n" + failures.map((f) => `  - ${f}`).join("\n"));
  process.exit(1);
}

console.log("PASS: Campaigns layout fits viewport with compact cards");
