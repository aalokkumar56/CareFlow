// global-teardown.js — runs once after the full Playwright suite.
// 1. Copies this run's screenshots from screenshotBase → screenshots/latest/
// 2. Compares with the previous timestamped run and prints a diff summary.

const fs = require("fs");
const path = require("path");

const SCREENSHOTS_ROOT = "D:\\Projects\\Sarvik\\Care-Flow\\screenshots";

function copyDirSync(src, dest) {
  if (!fs.existsSync(src)) return;
  fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDirSync(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
}

function collectPngs(dir, base = dir) {
  if (!fs.existsSync(dir)) return [];
  const results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...collectPngs(full, base));
    } else if (entry.name.endsWith(".png")) {
      results.push({ rel: path.relative(base, full), size: fs.statSync(full).size });
    }
  }
  return results;
}

module.exports = async function globalTeardown(config) {
  const screenshotBase = config.metadata?.screenshotBase;
  if (!screenshotBase) {
    console.log("[teardown] No screenshotBase in metadata — skipping screenshot sync.");
    return;
  }

  // 1. Copy to latest/
  const latestDir = path.join(SCREENSHOTS_ROOT, "latest");
  try {
    if (fs.existsSync(latestDir)) fs.rmSync(latestDir, { recursive: true, force: true });
    copyDirSync(screenshotBase, latestDir);
    console.log(`[teardown] Screenshots copied → ${latestDir}`);
  } catch (err) {
    console.warn("[teardown] Could not copy to latest/:", err.message);
  }

  // 2. Find two most recent timestamped runs for comparison
  let runs = [];
  try {
    runs = fs
      .readdirSync(SCREENSHOTS_ROOT, { withFileTypes: true })
      .filter((e) => e.isDirectory() && /^\d{4}-\d{2}-\d{2}/.test(e.name))
      .map((e) => e.name)
      .sort()
      .reverse(); // newest first
  } catch (_) {}

  if (runs.length < 2) {
    console.log("[teardown] No previous run to compare against — this is the first run.");
  } else {
    const currentDir = path.join(SCREENSHOTS_ROOT, runs[0]);
    const previousDir = path.join(SCREENSHOTS_ROOT, runs[1]);
    console.log(`\n[teardown] Screenshot diff: ${runs[0]} vs ${runs[1]}`);

    const currentPngs = collectPngs(currentDir);
    const previousMap = new Map(collectPngs(previousDir).map((f) => [f.rel, f.size]));

    const newFiles = [];
    const changed = [];
    const unchanged = [];

    for (const { rel, size } of currentPngs) {
      if (!previousMap.has(rel)) {
        newFiles.push(rel);
      } else if (previousMap.get(rel) !== size) {
        changed.push(rel);
      } else {
        unchanged.push(rel);
      }
    }

    const removed = [];
    for (const [rel] of previousMap) {
      if (!currentPngs.find((f) => f.rel === rel)) removed.push(rel);
    }

    if (newFiles.length)   console.log(`  NEW (${newFiles.length}):\n    ${newFiles.join("\n    ")}`);
    if (changed.length)    console.log(`  CHANGED/REGRESSION (${changed.length}):\n    ${changed.join("\n    ")}`);
    if (removed.length)    console.log(`  REMOVED/FIXED (${removed.length}):\n    ${removed.join("\n    ")}`);
    if (unchanged.length)  console.log(`  Unchanged: ${unchanged.length} screenshot(s)`);

    if (changed.length > 0) {
      console.warn("\n[teardown] ⚠️  Regressions detected — screenshots changed since last run. Review the images above.");
    } else if (newFiles.length === 0 && changed.length === 0) {
      console.log("\n[teardown] ✅ No screenshot regressions.");
    }
  }

  // 3. Write E2E bug log markdown
  try {
    const { writeBugLogMarkdown, MARKDOWN_FILE } = require("./helpers/bug-log");
    const result = writeBugLogMarkdown();
    console.log(`\n[teardown] Bug log written → ${MARKDOWN_FILE}`);
    console.log(`[teardown] Bugs found: ${result.bugs.length} (Critical: ${result.counts.Critical}, High: ${result.counts.High}, Medium: ${result.counts.Medium}, Low: ${result.counts.Low})`);
  } catch (err) {
    console.warn("[teardown] Could not write bug log:", err.message);
  }
};
