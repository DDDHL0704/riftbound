# Stage 4D-06H Hand Choice Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / hand-choice stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a hand-choice replay edge at the `MatchSession` prompt boundary: P1 resolves Undercover Agent's last-breath hand-choice prompt, the hand-choice window closes, chosen cards are discarded, and replacement draws are applied.

The accepted coverage proves that replaying the old prompt-scoped `CHOOSE_HAND_CARDS` raw command after the hand-choice window closes is rejected by `MatchSession` stale prompt protection before it can mutate the resolved hand / graveyard / deck state. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `UndercoverAgentHandChoiceStalePromptReplayAfterWindowClosesRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/UndercoverAgentTriggerTests.cs`.
- The test starts from the Undercover Agent last-breath trigger representative state after stack resolution opens P1's hand-choice prompt.
- It captures P1's current hand-choice prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving `HAND_CHOICE_RESOLVED`, two `CARD_DISCARDED` events, two drawn cards, closed `PendingHandChoice`, and no continuing hand-choice prompt.
- It replays the old prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate discard / draw / hand-choice resolution side effects, no hand / graveyard / main-deck drift, no reopened `PendingHandChoice`, and no choose-hand-cards prompt fork.

## Non-Closure

This narrows hand-choice stale prompt replay / closed-window fork risk only. It does not close full hand-choice breadth, full trigger / stack lifecycle breadth, hidden-info random-zone breadth, full action-window determinism, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
