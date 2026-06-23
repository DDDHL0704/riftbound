import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const testSource = readFileSync(
  resolve(scriptDir, "../../../tests/Riftbound.ConformanceTests/LocalPlayabilityRuleRegressionTests.cs"),
  "utf8"
);

const testName = "LocalTwoPlayerFlowTapsRecyclesPlaysResolvesScoresAdvancesTurnAndKeepsHiddenInfoSafe";
assert.ok(
  testSource.includes(testName),
  `${testName} must remain the local 2P integrated flow regression.`
);

for (const requiredEvent of [
  "TURN_END_DECLARED",
  "TURN_END_CLEANUP_STARTED",
  "UNTIL_END_OF_TURN_EXPIRED",
  "RUNE_POOL_CLEARED",
  "CLEANUP_REPEATED",
  "TURN_PLAYER_ADVANCED",
  "TURN_START_BEGAN",
  "RUNES_CALLED",
  "CARD_DRAWN",
  "MAIN_PHASE_BEGAN"
]) {
  assert.ok(
    testSource.includes(requiredEvent),
    `Local 2P integrated flow must assert server event ${requiredEvent}.`
  );
}

for (const requiredSnippet of [
  "AssertLocalTwoPlayerEndTurnAuthority",
  "AssertLocalTwoPlayerTableAuthority",
  "P2-RUNE-DECK",
  "P2-DRAW",
  "scoredThisTurn",
  "scoredThisTurnPlayerIds",
  "RunePool.Empty"
]) {
  assert.ok(
    testSource.includes(requiredSnippet),
    `Local 2P integrated flow must assert authoritative ${requiredSnippet} coverage.`
  );
}

console.log("Local two-player flow authority coverage check passed.");
