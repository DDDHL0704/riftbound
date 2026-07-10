# Riftbound Godot Client Structure

Godogen checkpoint for the existing Godot client. Keep this file aligned when
scene ownership, script responsibility, verification commands, or evidence
contracts change.

## Project

- `project.godot` sets `res://scenes/Main.tscn` as the main scene, enables C#,
  uses a 1440x900 reference viewport with responsive expansion, uses the
  `gl_compatibility` renderer, and loads the Godot MCP editor plugin.
- `scenes/Main.tscn` is a single `Control` shell that switches between the
  focused `LobbyScreen` and full-viewport `MatchScreen`, then mounts centered
  card-inspect, result, mulligan, trigger-order, and damage-assignment overlays
  above them. The only non-product child is the hidden log sink; no legacy
  battle renderer, prompt form, preview rail, or right result rail is mounted.
- `Riftbound.GodotClient.csproj` references `Riftbound.Contracts`; gameplay
  state and prompt semantics stay server-owned.

## Approved Target Architecture

The current runtime structure below remains authoritative until each migration
step lands. The approved replacement is documented in
`docs/2026-07-10-minimal-official-card-client-design.md`:

- an app shell owns connection/session lifecycle and switches between lobby,
  match, and result presentation;
- focused lobby, match, card-inspect, action-bar, and prompt-overlay scenes own
  their UI instead of `Main.cs` constructing the whole product;
- official catalog card fronts render at their native aspect ratio with only
  external focus/selection/target state;
- enabled server prompt sources and choices drive direct card/zone selection;
  local selection never decides legality;
- the former wire table, custom card frames, permanent right prompt rail, and
  raw technical summaries have been removed after prompt parity was proven.

The target supports 1440x900 and 1920x1080, with 1280x720 as the minimum
supported viewport. New layout code must not preserve the current fixed 820 px
wire-table height.

## Runtime Scripts

- `Main.cs` is the application coordinator: argument parsing, session identity,
  API and hub clients, lobby screen coordination, prompt rendering and
  submission, smoke helpers, snapshot rendering, result panel, screenshot
  capture, and theme setup. It forwards safe setup data and availability to
  `LobbyScreen`, while transport and server-authoritative commands remain here.
  It shows the lobby during the `ROOM` state, then hides it for non-room battle
  snapshots so the tabletop owns the combat viewport without blocking deck
  selection. Lobby deck submission and ready affordances mirror enabled
  `SUBMIT_DECK` and `READY` candidates from the current server prompt. It routes
  visible card activation into `CardInspectOverlay` and maps authoritative
  results into viewer-safe fields for `ResultOverlay`. Result mode latches match visibility so
  stale room snapshots cannot restore lobby controls before final screenshots
  are captured, and delayed result capture waits for the render server before
  reading the viewport texture.
- `RiftboundGameHubClient.cs` wraps SignalR hub calls and server push events.
- `RiftboundApiClient.cs` loads HTTP data such as preconstructed decks.
- `PlayerSessionSettings.cs` handles persistent or isolated session identity.
  `--riftbound-ephemeral-session` is required for same-machine multi-window
  playtests.
- `SpecialPromptCommandBuilder.cs` builds payloads only from server prompt
  metadata for prompt families such as trigger ordering and damage assignment.
  Its focused-overlay adapters retain every server trigger ID and validate
  selectable damage source/target pairs before the existing submission path
  serializes them.
- `scripts/interaction/PromptChoice.cs`, `PromptSelectionState.cs`, and
  `PromptInteractionController.cs` own local prompt selection state. They retain
  the exact current prompt ID/tick internally, accept only current server choice
  IDs or server-provided object aliases, reject disabled/special candidates, and
  clear or revalidate selection when prompt identity/candidates change. They do
  not inspect table placement, snapshots, hidden-card fields, or engine rules.

## Visual Layer

- `scenes/overlays/MulliganOverlay.tscn`, `TriggerOrderOverlay.tscn`, and
  `DamageAssignmentOverlay.tscn` are root-level focused controls. They never
  display prompt/tick/object identifiers and have no `OptionButton` controls.
  Mulligan waits for a non-empty visible self-hand snapshot, then receives only
  cards whose object IDs match the current action's source choices. This avoids
  a prompt-before-snapshot flash while retaining an explicit mismatch diagnostic.
  Trigger rows retain IDs internally
  while presenting server labels and keyboard/button ordering. Damage rows retain
  IDs internally while presenting only server participant metadata, remaining
  damage, and server-provided assignment choices.
