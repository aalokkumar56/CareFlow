/** @param {string[]} errors */
function filterBenignConsoleErrors(errors) {
  const benign = [
    "favicon",
    "404",
    "net::ERR",
    "Failed to load resource",
    "ResizeObserver loop",
    "webpack",
    "DevTools",
  ];
  return errors.filter((e) => !benign.some((b) => e.includes(b)));
}

/** @param {import('@playwright/test').Page} page */
function setupConsoleCapture(page) {
  const errors = [];
  const onConsole = (msg) => {
    if (msg.type() === "error") errors.push(msg.text());
  };
  const onPageError = (err) => errors.push(err.message);
  page.on("console", onConsole);
  page.on("pageerror", onPageError);
  return {
    getErrors: () => [...errors],
    getFilteredErrors: () => filterBenignConsoleErrors(errors),
    detach: () => {
      page.off("console", onConsole);
      page.off("pageerror", onPageError);
    },
  };
}

/** @param {import('@playwright/test').Page} page */
async function auditPageUI(page, pageName) {
  const issues = [];

  const headings = await page.locator("h1").all();
  for (let i = 0; i < headings.length; i += 1) {
    const h = headings[i];
    const font = await h.evaluate((el) => {
      const s = window.getComputedStyle(el);
      return { family: s.fontFamily, size: s.fontSize };
    });
    const family = font.family.toLowerCase();
    const isStandardHeading =
      family.includes("heading") || family.includes("inter") || family.includes("outfit");
    if (!isStandardHeading) {
      issues.push(`${pageName}: h1[${i}] uses non-standard font: ${font.family}`);
    }
    if (parseFloat(font.size) < 16) {
      issues.push(`${pageName}: h1[${i}] font-size ${font.size} is below 16px minimum`);
    }
  }

  const h2s = await page.locator("h2").all();
  for (let i = 0; i < Math.min(h2s.length, 5); i += 1) {
    const font = await h2s[i].evaluate((el) => window.getComputedStyle(el).fontFamily);
    const family = font.toLowerCase();
    if (!family.includes("heading") && !family.includes("outfit") && !family.includes("inter")) {
      issues.push(`${pageName}: h2[${i}] may not use font-heading: ${font}`);
    }
  }

  const buttons = page.locator("button:visible");
  const btnCount = await buttons.count();
  const btnStyles = new Map();
  for (let i = 0; i < Math.min(btnCount, 25); i += 1) {
    const btn = buttons.nth(i);
    const style = await btn.evaluate((el) => {
      const s = window.getComputedStyle(el);
      return {
        borderRadius: s.borderRadius,
        fontSize: s.fontSize,
        height: s.height,
        className: el.className.slice(0, 80),
      };
    });
    const key = `${style.borderRadius}|${style.fontSize}|${style.height}`;
    if (!btnStyles.has(key)) btnStyles.set(key, []);
    btnStyles.get(key).push(style.className);
  }
  if (btnStyles.size > 6) {
    issues.push(
      `${pageName}: ${btnStyles.size} distinct button style variants (expected ≤6)`,
    );
  }

  const brokenImages = await page.evaluate(() =>
    [...document.querySelectorAll("img")].filter((img) => img.complete && img.naturalWidth === 0).length,
  );
  if (brokenImages > 0) {
    issues.push(`${pageName}: ${brokenImages} broken image(s)`);
  }

  const horizontalOverflow = await page.evaluate(() => {
    const doc = document.documentElement;
    return doc.scrollWidth > doc.clientWidth + 2;
  });
  if (horizontalOverflow) {
    issues.push(`${pageName}: horizontal page overflow detected`);
  }

  return { issues };
}

/** @param {import('@playwright/test').Page} page */
function collectConsoleErrors(page) {
  const errors = [];
  page.on("console", (msg) => {
    if (msg.type() === "error") errors.push(msg.text());
  });
  page.on("pageerror", (err) => errors.push(err.message));
  return errors;
}

