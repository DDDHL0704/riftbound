# Riftbound Godot Client

Godot 4 .NET/C# desktop client for the existing Riftbound server.

The server remains authoritative. This client renders server snapshots and submits
commands; it must not reimplement legality, payment, timing, scoring, hidden
information, or win-condition rules locally.

Reference notes for Godot card-client architecture live in
`docs/CARD_CLIENT_REFERENCE_NOTES.md`.

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

Headless smoke:

```sh
/Applications/Godot_dotnet.app/Contents/MacOS/Godot --headless --path clients/godot --quit-after 600
```

Expected G0 evidence: the log shows `Connected`, `Authenticate`, `Joined`,
`Snapshot`, and `Prompt` messages from `/hubs/game`.

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

## Official Card Images

Card faces prefer official `frontImage` URLs from
`data/official/card-catalog.zh-CN.json`. Images are downloaded at runtime and
cached under `user://official-card-cache`, which maps to the local Godot user
data directory and is never committed.

If a card has no official image or the download fails, the client must degrade to
a text/vector card face instead of blocking the match UI.
