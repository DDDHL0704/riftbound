import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(resolve(scriptDir, "playwright-qa.mjs"), "utf8");

assert.match(
  source,
  /import\s+\{\s*tmpdir\s*\}\s+from\s+"node:os"/,
  "Playwright QA must write runtime screenshots and reports outside tracked artifacts by default."
);

assert.match(
  source,
  /RIFTBOUND_QA_OUTPUT_ROOT\s*\?\?\s*path\.join\(tmpdir\(\),\s*"riftbound-dev-ui-qa"\)/,
  "Playwright QA must default outputRoot to an OS temp directory unless RIFTBOUND_QA_OUTPUT_ROOT is explicitly set."
);

assert.match(
  source,
  /const\s+baselineDiffEnabled\s*=\s*process\.env\.RIFTBOUND_QA_BASELINE_DIFF\s*===\s*"1"/,
  "Playwright QA pixel baseline diff must be opt-in so stale tracked baselines do not block the white wireframe gate."
);

assert.match(
  source,
  /await\s+assertWireframeVisual\(buffer,\s*shot\.name\)/,
  "Playwright QA must still enforce a code-driven white wireframe visual invariant for every captured shot."
);

console.log("Playwright QA output policy check passed.");
