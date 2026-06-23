import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const testSource = readFileSync(
  resolve(scriptDir, "../../../tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs"),
  "utf8"
);

const testName = "OfficialTwoPlayerOpeningAndFirstEndTurnProjectFrontendTableAuthorityWithoutHiddenLeakage";
assert.ok(
  testSource.includes(testName),
  `${testName} must remain the official two-player opening table authority regression.`
);

for (const requiredSnippet of [
  "AssertOpeningRawZoneAuthority",
  "AssertHiddenDeckObjectsDoNotLeakToOpponentTableSnapshot",
  "hiddenMainDeckObjectId",
  "hiddenRuneDeckObjectId",
  "mainDeckCount",
  "runeDeckCount",
  "baseRunes",
  "legendZone",
  "championZone"
]) {
  assert.ok(
    testSource.includes(requiredSnippet),
    `Official opening table authority tests must assert ${requiredSnippet}.`
  );
}

console.log("Official opening table authority coverage check passed.");
