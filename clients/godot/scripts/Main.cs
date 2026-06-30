using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

namespace Riftbound.GodotClient;

public partial class Main : Control
{
    [Export] public string ServerUrl { get; set; } = "http://127.0.0.1:5088";
    [Export] public bool AutoConnectOnReady { get; set; } = true;
    [Export] public string OfficialCatalogSnapshotPath { get; set; } = "res://../../data/official/card-catalog.zh-CN.json";
    [Export] public string PreviewCardNo { get; set; } = "UNL-181/219";

    private readonly CancellationTokenSource _shutdown = new();
    private readonly PlayerSessionStore _sessionStore = new();
    private readonly List<PreconstructedDeck> _decks = [];
    private readonly OfficialCardImageLoader _cardImageLoader = new();

    private PlayerSessionSettings _session = PlayerSessionSettings.CreateDefault();
    private Label? _status;
    private RichTextLabel? _log;
    private Label? _boardSummary;
    private VBoxContainer? _snapshotRows;
    private HBoxContainer? _handRow;
    private TextureRect? _officialCardPreview;
    private LineEdit? _handleInput;
    private LineEdit? _roomInput;
    private OptionButton? _deckSelect;
    private Button? _connectButton;
    private Button? _reconnectButton;
    private Button? _loadDecksButton;
    private Button? _submitDeckButton;
    private Button? _readyButton;
    private HubConnection? _connection;
    private string _authenticatedHandle = string.Empty;
    private bool _autoSmoke;
    private bool _autoSmokeSubmitted;
    private bool _ephemeralSession;
    private Task? _officialCatalogLoadTask;
    private IReadOnlyDictionary<string, CardCatalogEntry> _officialCatalog =
        new Dictionary<string, CardCatalogEntry>(StringComparer.Ordinal);

    public override async void _Ready()
    {
        BindNodes();
        WireButtons();
        var args = CommandLineArgs();
        _autoSmoke = args.Contains("--riftbound-smoke-auto-ready");
        _ephemeralSession = args.Contains("--riftbound-ephemeral-session");
        AppendLog("Client booted. Waiting for server authority.");

        _session = await _sessionStore.LoadAsync();
        _session = ApplyCommandLineOverrides(_session, args);
        ApplySessionToInputs();
        _officialCatalogLoadTask = LoadOfficialCardPreviewAsync();
        _ = LoadDecksAsync();

        if (AutoConnectOnReady)
        {
            await ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        }
    }

    public override void _ExitTree()
    {
        _shutdown.Cancel();
        _ = DisconnectAsync();
        _shutdown.Dispose();
    }

    private void BindNodes()
    {
        _status = GetNode<Label>("Status");
        _log = GetNode<RichTextLabel>("Log");
        _boardSummary = GetNode<Label>("Controls/BoardSummary");
        _snapshotRows = GetNode<VBoxContainer>("Controls/SnapshotScroll/SnapshotRows");
        _handRow = GetNode<HBoxContainer>("Controls/HandScroll/HandRow");
        _officialCardPreview = GetNode<TextureRect>("OfficialCardPreviewFrame/OfficialCardPreview");
        _handleInput = GetNode<LineEdit>("Controls/SessionRow/HandleInput");
        _roomInput = GetNode<LineEdit>("Controls/SessionRow/RoomInput");
        _deckSelect = GetNode<OptionButton>("Controls/DeckRow/DeckSelect");
        _connectButton = GetNode<Button>("Controls/SessionRow/ConnectButton");
        _reconnectButton = GetNode<Button>("Controls/SessionRow/ReconnectButton");
        _loadDecksButton = GetNode<Button>("Controls/DeckRow/LoadDecksButton");
        _submitDeckButton = GetNode<Button>("Controls/DeckRow/SubmitDeckButton");
        _readyButton = GetNode<Button>("Controls/DeckRow/ReadyButton");
    }

    private void WireButtons()
    {
        _connectButton!.Pressed += () => _ = ConnectAndRequestSnapshotAsync(useReconnectToken: false);
        _reconnectButton!.Pressed += () => _ = ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        _loadDecksButton!.Pressed += () => _ = LoadDecksAsync();
        _submitDeckButton!.Pressed += () => _ = SubmitSelectedDeckAsync();
        _readyButton!.Pressed += () => _ = ReadyAsync();
    }

