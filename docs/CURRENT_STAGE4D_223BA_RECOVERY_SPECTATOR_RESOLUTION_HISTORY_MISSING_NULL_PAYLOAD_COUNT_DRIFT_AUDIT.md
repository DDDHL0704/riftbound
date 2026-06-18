# Stage 4D-223BA Recovery Spectator Resolution-History Missing/Null Payload Count Drift Audit

Date: 2026-06-18 09:54 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / code commit: `main` / `ed43cff4`

## Scope

This shard closes a narrow recovery-validator gap for spectator replay-frame timing resolution-history payloads.

When authoritative state retains battlefield or battle resolution history, spectator timing payloads that omit `battlefieldResolutions` / `battleResolutions` or set them to `null` now report both:

- the existing required-payload diagnostic; and
- count `0` versus the authoritative non-empty resolution-history count.

The empty-authoritative missing/null companions remain without count-mismatch diagnostics.

## Rule Source

Root PDF gate remained active; all five root PDF rule files were present before this checkpoint. This slice re-checked the latest extracted core-rule text in `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`, especially:

- 454-461 for battle and battlefield resolution context;
- 460.2.c / 460.2.d for assignment before simultaneous damage;
- 461.3 / 461.5 / 461.7 for battle result, battlefield control and battle-end cleanup; and
- 334-335 for task-processing boundaries around retained resolution history.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now computes authoritative battlefield/battle resolution counts before timing payload shape checks. Missing or null spectator payloads still emit the required-payload error, and additionally emit the same count-drift diagnostic used by present-but-empty/mismatched lists when the authoritative count is non-zero.

This changes only recovery diagnostic reporting. It does not change valid replay behavior, battle resolution creation, battle-step runtime behavior, prompt rendering, hidden-source redaction, authoritative state serialization, random determinism or source-object serialization.

## Tests

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now covers:

- `battlefieldResolutions` missing without count mismatch when authoritative history is empty;
- `battlefieldResolutions` missing with count mismatch when authoritative history is non-empty;
- `battlefieldResolutions = null` without count mismatch when authoritative history is empty;
- `battlefieldResolutions = null` with count mismatch when authoritative history is non-empty;
- `battleResolutions` missing without count mismatch when authoritative history is empty;
- `battleResolutions` missing with count mismatch when authoritative history is non-empty;
- `battleResolutions = null` without count mismatch when authoritative history is empty; and
- `battleResolutions = null` with count mismatch when authoritative history is non-empty.

Shared helpers build empty and retained resolution-history fixtures to keep the missing/null matrix aligned with the existing resolution-history shape/reference tests.

## Validation

- Focused missing/null payload matrix: `8/8` passed.
- Changed-class `MatchRecoveryTests`: `1964/1964` passed.
- Adjacent BattlefieldResolution/BattleResolution/ResolutionHistory/SpectatorReplayTiming/Recovery filter: `1969/1969` passed.
- Backend full via `Riftbound.slnx`: `8296/8296` passed.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

## Coordination

Local `main` was fast-forwarded to origin `36b514bf` (`Rebuild DevUi tabletop and QA gates`) before this slice. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 24 commits behind current `main` after code commit `ed43cff4` and with no commits ahead of `main`. `rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**. FullOfficial, frontend build/Chrome/formal E2E, real DB-backed Postgres smoke, remaining recovery/authoritative/spectator nested payload breadth and final readiness remain open.
