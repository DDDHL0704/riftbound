# Stage 4D 18OK-18OM Payment / SpellDuel / GameHub Audit

Date: 2026-06-06

Owner: A_MAIN

Project status: **NOT READY**

## Accepted Slice

A_MAIN integrated three parallel worktree slices on `main`:

- 18OK: `PaymentEngineUnificationTests.PendingPayCostTypedTemporaryResourceDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`
- 18OL: `SpellDuelBattleStateMachineTests.PassFocusSecondPlayerClosingSpellDuelStalePromptReplayRecordsRejectedJournalWithoutMutation`
- 18OM: `GameHubJoinTests.EndTurnWrapperAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate`

Worker source commits:

- 18OK source `d94e9b71`, cherry-picked on main as `7b89fea9`
- 18OL source `ed0aa5d5`, cherry-picked on main as `dead2424`
- 18OM source `91a1008f`, cherry-picked on main as `80ccf6c8`

Runtime changed: no. This is server test coverage only.

## Locked Behavior

Payment coverage now proves typed green temporary-resource `PAY_COST` accepts once, exact same `clientIntentId` plus exact same prompt-scoped raw payload replays the cached accepted result without journal growth, and the same intent with changed raw payload returns `CLIENT_INTENT_CONFLICT` without events, state/prompt/snapshot mutation or changed raw payload persistence. The accepted journal raw command preserves `cmdType`, typed `paymentId`, `paymentWindow`, typed `paymentChoiceIds`, `promptId` and `snapshotTick`, and excludes the changed `clientNote` field.

Spell-duel coverage now proves after P1 hands BF-A spell-duel focus to P2, P2's prompt-scoped raw `PASS_FOCUS` can close BF-A and start BF-B; replaying that stale P2 raw prompt with a new intent rejects with `PromptExpired`, emits no events, preserves post-close state/tick/prompts/snapshots and session snapshots, keeps BF-B active, and records a rejected journal entry preserving raw `cmdType`, P2 `promptId` and `snapshotTick`.

GameHub coverage now proves direct `.EndTurn(...)` after a finished match returns stable `MatchFinished`, redacts sentinel client-intent/raw/internal/debug text from the user-visible error, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and leaves finished P1/P2 snapshots unchanged.

## Validation

Validation passed on main:

- Focused new tests: `3/3`
- Touched class filter: `269/269`
- Broader adjacent server filter: `5264/5264`
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7342/7342`
- `git diff --check`: passed
- `git diff 0c964817..HEAD --check`: passed
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: no matches
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Open

This narrows typed temporary payment/session/spell-duel/GameHub finished-session protocol coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
