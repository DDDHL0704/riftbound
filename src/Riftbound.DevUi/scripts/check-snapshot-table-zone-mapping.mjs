import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const testSource = readFileSync(
  resolve(scriptDir, "../../../tests/Riftbound.ConformanceTests/SnapshotTableProjectionTests.cs"),
  "utf8"
);

for (const objectId of [
  "P1-GRAVEYARD",
  "P2-GRAVEYARD",
  "P1-BANISHED",
  "P2-BANISHED"
]) {
  assert.ok(
    testSource.includes(objectId),
    `Snapshot table projection tests must cover non-empty ${objectId} zone mapping.`
  );
}

for (const fieldName of [
  "Zones.MainDeckCount",
  "Zones.RuneDeckCount",
  "Zones.Graveyard",
  "Zones.Banished",
  "Zones.LegendZone",
  "Zones.ChampionZone",
  "[\"mainDeckCount\"]",
  "[\"runeDeckCount\"]",
  "[\"graveyard\"]",
  "[\"banished\"]",
  "[\"legendZone\"]",
  "[\"championZone\"]"
]) {
  assert.ok(
    testSource.includes(fieldName),
    `Snapshot table projection tests must assert ${fieldName} in typed table and snapshot payloads.`
  );
}

for (const standbyCountAssertion of [
  "tableLeftBattlefield.FaceDownStandbyCount",
  "tableRightBattlefield.FaceDownStandbyCount",
  "leftBattlefield[\"faceDownStandbyCount\"]",
  "rightBattlefield[\"faceDownStandbyCount\"]"
]) {
  assert.ok(
    testSource.includes(standbyCountAssertion),
    `Snapshot table projection tests must assert face-down standby authority field: ${standbyCountAssertion}.`
  );
}

for (const containment of [
  "Assert.Contains(\"P1-GRAVEYARD\", p1Objects.Keys)",
  "Assert.Contains(\"P1-BANISHED\", p1Objects.Keys)",
  "Assert.Contains(\"P1-LEGEND\", p1Objects.Keys)",
  "Assert.Contains(\"P1-HERO\", p1Objects.Keys)",
  "Assert.Contains(\"P2-GRAVEYARD\", p2Objects.Keys)",
  "Assert.Contains(\"P2-BANISHED\", p2Objects.Keys)",
  "Assert.Contains(\"P2-LEGEND\", p2Objects.Keys)",
  "Assert.Contains(\"P2-HERO\", p2Objects.Keys)"
]) {
  assert.ok(
    testSource.includes(containment),
    `Snapshot table projection tests must assert visible object payload coverage: ${containment}.`
  );
}

for (const stackRedactionAssertion of [
  "SnapshotRedactsHiddenStackSourcePerViewer",
  "STACK-HIDDEN-STANDBY",
  "sourceVisibility",
  "HIDDEN_REACTION",
  "UNL-099/219",
  "Assert.Equal([\"HIDDEN\", \"P1-LEFT-UNIT\"], StringList(p1StackItem[\"targetObjectIds\"]))",
  "AssertSerializedSnapshotDoesNotContain("
]) {
  assert.ok(
    testSource.includes(stackRedactionAssertion),
    `Snapshot table projection tests must assert hidden stack source redaction: ${stackRedactionAssertion}.`
  );
}

console.log("Snapshot table zone mapping coverage check passed.");