/** @param {import('@playwright/test').Page} page */
async function countIconButtonsWithoutAriaLabel(page) {
  return page.evaluate(() => {
    const buttons = [...document.querySelectorAll("button")].filter((btn) => {
      const role = btn.getAttribute("role");
      if (role === "switch" || role === "checkbox") return false;
      const style = window.getComputedStyle(btn);
      return style.display !== "none" && style.visibility !== "hidden" && btn.offsetParent !== null;
    });
    let unlabeled = 0;
    const offenders = [];
    for (const btn of buttons) {
      const text = (btn.textContent || "").trim();
      const hasAria = btn.getAttribute("aria-label") || btn.getAttribute("aria-labelledby");
      const hasTitle = btn.getAttribute("title");
      if (!text && !hasAria && !hasTitle) {
        unlabeled += 1;
        if (offenders.length < 5) {
          offenders.push(btn.className.slice(0, 60) || "(no class)");
        }
      }
    }
    return { count: unlabeled, offenders };
  });
}

/** @param {import('@playwright/test').Page} page */
async function auditPageHeading(page) {
  const pageTitle = await page.getByTestId("page-title").isVisible().catch(() => false);
  const breadcrumb = await page.getByTestId("page-breadcrumb").isVisible().catch(() => false);
  const h1Count = await page.locator("h1").count();
  return { hasPageTitle: pageTitle, hasBreadcrumb: breadcrumb, h1Count };
}

/** @param {import('@playwright/test').Page} page */
async function auditBorderRadiusMix(page) {
  return page.evaluate(() => {
    const classes = new Set();
    for (const el of document.querySelectorAll("[class*='rounded']")) {
      const match = el.className.match(/rounded-(sm|md|lg|xl|2xl|3xl|full)/g);
      if (match) match.forEach((m) => classes.add(m));
    }
    return [...classes];
  });
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {{ expectCommandPalette?: boolean, expectNotificationBell?: boolean }} opts
 */
async function auditAppshellFeatures(page, opts = {}) {
  const palette = await page.getByTestId("open-command-palette").isVisible().catch(() => false);
  const bellDesktop = await page.getByTestId("notification-bell-desktop").first().isVisible().catch(() => false);
  const bellMobile = await page.getByTestId("notification-bell-mobile").first().isVisible().catch(() => false);
  const bell = bellDesktop || bellMobile;

  const issues = [];
  if (opts.expectCommandPalette === true && !palette) {
    issues.push("Command palette button hidden but expected visible");
  }
  if (opts.expectCommandPalette === false && palette) {
    issues.push("Command palette visible but page uses hideHeaderSearch");
  }
  if (opts.expectNotificationBell === true && !bell) {
    issues.push("Notification bell hidden but expected visible");
  }
  if (opts.expectNotificationBell === false && bell) {
    issues.push("Notification bell visible but page uses hideNotifications");
  }
  return { palette, bell, issues };
}

/** @param {import('@playwright/test').Page} page */
async function auditPrimaryButtonColor(page) {
  const primaryBtn = page.locator(".btn-primary:visible").first();
  if ((await primaryBtn.count()) === 0) return null;
  return primaryBtn.evaluate((el) => window.getComputedStyle(el).backgroundColor);
}

/** @param {import('@playwright/test').Page} page */
async function countDuplicateHeadings(page, text) {
  return page.getByRole("heading", { name: text, exact: true }).count();
}

/**
 * Parse alpha channel from a computed backgroundColor string.
 * @param {string} bg
 */
function parseBackgroundAlpha(bg) {
  const m = bg.match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?\s*\)/);
  if (!m) return 1;
  return m[4] !== undefined ? parseFloat(m[4]) : 1;
}

/**
 * True when surface uses semi-transparent fill and/or backdrop blur (glassmorphism).
 * @param {{ backgroundColor: string, backdropFilter: string, webkitBackdropFilter?: string }} styles
 */
