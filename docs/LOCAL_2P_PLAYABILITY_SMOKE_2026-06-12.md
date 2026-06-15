# Local 2P Playability Smoke - 2026-06-12

Worktree: `/Users/dinghaolin/MyProjects/riftbound-local-2p-smoke`
Branch: `codex/local-2p-smoke-20260612`
Latest recorded base after final rebase: `origin/main` at `44fa81b3` (`checkpoint: stage 4D missing command type journaling`)

Scope: local two-player Web playability smoke only. This did not continue Stage 4D triggerQueue/runtime closure work and did not change shared board, completion audit, or closure plan docs. Project status remains **NOT READY**.

## Startup

- Backend started on `http://127.0.0.1:5088`.
- Frontend started on `http://127.0.0.1:5173/`.
- `GET /health` returned 200 with service `riftbound-dotnet` and .NET `10.0.0`.
- Vite root returned 200.
- Local configured Postgres was not used for this smoke; backend ran with `ConnectionStrings__Riftbound=` and the existing no-persistence stores.

The full attack smoke was executed after rebasing through `f8b0c063`. After the run, this branch was rebased through `831a4a93`, `44afea1d`, `ae92f4fc`, and `44fa81b3`; those later main commits changed `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` and Stage 4D docs only, not `src/` backend/frontend runtime files.

## Decks

Both local players submitted a complete official-style Jhin deck for a controlled mirror smoke:

- Legend: `UNL-181/219`
- Champion: `UNL-022/219`
- Main deck: 40 cards
- Rune deck: 12 cards
- Battlefields: `OGN·293/298`, `OGN·290/298`, `OGN·275/298`

The smoke covered server-side deck submission and official opening rather than seeding a dev scenario.

## Complete Match With Attacks

Replay artifact: `/tmp/riftbound-attack-full-match-smoke.json`

- Room: `attack-full-1781248544054`
- Players: `攻测P1`, `攻测P2`
- Final authoritative state:
  - `roomStatus`: `FINISHED`
  - `winnerPlayerId`: `攻测P1`
  - score: `攻测P1` 8, `攻测P2` 1
  - final tick: 148
  - final turn: 81
  - server errors: none
- Chrome visual check:
  - `http://127.0.0.1:5173/matches/attack-full-1781248544054/result`
  - opened and kept two Chrome tabs, one as `攻测P1` and one as `攻测P2`
  - both result pages showed winner `攻测P1`, score 8:1, and no Chrome console errors

Covered path:

- two SignalR clients joined the same room
- both submitted 40/12/3 decks
- both readied
- official opening ran
- mulligan flow completed
- turn start rune call and draw occurred
- runes were tapped for mana
- units were played to battlefield
- costs were paid
- stack items were added
- both players passed priority
- stack items resolved
- `DECLARE_BATTLE` was accepted 6 times
- combat damage was applied
- units were destroyed
- turn advancement continued until the server declared a winner

Observed event totals are from two connected clients, so broadcast events appear twice. The script-level count is 6 accepted declarations; the raw two-client event stream contains 12 `BATTLE_DECLARED` event copies, 24 `DAMAGE_APPLIED` copies, 20 `UNIT_DESTROYED` copies, and 2 `MATCH_WON` copies.

## Supplemental Score-To-Win Match

Replay artifact: `/tmp/riftbound-full-match-smoke.json`

- Room: `full-match-1781248315196-3`
- Players: `完整P1`, `完整P2`
- Final authoritative state:
  - `roomStatus`: `FINISHED`
  - `winnerPlayerId`: `完整P2`
  - score: `完整P1` 1, `完整P2` 8
  - final tick: 107
  - final turn: 71
  - server errors: none

This supplemental run verified a second from-zero-to-winner path with deck submission, opening, draw, rune tapping, card play, cost payment, priority passing, stack resolution, battlefield entry, turn advancement, scoring events, and winner declaration. It did not include accepted battle declarations, so the attack-capable run above is the primary complete playability evidence.

## P0/P1 Findings

- P0: none found in the local two-player smoke path.
- P1: default documented backend startup is fragile on this machine when the configured Postgres connection is unavailable; the smoke ran successfully only after clearing `ConnectionStrings__Riftbound` and using no-persistence stores.
- P1 watch item: long post-combat games can enter many low-action turns once both players run out of easy playable cards/resources. This did not block completion, but it makes manual playability feel slow without broader card coverage and UX affordances.

## Result

Local 2P playability smoke passed mechanically through backend startup, frontend startup, two local clients, room join, deck submit, ready, official opening, mulligan, draw, rune/mana, card play, cost payment, priority pass, stack resolution, combat declaration, damage, destruction, turn advancement, score/win resolution, and Chrome-visible result pages.

This is not a readiness claim. Broader P0/P1 runtime closure, full official card coverage, formal E2E, DB-backed persistence smoke, Stage 4D closure, `fullOfficial`, and final READY remain open.
