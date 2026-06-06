# Stage 4D 18QM-18QO Reveal Card Legend Act Raw Duplicate Audit

Date: 2026-06-06 14:19 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QM added `ConformanceFixtureRunnerTests.RevealCardBaseDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`.
- 18QN added `ConformanceFixtureRunnerTests.RevealCardReactionDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`.
- 18QO added `LegendResourceBridgeVerifierTests.LegendResourceBridgeDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- Session-level base `REVEAL_CARD` coverage now proves exact raw-payload duplicate intent retries replay the accepted base reveal result without journal growth, while changed raw payloads with the same `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.
- Session-level reaction `REVEAL_CARD` coverage now proves exact raw-payload duplicate intent retries replay the accepted stack reveal result without journal growth, while changed raw payloads with the same `clientIntentId` return `CLIENT_INTENT_CONFLICT` without stack, prompt, snapshot or journal drift.
- Legend resource bridge coverage now proves `LEGEND_ACT` exact raw-payload duplicate intent retries replay accepted resource-gain results across all success profiles without journal growth, while changed raw payloads with the same `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Source Commits

- 18QM worker source `f483d8e4762bade7f078a1edc76ff56f31b4c1d0`, cherry-picked to main as `95a2b4b4`.
- 18QN worker source `4def8386ea12012aa6822dbb3cb68895ab2684fa`, cherry-picked to main as `e55a5b8a`.
- 18QO worker source `3472fe302e6b5e33a891e1688902fb228ea54426`, cherry-picked to main as `d7d48ef6`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `11/11`.
- Touched class filter: `3159/3159`.
- Broader adjacent server filter: `5660/5660`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7404/7404`.
- `git diff --check`: passed.
- `git diff 3bf448e9..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 14:19 CST.

## Remaining Open

This narrows session `REVEAL_CARD` base/reaction raw duplicate-intent coverage and Legend resource bridge `LEGEND_ACT` raw duplicate-intent coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
