# Riftbound Godot Development Memory

This file records durable context for the Godot client work so long runs can
resume from repository state instead of conversation history alone.

## Current Shape

- The Godot client remains server-authoritative: it renders server snapshots and
  prompts, then submits server-provided commands or prompt templates. It must not
  infer legality, hidden identities, combat results, or win conditions locally.
- `scripts/Main.cs` owns session setup, hub connection, lobby controls, prompt
  rendering/submission, smoke helpers, result rendering, and viewport screenshot
  capture.
- `scripts/CardControlRenderer.cs` owns the visible table/card presentation:
  hand rows, opponent card backs, rune tracks, base/signature/standby/battlefield
  zones, card hover/click feedback, and prompt-source highlights.
- `scripts/RunestoneTheme.cs` and `scripts/RunestoneBackdrop.cs` implement the
  selected style route C: black-white inksteel tabletop with restrained crimson
  and antique-gold accents.
- Latest P3 correction after visual review: the combat viewport now preserves
  route C by using muted black/ivory linework and translucent ink-wash zones.
  Lobby/session/deck rows remain visible in `ROOM` so humans can choose decks,
  then collapse for non-room battle snapshots. The result panel is a root-level
  overlay in the right rail between the official-card preview and prompt panel,
  so it no longer pushes, compresses, or covers the tabletop in final
  screenshots. Match-result mode now preserves the right preview-result-prompt
  rail instead of blanking it; the official preview is clipped and compact, and
  table card faces use compact sizes/no effect text so all five wire-table bands
  remain visible at 1440x900.
- Official card fronts are loaded at runtime from catalog `frontImage` URLs via
  `OfficialCardImageLoader` into `user://official-card-cache`; they are not
  committed to git.

## Verification Pattern

- Build gate: `~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj`.
- Visual preflight: `clients/godot/tools/run-clean-main-simulated-playtest-stack.sh`
  opens two visible Godot windows from clean `origin/main`, but it uses
  auto-smoke and is not final P5 evidence. It runs the inksteel screenshot
  style guard on both result screenshots by default after the simulated stack
  finishes.
- `clients/godot/tools/check-inksteel-screenshot-style.sh` is a lightweight
  screenshot palette guard for the selected black/ivory inksteel route. It
  samples result screenshots and rejects obvious bright-gray UI, orange/gold
  dominance, or dropped neutral linework. Use it after visible preflights; it
  does not replace human visual review.
- `clients/godot/tools/check-battle-layout-scene-integrity.sh` is a static scene
  guard for the selected black/white line layout. It rejects a result panel that
  drifts back onto the main battle table, an unclipped/oversized right preview,
  a 1280px hard-width wire table, or compact table cards that stretch the five
  table bands.
- `clients/godot/tools/check-result-rail-visibility-integrity.sh` is a static
  behavior guard that keeps match-result mode from blanking the right-side card
  preview or prompt panel while showing the result panel.
- Final P5 path: `clients/godot/tools/run-clean-main-human-playtest-stack.sh`
  must be run from a clean pushed `main`, with two human operators, manual
  confirmations, final result screenshots, evidence packaging, and package
  verification.
- The final P5 wrapper supports `--precheck`; it validates final evidence gates
  and fetches `origin/main` without creating a worktree, opening Godot windows,
  or writing evidence. It also checks the configured Godot binary and local
  auto-start .NET binary, plus the evidence directory and package output
  parents. It now also catches invalid or non-empty custom clean-worktree paths
  before operators spend time on the final run. On the default local server, it
  refuses an existing healthy API on port 5088 so the final clean worktree starts
  and owns the backend used for evidence. Its success output includes the
  intended player handles and redacted key fingerprints for identity review.
  Run it immediately before the two-human session.
- The final P5 wrapper writes `${RIFTBOUND_SCREENSHOT_DIR}/OPERATOR_GUIDE.md`
  before launching Godot windows. This file mirrors the run parameters and final
  P5 operator checklist so a human handoff can recover the room, player handles,
  redacted player key fingerprints, evidence directory, package path, and
  hidden-information confirmation steps even if the terminal scrollback is lost.
