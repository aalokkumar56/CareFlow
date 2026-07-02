const fs = require("fs");
const path = require("path");

const BUG_LOG_DIR = path.join("D:\\Projects\\Sarvik\\Care-Flow\\Cure-Flow", "test_reports");
const RAW_FILE = path.join(BUG_LOG_DIR, "e2e-bugs-raw.jsonl");
const MARKDOWN_FILE = path.join(BUG_LOG_DIR, "e2e-bug-log.md");

/** @type {Array<object>} */
const _memory = [];

function ensureDir() {
  if (!fs.existsSync(BUG_LOG_DIR)) fs.mkdirSync(BUG_LOG_DIR, { recursive: true });
}

/**
 * @param {object} bug
 * @param {string} bug.severity - Critical | High | Medium | Low
 * @param {string} bug.page
 * @param {string} bug.title
 * @param {string} [bug.category]
 * @param {string} [bug.steps]
 * @param {string} [bug.expected]
 * @param {string} [bug.actual]
 * @param {string} [bug.screenshot]
 */
function reportBug(bug) {
  const entry = {
    ...bug,
    category: bug.category || "General",
    timestamp: new Date().toISOString(),
  };
  _memory.push(entry);
  ensureDir();
  fs.appendFileSync(RAW_FILE, `${JSON.stringify(entry)}\n`);
}

function clearBugLog() {
  _memory.length = 0;
  ensureDir();
  if (fs.existsSync(RAW_FILE)) fs.unlinkSync(RAW_FILE);
}

function loadAllBugs() {
  ensureDir();
  if (!fs.existsSync(RAW_FILE)) return [..._memory];
  const lines = fs.readFileSync(RAW_FILE, "utf8").trim().split("\n").filter(Boolean);
  return lines.map((line) => JSON.parse(line));
}

const SEVERITY_ORDER = ["Critical", "High", "Medium", "Low"];

/**
 * @param {{ passed?: number, failed?: number, skipped?: number, total?: number, durationMs?: number }} summary
 */
function writeBugLogMarkdown(summary = {}) {
  const bugs = loadAllBugs();
  const counts = { Critical: 0, High: 0, Medium: 0, Low: 0 };
  for (const b of bugs) {
    if (counts[b.severity] !== undefined) counts[b.severity] += 1;
  }

  const date = new Date().toISOString().slice(0, 10);
  const lines = [
    `# E2E Bug Log — ${date}`,
    "",
    `Generated: ${new Date().toISOString()}`,
    "",
    "## Test Run Summary",
    `- Total tests: ${summary.total ?? "—"}`,
    `- Passed: ${summary.passed ?? "—"}`,
    `- Failed: ${summary.failed ?? "—"}`,
    `- Skipped: ${summary.skipped ?? "—"}`,
    `- Duration: ${summary.durationMs ? `${Math.round(summary.durationMs / 1000)}s` : "—"}`,
    "",
    `## Summary: ${bugs.length} bugs (Critical: ${counts.Critical}, High: ${counts.High}, Medium: ${counts.Medium}, Low: ${counts.Low})`,
    "",
  ];

  for (const severity of SEVERITY_ORDER) {
    const group = bugs.filter((b) => b.severity === severity);
    if (group.length === 0) continue;
    lines.push(`### ${severity}`);
    lines.push("");
    for (const b of group) {
      let block = `- **[${b.page}]** ${b.title}`;
      if (b.component) block += `\n  - Component: ${b.component}`;
      if (b.cssClass) block += `\n  - CSS class: \`${b.cssClass}\``;
      if (b.filePath) block += `\n  - File: ${b.filePath}`;
      if (b.category) block += `\n  - Category: ${b.category}`;
      if (b.steps) block += `\n  - Steps: ${b.steps}`;
      if (b.expected) block += `\n  - Expected: ${b.expected}`;
      if (b.actual) block += `\n  - Actual: ${b.actual}`;
      if (b.screenshot) block += `\n  - Screenshot: ${b.screenshot}`;
      lines.push(block);
    }
    lines.push("");
  }

  if (bugs.length === 0) {
    lines.push("_No bugs recorded during this run._", "");
  }

  ensureDir();
  fs.writeFileSync(MARKDOWN_FILE, lines.join("\n"));
  return { path: MARKDOWN_FILE, bugs, counts };
}

module.exports = {
  reportBug,
  clearBugLog,
  loadAllBugs,
  writeBugLogMarkdown,
  BUG_LOG_DIR,
  MARKDOWN_FILE,
};
