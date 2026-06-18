# Stage 4D-223BB Recovery Spectator Pending-Hand-Choice Missing/Null Payload Count Drift Audit

Date: 2026-06-18 10:05 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / code commit: `main` / `adfa6f99`

## Scope

This shard closes a narrow recovery-validator gap for spectator replay-frame timing `pendingHandChoice`.

When authoritative state retains a pending hand choice, spectator timing payloads that omit `pendingHandChoice` or set it to `null` now report:

- the existing required-payload diagnostic;
- required count `0` versus the authoritative pending-hand-choice required count; and
- max count `0` versus the authoritative pending-hand-choice max count.

Missing empty-authoritative payloads still report only the required-payload diagnostic, and a `null` payload remains valid when authoritative state has no pending hand choice.

## Rule Source

The five root PDF rule files remained present. `/tmp/riftbound_rules_pdf_text/` was regenerated during this batch from the root PDFs before documenting the slice.

Relevant latest core-rule anchors re-checked:

- 108.7 and 128-129 for hand privacy and card-back hiding boundaries;
- 355 for choices and target boundaries, including private-hand non-target handling;
- 382-383 and 401-404 for triggered-skill placement, choice confirmation and cost handling;
- 422.4 for hand discard instructions as effect execution; and
- 808.1.d for Last Breath pending-item/source timing.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now computes the authoritative pending hand choice before shape checks. Missing or null spectator payloads still emit the required-payload error; when authoritative state has a pending hand choice, validation also emits required/max count `0` diagnostics against authoritative counts.

This changes only recovery diagnostic reporting. It does not change hand-choice creation, hand privacy, choice resolution, discard/draw execution, prompt rendering, hidden-source redaction, authoritative state serialization or valid replay behavior.

## Tests

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now covers:

- missing `pendingHandChoice` with empty authoritative state: required-payload error and no count mismatch;
- missing `pendingHandChoice` with non-empty authoritative state: required-payload error plus required/max count `0` mismatch;
- `pendingHandChoice = null` with empty authoritative state: accepted with no pending-hand-choice diagnostics; and
- `pendingHandChoice = null` with non-empty authoritative state: required-payload error plus required/max count `0` mismatch.

Shared helpers build empty and retained pending-hand-choice fixtures and mutate spectator replay timing payloads consistently.

## Validation

- Focused pending-hand-choice filter: `17/17` passed.
- Changed-class `MatchRecoveryTests`: `1967/1967` passed.
- Adjacent PendingHandChoice/HandChoice/ChooseHandCards/SpectatorReplayTiming/Recovery filter: `1997/1997` passed.
- Backend full via `Riftbound.slnx`: `8299/8299` passed.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 26 commits behind current `main` after code commit `adfa6f99` and with no commits ahead of `main`. `rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**. FullOfficial, frontend build/Chrome/formal E2E, real DB-backed Postgres smoke, remaining recovery/authoritative/spectator nested payload breadth and final readiness remain open.
