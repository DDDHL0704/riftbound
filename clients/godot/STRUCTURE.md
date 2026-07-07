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
  so the black/ivory preview-prompt composition remains intact. Result mode
  also latches battle chrome visibility so stale room snapshots cannot restore
  lobby controls before final screenshots are captured; result screenshots use a
  dedicated capture path that forces result chrome over several frames before
  reading the viewport texture.
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
  column, folds the legacy text summary and obsolete out-of-table hand rail out
  of battle snapshots, and follows the selected black/ivory reference layout:
  opponent resource rail, opponent play band, centered site divider, self play
  band, and self resource rail. The opponent play band, centered site divider,
  and self play band share one aligned lane shell with fixed left/right home
  columns, so each battlefield stacks as opponent units, site, and self units in
  the same vertical lane. Those home columns are intentionally narrow fixed
  rails with compact two-column home-card clusters, which keeps the central
  battlefield lanes visually dominant instead of expanding into large empty
  side panels. Empty home spacers are invisible, table card frames and empty
  sockets keep fixed dimensions inside HBox containers, lane/site text labels
  are reduced to small glyph markers, and deck/rune/public piles stay as fixed
  card-sized stacks rather than expanding into form-like rail cells. Opponent
  hidden hand information stays as card backs/counts only.
- `CardViewFactory.cs`, `CardViewData.cs`, `SnapshotCardRef.cs`,
  `OfficialCardCatalogService.cs`, and `OfficialCardImageLoader.cs` map visible
  server card refs to display data and runtime-cached official art.
- Opponent hidden cards must remain redacted: render card backs/counts only, and
  never infer identities client-side.

## Playtest Tools

- `start-godot-mcp-primary.sh` starts, stops, restarts, or reports the local
  `godot-mcp-server` primary bridge under a detached `screen` session. Use it
  when Codex MCP proxy tools report `127.0.0.1:6506` connection failures; it
  keeps the Godot editor WebSocket on `127.0.0.1:6505` and the proxy HTTP
  bridge on `127.0.0.1:6506`.
- `check-godot-mcp-primary-script.sh` statically checks that the MCP primary
  helper keeps the screen/port/status contract documented above.
- `run-local-human-playtest*.sh` starts local visible two-window playtests.
- `run-local-simulated-playtest-stack.sh` and
  `run-clean-main-simulated-playtest-stack.sh` run visible automated preflights.
  They are regression evidence, not final P5 evidence. The clean-main simulated
  wrapper also runs the inksteel screenshot style guard and the battle-layout
  screenshot geometry guard on both result screenshots by default.
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
- `start-clean-main-human-playtest-session.sh` is a convenience launcher for
  that same final P5 path. It runs the final wrapper precheck, then starts the
  unmodified clean-main human wrapper in a detached `screen` session while
  preserving manual confirmations, clean-git evidence, packaging, package
  verification, and `RIFTBOUND_WAIT=1`. Operators still need to attach with
  `screen -r ...` after play to answer the manual confirmation prompts; detached
  launch alone is not final P5 evidence. Its `--status` mode auto-discovers the
  latest `riftbound-p5-*` screen session or status file when a room/screen
  override is not supplied, so operators can recover an active detached final
  run without remembering the generated room suffix. The same status output
  summarizes the current evidence directory by player: latest prompt actions,
  setup receipts, hidden-information boundary line, and initial/result
  screenshot presence.
- `check-clean-main-human-session-launcher.sh` statically guards the detached
  launcher so it cannot become an automated smoke or no-wait path.
- `check-human-playtest-evidence.sh` validates raw logs/screenshots,
  preconstructed deck load, accepted `SubmitDeck`/`Ready` receipts, hidden
  information boundary log lines, the inksteel screenshot style guard, the
  battle-layout screenshot geometry guard, and writes `playtest-report.md`.
- `check-inksteel-screenshot-style.sh` samples result screenshot pixels to catch
  obvious drift away from the selected black/ivory inksteel route. It is a visual
  regression guard, not a replacement for human screenshot review.
- `check-battle-layout-screenshot.sh` samples result screenshots for the
  black/ivory wire-table geometry: enough horizontal table bands, a reference
  tabletop bottom with intentional breathing room, and right result-rail
  linework. It catches layout regressions where the palette still passes but the
  tabletop is clipped or no longer matches the selected wire layout.
- `check-battle-layout-scene-integrity.sh` statically checks that the result
  panel stays in the right information rail between a clipped compact official
  preview and the prompt panel, that the wire table has no 1280px hard width,
  that the legacy out-of-table `HandScroll` no longer reserves battle layout
  height, that the table follows the resource/play/site/play/resource order, and
  that compact table cards cannot stretch the black/ivory grid.
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
  inksteel style and battle-layout machine checks, then re-runs both screenshot
  guards on the packaged result screenshots. `OPERATOR_GUIDE.md` must also keep redacted
  Player A/B key fingerprints plus non-empty evidence-package and
  playtest-report path fields, and those fields must point to a `.tar.gz`
  evidence package and `playtest-report.md`, so a reviewer can recover the
  handoff files without terminal scrollback or placeholder values. The verifier
  rejects missing key fingerprints and any full `pk_...` player-key token leaked
  anywhere in the packaged text evidence files.

## Standard Gates

- Build: `~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj`
- Script safety: `clients/godot/tools/check-human-playtest-script-safety.sh`
- Evidence checker tests:
  `clients/godot/tools/check-human-playtest-evidence-integrity.sh`
- Package verifier tests:
  `clients/godot/tools/check-human-playtest-package-integrity.sh`
- Inksteel screenshot style tests:
  `clients/godot/tools/check-inksteel-screenshot-style-integrity.sh`
- Battle layout screenshot tests:
  `clients/godot/tools/check-battle-layout-screenshot-integrity.sh`
- Battle scene layout test:
  `clients/godot/tools/check-battle-layout-scene-integrity.sh`
- Result rail visibility test:
  `clients/godot/tools/check-result-rail-visibility-integrity.sh`
- Godot MCP primary helper test:
  `clients/godot/tools/check-godot-mcp-primary-script.sh`
- Detached final P5 launcher test:
  `clients/godot/tools/check-clean-main-human-session-launcher.sh`
- Clean simulated wrapper tests:
  `clients/godot/tools/check-clean-main-simulated-playtest-script.sh`
- Shell syntax: `find clients/godot/tools -name '*.sh' -print0 | xargs -0 -n1 bash -n`
- Whitespace: `git diff --check -- clients/godot`
