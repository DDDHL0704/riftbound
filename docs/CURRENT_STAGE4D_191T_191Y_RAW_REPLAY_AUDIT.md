# Stage 4D-191T-191Y Raw Replay Audit

Date: 2026-06-07 11:30 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 191T added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for First Mate any-unit target scope.
- 191U added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Sfur Song.
- 191V added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Agile equipment direct play attach.
- 191W added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Sea Monster Hook.
- 191X added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Isolate.
- 191Y added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Hunt the Weak.

Runtime changed: no. Server test coverage only. These edits strengthen existing stale replay tests rather than adding new test methods, so the full conformance test count remains unchanged.

## Commits

- 191T source `a3dd5d03` -> main `9331257b`
- 191U source `b83d6b1e` -> main `0b75a1c7`
- 191V source `0ea50d9b` -> main `1cfc8797`
- 191W source `43b7b205` -> main `5f42e0a1`
- 191X source `b15a5ff9` -> main `76b7b0cb`
- 191Y source `ad334583` -> main `f5ef9b2f`

## Validation

- Worktree focused tests: `1/1` for each 191T through 191Y.
- Main focused batch: `6/6`.
- Main changed-class filter: `61/61`.
- Main adjacent raw/replay/client-intent filter: `980/980`.
- Backend full conformance project: `7577/7577`.
- `git diff --check`: passed before docs.

## Coordination Notes

- Workers remained patch-only and did not stage, commit, push or edit docs.
- 191U reported and corrected an accidental initial main-worktree patch before returning; A_MAIN verified the main worktree was clean before accepting the source worktree diff.
- A_MAIN handled focused validation, source commits, cherry-picks, main validation and docs checkpointing.

## Remaining Open

This closes only a narrow reordered stale raw `PLAY_CARD` replay audit slice. Broader P0/P1, command/recovery/random determinism, recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
