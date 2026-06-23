import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const apiStarterScripts = [
  "chrome-smoke.mjs",
  "playwright-qa.mjs"
];

for (const scriptName of apiStarterScripts) {
  const source = readFileSync(resolve(scriptDir, scriptName), "utf8");

  assert.match(
    source,
    /ConnectionStrings__Riftbound:\s*process\.env\.RIFTBOUND_QA_CONNECTION_STRING\s*\?\?\s*""/,
    `${scriptName} must default API persistence to Noop by clearing ConnectionStrings__Riftbound unless RIFTBOUND_QA_CONNECTION_STRING is explicitly set.`
  );

  assert.match(
    source,
    /ASPNETCORE_ENVIRONMENT:\s*"Development"/,
    `${scriptName} should continue using Development behavior for local room fixtures.`
  );

  assert.match(
    source,
    /ASPNETCORE_URLS:\s*serverUrl/,
    `${scriptName} must keep binding the API to the script-controlled serverUrl.`
  );
}

console.log("Playwright QA API environment check passed.");
