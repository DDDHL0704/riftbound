import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const testSource = readFileSync(
  resolve(scriptDir, "../../../tests/Riftbound.ConformanceTests/SnapshotTableProjectionTests.cs"),
  "utf8"
);

for (const helper of [
  "AssertObjectLocation(",
  "AssertObjectLocationAbsent("
]) {
  assert.ok(
    testSource.includes(helper),
    `Snapshot table projection tests must use ${helper} to lock object location payloads.`
  );
}

for (const expectedAssertion of [
  "AssertObjectLocation(p1Objects, \"P1-BASE-UNIT\", \"P1\", \"BASE\", \"base\")",
  "AssertObjectLocation(p1Objects, \"P1-RUNE-1\", \"P1\", \"BASE\", \"rune\")",
  "AssertObjectLocation(p1Objects, \"BF-LEFT\", \"P1\", \"BATTLEFIELD\", \"battlefield-site\")",
  "AssertObjectLocation(p1Objects, \"P1-LEFT-UNIT\", \"P1\", \"BATTLEFIELD\", \"battlefield\", \"BF-LEFT\")",
  "AssertObjectLocation(p1Objects, \"P1-GRAVEYARD\", \"P1\", \"GRAVEYARD\", \"graveyard\")",
  "AssertObjectLocation(p1Objects, \"P1-BANISHED\", \"P1\", \"BANISHED\", \"banished\")",
  "AssertObjectLocation(p1Objects, \"P1-LEGEND\", \"P1\", \"LEGEND\", \"legend\")",
  "AssertObjectLocation(p1Objects, \"P1-HERO\", \"P1\", \"CHAMPION\", \"champion\")",
  "AssertObjectLocation(p2Objects, \"P2-RIGHT-STANDBY\", \"P2\", \"BATTLEFIELD\", \"battlefield\", \"BF-RIGHT\")",
  "AssertObjectLocation(p2ObjectsFromP2, \"P2-HIDDEN-STANDBY\", \"P2\", \"BATTLEFIELD\", \"battlefield\", \"BF-LEFT\")",
  "AssertObjectLocationAbsent(p2Objects, \"P2-HIDDEN-STANDBY\")",
  "AssertObjectLocationAbsent(p1ObjectsFromP2, \"P1-STANDBY\")"
]) {
  assert.ok(
    testSource.includes(expectedAssertion),
    `Snapshot table projection tests must assert object location payload: ${expectedAssertion}.`
  );
}

console.log("Snapshot object location coverage check passed.");
