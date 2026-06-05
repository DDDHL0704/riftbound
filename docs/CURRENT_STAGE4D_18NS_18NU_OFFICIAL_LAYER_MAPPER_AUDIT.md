# Stage 4D-18NS/18NT/18NU Official / Layer / Mapper Audit

Date: 2026-06-06

Project status remains **NOT READY**.

## Scope

A_MAIN accepted a parallel server-test breadth batch across three disjoint surfaces:

- 18NS: `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
- 18NT: `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`
- 18NU: `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

Runtime code changed: no. Test coverage changed: yes.

## Added Coverage

- `OfficialFirstTurnSurrenderFreshEndTurnAfterMatchFinishedThrowsStableErrorWithoutMutation` proves a fresh `END_TURN` submission after an official first-turn surrender has finished the match throws the stable `MatchFinished` session exception, does not append a journal entry, and leaves public snapshots/prompts unchanged at the match-result prompt.
- `LayerEngineObjectStaticAuraParticipantMetadataRecomputesAcrossPlayerViewsWhenEquipmentParticipantLeavesPublicField` proves Ornn object static-aura participant metadata recomputes to the remaining public equipment after another friendly equipment participant leaves public field, with matching P1/P2 snapshot signatures and no removed/hidden participant metadata leakage.
- `GameCommandMapperTrimsAndDropsUnreadableHideCardOptionalCosts` proves `HIDE_CARD` non-strict `optionalCosts` arrays trim valid strings and drop blank/null/non-string/unreadable entries while preserving source/card/destination fields.

## Worker / Integration Notes

- 18NU worker source commit `1a2ee535` was cherry-picked to main as `9b2f246f`.
- 18NS worker source commit `23949fa9` was reviewed by A_MAIN; A_MAIN removed a private-reflection helper before committing the main version as `5f104aa6`.
- 18NT worker accidentally applied its patch to the main worktree through a relative `apply_patch` path and stopped without a worker commit. A_MAIN restored the intended test in main, repaired the displaced `[Fact]`, validated it, and committed it as `95c706f9`.

## Validation

- Focused new tests: `3/3`.
- Touched class filter (`OfficialOpeningTests|LayerEngineTimestampDependencyTests|ConformanceFixtureShapeTests`): `733/733`.
- Broader adjacent server filter: `5398/5398`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7324/7324`.
- Mechanical checks passed before checkpoint: `git diff --check`, `git diff --cached --check`, anchored conflict-marker scan, and matrix JSON parse.

## Remaining Open

This narrows official session boundary, LayerEngine object static-aura metadata, and mapper payload-shape coverage only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open. Project remains **NOT READY**.
