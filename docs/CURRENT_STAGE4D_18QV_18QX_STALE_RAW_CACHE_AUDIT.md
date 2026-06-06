# Stage 4D-18QV/18QW/18QX Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

## Scope

This batch integrated three parallel worker slices focused on rejected stale prompt-scoped raw command cache semantics:

- 18QV: `PAY_COST` ordinary payment-window stale raw replay in `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`.
- 18QW: natural `ASSIGN_COMBAT_DAMAGE` stale raw replay after the next contest starts in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- 18QX: Undercover Agent `CHOOSE_HAND_CARDS` stale raw replay after the hand-choice window closes in `tests/Riftbound.ConformanceTests/UndercoverAgentTriggerTests.cs`.

Runtime code was not changed.

## Integrated Commits

- Worker `9a0410c` was cherry-picked to main as `abb6b371`.
- Worker `0a313d38` was cherry-picked to main as `93f5b29f`.
- Worker `3b5e5239` was cherry-picked to main as `8949b3a2`.

## Coverage Added

The extended tests now prove that exact duplicate submissions using the same rejected stale `clientIntentId` and identical raw payload replay the cached rejected `PromptExpired` result without journal growth. Changed raw payloads using that same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, event or journal drift, and without persisting the changed raw sentinel.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `147/147`.
- Broader adjacent server filter: `5568/5568`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- `git diff --check`, range diff check, anchored conflict-marker scan and matrix JSON parse passed before docs sync.

## Remaining Open

Project remains **NOT READY**. This closes only a narrow rejected stale raw cache semantic gap. Broader P0/P1 closure, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