- P4 local deployment check has been validated from clean
  `origin/main@d73546b396a70e49f8b978e7f549c3618a12461c`: Docker image
  `riftbound-api:p4-docker-192252` built, Production memory-mode container
  returned `/health` OK, `/metrics` OK, Dev UI root HTML OK, and Docker
  `HEALTHCHECK` reached `healthy`.
- The evidence checker now records the room id and both player handles from the
  Godot logs; final packages must prove the report and logs agree and that the
  two player identities are distinct. It also rejects raw evidence before manual
  confirmations if either client lacks preconstructed deck loading plus accepted
  `SubmitDeck` and `Ready` receipts.
- The Godot client logs `Hidden info boundary ok` for every rendered table
  snapshot. The evidence checker and final package verifier now require both
  client logs to report `opponentHandFaces=0` and `hiddenCardIdentityLeaks=0`,
  and reject any hidden-boundary `VIOLATION` or nonzero opponent hand face /
  hidden identity leak count. This does not replace the final screenshot human
  confirmation, but it makes hidden-info safety machine-checkable in the P5
  evidence package.
- Evidence packages include `P5_HANDOFF.md`, generated from the report, so a
  reviewer can quickly verify the room, player identities, result screenshots,
  report file, machine hidden-information boundary conclusion, and
  manual-confirmation mode.
- Evidence package `README.md` also repeats the machine hidden-information
  boundary conclusion from the checked report, and the verifier rejects packages
  where that README summary is missing.
- Evidence packages also repeat the checked `Inksteel style: passed` machine
  conclusion in `README.md`, `P5_HANDOFF.md`, and `VISUAL_REVIEW.md`; the
  verifier rejects packages where any of those summaries drift from the report
  or where the packaged result screenshots no longer pass the inksteel style
  guard.
- Evidence packages now include `OPERATOR_GUIDE.md`; the packager copies the
  runtime guide from the evidence directory, or generates a fallback from the
  checked report for manual packaging. The verifier requires this file and its
  checksum coverage so the final tarball keeps the operator-facing room,
  player, redacted key-fingerprint, evidence, and hidden-information checklist
  context. It also requires `Evidence package:` to name a `.tar.gz` package and
  `Playtest report:` to name `playtest-report.md` so the packaged guide remains
  a recoverable handoff index instead of accepting placeholder values. Packages
  missing Player A/B key fingerprints, or leaking a full `pk_...` player-key
  token anywhere in the guide, are rejected.
- Evidence packages include `VISUAL_REVIEW.md`, generated beside the handoff,
  so reviewers have a package-local checklist for result-panel visibility and
  hidden-information inspection of both screenshots. It repeats the machine
  hidden-information boundary conclusion from the checked report.
- Latest clean pushed-main validation after engine-side commit
  `9ee4d80a4`: a visible clean-main simulated two-window run opened Godot from a
  temporary clean `origin/main` worktree, started the local memory-mode API,
  reached both result panels, and wrote 1440x900 result screenshots plus a clean
  machine-check report. The simulated evidence directory was
  `/tmp/riftbound-simulated-playtest-clean-sim-latest-203656`; it is useful
  regression evidence only and remains invalid for final P5 because it contains
  auto-smoke markers.
- Latest local dirty-worktree visual correction check:
  `/tmp/riftbound-simulated-playtest-sim-local-094334` opened two visible Godot
  windows, kept deck/lobby controls visible in `ROOM`, collapsed that chrome in
  result screenshots, rendered the compact overlay result panel, and preserved
  opponent hidden hands as backs/counts. It is regression evidence only because
  it is automated and not captured from clean pushed `main`.

## Open Risks

- P5 is still incomplete until two real humans complete a Godot match to the
  server result panel and produce a verified final evidence package.
- Automated smoke screenshots are useful regression evidence but do not satisfy
  the two-human hidden-information gate.
- Keep changes scoped to `clients/godot/` unless the user explicitly approves a
  backend, contract, DevUi, or deployment change.