function isGlassLikeSurface(styles) {
  const alpha = parseBackgroundAlpha(styles.backgroundColor);
  const blur = styles.backdropFilter || styles.webkitBackdropFilter || "";
  const hasBlur = blur !== "none" && blur.length > 0;
  if (hasBlur && alpha < 0.92) return true;
  if (alpha < 0.88) return true;
  return false;
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {string} selector
 */
async function sampleSurfaceStyles(page, selector) {
  const el = page.locator(selector).first();
  if ((await el.count()) === 0) return null;
  return el.evaluate((node) => {
    const s = window.getComputedStyle(node);
    return {
      backgroundColor: s.backgroundColor,
      backdropFilter: s.backdropFilter,
      webkitBackdropFilter: s.webkitBackdropFilter,
      className: typeof node.className === "string" ? node.className.slice(0, 120) : "",
    };
  });
}

/**
 * Audit kanban column drop zones inside a board test id.
 * @param {import('@playwright/test').Page} page
 * @param {string} boardTestId
 */
async function auditKanbanColumnGlass(page, boardTestId) {
  const columns = page.locator(`[data-testid="${boardTestId}"] .grid > div.min-w-0 > div:nth-child(2)`);
  const count = await columns.count();
  const issues = [];
  for (let i = 0; i < count; i += 1) {
    const styles = await columns.nth(i).evaluate((node) => {
      const s = window.getComputedStyle(node);
      return {
        backgroundColor: s.backgroundColor,
        backdropFilter: s.backdropFilter,
        webkitBackdropFilter: s.webkitBackdropFilter,
        className: node.className.slice(0, 120),
      };
    });
    if (!isGlassLikeSurface(styles)) {
      issues.push({
        columnIndex: i,
        ...styles,
        reason: "Kanban column drop zone is opaque/solid instead of glass",
      });
    }
  }
  return { columnCount: count, issues };
}

/**
 * Audit first visible kanban card inside a board.
 * @param {import('@playwright/test').Page} page
 * @param {string} boardTestId
 */
async function auditKanbanCardGlass(page, boardTestId) {
  const card = page.locator(`[data-testid="${boardTestId}"] [draggable="true"] > div`).first();
  if ((await card.count()) === 0) return { hasCard: false, issues: [] };
  const styles = await card.evaluate((node) => {
    const s = window.getComputedStyle(node);
    return {
      backgroundColor: s.backgroundColor,
      backdropFilter: s.backdropFilter,
      webkitBackdropFilter: s.webkitBackdropFilter,
      className: node.className.slice(0, 120),
    };
  });
  const issues = isGlassLikeSurface(styles)
    ? []
    : [{ ...styles, reason: "Kanban card uses solid opaque background" }];
  return { hasCard: true, issues };
}

/** @param {import('@playwright/test').Page} page */
async function checkApiErrors(page) {
  const failed = [];
  page.on("response", (res) => {
    const url = res.url();
    if (url.includes("/api/") && res.status() >= 500) {
      failed.push({ url, status: res.status() });
    }
  });
  return failed;
}

/**
 * Audit glassmorphism: flags opaque panels where glass (transparent + backdrop-blur) is expected.
 * @param {import('@playwright/test').Page} page
 * @param {string} pagePath
 */
async function auditGlassmorphism(page, pagePath) {
  return page.evaluate((path) => {
    /** @param {string} bg */
    function parseAlpha(bg) {
      if (!bg || bg === "transparent") return 0;
      const rgba = bg.match(/rgba?\(([^)]+)\)/);
      if (!rgba) return 1;
      const parts = rgba[1].split(",").map((s) => s.trim());
      if (parts.length === 4) return parseFloat(parts[3]);
      return 1;
    }

    /** @param {Element} el */
    function elementInfo(el) {
      const s = window.getComputedStyle(el);
      const rect = el.getBoundingClientRect();
      return {
        tag: el.tagName.toLowerCase(),
        className: (el.className || "").toString().slice(0, 140),
        testId: el.getAttribute("data-testid") || null,
        background: s.backgroundColor,
        alpha: parseAlpha(s.backgroundColor),
        backdropFilter: s.backdropFilter || s.webkitBackdropFilter || "none",
        borderRadius: s.borderRadius,
        width: Math.round(rect.width),
        height: Math.round(rect.height),
      };
    }

    const issues = [];
    const main =
      document.querySelector("main") ||
      document.querySelector("[data-testid='app-main']") ||
      document.querySelector(".app-shell-main") ||
      document.body;

    const glassClasses = ["glass-card", "glass-panel", "dashboard-glass-card"];
    for (const cls of glassClasses) {
      for (const el of document.querySelectorAll(`.${cls}`)) {
        const info = elementInfo(el);
        if (info.width < 120 || info.height < 48) continue;

        if (info.alpha >= 0.88) {
          issues.push({
            issue: "glass-class-opaque-background",
            cssClass: cls,
            component: info.testId || cls,
            fileHint: "src/index.css",
            ...info,
          });
        }
        if (info.backdropFilter === "none") {
          issues.push({
            issue: "glass-class-missing-backdrop-blur",
            cssClass: cls,
            component: info.testId || cls,
            fileHint: "src/index.css",
            ...info,
          });
        }
      }
    }

    for (const el of main.querySelectorAll(".solid-card")) {
      const info = elementInfo(el);
      if (info.width >= 200 && info.height >= 72) {
        issues.push({
          issue: "solid-card-instead-of-glass",
          cssClass: "solid-card",
          component: info.testId || "solid-card panel",
          fileHint: "src/components/glass/GlassCard.jsx (variant=solid)",
          ...info,
        });
      }
    }

    for (const el of main.querySelectorAll('[class*="bg-white"]')) {
      const className = el.className.toString();
      if (/bg-white\/\d/.test(className)) continue;
      if (!/(^|\s)bg-white(\s|$)/.test(className)) continue;

      const info = elementInfo(el);
      if (info.width >= 280 && info.height >= 100 && info.alpha >= 0.92) {
        issues.push({
          issue: "opaque-bg-white-panel",
          cssClass: "bg-white",
          component: info.testId || className.slice(0, 60),
          fileHint: "page component (inline Tailwind)",
          ...info,
        });
      }
    }

  /** Sidebar reference for contrast drift */
    const sidebar = document.querySelector(".glass-sidebar");
    let sidebarRef = null;
    if (sidebar) {
      const s = window.getComputedStyle(sidebar);
      sidebarRef = {
        background: s.backgroundColor,
        alpha: parseAlpha(s.backgroundColor),
        backdropFilter: s.backdropFilter || s.webkitBackdropFilter,
      };
    }

    return { path, issues, sidebarRef };
  }, pagePath);
}

