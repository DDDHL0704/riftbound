# M8 Clean Full-Flow Evidence

## Scope

- Revision: `origin/main@1ec37e5e7`
- Room: `m8-clean-fullflow`
- Runtime: clean detached worktree, memory-mode API, two visible Godot 4.7 .NET clients at 1440x900
- Operator mode: Codex dual-client simulation, explicitly authorized by the user
- Human claim: none; this evidence intentionally does not claim two human operators

## Result

- Both clients selected preconstructed decks, readied, completed mulligan, and reached a natural score victory without surrender.
- Player B won with score 8 at server tick 53.
- Both clients rendered 54 table snapshots and the same authoritative result.
- Submitted flow covered 8 rune taps, 2 card plays, 7 moves, 1 battle declaration, 4 priority passes, 16 focus passes, and 10 end-turn commands.
- Both logs contained zero `ERROR` and zero `REJECTED` entries.

## Safety

- Every hidden-boundary line reported `opponentHandFaces=0`.
- Every hidden-boundary line reported `opponentStandbyFaces=0`.
- Every hidden-boundary line reported `hiddenCardIdentityLeaks=0`.
- The final screenshots show opponent hidden hand information only as a card back and count.
- No backend, contract, engine, or DevUi source changed in this Godot completion run.

## Visual Review

- Official card fronts remain the primary card face presentation.
- Opponent/self public zones, both battlefields, hand, score, deck/rune counts, and the contextual action area remain visible behind the result dimmer.
- The authoritative result panel is centered and does not clip the 1440x900 viewport.
- Player A and Player B have distinct final screenshots and viewer-relative win/loss text.
- Machine checks passed: official-card table, centered result overlay, minimum PNG dimensions, log/result lifecycle, and hidden-information boundary.

## Artifacts

- `m8-clean-fullflow-1440x900-player-a.png`
  SHA-256: `42914d7efcf44a5564d403254eba08cff68bfe1608fde840fee2f313f3d181a8`
- `m8-clean-fullflow-1440x900-player-b.png`
  SHA-256: `e5cba7f210d746adc4b50312f310cb1f72abae70db1d0f0b36fb8283b075ac37`
- `m8-minimal-client-flow-16s.mp4`
  1440x900, 12 fps, 202 frames, 16.83 seconds
  SHA-256: `7f7f3078192cb0a7810568b6755977f4d3ee89a1840a057d406c45da7629b6f2`
- Local checksummed package:
  `/private/tmp/riftbound-codex-simulated-evidence-1ec37e5e7.tar.gz`
  SHA-256: `ca03faa090f1f827b1ca215419f7365a88d95f212b747e0bfbcc89054611533b`

The package contains both client logs, API log, both result screenshots, seven
sequence frames, the 16.83-second MP4, report/review files, and SHA-256 coverage
for every file. The human-only package verifier rejects it by design because it
contains simulation markers and unchecked two-human confirmations.
