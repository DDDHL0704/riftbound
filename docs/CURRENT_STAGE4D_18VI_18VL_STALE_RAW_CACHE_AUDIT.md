# Stage 4D-18VI/18VJ/18VK/18VL Stale Raw Cache Audit

Date: 2026-06-07

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18VI `codex/stage4d-18vi-sett-declare-stale-cache`: source `c1cfbe7b` cherry-picked as `55128dc5`, touching `tests/Riftbound.ConformanceTests/SettLegendActionDomainGuardTests.cs`.
- 18VJ `codex/stage4d-18vj-void-declare-stale-cache`: source `4737db96` cherry-picked as `4b78f700`, touching `tests/Riftbound.ConformanceTests/VoidBurrowerLegendActionDomainGuardTests.cs`.
- 18VK `codex/stage4d-18vk-ezreal-blue-swift-stale-cache`: source `370ac565` cherry-picked as `0695bb72`, touching `tests/Riftbound.ConformanceTests/EzrealBlueSwiftMoveToBaseActivatedAbilityTests.cs`.
- 18VL `codex/stage4d-18vl-ornn-play-stale-cache`: source `00280379` cherry-picked as `72c0f7b8`, touching `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`.

A_MAIN added integration fix `e3812d6f` to use analyzer-safe `Assert.Single(..., predicate)` forms and to align Sett/Void prompt assertions with declare-battle prompt materialization behavior. Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the rejected stale prompt-scoped raw cache contract after the first accepted command advances the authoritative state:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, journal, raw-command or relevant zone/object/card drift.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Sett `DECLARE_BATTLE`, Void Burrower `DECLARE_BATTLE`, Ezreal Blue Swift `ACTIVATE_ABILITY`, and Ornn `PLAY_CARD`.

## Validation

- Focused changed tests: `58/58`.
- First adjacent server filter: `1397/1397`.
- Broader adjacent server filter: `5256/5256`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7475/7475`.
- Mechanical checks passed: `git diff --check`, range review from `31a39549..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-07 01:40 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for two legend-action `DECLARE_BATTLE` surfaces, one typed-blue `ACTIVATE_ABILITY` surface, and one Ornn `PLAY_CARD` surface only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