    private void ApplySessionToInputs()
    {
        if (_handleInput is not null)
        {
            _handleInput.Text = _session.Handle;
        }

        if (_roomInput is not null)
        {
            _roomInput.Text = _session.RoomId;
        }
    }

    private PlayerSessionSettings ReadSessionFromInputs()
    {
        var handle = _handleInput?.Text.Trim() ?? PlayerSessionSettings.DefaultHandle;
        var room = _roomInput?.Text.Trim() ?? PlayerSessionSettings.DefaultRoomId;
        if (string.IsNullOrWhiteSpace(handle))
        {
            handle = PlayerSessionSettings.DefaultHandle;
        }

        if (string.IsNullOrWhiteSpace(room))
        {
            room = PlayerSessionSettings.DefaultRoomId;
        }

        return _session with { Handle = handle, RoomId = room };
    }

    private async Task LoadDecksAsync()
    {
        try
        {
            var decks = await new RiftboundApiClient(ServerUrl).GetPreconstructedDecksAsync(_shutdown.Token);
            _decks.Clear();
            _decks.AddRange(decks);
            QueueMainThread(nameof(ApplyDeckOptions));
            AppendLog($"Preconstructed decks loaded: {_decks.Count}.");

            await RunAutoSmokeSetupIfReadyAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Unable to load preconstructed decks: {Escape(ex.Message)}[/color]");
        }
    }

    private async Task LoadOfficialCardPreviewAsync()
    {
        try
        {
            var catalog = await new OfficialCardCatalogService()
                .LoadSnapshotAsync(OfficialCatalogSnapshotPath, _shutdown.Token);
            _officialCatalog = catalog;
            AppendLog($"Official catalog loaded: {catalog.Count} cards.");

            if (!catalog.TryGetValue(PreviewCardNo, out var card))
            {
                AppendLog($"[color=yellow]Official preview card not found: {Escape(PreviewCardNo)}[/color]");
                return;
            }

            var image = await _cardImageLoader.LoadOfficialFrontImageAsync(card, _shutdown.Token);
            if (image is null)
            {
                AppendLog($"[color=yellow]No official front image for {Escape(card.CardNo)} {Escape(card.CardName)}[/color]");
                return;
            }

            QueueMainThread(nameof(ApplyOfficialCardPreview), image);
            AppendLog($"Official card image loaded: {Escape(card.CardNo)} {Escape(card.CardName)}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Official image preview unavailable: {Escape(ex.Message)}[/color]");
        }
    }

    private async Task ConnectAndRequestSnapshotAsync(bool useReconnectToken)
    {
        try
        {
            _session = PlayerSessionSettings.WithUsableKey(ReadSessionFromInputs());
            await SaveSessionAsync();

            SetStatus("Connecting");
            if (_connection is null || _connection.State == HubConnectionState.Disconnected)
            {
                _connection = BuildConnection();
                RegisterServerHandlers(_connection);
                await _connection.StartAsync(_shutdown.Token);
            }

            SetStatus("Connected");
            AppendLog($"Connected to {ServerUrl}/hubs/game.");

            var auth = await _connection.InvokeAsync<AuthResultDto>(
                "Authenticate",
                _session.Handle,
                _session.PlayerKey,
                _shutdown.Token);
            AppendLog($"Authenticate: {auth.Status} ({auth.Handle}).");
            if (!auth.Authenticated)
            {
                SetStatus($"Authentication rejected: {auth.Status}");
                return;
            }

            _authenticatedHandle = auth.Handle;
            var shouldReconnect = useReconnectToken && !string.IsNullOrWhiteSpace(_session.ReconnectToken);
            if (shouldReconnect)
            {
                await _connection.InvokeAsync(
                    "Reconnect",
                    _session.RoomId,
                    _authenticatedHandle,
                    _session.ReconnectToken,
                    _shutdown.Token);
                AppendLog($"Reconnect requested: room={_session.RoomId}, player={_authenticatedHandle}.");
            }
            else
            {
                await _connection.InvokeAsync(
                    "JoinRoom",
                    _session.RoomId,
                    _authenticatedHandle,
                    null,
                    _shutdown.Token);
                AppendLog($"JoinRoom requested: room={_session.RoomId}, player={_authenticatedHandle}.");
            }

            await _connection.InvokeAsync("RequestSnapshot", _session.RoomId, _authenticatedHandle, _shutdown.Token);
            AppendLog("RequestSnapshot submitted.");

            await RunAutoSmokeSetupIfReadyAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus("Connection error");
            AppendLog($"[color=red]{Escape(ex.GetType().Name)}: {Escape(ex.Message)}[/color]");
            GD.PushError(ex.ToString());
        }
    }