- `scripts/ui/AppScreen.cs` provides the shared visibility boundary for focused
  runtime screens.
- `scenes/screens/LobbyScreen.tscn` and `scripts/ui/LobbyScreen.cs` own room
  entry, matchmaking, deck choice, submit, and ready presentation. The screen
  emits intents to `Main.cs`; it does not own HTTP, SignalR, player keys,
  reconnect tokens, host/player identifiers, or diagnostics.
  `check-minimal-lobby-scene.sh` rejects table, prompt, and raw-log nodes in this
  screen and guards the shell visibility plus prompt-driven setup contract.
- `scenes/screens/MatchScreen.tscn`, `scripts/ui/MatchScreen.cs`, and
  `scripts/ui/MatchTableRenderer.cs` own the responsive opponent/two-lane/self/
  hand/action layout. The renderer consumes only the existing safe `wireTable`
  dictionary, instantiates `OfficialCardView` for faces and backs, omits raw
  player identifiers, and indexes only visible object IDs for future prompt
  state. `check-minimal-match-scene.sh` rejects fixed wire geometry, permanent
  right rails, debug identifiers, and hidden-card identity copies.
- `scenes/components/ActionBar.tscn` and `scripts/ui/ActionBar.cs` occupy the
  stable bottom host in `MatchScreen`. They show localized enabled actions,
  current step guidance, safe server choice labels, selection count, cancel,
  and confirm controls. Prompt IDs, ticks, object IDs, raw action enums, and
  command payload construction never enter this view.
- `scenes/overlays/CardInspectOverlay.tscn` and
  `scripts/ui/CardInspectOverlay.cs` own full-card inspection. The overlay
  rejects hidden/face-down dictionaries before calling `OfficialCardView`,
  closes with Escape or its explicit button, and restores prior keyboard focus.
- `scenes/overlays/ResultOverlay.tscn` and `scripts/ui/ResultOverlay.cs` own the
  centered authoritative match result and return-to-lobby action. They consume
  only localized outcome, viewer-relative winner, score, and reason fields; raw
  player IDs, ticks, prompt metadata, and transport fields never enter the
  overlay.
- `scripts/ui/MinimalTheme.cs` owns the replacement graphite palette, readable
  text, compact surface styles, button states, and semantic selection colors.
- `scenes/components/OfficialCardView.tscn` and
  `scripts/ui/OfficialCardView.cs` render cached official card fronts at their
  full aspect ratio. The component accepts only server-visible card
  dictionaries, refuses `imagePath` when a card is hidden or face-down, and
  places selection/target state outside the official art. Official battlefield
  images are rotated counterclockwise at runtime only when the safe card view
  carries `rotated=true`, keeping their full landscape face readable.
- `scenes/debug/OfficialCardViewProof.tscn` is a focused visible test harness.
  With `--riftbound-proof-capture=<res://path>` it captures an exact external
  viewport after layout settles; it does not participate in the product flow.
- `scenes/debug/FocusedPromptOverlayProof.tscn` and
  `scripts/debug/FocusedPromptOverlayProof.cs` are an isolated non-headless
  rendering harness for trigger-order and damage-assignment overlays. Fixture
  dictionaries mirror server conformance metadata and pass through the real
  `SpecialPromptCommandBuilder`; neither file is referenced by `Main.tscn`.
- `scripts/ui/CardTextureLoader.cs` is the only disk-image decoder used by
  `OfficialCardView`. It supports PNG, JPEG, WebP, and the server-supplied
  counterclockwise battlefield orientation without any legacy frame code.
- `project.godot`, `Main.HandleKeyboardAction`, `LobbyScreen`, and `ActionBar`
  define deterministic keyboard behavior. The window minimum is 1280x720;
  inspect/cancel/confirm/previous/next actions route to only the visible top
  overlay or current server prompt controls.
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
- `check-prompt-interaction-controller.sh` guards prompt identity reset,
  server-choice membership, disabled-candidate rejection, interaction-layer
  independence from rules/table/hidden state, ActionBar mounting, safe display
  text, and reuse of the existing submission path.
- `check-auto-smoke-prompt-serialization.sh` keeps diagnostic prompt submission
  on a latest-only serialized queue. It prevents SignalR callbacks, prompt UI,
  and asynchronous snapshot rendering from submitting the same or an older
  server prompt concurrently.
