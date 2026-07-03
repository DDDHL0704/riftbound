# Riftbound Godot Client

Godot 4 .NET/C# desktop client for the existing Riftbound server.

The server remains authoritative. This client renders server snapshots and submits
commands; it must not reimplement legality, payment, timing, scoring, hidden
information, or win-condition rules locally.

Reference notes for Godot card-client architecture live in
`docs/CARD_CLIENT_REFERENCE_NOTES.md`.

## MCP Editor Access

The project includes the `Godot MCP` editor plugin under
`addons/godot_mcp/`, enabled in `project.godot`. It connects the running Godot
editor to a local MCP server over `ws://127.0.0.1:6505` so agents can inspect
and edit scenes, scripts, project settings, and editor state through MCP tools.

Local Codex MCP setup lives outside the repo in `~/.codex/config.toml`:

```toml
[mcp_servers.godot]
command = "/opt/homebrew/bin/npx"
args = ["-y", "godot-mcp-server"]
startup_timeout_sec = 120
```

After Codex reloads MCP tools, open the editor with:

```sh
godot-mono --editor --path clients/godot
```

Expected MCP evidence: `get_godot_status` reports
`project_path=/Users/dinghaolin/IdeaProjects/riftbound/clients/godot/`, and
tools such as `read_file`, `read_scene`, `add_node`, and `attach_script`
execute through the Godot editor plugin.

## Local Smoke

Start the API in memory mode from the repository root:

```sh
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5088 \
ConnectionStrings__Riftbound="" \
~/.dotnet/dotnet run --project src/Riftbound.Api
```

Build and run the Godot client:

```sh
~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj
/Applications/Godot_dotnet.app/Contents/MacOS/Godot --path clients/godot
```

Pass `--riftbound-server=http://127.0.0.1:5088` after Godot's `--` separator
to point a headless or editor run at a non-default local API port.

Headless smoke:

```sh
/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 600 -- \
  --riftbound-server=http://127.0.0.1:5088
```

Expected G0 evidence: the log shows `Connected`, `Authenticate`, `Joined`,
`Snapshot`, and `Prompt` messages from `/hubs/game`.

## Visual Smoke

For view/layout work, do not rely on headless smoke alone. Run at least one
visible Godot window against the real memory-mode API and save a screenshot
after the log reports visible cards:

```sh
room="godot-visual-$(date +%H%M%S)"
server="http://127.0.0.1:5088"
shot="/tmp/${room}.png"
/Applications/Godot_dotnet.app/Contents/MacOS/Godot \
  --windowed --always-on-top --resolution 1600x900 --position 30,60 \
  --path clients/godot -- \
  --riftbound-server="${server}" \
  --riftbound-smoke-auto-ready \
  --riftbound-smoke-auto-mulligan \
  --riftbound-smoke-preview-first-card \
  --riftbound-visual-screenshot="${shot}" \
  --riftbound-visual-screenshot-min-table-cards=1 \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-room="${room}" \
  --riftbound-handle="godot-a-${room}" \
  --riftbound-player-key="pk_${room}_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
```

Pair it with a second Godot or DevUi opponent. Evidence should include both the
logs (`Snapshot table rendered: visibleHand=... tableCards=...` and
`Visual screenshot saved: ...`) and the saved Godot viewport screenshot where
cards, prompt UI, and the preview panel are actually visible. A protocol smoke
that passes while the visible window is blank, clipped, or unreadable does not
count as view/layout validation. macOS `screencapture` can omit windows when the
calling terminal lacks Screen Recording permission, so prefer the Godot viewport
PNG for layout evidence and use system screenshots only as an extra check.

Use `--riftbound-ephemeral-session` for same-machine multi-window testing. In
that mode the client starts from an in-memory default session and only applies
the provided `--riftbound-room=`, `--riftbound-handle=`, and
`--riftbound-player-key=` overrides; it does not read or write the shared
`user://session.json` file. For persistent isolated identities, pass
`--riftbound-session-file=/absolute/path/to/session.json` for each instance.

G1/G2 dual-client smoke can be run against the same memory-mode API:

```sh
room="godot-dual-$(date +%H%M%S)"
/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 1800 -- \
  --riftbound-smoke-auto-ready \
  --riftbound-smoke-auto-mulligan \
  --riftbound-smoke-auto-tap-rune \
  --riftbound-smoke-auto-play-card \
  --riftbound-smoke-auto-followups \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-room="${room}" \
  --riftbound-handle="godot-a-${room}" \
  --riftbound-player-key="pk_${room}_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" \
  > /tmp/riftbound-godot-a.log 2>&1 &
pid_a=$!

/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 1800 -- \
  --riftbound-smoke-auto-ready \
  --riftbound-smoke-auto-mulligan \
  --riftbound-smoke-auto-tap-rune \
  --riftbound-smoke-auto-play-card \
  --riftbound-smoke-auto-followups \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-room="${room}" \
  --riftbound-handle="godot-b-${room}" \
  --riftbound-player-key="pk_${room}_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" \
  > /tmp/riftbound-godot-b.log 2>&1 &
pid_b=$!

wait "$pid_a" "$pid_b"
cat /tmp/riftbound-godot-a.log /tmp/riftbound-godot-b.log
```

Expected G1/G2 evidence: each client logs accepted `SubmitDeck` and `Ready`
receipts; after both are ready, the server pushes `START`, `Snapshot`, and
`Prompt` messages. With `--riftbound-smoke-auto-mulligan`, each active opening
prompt also confirms a 0-card `MULLIGAN`, proving the Godot client can submit
the server-stamped mulligan command without learning any rules locally. With
`--riftbound-smoke-auto-tap-rune`, the first post-mulligan `TAP_RUNE` candidate
is submitted from the server-provided source choice and server command template.
With `--riftbound-smoke-auto-play-card`, the first post-rune `PLAY_CARD`
candidate is submitted from the server-provided source choice and template,
tapping additional server-provided rune candidates first if no playable source
has appeared yet.
With `--riftbound-smoke-auto-followups`, the smoke runner then tries the first
server-template follow-up choices for movement, battle declaration, pass, and
end-turn actions. It still does not decide legality locally; it only submits the
server-provided required selections.
The same follow-up smoke path also handles server-metadata `ORDER_TRIGGERS` and
`ASSIGN_COMBAT_DAMAGE` prompts when they appear, submitting the default trigger
order or first legal damage assignments exposed by the server.
With `--riftbound-smoke-preview-first-card`, the first server-visible hand card
is sent through the same preview panel used by card clicks. Hidden or face-down
cards still render only the server-redacted placeholder.
With `--riftbound-smoke-auto-surrender`, the client submits `SURRENDER` only
after the current server prompt exposes it as enabled. The result panel then
waits for the authoritative `MATCH_WON` event before displaying the winner.

Result smoke can be layered onto the dual-client script by adding
`--riftbound-smoke-auto-surrender`, usually together with
`--riftbound-smoke-auto-mulligan` so the match reaches an actionable state first.
For deterministic headless regression, pass the surrender flag to both clients:
only the client with a server-enabled `SURRENDER` prompt will submit it. Expected
evidence: one client logs an accepted `SURRENDER` receipt, both clients receive
`MATCH_WON`, and the result panel prints the server winner and reason.

## Manual Two-Player Playtest

Use this for the Playable v1 human check. For a same-machine local test, the
stack helper starts the memory-mode API when needed, opens two visible Godot
clients in the same room, waits for both windows to close, then runs the
machine-readable evidence checker:

```sh
clients/godot/tools/run-local-human-playtest-stack.sh
```

If the API is already running, or if you want to point at a LAN/public server,
use the window-only helper. It starts both windows with isolated ephemeral
sessions and writes logs/screenshots to `/tmp`:

```sh
clients/godot/tools/run-local-human-playtest.sh
```

Useful overrides:

```sh
RIFTBOUND_SERVER=http://127.0.0.1:5088 \
RIFTBOUND_ROOM=human-local-test \
RIFTBOUND_SCREENSHOT_DIR=/tmp/human-local-test \
clients/godot/tools/run-local-human-playtest-stack.sh
```

The stack helper runs Godot `--headless --build-solutions` before opening
windows so a clean worktree can run `Main.tscn` with the generated C# script
metadata. Set `RIFTBOUND_BUILD_GODOT=0` only when reusing an already-built local
client.
Set `RIFTBOUND_EXTRA_GODOT_ARGS="--riftbound-smoke-auto-ready ..."` to append
the same extra Godot user arguments to both launched clients for simulated or
diagnostic runs. Do not use auto-smoke output as final two-human P5 evidence.

