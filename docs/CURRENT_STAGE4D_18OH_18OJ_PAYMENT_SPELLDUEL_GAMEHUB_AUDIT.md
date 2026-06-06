# Stage 4D 18OH-18OJ Payment / SpellDuel / GameHub Audit

Date: 2026-06-06

Owner: A_MAIN

Project status: **NOT READY**

## Accepted Slice

A_MAIN integrated three parallel worktree slices on `main`:

- 18OH: `PaymentEngineUnificationTests.PendingPayCostPromptScopedTypedTemporaryResourceReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation`
- 18OI: `SpellDuelBattleStateMachineTests.PassFocusStalePromptReplayAfterFocusHandoffRecordsRejectedJournalWithoutMutation`
- 18OJ: `GameHubJoinTests.PassAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate`

Worker source commits:

- 18OH source `e5d2d818`, cherry-picked on main as `fd990567`
- 18OI source `76c2b2f8`, cherry-picked on main as `588cf094`
- 18OJ source `161e8ea6`, cherry-picked on main as `7bb7568c`

Runtime changed: no. This is server test coverage only.

## Locked Behavior

Payment coverage now proves stale prompt-scoped raw `PAY_COST` after a typed temporary-payment-resource payment window closes rejects with `PromptExpired`, emits no events, preserves post-payment state/prompts/snapshots, keeps pending payment and temporary resources cleared, and records one rejected `PAY_COST` journal entry preserving raw `cmdType`, typed `paymentId`, `paymentWindow`, `paymentChoiceIds`, `promptId` and `snapshotTick`.

Spell-duel coverage now proves stale prompt-scoped raw `PASS_FOCUS` replay after ordinary focus handoff from P1 to P2 rejects with `PromptExpired`, emits no events, preserves post-handoff state/tick/prompts/snapshots and session snapshots, keeps P2 focused on BF-A, and records a rejected `PASS_FOCUS` journal entry with the old prompt metadata and post-handoff authoritative state/prompts/snapshots.

GameHub coverage now proves direct `.Pass(...)` after a finished match returns stable `MatchFinished`, redacts sentinel client-intent/raw/internal/debug text from the user-visible error, emits no caller/group events, snapshots or prompts, does not grow the journal, and leaves finished P1/P2 snapshots unchanged.

## Validation

Validation passed on main:

- Focused new tests: `3/3`
- Touched class filter: `266/266`
- Broader adjacent server filter: `5261/5261`
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7339/7339`
- `git diff --check`: passed
- `git diff e7b9cab0..HEAD --check`: passed
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: no matches
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Open

This narrows typed temporary payment/session/spell-duel/GameHub finished-session protocol coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
