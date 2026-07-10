# M4 Minimal Overlay Visible Runs

These captures came from normal, windowed Godot 4.7 .NET clients at 1440x900
against the local memory-mode API. They are visual regression evidence, not the
final two-human full-match package.

## Card Inspection

- Room: `m4-inspect-180346`
- Captures: `m4-card-inspect-1440x900-player-a.png` and
  `m4-card-inspect-1440x900-player-b.png`
- Both clients used server prompts to submit a preconstructed deck, ready, and
  confirm a zero-card mulligan. The smoke helper then opened the first visible
  card only after a real official-card table snapshot rendered.
- Direct inspection confirmed the official face is uncropped, the safe catalog
  summary is readable, the table remains visible behind the dim layer, and the
  close action has a visible focus state.

## Match Result

- Room: `m4-result-180420`
- Captures: `m4-result-1440x900-player-a.png` and
  `m4-result-1440x900-player-b.png`
- The automated surrender path was gated until each client had rendered a real
  table. The logs show 12/13 table cards and 4/5 official hand fronts before the
  server-enabled `SURRENDER` command was submitted.
- Direct inspection confirmed viewer-relative victory/defeat, winner, winning
  score, reason, and return-to-lobby action over the latched final table.

## Safety And Runtime

- Every final table log reports `opponentHandFaces=0`,
  `opponentStandbyFaces=0`, and `hiddenCardIdentityLeaks=0`.
- Opponent hands show neutral backs and counts only; no hidden card opens the
  inspection overlay.
- Both runs used the OpenGL/Metal windowed renderer. The paired `*-visible-run.txt`
  files contain no Godot warning, error, exception, or object-leak report.
- `check-minimal-overlays.sh`, the adjacent minimal scene checks, and the .NET
  build complete with zero errors.