For a one-command visible simulated preflight, run:

```sh
clients/godot/tools/run-local-simulated-playtest-stack.sh
```

This opens two visible Godot clients, uses server-exposed auto-smoke actions to
submit preconstructed decks, ready, mulligan, and reach a surrender result, then
runs the machine-readable checker. Its report intentionally contains auto-smoke
notes and unchecked manual confirmations, so it is useful for P2/P3 diagnostics
but not valid final P5 evidence.

To run the same simulated preflight from a temporary clean `origin/main`
worktree, use:

```sh
clients/godot/tools/run-clean-main-simulated-playtest-stack.sh
```

This avoids local dirty files affecting the preflight. It still uses auto-smoke
actions and is not valid final P5 evidence.

For the final P5 evidence run, use a clean pushed `main` worktree so unrelated
local edits cannot pollute the report. This wrapper creates a temporary clean
worktree, prompts for the human-only confirmations, and packages the evidence
immediately after both windows close. It then verifies the final handoff package
before returning success:

```sh
clients/godot/tools/run-clean-main-human-playtest-stack.sh --precheck
```

Run the precheck first while both operators are preparing. It validates the
final P5 gate settings, fetches `origin/main`, and checks the configured
`RIFTBOUND_GODOT_BIN` plus the local auto-start `RIFTBOUND_DOTNET_BIN` when
using the default local API. It also checks that the configured evidence
directory, package output, and custom clean-worktree parents are usable writable
directories, that a custom clean worktree is empty if it already exists, and
that the default local API port is not already serving another process. It does
this without creating a worktree, opening Godot windows, or writing evidence.
After it prints `Final P5 precheck passed`, run the real collection command:

```sh
clients/godot/tools/run-clean-main-human-playtest-stack.sh
```

The wrapper prints the final P5 operator checklist before launching the two
clients and also writes `${RIFTBOUND_SCREENSHOT_DIR}/OPERATOR_GUIDE.md` before
the Godot windows open. Two human players must operate the clients, use
preconstructed decks, play to the server result panel, keep both final
screenshots, and confirm from those screenshots that opponent hands and hidden
cards are shown only as card backs/counts before answering the manual prompts.
If the terminal scrollback is lost during the run, use `OPERATOR_GUIDE.md` to
recover the room, player handles, evidence directory, package path, and final
operator checklist.

Set `RIFTBOUND_EVIDENCE_PACKAGE=/tmp/riftbound-human-playtest.tar.gz` to choose
the output tarball path. If unset, the wrapper writes
`/tmp/riftbound-human-playtest-<room>.tar.gz`, where `<room>` is
`RIFTBOUND_ROOM` or the generated local room id. The package is valid P5
evidence only when the prompts were answered by the two human operators after a
real completed match. The wrapper defaults to `RIFTBOUND_REQUIRE_CLEAN_GIT=1`,
`RIFTBOUND_CONFIRM_MANUAL=1`, `RIFTBOUND_PACKAGE_EVIDENCE=1`, and
`RIFTBOUND_VERIFY_EVIDENCE_PACKAGE=1`. It removes the temporary worktree after
the run unless `RIFTBOUND_KEEP_CLEAN_WORKTREE=1` is set. It refuses to start
with automated or extra Godot client arguments, disabled manual/clean-git/
evidence/package/build/wait gates, skipped `origin/main` fetches, non-`origin/main`
refs, duplicate player handles/keys, non-empty evidence directories, existing
evidence package paths, unusable evidence output parents, unusable or non-empty
custom clean worktree directories, an already-running default local API on port
5088, or custom playtest report paths. This ensures the clean worktree starts
the local API used by the final playtest instead of reusing a stale server. It
prints the two handles and short player-key fingerprints before launching so
operators can catch identity mistakes without exposing full keys. For wrapper
development only, set
`RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE=1` before disabling one of those
gates; that run writes `Incomplete human evidence: 1` into the report and the
final package verifier rejects it.

The wrapper runs this verifier automatically by default; run it manually only
for an existing package:

```sh
clients/godot/tools/verify-human-playtest-package.sh /tmp/riftbound-human-playtest.tar.gz
```

