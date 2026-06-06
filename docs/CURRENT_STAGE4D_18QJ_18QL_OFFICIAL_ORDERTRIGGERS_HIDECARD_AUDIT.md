# Stage 4D 18QJ-18QL Official Order Triggers Hide Card Audit

Date: 2026-06-06 13:58 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QJ added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshChooseHandCardsAfterMatchFinishedThrowsStableErrorWithoutMutation`.
- 18QK added `ConformanceFixtureShapeTests.OrderTriggersStaleRawPromptAfterStackPriorityStartsRecordsRejectedJournalWithoutMutation`.
- 18QL added `ConformanceFixtureRunnerTests.HideCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- Official session coverage now proves a fresh `CHOOSE_HAND_CARDS` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.
- Order-trigger prompt coverage now proves a stale prompt-scoped raw `ORDER_TRIGGERS` replay after stack priority starts records a rejected journal entry with preserved raw prompt fields, returns `PromptExpired`, emits no events, and preserves trigger queue, stack ordering, priority, prompts and snapshots.
- Session-level HIDE_CARD coverage now proves exact raw-payload duplicate intent retries replay the cached accepted hide-card result without journal growth, while changed raw payloads with the same `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Source Commits

- 18QJ worker source `e9aabbe22b370dc5da8e6d7276768a8924aeea5a`, cherry-picked to main as `3b170b9e`.
- 18QK worker source `4ba160a81d4f0716fc878c9a911b39510fb5bf58`, cherry-picked to main as `d99d9337`.
- 18QL worker source `6b8da23ce0319e3995d9430829a57cd84e3d32b7`, cherry-picked to main as `c3c37e04`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `3793/3793`.
- Broader adjacent server filter: `5550/5550`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7393/7393`.
- `git diff --check`: passed.
- `git diff f073e8e0..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 13:58 CST.

## Remaining Open

This narrows official finished-session `CHOOSE_HAND_CARDS`, order-trigger stale raw rejected-journal, and session HIDE_CARD raw duplicate-intent coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