    private async Task SubmitSelectedDeckAsync()
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Submit deck skipped: not connected/authenticated.[/color]");
            return;
        }

        var deck = SelectedDeck();
        if (deck is null)
        {
            AppendLog("[color=yellow]Submit deck skipped: no preconstructed deck selected.[/color]");
            return;
        }

        var command = new SubmitDeckCommand(
            deck.LegendCardNo,
            deck.ChampionCardNo,
            deck.MainDeck,
            deck.RuneDeck,
            deck.Battlefields);
        var receipt = await _connection!.InvokeAsync<CommandReceiptDto>(
            "SubmitIntent",
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId("submit-deck"),
            command,
            _shutdown.Token);
        AppendReceipt("SubmitDeck", receipt);

        if (receipt.Accepted)
        {
            _session = _session with { LastDeckId = deck.Id };
            await SaveSessionAsync();
        }
    }

    private async Task RunAutoSmokeSetupIfReadyAsync()
    {
        if (!_autoSmoke || _autoSmokeSubmitted || _decks.Count == 0 || !IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            return;
        }

        _autoSmokeSubmitted = true;
        AppendLog("Auto smoke: submitting first preconstructed deck and readying.");
        await SubmitSelectedDeckAsync();
        await ReadyAsync();
    }

    private async Task ReadyAsync()
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Ready skipped: not connected/authenticated.[/color]");
            return;
        }

        var receipt = await _connection!.InvokeAsync<CommandReceiptDto>(
            "Ready",
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId("ready"),
            _shutdown.Token);
        AppendReceipt("Ready", receipt);
    }

    private PreconstructedDeck? SelectedDeck()
    {
        if (_decks.Count == 0)
        {
            return null;
        }

        var selected = _deckSelect is null ? 0 : Math.Max(0, _deckSelect.Selected);
        return selected < _decks.Count ? _decks[selected] : _decks[0];
    }

    private HubConnection BuildConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl($"{ServerUrl.TrimEnd('/')}/hubs/game")
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();
    }

    private void RegisterServerHandlers(HubConnection connection)
    {
        connection.Reconnecting += error =>
        {
            SetStatus("Reconnecting");
            AppendLog($"Reconnecting: {error?.Message ?? "unknown reason"}.");
            return Task.CompletedTask;
        };
        connection.Reconnected += connectionId =>
        {
            SetStatus("Connected");
            AppendLog($"Reconnected: {connectionId ?? "no connection id"}.");
            return Task.CompletedTask;
        };
        connection.Closed += error =>
        {
            SetStatus("Disconnected");
            AppendLog($"Closed: {error?.Message ?? "normal close"}.");
            return Task.CompletedTask;
        };

        connection.On<WsServerMessage>("Joined", message => LogMessage("Joined", message));
        connection.On<WsServerMessage>("Snapshot", message => LogMessage("Snapshot", message));
        connection.On<WsServerMessage>("Prompt", message => LogMessage("Prompt", message));
        connection.On<WsServerMessage>("Events", message => LogMessage("Events", message));
        connection.On<WsServerMessage>("Error", message => LogMessage("Error", message));
        connection.On<WsServerMessage>("Matchmaking", message => LogMessage("Matchmaking", message));
    }

    private void LogMessage(string channel, WsServerMessage message)
    {
        if (channel == "Joined")
        {
            UpdateJoinedSession(message);
        }
        else if (channel == "Error")
        {
            HandleServerError(message);
        }
        else if (channel == "Snapshot")
        {
            _ = RenderSnapshotAsync(message);
        }

        AppendLog(
            $"[b]{Escape(channel)}[/b] type={message.Type} room={Escape(message.RoomId)} player={Escape(message.PlayerId)} tick={message.ServerTick} payload={PayloadSummary(message.Payload)}");
    }

    private async Task RenderSnapshotAsync(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        try
        {
            var table = element.TryGetProperty("table", out var tableElement) && tableElement.ValueKind == JsonValueKind.Object
                ? tableElement
                : default;
            var summary = BuildSnapshotSummary(element, table);
            var handCards = VisibleHandCards(element, table);
            if (_officialCatalogLoadTask is { IsCompleted: false } catalogLoadTask)
            {
                await catalogLoadTask;
            }

            var views = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            var officialImageCount = 0;
            foreach (var handCard in handCards.Take(12))
            {
                var view = await BuildCardViewAsync(handCard);
                if (view.ContainsKey("image"))
                {
                    officialImageCount++;
                }

                views.Add(view);
            }

            var objectIndex = VisibleObjectIndex(element, table);
            var tableSections = await BuildTableSectionsAsync(element, table, objectIndex);
            QueueMainThread(nameof(ApplyBoardSummary), summary);
            QueueMainThread(nameof(ApplyHandCards), views);
            QueueMainThread(nameof(ApplySnapshotSections), tableSections.Sections);
            AppendLog(
                $"Snapshot table rendered: visibleHand={views.Count}, handOfficialImages={officialImageCount}, tableCards={tableSections.CardCount}, tableOfficialImages={tableSections.OfficialImageCount}.");
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Snapshot render skipped: {Escape(ex.Message)}[/color]");
        }
    }

    private string BuildSnapshotSummary(JsonElement snapshot, JsonElement table)
    {
        var tick = ReadLong(snapshot, "tick");
        var turn = ReadInt(snapshot, "turnNumber");
        var turnState = ReadString(snapshot, "turnState");
        var active = ReadString(snapshot, "activePlayerId");
        var players = table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("players", out var playersElement)
            && playersElement.ValueKind == JsonValueKind.Array
            ? playersElement.EnumerateArray().Select(PlayerSummary).ToArray()
            : [];

        var playerSummary = players.Length == 0 ? "players: unknown" : string.Join(" | ", players);
        return $"tick {tick} / turn {turn} / state {turnState} / active {active} / {playerSummary}";
    }

    private static string PlayerSummary(JsonElement player)
    {
        var zones = player.TryGetProperty("zones", out var zonesElement) && zonesElement.ValueKind == JsonValueKind.Object
            ? zonesElement
            : default;
        var id = ReadString(player, "playerId");
        var seat = ReadString(player, "seat");
        var hand = ReadArrayCount(zones, "hand");
        var hidden = ReadInt(zones, "handHidden");
        var main = ReadInt(zones, "mainDeckCount");
        var rune = ReadInt(zones, "runeDeckCount");
        return $"{id} {seat} hand {hand}+{hidden} deck {main} rune {rune}";
    }

    private async Task<(Godot.Collections.Array<Godot.Collections.Dictionary> Sections, int CardCount, int OfficialImageCount)> BuildTableSectionsAsync(
        JsonElement snapshot,
        JsonElement table,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var sections = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var cardCount = 0;
        var officialImageCount = 0;

        if (table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("players", out var players)
            && players.ValueKind == JsonValueKind.Array)
        {
            foreach (var player in players.EnumerateArray())
            {
                var section = await BuildPlayerSectionAsync(player, objectIndex);
                sections.Add(section.Section);
                cardCount += section.CardCount;
                officialImageCount += section.OfficialImageCount;
            }
        }

        if (table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("battlefields", out var battlefields)
            && battlefields.ValueKind == JsonValueKind.Array)
        {
            foreach (var battlefield in battlefields
                .EnumerateArray()
                .OrderBy(field => ReadInt(field, "index")))
            {
                var section = await BuildBattlefieldSectionAsync(battlefield, objectIndex);
                sections.Add(section.Section);
                cardCount += section.CardCount;
                officialImageCount += section.OfficialImageCount;
            }
        }

        if (sections.Count == 0)
        {
            sections.Add(new Godot.Collections.Dictionary
            {
                ["title"] = "Snapshot table",
                ["zones"] = new Godot.Collections.Array<Godot.Collections.Dictionary>
                {
                    CountZone("No server table projection yet", 0)
                }
            });
        }

        return (sections, cardCount, officialImageCount);
    }

    private async Task<(Godot.Collections.Dictionary Section, int CardCount, int OfficialImageCount)> BuildPlayerSectionAsync(
        JsonElement player,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var zones = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var playerId = ReadString(player, "playerId");
        var perspective = ReadString(player, "perspective");
        var title = $"{PlayerPerspectiveLabel(perspective)} {playerId}";
        var cardCount = 0;
        var officialImageCount = 0;

        if (player.TryGetProperty("zones", out var zoneElement) && zoneElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var zone in new[]
            {
                ("Legend", "legendZone"),
                ("Champion", "championZone"),
                ("Base", "baseCards"),
                ("Base runes", "baseRunes"),
                ("Graveyard", "graveyard"),
                ("Banished", "banished")
            })
            {
                var zoneView = await CardZoneAsync(zone.Item1, ReadStringArray(zoneElement, zone.Item2), objectIndex);
                zones.Add(zoneView.Zone);
                cardCount += zoneView.CardCount;
                officialImageCount += zoneView.OfficialImageCount;
            }

            if (ReadBool(player, "isViewer"))
            {
                zones.Add(CountZone("Hand", ReadArrayCount(zoneElement, "hand")));
            }
            else
            {
                zones.Add(CountZone("Hidden hand", ReadInt(zoneElement, "handHidden")));
            }

            zones.Add(CountZone("Main deck", ReadInt(zoneElement, "mainDeckCount")));
            zones.Add(CountZone("Rune deck", ReadInt(zoneElement, "runeDeckCount")));
        }

        return (new Godot.Collections.Dictionary
        {
            ["title"] = title,
            ["zones"] = zones
        }, cardCount, officialImageCount);
    }

    private async Task<(Godot.Collections.Dictionary Section, int CardCount, int OfficialImageCount)> BuildBattlefieldSectionAsync(
        JsonElement battlefield,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var zones = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var cardCount = 0;
        var officialImageCount = 0;
        var index = ReadInt(battlefield, "index") + 1;
        var battlefieldId = ReadString(battlefield, "battlefieldObjectId");
        var cardNo = ReadString(battlefield, "cardNo");
        var title = $"Battlefield {index} {battlefieldId}";

        var site = new SnapshotCardRef(
            battlefieldId,
            cardNo,
            !string.IsNullOrWhiteSpace(cardNo),
            false);
        var siteZone = await CardZoneAsync("Site", [site]);
        zones.Add(siteZone.Zone);
        cardCount += siteZone.CardCount;
        officialImageCount += siteZone.OfficialImageCount;

        if (battlefield.TryGetProperty("unitsBySide", out var unitsBySide)
            && unitsBySide.ValueKind == JsonValueKind.Object)
        {
            foreach (var side in unitsBySide.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                var zoneView = await CardZoneAsync($"Units {side.Name}", ReadStringArray(side.Value), objectIndex);
                zones.Add(zoneView.Zone);
                cardCount += zoneView.CardCount;
                officialImageCount += zoneView.OfficialImageCount;
            }
        }
        else
        {
            var zoneView = await CardZoneAsync("Units", ReadStringArray(battlefield, "occupantObjectIds"), objectIndex);
            zones.Add(zoneView.Zone);
            cardCount += zoneView.CardCount;
            officialImageCount += zoneView.OfficialImageCount;
        }

        var standby = await StandbyZoneAsync(battlefield, objectIndex);
        zones.Add(standby.Zone);
        cardCount += standby.CardCount;
        officialImageCount += standby.OfficialImageCount;

        return (new Godot.Collections.Dictionary
        {
            ["title"] = title,
            ["zones"] = zones
        }, cardCount, officialImageCount);
    }

    private async Task<(Godot.Collections.Dictionary Zone, int CardCount, int OfficialImageCount)> StandbyZoneAsync(
        JsonElement battlefield,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        if (!battlefield.TryGetProperty("standbySlots", out var slots)
            || slots.ValueKind != JsonValueKind.Array)
        {
            return await CardZoneAsync("Standby", ReadStringArray(battlefield, "standbyObjectIds"), objectIndex);
        }

        var cards = new List<SnapshotCardRef>();
        foreach (var slot in slots.EnumerateArray())
        {
            if (ReadBool(slot, "visible"))
            {
                var objectId = ReadString(slot, "objectId");
                cards.Add(objectIndex.TryGetValue(objectId, out var card)
                    ? card
                    : new SnapshotCardRef(objectId, string.Empty, false, ReadBool(slot, "isFaceDown")));
            }
            else
            {
                cards.Add(new SnapshotCardRef(
                    ReadString(slot, "slotId"),
                    string.Empty,
                    false,
                    true));
            }
        }

        return await CardZoneAsync("Standby", cards);
    }

    private async Task<(Godot.Collections.Dictionary Zone, int CardCount, int OfficialImageCount)> CardZoneAsync(
        string label,
        IReadOnlyList<string> objectIds,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var cards = objectIds
            .Select(objectId => objectIndex.TryGetValue(objectId, out var card)
                ? card
                : new SnapshotCardRef(objectId, string.Empty, false, true))
            .ToArray();
        return await CardZoneAsync(label, cards);
    }

    private async Task<(Godot.Collections.Dictionary Zone, int CardCount, int OfficialImageCount)> CardZoneAsync(
        string label,
        IReadOnlyList<SnapshotCardRef> cards)
    {
        var views = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var officialImageCount = 0;
        foreach (var card in cards)
        {
            var view = await BuildCardViewAsync(card);
            if (view.ContainsKey("image"))
            {
                officialImageCount++;
            }

            views.Add(view);
        }

        return (new Godot.Collections.Dictionary
        {
            ["label"] = label,
            ["cards"] = views
        }, views.Count, officialImageCount);
    }

    private static Godot.Collections.Dictionary CountZone(string label, int count)
    {
        return new Godot.Collections.Dictionary
        {
            ["label"] = label,
            ["count"] = Math.Max(0, count),
            ["cards"] = new Godot.Collections.Array<Godot.Collections.Dictionary>()
        };
    }

    private static string PlayerPerspectiveLabel(string perspective)
    {
        return perspective switch
        {
            "self" => "Self",
            "opponent" => "Opponent",
            "spectator" => "Spectator",
            _ => "Player"
        };
    }

    private IReadOnlyList<SnapshotCardRef> VisibleHandCards(JsonElement snapshot, JsonElement table)
    {
        if (table.ValueKind != JsonValueKind.Object
            || !table.TryGetProperty("viewerPlayerId", out var viewerProperty)
            || viewerProperty.ValueKind != JsonValueKind.String)
        {
            return [];
        }

        var viewer = viewerProperty.GetString() ?? string.Empty;
        var handIds = ViewerHandIds(table, viewer);
        if (handIds.Count == 0)
        {
            return [];
        }

        var objects = ViewerObjects(snapshot, viewer);
        return handIds
            .Select(objectId => CardRefFor(objectId, objects))
            .ToArray();
    }

    private static IReadOnlyList<string> ViewerHandIds(JsonElement table, string viewer)
    {
        if (!table.TryGetProperty("players", out var playersElement) || playersElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var player in playersElement.EnumerateArray())
        {
            if (!string.Equals(ReadString(player, "playerId"), viewer, StringComparison.Ordinal))
            {
                continue;
            }

            if (player.TryGetProperty("zones", out var zones)
                && zones.ValueKind == JsonValueKind.Object
                && zones.TryGetProperty("hand", out var hand)
                && hand.ValueKind == JsonValueKind.Array)
            {
                return hand
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => item.Length > 0)
                    .ToArray();
            }
        }

        return [];
    }

    private static IReadOnlyDictionary<string, JsonElement> ViewerObjects(JsonElement snapshot, string viewer)
    {
        if (!snapshot.TryGetProperty("players", out var players)
            || players.ValueKind != JsonValueKind.Object
            || !players.TryGetProperty(viewer, out var player)
            || player.ValueKind != JsonValueKind.Object
            || !player.TryGetProperty("objects", out var objects)
            || objects.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        return objects.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, SnapshotCardRef> VisibleObjectIndex(JsonElement snapshot, JsonElement table)
    {
        var index = new Dictionary<string, SnapshotCardRef>(StringComparer.Ordinal);
        if (snapshot.TryGetProperty("players", out var players) && players.ValueKind == JsonValueKind.Object)
        {
            foreach (var player in players.EnumerateObject())
            {
                if (player.Value.ValueKind != JsonValueKind.Object
                    || !player.Value.TryGetProperty("objects", out var objects)
                    || objects.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var cardObject in objects.EnumerateObject())
                {
                    index[cardObject.Name] = CardRefFromObject(cardObject.Name, cardObject.Value);
                }
            }
        }

        if (table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("battlefields", out var battlefields)
            && battlefields.ValueKind == JsonValueKind.Array)
        {
            foreach (var battlefield in battlefields.EnumerateArray())
            {
                var objectId = ReadString(battlefield, "battlefieldObjectId");
                var cardNo = ReadString(battlefield, "cardNo");
                if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(cardNo))
                {
                    continue;
                }

                index[objectId] = new SnapshotCardRef(objectId, cardNo, true, false);
            }
        }

        return index;
    }

    private static SnapshotCardRef CardRefFor(string objectId, IReadOnlyDictionary<string, JsonElement> objects)
    {
        if (!objects.TryGetValue(objectId, out var card) || card.ValueKind != JsonValueKind.Object)
        {
            return new SnapshotCardRef(objectId, string.Empty, false, true);
        }

        return CardRefFromObject(objectId, card);
    }

    private static SnapshotCardRef CardRefFromObject(string objectId, JsonElement card)
    {
        var faceDown = ReadBool(card, "isFaceDown");
        var cardNo = faceDown ? string.Empty : ReadString(card, "cardNo");
        return new SnapshotCardRef(objectId, cardNo, !string.IsNullOrWhiteSpace(cardNo), faceDown);
    }

    private async Task<Godot.Collections.Dictionary> BuildCardViewAsync(SnapshotCardRef card)
    {
        var view = new Godot.Collections.Dictionary
        {
            ["label"] = string.IsNullOrWhiteSpace(card.CardNo) ? "Hidden" : card.CardNo,
            ["objectId"] = card.ObjectId
        };

        if (card.Visible
            && _officialCatalog.TryGetValue(card.CardNo, out var entry)
            && await _cardImageLoader.LoadOfficialFrontImageAsync(entry, _shutdown.Token) is { } image)
        {
            view["image"] = image;
        }

        return view;
    }

    private void UpdateJoinedSession(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var token = ReadString(element, "reconnectToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        _session = _session with { ReconnectToken = token };
        _ = SaveSessionAsync();
    }

    private void HandleServerError(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var code = ReadString(element, "code");
        if (string.Equals(code, ErrorCodes.InvalidReconnectToken, StringComparison.Ordinal))
        {
            _session = _session with { ReconnectToken = null };
            _ = SaveSessionAsync();
            AppendLog("[color=yellow]Reconnect token was invalid and has been cleared.[/color]");
        }
    }

    private async Task SaveSessionAsync()
    {
        if (!_ephemeralSession)
        {
            await _sessionStore.SaveAsync(_session);
        }
    }

    private static PlayerSessionSettings ApplyCommandLineOverrides(
        PlayerSessionSettings session,
        IReadOnlyList<string> args)
    {
        return session with
        {
            Handle = ArgValue(args, "--riftbound-handle=") ?? session.Handle,
            RoomId = ArgValue(args, "--riftbound-room=") ?? session.RoomId,
            PlayerKey = ArgValue(args, "--riftbound-player-key=") ?? session.PlayerKey,
            ReconnectToken = args.Contains("--riftbound-ignore-reconnect") ? null : session.ReconnectToken
        };
    }

    private static string? ArgValue(IReadOnlyList<string> args, string prefix)
    {
        return args
            .FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))
            ?[prefix.Length..]
            .Trim();
    }

    private static IReadOnlyList<string> CommandLineArgs()
    {
        return OS.GetCmdlineArgs()
            .Concat(OS.GetCmdlineUserArgs())
            .ToArray();
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number,
            _ => 0
        };
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(property.GetString(), out var number) => number,
            _ => 0
        };
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            && property.GetBoolean();
    }

    private static int ReadArrayCount(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            ? ReadStringArray(property)
            : [];
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array
            ? element
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToArray()
            : [];
    }

    private static string PayloadSummary(object? payload)
    {
        return payload switch
        {
            null => "null",
            JsonElement element => element.ValueKind.ToString(),
            _ => payload.GetType().Name
        };
    }

    private void AppendReceipt(string label, CommandReceiptDto receipt)
    {
        var tone = receipt.Accepted ? "green" : "red";
        AppendLog(
            $"[color={tone}]{Escape(label)} receipt accepted={receipt.Accepted} state={Escape(receipt.State)} message={Escape(receipt.Message)}[/color]");
    }

    private async Task DisconnectAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync();
        _connection = null;
    }

    private bool IsConnected()
    {
        return _connection?.State == HubConnectionState.Connected;
    }

    private static string NewIntentId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private void SetStatus(string text)
    {
        GD.Print($"[Riftbound] {text}");
        QueueMainThread(nameof(ApplyStatus), text);
    }

    private void AppendLog(string text)
    {
        GD.Print($"[Riftbound] {text}");
        QueueMainThread(nameof(ApplyLog), text);
    }

    public void ApplyStatus(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    public void ApplyLog(string text)
    {
        if (_log is null)
        {
            return;
        }

        _log.AppendText($"{text}\n");
    }

    public void ApplyBoardSummary(string text)
    {
        if (_boardSummary is not null)
        {
            _boardSummary.Text = text;
        }
    }

    public void ApplyHandCards(Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        if (_handRow is null)
        {
            return;
        }

        foreach (var child in _handRow.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var card in cards)
        {
            _handRow.AddChild(CardNode(card));
        }
    }

    public void ApplySnapshotSections(Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        if (_snapshotRows is null)
        {
            return;
        }

        foreach (var child in _snapshotRows.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var section in sections)
        {
            _snapshotRows.AddChild(SectionNode(section));
        }
    }

    private static Control SectionNode(Godot.Collections.Dictionary section)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 0)
        };
        var rows = new VBoxContainer();
        rows.AddChild(new Label
        {
            Text = section.TryGetValue("title", out var title) ? title.AsString() : "Section"
        });

        var zones = section.TryGetValue("zones", out var zoneValue)
            ? zoneValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        foreach (var zone in zones)
        {
            rows.AddChild(ZoneNode(zone));
        }

        frame.AddChild(rows);
        return frame;
    }

    private static Control ZoneNode(Godot.Collections.Dictionary zone)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 104)
        };
        row.AddChild(new Label
        {
            CustomMinimumSize = new Vector2(112, 0),
            Text = ZoneLabel(zone),
            VerticalAlignment = VerticalAlignment.Center
        });

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 104),
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var cards = new HBoxContainer();
        var cardViews = zone.TryGetValue("cards", out var cardsValue)
            ? cardsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];

        if (cardViews.Count == 0)
        {
            cards.AddChild(new Label
            {
                CustomMinimumSize = new Vector2(88, 96),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "empty"
            });
        }
        else
        {
            foreach (var card in cardViews)
            {
                cards.AddChild(CardNode(card, new Vector2(64, 90), new Vector2(58, 82)));
            }
        }

        scroll.AddChild(cards);
        row.AddChild(scroll);
        return row;
    }

    private static string ZoneLabel(Godot.Collections.Dictionary zone)
    {
        var label = zone.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Zone";
        if (zone.TryGetValue("count", out var countValue))
        {
            return $"{label} {countValue.AsInt32()}";
        }

        return label;
    }

    private static Control CardNode(Godot.Collections.Dictionary card)
    {
        return CardNode(card, new Vector2(92, 128), new Vector2(84, 120));
    }

    private static Control CardNode(Godot.Collections.Dictionary card, Vector2 frameSize, Vector2 contentSize)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = frameSize
        };
        var image = card.TryGetValue("image", out var imageValue) ? imageValue.As<Image>() : null;
        if (image is not null)
        {
            frame.AddChild(new TextureRect
            {
                CustomMinimumSize = contentSize,
                Texture = ImageTexture.CreateFromImage(image),
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            });
            return frame;
        }

        frame.AddChild(new Label
        {
            CustomMinimumSize = contentSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = card.TryGetValue("label", out var label) ? label.AsString() : "Card"
        });
        return frame;
    }

    public void ApplyDeckOptions()
    {
        if (_deckSelect is null)
        {
            return;
        }

        _deckSelect.Clear();
        for (var i = 0; i < _decks.Count; i++)
        {
            var deck = _decks[i];
            _deckSelect.AddItem($"{deck.Name} · {deck.Description}", i);
            if (string.Equals(deck.Id, _session.LastDeckId, StringComparison.Ordinal))
            {
                _deckSelect.Select(i);
            }
        }
    }

    public void ApplyOfficialCardPreview(Image image)
    {
        if (_officialCardPreview is not null)
        {
            _officialCardPreview.Texture = ImageTexture.CreateFromImage(image);
        }
    }

    private void QueueMainThread(StringName method, Variant value)
    {
        if (!IsInsideTree())
        {
            return;
        }

        CallDeferred(method, value);
    }

    private void QueueMainThread(StringName method)
    {
        if (!IsInsideTree())
        {
            return;
        }

        CallDeferred(method);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("[", "[lb]", StringComparison.Ordinal)
            .Replace("]", "[rb]", StringComparison.Ordinal);
    }
}
