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
- Official card fronts are loaded at runtime from catalog `frontImage` URLs via
  `OfficialCardImageLoader` into `user://official-card-cache`; they are not
  committed to git.

## Verification Pattern

- Build gate: `~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj`.
- Visual preflight: `clients/godot/tools/run-clean-main-simulated-playtest-stack.sh`
  opens two visible Godot windows from clean `origin/main`, but it uses
  auto-smoke and is not final P5 evidence.
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
  and owns the backend used for evidence. Run it immediately before the
  two-human session.
- The final P5 wrapper writes `${RIFTBOUND_SCREENSHOT_DIR}/OPERATOR_GUIDE.md`
  before launching Godot windows. This file mirrors the run parameters and final
  P5 operator checklist so a human handoff can recover the room, player handles,
  evidence directory, package path, and hidden-information confirmation steps
  even if the terminal scrollback is lost.
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
- Evidence packages include `P5_HANDOFF.md`, generated from the report, so a
  reviewer can quickly verify the room, player identities, result screenshots,
  report file, and manual-confirmation mode.
- Evidence packages include `VISUAL_REVIEW.md`, generated beside the handoff,
  so reviewers have a package-local checklist for result-panel visibility and
  hidden-information inspection of both screenshots.
- Latest clean pushed-main validation after engine-side commit
  `bc456c392`: Godot client build passed, final P5 `--precheck` passed, and a
  visible clean-main simulated two-window run reached both result panels. The
  simulated evidence directory was
  `/tmp/riftbound-simulated-playtest-clean-sim-latest-main-200109`; it is
  useful regression evidence only and remains invalid for final P5 because it
  contains auto-smoke markers.

## Open Risks

- P5 is still incomplete until two real humans complete a Godot match to the
  server result panel and produce a verified final evidence package.
- Automated smoke screenshots are useful regression evidence but do not satisfy
  the two-human hidden-information gate.
- Keep changes scoped to `clients/godot/` unless the user explicitly approves a
  backend, contract, DevUi, or deployment change.