The verifier checks the package structure, valid `SHA256SUMS` coverage for all
required evidence files, absence of undeclared extra files, clean-git report
markers, a report Git revision contained in `origin/main`, manual confirmation
mode, all five human confirmation boxes, both clients loading preconstructed
decks and receiving accepted `SubmitDeck`/`Ready` receipts, match lifecycle
logs, final result screenshot logs, valid PNG result screenshots at least
`800x600`, report/log agreement on the original result screenshot paths,
distinct A/B log and result screenshot files, both client logs reporting
`Hidden info boundary ok` with `opponentHandFaces=0` and
`hiddenCardIdentityLeaks=0`, the `OPERATOR_GUIDE.md` operator checklist, the
`VISUAL_REVIEW.md` screenshot checklist, and absence of crash/rejection/
auto-smoke evidence.

For same-machine testing without the script, keep the identities isolated with
`--riftbound-ephemeral-session` or two different `--riftbound-session-file=`
paths:

```sh
room="human-local-$(date +%H%M%S)"
server="http://127.0.0.1:5088"

/Applications/Godot_dotnet.app/Contents/MacOS/Godot \
  --windowed --resolution 1440x900 --position 20,60 --path clients/godot -- \
  --riftbound-server="${server}" \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-room="${room}" \
  --riftbound-handle="player-a-${room}" \
  --riftbound-player-key="pk_${room}_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
```

Run the second window with the same `room` and `server`, but a distinct handle
and player key:

```sh
/Applications/Godot_dotnet.app/Contents/MacOS/Godot \
  --windowed --resolution 1440x900 --position 220,120 --path clients/godot -- \
  --riftbound-server="${server}" \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-room="${room}" \
  --riftbound-handle="player-b-${room}" \
  --riftbound-player-key="pk_${room}_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
```

For LAN testing, bind the API to the host interface and point both Godot clients
at that host IP:

```sh
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://0.0.0.0:5088 \
ConnectionStrings__Riftbound="" \
~/.dotnet/dotnet run --project src/Riftbound.Api
```

Then set `server="http://<host-lan-ip>:5088"` on both clients. The two players
should choose preconstructed decks, ready, complete mulligan, and play to the
server result panel. Validation evidence should include screenshots from both
players showing the final result, plus a hidden-information check: each player
must see the opponent hand only as card backs/counts, never as front faces or
card identities.

After both windows close, run the evidence checker against the script's output
directory:

```sh
clients/godot/tools/check-human-playtest-evidence.sh /tmp/riftbound-human-playtest-human-local-test
```

The checker validates machine-readable gates: both logs exist, both final result
screenshots are valid PNG files at least `800x600`, the logs include
preconstructed deck loading, accepted `SubmitDeck` and `Ready` receipts,
`MATCH_STARTED`, `MATCH_WON`/result rendering, and `Hidden info boundary ok`
with `opponentHandFaces=0` plus `hiddenCardIdentityLeaks=0`, and no
crash/error/rejection patterns are present. It records the room id and both
player handles from the Godot logs, rejects duplicate player identities, missing
deck/ready setup, hidden-boundary violations, and identical A/B logs or
identical A/B result screenshots before prompting for manual confirmations. It
cannot prove that two humans operated the clients, and the final screenshots
must still be inspected by humans for hidden-information safety. When the
machine checks pass, it writes `playtest-report.md` in the evidence directory
with those identity fields, the hidden-boundary machine-check line, and manual
confirmation boxes. To have the checker prompt for and record those
confirmations, run it with `RIFTBOUND_CONFIRM_MANUAL=1`.

To archive a completed local/LAN playtest manually for the final Playable v1
handoff, package the evidence directory after the checker passes:

```sh
RIFTBOUND_CONFIRM_MANUAL=1 \
clients/godot/tools/package-human-playtest-evidence.sh /tmp/riftbound-human-playtest-human-local-test
```

The package contains both player logs, both result screenshots,
`playtest-report.md`, `OPERATOR_GUIDE.md`, `P5_HANDOFF.md`, `VISUAL_REVIEW.md`,
and `SHA256SUMS`. `OPERATOR_GUIDE.md` is copied from the evidence directory when
present, or generated from the checked report during manual packaging. The
handoff summary is generated from the checked report so reviewers can quickly
see the room, both player handles, and result screenshot filenames.
`VISUAL_REVIEW.md` is a focused checklist for inspecting the final screenshots
for result-panel visibility and hidden-information safety. The package is still
only valid for P5 when the report includes the real two-human manual
confirmations.

