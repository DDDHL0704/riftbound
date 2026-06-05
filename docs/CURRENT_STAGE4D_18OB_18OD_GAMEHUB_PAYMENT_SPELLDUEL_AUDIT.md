# Stage 4D-18OB/18OC/18OD GameHub / Payment / SpellDuel Audit

Date: 2026-06-06

Owner: `A_MAIN`

Status: accepted on main after review of three parallel worker/worktree slices. Project remains **NOT READY**.

## Scope

- 18OB source `c6507550`: `GameHubJoinTests` now covers after-finished `MULLIGAN` sentinel payload handling, proving the generic finished-session path returns stable `MatchFinished`, redacts raw/client-intent/internal/debug text, emits no caller/group events, snapshots or prompts, leaves finished P1/P2 snapshots unchanged, and does not grow the journal.
- 18OC source `a011a6ea`: `PaymentEngineUnificationTests` now covers stale prompt-scoped raw `PAY_COST` after the ordinary payment window closes, proving it rejects with `PromptExpired`, records a rejected journal entry with the stale raw prompt metadata, and leaves state, prompts, snapshots, rune pool, stack and pending payment unchanged.
- 18OD source `1e8fea33`: `SpellDuelBattleStateMachineTests` now covers stale prompt-scoped raw `PASS_FOCUS` after the first spell duel closes and the next contest starts, proving it rejects with `PromptExpired`, records a rejected journal entry with stale BF-A prompt metadata, and leaves the post-close BF-B spell-duel state/prompts/snapshots unchanged.

## Main Integration

- 18OB cherry-picked as `49de4d55`.
- 18OC cherry-picked as `6dca360d`.
- 18OD cherry-picked as `1e59dd50`.
- Runtime code changed: no.
- Protocol shape changed: no.
- Matrix JSON changed: no.
- Frontend changed: no.

## Validation

- Focused new tests: `3/3`.
- Touched class filter: `260/260`.
- Broader adjacent server filter: `5255/5255`.
- Backend full via tracked `Riftbound.slnx`: `7333/7333` under the current no-DB environment.
- `git diff --check`: passed before docs sync.
- `git diff b6519bdf..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

## Remaining Open

- Broader P0/P1 closure.
- Command/recovery/random determinism outside this batch.
- Remaining recovered/spectator/authoritative nested payload breadth.
- Full LayerEngine breadth.
- Real DB-backed Postgres smoke, because no `ConnectionStrings__Riftbound` is available in this environment.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness status.
