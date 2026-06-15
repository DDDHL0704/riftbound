# Local 2P Rule Audit - 2026-06-15

Status: NOT READY. Independent RULE_AUDIT / local two-player correctness record only.

Scope:

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-rule-audit-local2p-20260615`
- Branch: `codex/rule-audit-local2p-worktree-20260615`
- Base: local `main` at `d5d776e7` after merging `codex/local-2p-smoke-20260612`
- Non-scope: Stage 4D triggerQueue closure slices, shared coordination board, completion audit, and closure plan docs.
- Server remains the only rule authority; frontend must submit intents and render server state/prompt constraints.

Rule source baseline:

- Official Rules Hub: `https://riftbound.leagueoflegends.com/en-us/rules-hub/`
- Current Core Rules document is linked there and marked last updated `3/30/26`.
- Local smoke evidence:
  - `docs/LOCAL_2P_CHROME_CLICK_SMOKE_2026-06-12.md`
  - `docs/LOCAL_2P_REGULAR_LEGEND_BATCH_SMOKE_2026-06-12.md`

## Gap L2P-RG-001 - Standard MOVE_UNIT Exhaustion

- Rule source: current Core Rules via the official Rules Hub; standard move exhausts the moving unit as the action cost. Card/effect movement is separate and must not be conflated with standard move.
- Frontend reproduction: in a local two-player Chrome room, P1 played a unit to base, selected it on the match page, chose an opponent battlefield, and clicked `确认移动`.
- Expected: accepted standard move changes the unit location and leaves the moved unit exhausted. An already exhausted unit must not be offered as a standard-move source and must be rejected if submitted directly.
- Actual before local-2p merge: `UNIT_MOVED_TO_BATTLEFIELD` was logged, but the moved unit remained `正常`; the main-action prompt still exposed `MOVE_UNIT` for a unit that should have been exhausted.
- Blocking level: P1. This does not prevent room startup, but it breaks repeated local two-player turns by allowing illegal extra movement and misleading the visible board state.
- Minimal backend tests:
  - `BoardTaskQueueFoundationTests.BaseToBattlefieldMoveIntoEmptyBattlefieldKeepsTaskQueueIdle` now asserts the moved source is exhausted.
  - `BoardTaskQueueFoundationTests.ExhaustedMoveUnitSourceIsRejectedAndHiddenFromMainActionPrompt` covers prompt filtering plus direct command rejection for an exhausted source.
- Backend status: fixed by the local-2p merge and covered by this branch's focused conformance test.
- Frontend fix needed: no separate frontend fix currently required. The frontend already renders server `isExhausted` and uses server prompt candidates; it benefits from the server prompt filter.
- Chrome/2P smoke status: covered by `LOCAL_2P_CHROME_CLICK_SMOKE_2026-06-12.md`, including actual clicks for move and post-fix board state.

## Gap L2P-RG-002 - HASTE_READY Unpaid Entry State

- Rule source: current Core Rules via the official Rules Hub plus card text modeled by `HASTE_READY`: optional extra payment allows a Haste unit to enter active/ready; without that optional payment, active entry should not be assumed.
- Frontend reproduction: local two-player smoke with `OGN·010/298 军团后卫` showed the unit entering base as `正常` when played without paying `HASTE_READY`.
- Expected: units whose active entry depends on the Haste optional ready cost should enter exhausted unless the server validates and records the `HASTE_READY` optional payment branch.
- Actual current status: multiple no-optional Haste fixtures still expect `isExhausted: false`, so the exact no-optional entry rule is not closed across the card set.
- Blocking level: P1 open. It affects visible board legality and follow-up actions, but it is broader than a single local smoke route because it touches Haste entry modeling, fixture expectations, optional-cost exactness, and existing card matrix breadth.
- Minimal backend test needed next:
  - Add a representative no-optional Haste entry conformance test that expects exhausted entry for a current local-smoke card or a narrow Haste representative.
  - Keep the paid `HASTE_READY` branch asserting active entry.
- Backend status: open; not fixed in this turn.
- Frontend fix needed: likely no frontend logic fix if the server exposes correct `isExhausted` and prompt legality. Frontend may only need smoke coverage after the server rule is corrected.
- Chrome/2P smoke status: needs rerun after a backend fix for the selected representative.

## Current Priority

1. Keep `L2P-RG-001` locked with backend conformance coverage.
2. Next P1 candidate is `L2P-RG-002`: first add a failing backend test on latest main, then implement the smallest server-side Haste entry correction that does not broaden unrelated card matrix claims.
3. Project remains NOT READY. Full official card matrix, broader Haste optional-cost exactness, DB-backed persistence smoke, formal E2E, and final readiness remain open.