For final P5 collection from pushed `main`, use
`clients/godot/tools/run-clean-main-human-playtest-stack.sh`. That wrapper
refuses automated smoke arguments, disabled manual/clean-git/evidence/package/
build/wait gates, non-`origin/main` clean worktrees, and any
`RIFTBOUND_QUIT_AFTER` value or `RIFTBOUND_EXTRA_GODOT_ARGS`. It also refuses a
non-empty `RIFTBOUND_SCREENSHOT_DIR`, so stale logs or screenshots cannot be
mixed into the final package, and it refuses an existing
`RIFTBOUND_EVIDENCE_PACKAGE` path so a previous tarball is never overwritten. It
also refuses custom `RIFTBOUND_PLAYTEST_REPORT` paths; the report must be
generated inside the new evidence directory. It also requires distinct
`RIFTBOUND_HANDLE_A`/`RIFTBOUND_HANDLE_B` and
`RIFTBOUND_PLAYER_KEY_A`/`RIFTBOUND_PLAYER_KEY_B` values. Before launching the
windows it writes `OPERATOR_GUIDE.md` into the fresh evidence directory with the
room, player handles, evidence/package paths, and the final P5 operator
checklist. If the wrapper development escape hatch is used, the report is marked
`Incomplete human evidence: 1` and the final package verifier rejects it. Final
evidence needs two human operators to close the Godot windows after reaching the
server result panel; automatic quit timers and extra client arguments are only
for simulated diagnostics.

Quick-match G2 smoke uses the server-owned matchmaking queue instead of a shared
manual room id:

```sh
stamp="godot-mm-$(date +%H%M%S)"
/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 2200 -- \
  --riftbound-smoke-auto-quick-match \
  --riftbound-smoke-auto-ready \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-handle="godot-mm-a-${stamp}" \
  --riftbound-player-key="pk_${stamp}_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" \
  > /tmp/riftbound-godot-mm-a.log 2>&1 &
pid_a=$!

/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 2200 -- \
  --riftbound-smoke-auto-quick-match \
  --riftbound-smoke-auto-ready \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-handle="godot-mm-b-${stamp}" \
  --riftbound-player-key="pk_${stamp}_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" \
  > /tmp/riftbound-godot-mm-b.log 2>&1 &
pid_b=$!

wait "$pid_a" "$pid_b"
cat /tmp/riftbound-godot-mm-a.log /tmp/riftbound-godot-mm-b.log
```

Expected quick-match evidence: both clients log `Queued`/`Matched`, receive a
server room id from `MatchmakingStatusDto`, join that matched room, and then
submit accepted `SubmitDeck` and `Ready` receipts without computing any
matchmaking or game legality locally.

Public-match listing smoke covers the `/matches` directory and join path:

```sh
stamp="godot-public-list-$(date +%H%M%S)"
/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 2200 -- \
  --riftbound-smoke-auto-public-match \
  --riftbound-smoke-auto-ready \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-handle="godot-host-${stamp}" \
  --riftbound-player-key="pk_${stamp}_hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" \
  > /tmp/riftbound-godot-public-host.log 2>&1 &
pid_host=$!

/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 2200 -- \
  --riftbound-smoke-auto-join-public-match \
  --riftbound-smoke-auto-ready \
  --riftbound-ephemeral-session \
  --riftbound-ignore-reconnect \
  --riftbound-handle="godot-join-${stamp}" \
  --riftbound-player-key="pk_${stamp}_jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" \
  > /tmp/riftbound-godot-public-join.log 2>&1 &
pid_join=$!

wait "$pid_host" "$pid_join"
cat /tmp/riftbound-godot-public-host.log /tmp/riftbound-godot-public-join.log
```

Expected public-list evidence: the host logs `Public match created`, the joiner
logs public-match list loading plus `Public match joined`, and both clients then
submit accepted `SubmitDeck` and `Ready` receipts in the same server room.

## Official Card Images

Card faces prefer official `frontImage` URLs from
`data/official/card-catalog.zh-CN.json`. Images are downloaded at runtime and
cached under `user://official-card-cache`, which maps to the local Godot user
data directory and is never committed.

If a card has no official image or the download fails, the client must degrade to
a text/vector card face instead of blocking the match UI.
