# Stage 4D-191A-191G Raw Mapper Recovery Audit

Date: 2026-06-07 10:39 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 191A added reordered stale raw `DECLARE_BATTLE` rejected-cache replay coverage for Void Burrower.
- 191B added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Tempered optional attach.
- 191C added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Charm.
- 191D added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Firestorm.
- 191E added a `PAY_COST` raw `paymentWindow` required diagnostic assertion in recovery validation.
- 191F added hub-level reordered raw `SUBMIT_DECK` duplicate-intent replay coverage.
- 191G added opening mapper text-array normalization coverage for `SUBMIT_DECK` and `MULLIGAN`.

Runtime changed: no. Server test coverage only.

## Commits

- 191A source `e0a49a46` -> main `67394432`
- 191B source `0114f80a` -> main `b5567fea`
- 191C source `9debfed2` -> main `2c173114`
- 191D source `656b4c13` -> main `1992439f`
- 191E source `56be7a82` -> main `01cf55ad`
- 191F source `c9c44df7` -> main `d70969b5`
- 191G source `a34af6d7` -> main `266a192c`

## Validation

- Worktree focused tests: `1/1` for each 191A through 191G.
- Main focused batch: `7/7`.
- Main changed-class filter: `1665/1665`.
- Main adjacent raw/replay/recovery/mapper/GameHub/shape filter: `2020/2020`.
- Backend full conformance project: `7577/7577`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.

## Coordination Notes

- 191C and 191F initially stopped safely because the inherited cwd was the main worktree. The retry prompt corrected the workflow to `cd` into the assigned worktree before validating `pwd`.
- Workers remained patch-only and did not stage, commit, push or edit docs.
- A_MAIN handled focused validation, source commits, cherry-picks, main validation and docs checkpointing.

## Remaining Open

This closes only a narrow raw replay, mapper and recovery diagnostic audit slice. Broader P0/P1, command/recovery/random determinism, recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
