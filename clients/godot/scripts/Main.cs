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

    private PlayerSessionSettings _session = PlayerSessionSettings.CreateDefault();
    private Label? _status;
    private RichTextLabel? _log;
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
        _ = LoadOfficialCardPreviewAsync();
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
            AppendLog($"Official catalog loaded: {catalog.Count} cards.");

            if (!catalog.TryGetValue(PreviewCardNo, out var card))
            {
                AppendLog($"[color=yellow]Official preview card not found: {Escape(PreviewCardNo)}[/color]");
                return;
            }

            var image = await new OfficialCardImageLoader().LoadOfficialFrontImageAsync(card, _shutdown.Token);
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

        AppendLog(
            $"[b]{Escape(channel)}[/b] type={message.Type} room={Escape(message.RoomId)} player={Escape(message.PlayerId)} tick={message.ServerTick} payload={PayloadSummary(message.Payload)}");
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
