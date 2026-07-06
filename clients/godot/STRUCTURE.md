# Riftbound Godot Client Structure

Godogen checkpoint for the existing Godot client. Keep this file aligned when
scene ownership, script responsibility, verification commands, or evidence
contracts change.

## Project

- `project.godot` sets `res://scenes/Main.tscn` as the main scene, enables C#,
  uses the `gl_compatibility` renderer, and loads the Godot MCP editor plugin.
- `scenes/Main.tscn` is a single `Control` root with lobby/session controls,
  deck controls, a snapshot/table scroll area, a right-rail result panel,
  right-side official card preview, prompt panel, and hidden log panel.
- `Riftbound.GodotClient.csproj` references `Riftbound.Contracts`; gameplay
  state and prompt semantics stay server-owned.

## Runtime Scripts

- `Main.cs` is the application coordinator: argument parsing, session identity,
  API and hub clients, lobby/deck flow, prompt rendering and submission, smoke
  helpers, snapshot rendering, result panel, screenshot capture, and theme setup.
  It keeps lobby/deck chrome visible during the `ROOM` state, then hides it for
  non-room battle snapshots so the tabletop owns the combat viewport without
  blocking deck selection. In match-result mode, the right rail keeps the
  official-card preview, result panel, and prompt panel as three visible bands
  so the black/ivory preview-prompt composition remains intact.
- `RiftboundGameHubClient.cs` wraps SignalR hub calls and server push events.
- `RiftboundApiClient.cs` loads HTTP data such as preconstructed decks.
- `PlayerSessionSettings.cs` handles persistent or isolated session identity.
  `--riftbound-ephemeral-session` is required for same-machine multi-window
  playtests.
- `SpecialPromptCommandBuilder.cs` builds payloads only from server prompt
  metadata for prompt families such as trigger ordering and damage assignment.

## Visual Layer

- `RunestoneTheme.cs`, `RunestoneBackdrop.cs`, and `RunestoneSurface.cs` define
  the procedural inksteel visual style selected for the client: low-saturation
  black/ivory linework, translucent ink-wash zones, restrained crimson and muted
  antique-gold accents.
- `CardControlRenderer.cs` owns tabletop layout, compact table card frames, card
  backs, visible card faces, rune tracks, zone panels, prompt-source highlights,
  and card hover/click feedback. The wire table is responsive to the main battle
  column and keeps the five black/ivory bands visible at 1440x900.
- `CardViewFactory.cs`, `CardViewData.cs`, `SnapshotCardRef.cs`,
  `OfficialCardCatalogService.cs`, and `OfficialCardImageLoader.cs` map visible
  server card refs to display data and runtime-cached official art.
- Opponent hidden cards must remain redacted: render card backs/counts only, and
  never infer identities client-side.

## Playtest Tools

- `run-local-human-playtest*.sh` starts local visible two-window playtests.
- `run-local-simulated-playtest-stack.sh` and
  `run-clean-main-simulated-playtest-stack.sh` run visible automated preflights.
  They are regression evidence, not final P5 evidence. The clean-main simulated
  wrapper also runs the inksteel screenshot style guard on both result
  screenshots by default.
- `run-clean-main-human-playtest-stack.sh` is the final P5 collection path. It
  requires clean pushed `origin/main`, distinct handles/player keys, manual
  confirmations, evidence packaging, and package verification. Its `--precheck`
  mode validates the final gate settings, configured Godot/.NET executables,
  evidence output parents, custom clean-worktree paths, and fetches
  `origin/main` without launching the Godot windows. It also prints the
  intended player handles plus redacted key fingerprints for pre-run identity
  review. With the default local server it refuses an already-running API on
  port 5088 so the clean worktree owns the server process for final evidence.
  When the real run starts, it writes `OPERATOR_GUIDE.md` into the fresh
  evidence directory before launching Godot so the operators have the room,
  player handles, redacted player key fingerprints, evidence/package paths, and
  final checklist outside terminal scrollback.
- `check-human-playtest-evidence.sh` validates raw logs/screenshots,
  preconstructed deck load, accepted `SubmitDeck`/`Ready` receipts, hidden
  information boundary log lines, the inksteel screenshot style guard, and
  writes `playtest-report.md`.
- `check-inksteel-screenshot-style.sh` samples result screenshot pixels to catch
  obvious drift away from the selected black/ivory inksteel route. It is a visual
  regression guard, not a replacement for human screenshot review.
- `check-battle-layout-scene-integrity.sh` statically checks that the result
  panel stays in the right information rail between a clipped compact official
  preview and the prompt panel, that the wire table has no 1280px hard width,
  and that compact table cards cannot stretch the black/ivory grid.
- `check-result-rail-visibility-integrity.sh` statically checks that entering
  match-result mode shows the result panel without blanking the right-side card
  preview or prompt panel.
- `package-human-playtest-evidence.sh` creates the handoff tarball.
- `verify-human-playtest-package.sh` verifies the final package, including clean
  git markers, checksums, screenshot validity, manual confirmations, room/player
  identity consistency, `OPERATOR_GUIDE.md`, `P5_HANDOFF.md`,
  `VISUAL_REVIEW.md`, and absence of auto-smoke markers.
  It also requires both client logs and the report to include the machine hidden
  information boundary check: zero opponent hand faces and zero hidden identity
  leaks, and requires the package README plus generated handoff and visual
  review files to repeat that report conclusion. It also requires the report,
  package README, handoff, and visual review files to include the passed
  inksteel style machine check, then re-runs the inksteel style guard on the
  packaged result screenshots. `OPERATOR_GUIDE.md` must also keep redacted
  Player A/B key fingerprints plus non-empty evidence-package and
  playtest-report path fields, and those fields must point to a `.tar.gz`
  evidence package and `playtest-report.md`, so a reviewer can recover the
  handoff files without terminal scrollback or placeholder values. The verifier
  rejects missing key fingerprints and full player-key leakage.

## Standard Gates

- Build: `~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj`
- Script safety: `clients/godot/tools/check-human-playtest-script-safety.sh`
- Evidence checker tests:
  `clients/godot/tools/check-human-playtest-evidence-integrity.sh`
- Package verifier tests:
  `clients/godot/tools/check-human-playtest-package-integrity.sh`
- Inksteel screenshot style tests:
  `clients/godot/tools/check-inksteel-screenshot-style-integrity.sh`
- Battle scene layout test:
  `clients/godot/tools/check-battle-layout-scene-integrity.sh`
- Result rail visibility test:
  `clients/godot/tools/check-result-rail-visibility-integrity.sh`
- Clean simulated wrapper tests:
  `clients/godot/tools/check-clean-main-simulated-playtest-script.sh`
- Shell syntax: `find clients/godot/tools -name '*.sh' -print0 | xargs -0 -n1 bash -n`
- Whitespace: `git diff --check -- clients/godot`
