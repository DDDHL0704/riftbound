# M6 Focused Prompt Overlay Visible Evidence

Date: 2026-07-10

## Real two-window mulligan

Two normal, non-headless Godot windows ran at 1440x900 against the local
memory-mode API. Both clients used only the existing setup automation to submit
a preconstructed deck and ready; automatic mulligan was intentionally disabled.

- `m6-mulligan-overlay-real-1440x900.png` shows the actionable player's
  centered mulligan overlay with four official card faces and the server range
  of zero to two selections.
- `m6-mulligan-opponent-hidden-1440x900.png` shows the other player's waiting
  view. The opponent hand is four card backs plus a count, never card faces.
- `m6-mulligan-player-a-visible-run.txt` and
  `m6-mulligan-player-b-visible-run.txt` contain the complete visible-run logs.
  Both finish with `opponentHandFaces=0`, `opponentStandbyFaces=0`, and
  `hiddenCardIdentityLeaks=0`. No overlay error or transient missing-hand
  diagnostic is present.

The prompt can arrive before the opening-hand snapshot. Product code now waits
for a non-empty visible self-hand snapshot before presenting the overlay. Once
that snapshot exists, an ID mismatch still disables the overlay with a contract
diagnostic rather than inferring any card or rule data.

## Trigger-order and damage-assignment rendering

The natural preconstructed opening route does not deterministically reach these
rare prompt families. Their visual proof therefore uses the isolated
`FocusedPromptOverlayProof.tscn` debug scene in normal, non-headless 1440x900
Godot windows. The proof scene is not referenced by `Main.tscn`; it passes
fixture dictionaries through the production `SpecialPromptCommandBuilder` and
the production overlay scripts.

- `m6-trigger-order-fixture-1440x900.png` shows four safe trigger labels,
  constrained move controls, reset, and confirm. Internal trigger IDs are not
  rendered.
- `m6-damage-assignment-fixture-1440x900.png` shows server participant labels,
  source power, remaining damage, lethal thresholds, and integer assignment
  controls. Internal source/target IDs are not rendered.
- `m6-trigger-order-fixture-visible-run.txt` and
  `m6-damage-assignment-fixture-visible-run.txt` record clean Godot startup and
  rendering with no engine errors.

The dictionaries mirror the server metadata asserted by
`ConformanceFixtureShapeTests`, `RealTriggerQueueTests`, and
`BattleDamageAssignmentLifecycleTests`. This is visual/adapter evidence, not a
claim that a scripted full match naturally reached the prompts.

