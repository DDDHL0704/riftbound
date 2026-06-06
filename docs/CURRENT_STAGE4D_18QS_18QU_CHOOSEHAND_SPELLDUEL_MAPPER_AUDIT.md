# Stage 4D 18QS-18QU Choose-Hand Spell-Duel Mapper Audit

Date: 2026-06-06 15:06 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QS added `MatchRecoveryTests.RecoveryValidatorRejectsChooseHandCardsRawPayloadPropertyAndListDrift`.
- 18QT extended `SpellDuelBattleStateMachineTests.PassPriorityStalePromptReplayAfterStackResolvesRecordsRejectedJournalWithoutMutation`.
- 18QU added `ConformanceFixtureShapeTests.GameCommandMapperChooseHandCardsUsesCurrentFieldsOverVisibleChoiceMetadata`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- Recovery validation coverage now proves recovered raw `CHOOSE_HAND_CARDS` payload drift diagnostics for malformed `chosenObjectIds` entries, duplicate raw properties, surrounding-whitespace property names and surrounding-whitespace `choiceId`/`choiceWindow`/chosen-object values.
- Spell-duel coverage now proves rejected stale prompt-scoped raw `PASS_PRIORITY` submissions after stack resolution are cached for exact duplicate replay without journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without events, journal growth, state drift, prompt drift or snapshot drift.
- Mapper coverage now proves `CHOOSE_HAND_CARDS` maps current top-level `choiceId`, `choiceWindow` and trimmed `chosenObjectIds` instead of backfilling from visible top-level prompt metadata or nested `candidate.metadata`.

## Source Commits

- 18QS worker source `487dbd4cb82197becd72fe408235a8b8bb420624`, cherry-picked to main as `1fe0754d`.
- 18QT worker source `21e9ae0de620802db1ee8825eec834513063e8dc`, cherry-picked to main as `b07ba928`.
- 18QU worker source `a7f1dce856f1077e1398124f0ca205c058b61500`, cherry-picked to main as `6f6207ef`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `1459/1459`.
- Broader adjacent server filter: `5443/5443`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- `git diff --check`: passed.
- `git diff cd6d9912..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 15:06 CST.

## Remaining Open

This narrows recovery raw `CHOOSE_HAND_CARDS` payload-shape diagnostics, rejected stale `PASS_PRIORITY` raw-intent cache semantics and choose-hand mapper current-field authority only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
