/**
 * Collects product/UX observations during human walkthroughs.
 * These are logged for review — they do not fail tests unless `failOn` is set.
 */
function createUxFindings() {
  /** @type {Array<{ area: string, observation: string, severity: 'info'|'question'|'issue' }>} */
  const items = [];

  return {
    note(area, observation, severity = "question") {
      items.push({ area, observation, severity });
      console.log(`[UX ${severity}] ${area}: ${observation}`);
    },
    list() {
      return [...items];
    },
    summary() {
      if (!items.length) return "No UX notes recorded.";
      return items.map((i) => `  [${i.severity}] ${i.area} — ${i.observation}`).join("\n");
    },
  };
}

module.exports = { createUxFindings };
