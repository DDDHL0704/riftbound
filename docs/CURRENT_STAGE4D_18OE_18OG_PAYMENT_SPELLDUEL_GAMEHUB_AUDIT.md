# Stage 4D 18OE-18OG Payment / SpellDuel / GameHub Audit

Date: 2026-06-06

Owner: A_MAIN

Project status: **NOT READY**

## Accepted Slice

A_MAIN integrated three parallel worktree slices on `main`:

- 18OE: `PaymentEngineUnificationTests.PendingPayCostPromptScopedTemporaryResourceReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation`
- 18OF: `SpellDuelBattleStateMachineTests.PassPriorityStalePromptReplayAfterStackResolvesRecordsRejectedJournalWithoutMutation`
- 18OG: `GameHubJoinTests.ReadyAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate`

Worker source commits:

- 18OE source `6a49d18c`, cherry-picked on main as `649ad6f9`
- 18OF source `b5e61e31`, cherry-picked on main as `f3be790d`
- 18OG source `f7719aed`, cherry-picked on main as `8a16b44d`

Runtime changed: no. This is server test coverage only.

## Locked Behavior

Payment coverage now proves a stale prompt-scoped raw `PAY_COST` replay after a temporary-payment-resource payment window closes rejects with `PromptExpired`, emits no events, preserves post-payment state/prompts/snapshots, keeps the accepted payment journal entry intact, and records one rejected journal entry that preserves the stale raw command metadata including `cmdType`, `paymentId`, `paymentWindow`, `paymentChoiceIds`, `promptId` and `snapshotTick`.

Spell-duel coverage now proves stale prompt-scoped raw `PASS_PRIORITY` replay after stack resolution rejects with `PromptExpired`, emits no events, preserves the post-resolution spell-duel state/tick/prompts/snapshots, and records a rejected journal entry with the stale prompt metadata.

GameHub coverage now proves direct `Ready` after a finished match returns stable `MatchFinished`, redacts sentinel `clientIntentId` / raw / internal debug text from the user-visible error, emits no caller/group events, snapshots or prompts, does not grow the journal, and leaves finished P1/P2 snapshots unchanged.

## Validation

Validation passed on main:

- Focused new tests: `3/3`
- Touched class filter: `263/263`
- Broader adjacent server filter: `5258/5258`
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7336/7336`
- `git diff --check`: passed
- `git diff 78498982..HEAD --check`: passed
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: no matches
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Open

This narrows payment/session/spell-duel/GameHub finished-session protocol coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
