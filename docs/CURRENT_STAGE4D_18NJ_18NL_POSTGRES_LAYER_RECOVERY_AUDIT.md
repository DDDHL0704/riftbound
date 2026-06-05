# Stage 4D-18NJ/18NK/18NL Postgres / LayerEngine / Recovery Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main as a server test breadth checkpoint. Project remains **NOT READY**.

## Scope

- 18NJ added `PostgresRecoveryStoreReplaysSingleAcceptedCommandAfterExactDuplicateJournalEntry` in `tests/Riftbound.ConformanceTests/PostgresMatchRecoveryStoreSmokeTests.cs`. The smoke test preserves the no-connection-string early return and, when a real `ConnectionStrings__Riftbound` is present, records an accepted command through a capturing journal, writes the exact duplicate journal entry again, verifies `command_log` keeps one raw payload row, loads recovery, and validates the recovery frame replays without drift.
- 18NK added `LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataRecomputesWhenParticipantMovesAroundSourcesAcrossPlayerViews` in `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`. A_MAIN also preserved an already-present main-worktree LayerEngine coverage addition, `LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataTracksReorderedPublicFieldOrderAcrossPlayerViews`, after reviewing it as a valid non-runtime test drift. Together they cover source-order recomputation when source and non-source battlefield ordering changes while authoritative/player snapshot dependency metadata remains aligned.
- 18NL added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListElementShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`. It locks temporary-payment-resource `allowedPaymentKinds` element-shape validation separately from existing missing/null/empty/value/list-payload-shape coverage.

## Integration Notes

- Worker source commits: `b1fe0776` (18NJ), `212bc4e1` (18NK), `31eb9904` (18NL).
- `b1fe0776` and `31eb9904` were cherry-picked with `-n`. `212bc4e1` could not be direct cherry-picked because the main worktree already had a same-file LayerEngine test addition; A_MAIN manually merged the 18NK test and kept both LayerEngine coverage points.
- Runtime changed: no. Test coverage only.
- Real DB-backed Postgres execution remains open in this environment because `ConnectionStrings__Riftbound` was absent; the Postgres smoke passed only its early-return path locally.

## Validation

- Focused new tests: `4/4`.
- Touched class filter (`PostgresMatchRecoveryStoreSmokeTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests`): `1319/1319`.
- Broader adjacent server filter: `5389/5389`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7315/7315`.
- Pre-doc mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Open

- P0/P1 closure, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
