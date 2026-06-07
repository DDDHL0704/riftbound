# Stage 4D-191N-191S Raw Replay Audit

Date: 2026-06-07 11:15 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 191N added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Battle or Flight.
- 191O added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Hostile Takeover.
- 191P added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Gust.
- 191Q added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Reprimand.
- 191R added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Spirit Fire.
- 191S added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Zenith Blade.

Runtime changed: no. Server test coverage only. These edits strengthen existing stale replay tests rather than adding new test methods, so the full conformance test count remains unchanged.

## Commits

- 191N source `981fd48b` -> main `6477315d`
- 191O source `1828e410` -> main `2baa9d9a`
- 191P source `b75148cb` -> main `906d11d8`
- 191Q source `1a247295` -> main `f78c21ff`
- 191R source `799daa7b` -> main `005cc5e7`
- 191S source `2dd75d0f` -> main `ae298dc6`

## Validation

- Worktree focused tests: `1/1` for each 191N through 191S.
- Main focused batch: `6/6`.
- Main changed-class filter: `63/63`.
- Main adjacent raw/replay/client-intent filter: `980/980`.
- Backend full conformance project: `7577/7577`.
- `git diff --check`: passed before docs.

## Coordination Notes

- Workers remained patch-only and did not stage, commit, push or edit docs.
- A_MAIN reviewed worker diffs and confirmed the reordered raw assertions preserved the existing same-raw duplicate replay and changed-payload `CLIENT_INTENT_CONFLICT` checks.
- A_MAIN handled focused validation, source commits, cherry-picks, main validation and docs checkpointing.

## Remaining Open

This closes only a narrow reordered stale raw `PLAY_CARD` replay audit slice. Broader P0/P1, command/recovery/random determinism, recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
