# Riftbound Godot Client

Godot 4 .NET/C# desktop client for the existing Riftbound server.

The server remains authoritative. This client renders server snapshots and submits
commands; it must not reimplement legality, payment, timing, scoring, hidden
information, or win-condition rules locally.

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

## Official Card Images

Card faces prefer official `frontImage` URLs from
`data/official/card-catalog.zh-CN.json`. Images are downloaded at runtime and
cached under `user://official-card-cache`, which maps to the local Godot user
data directory and is never committed.

If a card has no official image or the download fails, the client must degrade to
a text/vector card face instead of blocking the match UI.
