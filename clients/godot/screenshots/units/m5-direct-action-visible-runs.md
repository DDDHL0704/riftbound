# M5 Direct Action Visible Runs

These captures and logs came from pairs of normal, non-headless Godot 4.7 .NET
windows at 1440x900 connected to the real memory-mode API. Automation was used
only to reach deterministic server prompts; it selected and submitted through
the same `PromptInteractionController`, `ActionBar`, and existing command methods
as the visible controls. This is regression evidence, not final two-human proof.

## Captures

- `m5-direct-selection-1440x900.png`: the acting player selected a server-listed
  rune. The card has selected state and the bottom bar says only "已选择 1 项";
  no object ID, prompt ID, tick, or generic dropdown is visible.
- `m5-waiting-1440x900.png`: the opponent sees a waiting action bar and only a
  card back/count for the acting player's hand.
- `m5-play-card-submit-1440x900.png`: card play passed through the new selection
  and submission path while official card art and table zones remain readable.

## Server Receipts

- `m5-tap-select-player-*-visible-run.txt` records current-prompt selection and
  hidden-information boundaries for the direct rune state.
- `m5-play-card-player-*-visible-run.txt` records accepted `PLAY_CARD` plus the
  resulting priority prompt.
- `m5-pass-priority-player-*-visible-run.txt` records both clients submitting an
  accepted `PASS_PRIORITY` through the new path.
- `m5-end-turn-player-*-visible-run.txt` records accepted `END_TURN` and turn
  advancement.

All archived runs report `opponentHandFaces=0`, `opponentStandbyFaces=0`, and
`hiddenCardIdentityLeaks=0`. No Godot error, warning, exception, or crash marker
appears in the archived client logs.