- `run-local-human-playtest*.sh` starts local visible two-window playtests.
- `run-local-simulated-playtest-stack.sh` and
  `run-clean-main-simulated-playtest-stack.sh` run visible automated preflights.
  They are regression evidence, not final P5 evidence. The clean-main simulated
  wrapper also checks official-card table content and the centered result
  overlay on both result screenshots by default.
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
  refreshes local `origin/main`, summarizes the running clean-main revision, and
  warns when it is stale versus the refreshed reference. Offline operators can
  set `RIFTBOUND_P5_STATUS_FETCH=0` to skip the refresh. It then summarizes the
  current evidence directory by player: latest prompt actions, setup receipts,
  hidden-information boundary line, and initial/result screenshot presence.
- `check-clean-main-human-session-launcher.sh` statically guards the detached
  launcher so it cannot become an automated smoke or no-wait path.
- `check-human-playtest-evidence.sh` validates raw logs/screenshots,
  preconstructed deck load, accepted `SubmitDeck`/`Ready` receipts, hidden
  information boundary log lines, official-card table content, the centered
  result overlay, and writes `playtest-report.md`.
- `check-official-card-table-screenshot.sh` rejects blank/default-control result
  screenshots and requires dark table coverage plus meaningful card color and
  visual detail.
- `check-centered-result-overlay-screenshot.sh` requires a neutral authoritative
  result panel centered over the still-visible match table.
- `check-battle-layout-scene-integrity.sh` statically checks that the result
  runtime contains only the responsive minimal `MatchScreen` and that the old
  renderer/theme classes and debug rails remain removed. The historical
  filename is retained as a compatibility entry point.
- `check-result-rail-visibility-integrity.sh` statically checks that entering
  result mode shows the centered authoritative overlay while keeping the match
  screen rendered. The historical filename is retained for compatibility.
- `package-human-playtest-evidence.sh` creates the handoff tarball and includes
  `match-sequence-NN.png` plus `match-sequence.mp4` when those final-run media
  files are present in the evidence directory.
- `verify-human-playtest-package.sh` verifies the final package, including clean
  git markers, checksums, screenshot validity, manual confirmations, room/player
  identity consistency, `OPERATOR_GUIDE.md`, `P5_HANDOFF.md`,
  `VISUAL_REVIEW.md`, and absence of auto-smoke markers.
  It also requires both client logs and the report to include the machine hidden
  information boundary check: zero opponent hand faces, zero opponent standby
  faces, and zero hidden identity leaks, and requires the package README plus
  generated handoff and visual review files to repeat that report conclusion. It also requires the report,
  package README, handoff, and visual review files to include the passed
  official-card table and centered-result machine checks, then re-runs both
  screenshot guards on the packaged result screenshots. `OPERATOR_GUIDE.md` must also keep
  redacted Player A/B key fingerprints plus non-empty evidence-package and
  playtest-report path fields, and those fields must point to a `.tar.gz`
  evidence package and `playtest-report.md`, so a reviewer can recover the
  handoff files without terminal scrollback or placeholder values. When launched
  through the detached P5 launcher, the runtime guide also records the screen
  session, attach command, status command, status file, and screen log. The
  verifier rejects missing key fingerprints and any full `pk_...` player-key
  token leaked anywhere in the packaged text evidence files.

## Standard Gates

- Build: `~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj`
- Focused lobby scene: `clients/godot/tools/check-minimal-lobby-scene.sh`
- Focused overlays: `clients/godot/tools/check-minimal-overlays.sh`
- Simulated prompt serialization:
  `clients/godot/tools/check-auto-smoke-prompt-serialization.sh`
- Script safety: `clients/godot/tools/check-human-playtest-script-safety.sh`
- Evidence checker tests:
  `clients/godot/tools/check-human-playtest-evidence-integrity.sh`
- Package verifier tests:
  `clients/godot/tools/check-human-playtest-package-integrity.sh`
- Minimal screenshot evidence tests:
  `clients/godot/tools/check-minimal-screenshot-evidence-integrity.sh`
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

## Completion Evidence

- `screenshots/units/m8-clean-fullflow-evidence.md` is the authoritative index
  for the clean pushed-main Codex dual-client simulation.
- `screenshots/units/m8-clean-fullflow-1440x900-player-{a,b}.png` are the two
  viewer-relative authoritative result captures.
- `screenshots/units/m8-minimal-client-flow-16s.mp4` is the 1440x900 visual flow
  review from lobby through focused interaction to the final result.
- These artifacts prove the user-authorized simulated scope. They do not replace
  or claim the optional two-human P5 certification produced by
  `run-clean-main-human-playtest-stack.sh`.
