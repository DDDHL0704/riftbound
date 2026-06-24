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

const promptScopedTestName = "LocalTwoPlayerRuneActionsUsePromptScopedSessionSubmissionAndRejectStalePrompt";
assert.ok(
  testSource.includes(promptScopedTestName),
  `${promptScopedTestName} must keep local 2P rune actions wired through MatchSession prompt/tick authority.`
);

for (const requiredEvent of [
  "UNIT_PLAYED_TO_BATTLEFIELD",
  "BATTLEFIELD_CONTESTED",
  "SPELL_DUEL_STARTED",
  "SPELL_DUEL_CLOSED",
  "BATTLEFIELD_CONTROL_RESOLVED",
  "BATTLEFIELD_CONQUERED",
  "SCORE_GAINED",
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
  "AssertLocalTwoPlayerSpellDuelAuthority",
  "AssertLocalTwoPlayerEndTurnAuthority",
  "AssertLocalTwoPlayerTableAuthority",
  "AssertLocalTwoPlayerBattlefieldProjectionAuthority",
  "p2PriorityPass.Events.Select",
  "p2FocusPass.Events.Select",
  "P2-RUNE-DECK",
  "P2-DRAW",
  "P2-FACE-DOWN-STANDBY",
  "MainDeckCount",
  "RuneDeckCount",
  "Zones.Base",
  "Zones.BaseCards",
  "Zones.BaseRunes",
  "Zones.Battlefields",
  "BattlefieldHiddenStandbyCount",
  "ExpectedHiddenStandbyCount",
  "p2Table.Battlefields.Single",
  "scoredThisTurn",
  "scoredThisTurnPlayerIds",
  "RunePool.Empty"
]) {
  assert.ok(
    testSource.includes(requiredSnippet),
    `Local 2P integrated flow must assert authoritative ${requiredSnippet} coverage.`
  );
}

for (const requiredPromptScopedSnippet of [
  "PromptScopedRuneRawCommand",
  "session.SubmitAsync",
  "ErrorCodes.PromptExpired",
  "行动快照已过期，请按最新状态重新提交。",
  "MatchStateHasher.Hash(staleRecycle.State)",
  "AssertPromptScopedRuneRawCommand",
  "CommandTypes.TapRune",
  "CommandTypes.RecycleRune"
]) {
  assert.ok(
    testSource.includes(requiredPromptScopedSnippet),
    `Local 2P prompt-scoped flow must assert ${requiredPromptScopedSnippet}.`
  );
}

console.log("Local two-player flow authority coverage check passed.");