/** @param {import('@playwright/test').Page} page @param {string} selector */
async function auditElementTypography(page, selector) {
  const el = page.locator(selector).first();
  if ((await el.count()) === 0) return null;
  return el.evaluate((node) => {
    const s = window.getComputedStyle(node);
    return { fontFamily: s.fontFamily, fontSize: s.fontSize, color: s.color, lineHeight: s.lineHeight };
  });
}

/** @param {import('@playwright/test').Page} page */
async function auditBodyTypography(page) {
  return page.evaluate(() => {
    const body = document.body;
    const s = window.getComputedStyle(body);
    return { fontFamily: s.fontFamily, fontSize: s.fontSize, color: s.color };
  });
}

module.exports = {
  filterBenignConsoleErrors,
  setupConsoleCapture,
  auditPageUI,
  countIconButtonsWithoutAriaLabel,
  auditPageHeading,
  auditBorderRadiusMix,
  auditAppshellFeatures,
  auditPrimaryButtonColor,
  countDuplicateHeadings,
  checkApiErrors,
  collectConsoleErrors,
  auditGlassmorphism,
  parseBackgroundAlpha,
  isGlassLikeSurface,
  sampleSurfaceStyles,
  auditKanbanColumnGlass,
  auditKanbanCardGlass,
  auditElementTypography,
  auditBodyTypography,
};
