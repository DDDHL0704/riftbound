# Stage 4D-18VQ/18VR/18VS/18VT/18VU Payment, Layer, Protocol Audit

Date: 2026-06-07

Status: accepted into A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN accepted five parallel server-test slices covering payment-resource composition, Lux spell-only resource composition, battle-response held-score payment-resource composition, LayerEngine legacy remainder snapshot determinism, and GameHub protocol version envelopes.

Runtime changed: no. Test coverage only.

## Accepted Commits

- 18VQ PaymentEngine: worker commit `f6e5d9057197f058233d01fc1111d0ceaea6c809` accepted into main as `912cde69`, adding `PlayCardGenericPowerShortfallQuotesAndCommitsTwoTemporaryPaymentResourcesWhenNeitherAlonePaysCost` in `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`.
- 18VR Lux: worker reported a cwd mistake and left no source commit in its dedicated worktree; A_MAIN reviewed the resulting main-worktree patch and accepted it as `8d5becc6`, adding `LuxSpellOnlyResourceUsesTwoReadyLuxSourcesForLargeSpellShortfallAndCleansEachInlineResource` in `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`.
- 18VS BattleDamageAssignment: worker commit `fd38dff14739aa592ec9bb6998785b3a8e42a317` accepted into main as `db5e3b62`, adding `NaturalBattleResponseHeldScoreConsumesTwoTemporaryResourcesWhenScoreCostNeedsBoth` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- 18VT LayerEngine: worker reported a cwd mistake and left no source commit in its dedicated worktree; A_MAIN reviewed the resulting main-worktree patch and accepted it as `cb626a6f`, adding `LayerEnginePowerModifierLedgerLegacyRemainderSnapshotsAreDeterministicAcrossPlayersAndBuilds` in `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`.
- 18VU GameHub: worker commit `0763669af4be12e623b7e20e428b87b969e0bdcc` accepted into main as `f8588450`, adding `HubMessagesCarryProtocolVersionsOnJoinSnapshotPromptAndError` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

## Coordination Note

This batch proved useful parallel throughput across five disjoint files, but also exposed a cwd coordination failure: 18VR and 18VT initially applied patches to `/Users/dinghaolin/IdeaProjects/riftbound` instead of their assigned worktrees. A_MAIN interrupted active workers, required `pwd`, `git status --short --branch`, and `git rev-parse HEAD` reports, kept the accidental main patches intact, reviewed them as integration patches, and split them into separate A_MAIN commits. Future worker prompts should require the environment check before any edit.

## Validation

- Focused Lux/Layer pre-integration filter: `36/36`.
- Focused changed-test bundle: `353/353` for `PaymentEngineUnificationTests|LuxResourceSkillTests|BattleDamageAssignmentLifecycleTests|LayerEngineTimestampDependencyTests|GameHubJoinTests`.
- Adjacent/broader server filter: `5469/5469` for payment, Lux, battle damage assignment, LayerEngine, GameHub, fixture, recovery/opening and Ornn adjacent coverage.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7484/7484`.
- `git diff --check b0550e94 HEAD` passed before docs sync.

## Remaining Risk

This narrows payment-resource composition, Lux spell-only resource composition, held-score battle-response temporary-resource consumption, LayerEngine legacy remainder snapshots and GameHub protocol envelope coverage only. It does not close P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, DOC_MATRIX future scope or final readiness.
