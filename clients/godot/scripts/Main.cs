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
    private const int AutoSmokePlayCardTapRuneLimit = 4;
    private const int AutoSmokeBoardActionLimit = 4;
    private const int AutoSmokeTempoActionLimit = 24;
    private static readonly string[] AutoSmokePostPlayActions =
    [
        "MOVE_UNIT",
        "DECLARE_BATTLE"
    ];
    private static readonly string[] AutoSmokeSpecialActions =
    [
        "ORDER_TRIGGERS",
        "ASSIGN_COMBAT_DAMAGE"
    ];
    private static readonly string[] AutoSmokeTempoActions =
    [
        "PASS_PRIORITY",
        "PASS_FOCUS",
        "PASS",
        "END_TURN"
    ];

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
    private Label? _promptSummary;
    private VBoxContainer? _promptActions;
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
    private bool _autoSmokeMulligan;
    private bool _autoSmokeTapRune;
    private bool _autoSmokePlayCard;
    private bool _autoSmokeFollowups;
    private bool _autoSmokeSubmitted;
    private int _autoSmokeTapRuneSubmissions;
    private bool _autoSmokePlayCardSubmitted;
    private bool _ephemeralSession;
    private bool _isShuttingDown;
    private readonly HashSet<string> _autoSmokePromptSubmissions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _autoSmokeActionSubmissions = new(StringComparer.Ordinal);
    private Task? _officialCatalogLoadTask;
    private IReadOnlyDictionary<string, CardCatalogEntry> _officialCatalog =
        new Dictionary<string, CardCatalogEntry>(StringComparer.Ordinal);

    public override async void _Ready()
    {
        BindNodes();
        WireButtons();
        var args = CommandLineArgs();
        _autoSmoke = args.Contains("--riftbound-smoke-auto-ready");
        _autoSmokeMulligan = args.Contains("--riftbound-smoke-auto-mulligan");
        _autoSmokeTapRune = args.Contains("--riftbound-smoke-auto-tap-rune");
        _autoSmokePlayCard = args.Contains("--riftbound-smoke-auto-play-card");
        _autoSmokeFollowups = args.Contains("--riftbound-smoke-auto-followups");
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
        _isShuttingDown = true;
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
        _promptSummary = GetNode<Label>("PromptFrame/PromptBox/PromptSummary");
        _promptActions = GetNode<VBoxContainer>("PromptFrame/PromptBox/PromptScroll/PromptActions");
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

    private async Task SubmitPromptActionAsync(
        string submitKind,
        string cmdType,
        string promptId,
        long snapshotTick,
        string label)
    {
        switch (submitKind)
        {
            case "ready":
                await ReadyAsync();
                return;
            case "submitDeck":
                await SubmitSelectedDeckAsync();
                return;
            case "command":
                await SubmitPromptCommandAsync(cmdType, promptId, snapshotTick, label);
                return;
            default:
                AppendLog($"[color=yellow]Prompt action requires choices: {Escape(label)}.[/color]");
                return;
        }
    }

    private async Task SubmitPromptCommandAsync(string cmdType, string promptId, long snapshotTick, string label)
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Prompt command skipped: not connected/authenticated.[/color]");
            return;
        }

        if (string.IsNullOrWhiteSpace(cmdType))
        {
            AppendLog($"[color=yellow]Prompt command skipped: {Escape(label)} has no command type.[/color]");
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["cmdType"] = cmdType
        };
        if (!string.IsNullOrWhiteSpace(promptId))
        {
            payload["promptId"] = promptId;
        }

        if (snapshotTick >= 0)
        {
            payload["snapshotTick"] = snapshotTick;
        }

        var cmd = JsonSerializer.SerializeToElement(payload);
        var receipt = await _connection!.InvokeAsync<CommandReceiptDto>(
            "SubmitIntent",
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId($"prompt-{cmdType.ToLowerInvariant()}"),
            cmd,
            _shutdown.Token);
        AppendReceipt(label, receipt);
    }

    private async Task SubmitPromptPayloadAsync(
        Godot.Collections.Dictionary action,
        Dictionary<string, object?> payload,
        string intentSuffix)
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Prompt command skipped: not connected/authenticated.[/color]");
            return;
        }

        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Prompt action";
        var promptId = action.TryGetValue("promptId", out var promptIdValue) ? promptIdValue.AsString() : string.Empty;
        var snapshotTick = action.TryGetValue("snapshotTick", out var snapshotTickValue) ? snapshotTickValue.AsInt64() : -1L;
        if (!string.IsNullOrWhiteSpace(promptId))
        {
            payload["promptId"] = promptId;
        }

        if (snapshotTick >= 0)
        {
            payload["snapshotTick"] = snapshotTick;
        }

        var cmd = JsonSerializer.SerializeToElement(payload);
        var receipt = await _connection!.InvokeAsync<CommandReceiptDto>(
            "SubmitIntent",
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId($"prompt-{intentSuffix}"),
            cmd,
            _shutdown.Token);
        AppendReceipt(label, receipt);
    }

    private async Task SubmitMulliganAsync(
        Godot.Collections.Dictionary action,
        IReadOnlyList<string> handObjectIds)
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Mulligan skipped: not connected/authenticated.[/color]");
            return;
        }

        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Mulligan";
        var promptId = action.TryGetValue("promptId", out var promptIdValue) ? promptIdValue.AsString() : string.Empty;
        var snapshotTick = action.TryGetValue("snapshotTick", out var snapshotTickValue) ? snapshotTickValue.AsInt64() : -1L;
        var maxSelectionCount = action.TryGetValue("maxSelectionCount", out var maxValue) ? maxValue.AsInt32() : -1;
        if (maxSelectionCount >= 0 && handObjectIds.Count > maxSelectionCount)
        {
            AppendLog($"[color=yellow]Mulligan skipped: selected {handObjectIds.Count} exceeds server max {maxSelectionCount}.[/color]");
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["cmdType"] = "MULLIGAN",
            ["handObjectIds"] = handObjectIds.ToArray()
        };
        if (!string.IsNullOrWhiteSpace(promptId))
        {
            payload["promptId"] = promptId;
        }

        if (snapshotTick >= 0)
        {
            payload["snapshotTick"] = snapshotTick;
        }

        var cmd = JsonSerializer.SerializeToElement(payload);
        var receipt = await _connection!.InvokeAsync<CommandReceiptDto>(
            "SubmitIntent",
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId("prompt-mulligan"),
            cmd,
            _shutdown.Token);
        AppendReceipt(label, receipt);
    }

    private async Task SubmitPromptTemplateAsync(
        Godot.Collections.Dictionary action,
        IReadOnlyList<PromptSelector> selectors)
    {
        await SubmitPromptTemplateAsync(action, PromptSelection.FromSelectors(selectors));
    }

    private async Task SubmitPromptTemplateAsync(
        Godot.Collections.Dictionary action,
        PromptSelection selection)
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Prompt template skipped: not connected/authenticated.[/color]");
            return;
        }

        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Prompt action";
        var promptId = action.TryGetValue("promptId", out var promptIdValue) ? promptIdValue.AsString() : string.Empty;
        var snapshotTick = action.TryGetValue("snapshotTick", out var snapshotTickValue) ? snapshotTickValue.AsInt64() : -1L;
        var candidateJson = action.TryGetValue("candidateJson", out var candidateValue) ? candidateValue.AsString() : string.Empty;
        if (string.IsNullOrWhiteSpace(candidateJson))
        {
            AppendLog($"[color=yellow]Prompt template skipped: {Escape(label)} has no candidate JSON.[/color]");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(candidateJson);
            var payload = CommandFromTemplate(document.RootElement, selection, promptId, snapshotTick);
            if (payload is null)
            {
                AppendLog($"[color=yellow]Prompt template incomplete: {Escape(label)} needs required selections.[/color]");
                return;
            }

            var cmdType = payload.TryGetValue("cmdType", out var cmdTypeValue) ? Convert.ToString(cmdTypeValue) ?? "command" : "command";
            var cmd = JsonSerializer.SerializeToElement(payload);
            var receipt = await _connection!.InvokeAsync<CommandReceiptDto>(
                "SubmitIntent",
                _session.RoomId,
                _authenticatedHandle,
                NewIntentId($"prompt-{cmdType.ToLowerInvariant()}"),
                cmd,
                _shutdown.Token);
            AppendReceipt(label, receipt);
        }
        catch (JsonException ex)
        {
            AppendLog($"[color=yellow]Prompt template skipped: malformed candidate JSON ({Escape(ex.Message)}).[/color]");
        }
    }

    private static Dictionary<string, object?>? CommandFromTemplate(
        JsonElement candidate,
        PromptSelection selection,
        string promptId,
        long snapshotTick)
    {
        if (!candidate.TryGetProperty("commandTemplate", out var template)
            || template.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var cmdType = ReadString(template, "cmdType");
        if (string.IsNullOrWhiteSpace(cmdType)
            || !template.TryGetProperty("bindings", out var bindings)
            || bindings.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var command = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cmdType"] = cmdType
        };
        var requirement = SourceRequirementFor(candidate, selection.SourceId);
        foreach (var binding in bindings.EnumerateArray())
        {
            var field = ReadString(binding, "field");
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            var value = CommandTemplateValue(binding, candidate, requirement, selection);
            var missing = IsMissingCommandValue(value);
            var required = ReadBool(binding, "required");
            var omitEmpty = ReadOptionalBool(binding, "omitEmpty") ?? true;
            if (missing)
            {
                if (required)
                {
                    return null;
                }

                if (omitEmpty)
                {
                    continue;
                }

                value = ReadBool(binding, "asArray") ? Array.Empty<string>() : string.Empty;
            }

            command[field] = value;
        }

        if (!string.IsNullOrWhiteSpace(promptId))
        {
            command["promptId"] = promptId;
        }

        if (snapshotTick >= 0)
        {
            command["snapshotTick"] = snapshotTick;
        }

        return command;
    }

    private static object? CommandTemplateValue(
        JsonElement binding,
        JsonElement candidate,
        JsonElement? requirement,
        PromptSelection selection)
    {
        var source = ReadString(binding, "source");
        object? rawValue = source switch
        {
            "selectedSource" => selection.SourceId,
            "selectedTarget" => selection.TargetObjectIds.FirstOrDefault(),
            "selectedTargets" => selection.TargetObjectIds,
            "selectedDestination" => selection.DestinationId,
            "selectedMode" => selection.Mode,
            "selectedOptionalCosts" => selection.OptionalCostIds,
            "candidateMetadata" => MetadataTemplateValue(binding, MetadataElement(candidate)),
            "requirementMetadata" => MetadataTemplateValue(binding, requirement),
            _ => null
        };

        if (!ReadBool(binding, "asArray"))
        {
            return rawValue;
        }

        return rawValue switch
        {
            IReadOnlyList<string> values => values.ToArray(),
            string value when !string.IsNullOrWhiteSpace(value) => new[] { value },
            _ => Array.Empty<string>()
        };
    }

    private static JsonElement? MetadataElement(JsonElement candidate)
    {
        return candidate.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? metadata
            : null;
    }

    private static JsonElement? SourceRequirementFor(JsonElement candidate, string? sourceObjectId)
    {
        if (string.IsNullOrWhiteSpace(sourceObjectId)
            || MetadataElement(candidate) is not { } metadata
            || !metadata.TryGetProperty("sourceRequirements", out var requirements))
        {
            return null;
        }

        if (requirements.ValueKind == JsonValueKind.Array)
        {
            foreach (var requirement in requirements.EnumerateArray())
            {
                if (requirement.ValueKind == JsonValueKind.Object
                    && string.Equals(ReadString(requirement, "sourceObjectId"), sourceObjectId, StringComparison.Ordinal))
                {
                    return requirement;
                }
            }
        }
        else if (requirements.ValueKind == JsonValueKind.Object)
        {
            foreach (var requirement in requirements.EnumerateObject())
            {
                if (requirement.Value.ValueKind == JsonValueKind.Object
                    && string.Equals(ReadString(requirement.Value, "sourceObjectId"), sourceObjectId, StringComparison.Ordinal))
                {
                    return requirement.Value;
                }
            }
        }

        return null;
    }

    private static object? MetadataTemplateValue(JsonElement binding, JsonElement? metadata)
    {
        if (metadata is not { ValueKind: JsonValueKind.Object } metadataElement)
        {
            return null;
        }

        foreach (var key in MetadataKeys(binding))
        {
            if (!metadataElement.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var strings = ReadStringArray(value).ToArray();
                if (strings.Length > 0)
                {
                    return strings;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> MetadataKeys(JsonElement binding)
    {
        var key = ReadString(binding, "metadataKey");
        if (!string.IsNullOrWhiteSpace(key))
        {
            yield return key;
        }

        if (binding.TryGetProperty("metadataKeys", out var keys)
            && keys.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in keys.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    yield return item.GetString()!;
                }
            }
        }
    }

    private static bool IsMissingCommandValue(object? value)
    {
        return value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            IReadOnlyCollection<string> values => values.Count == 0,
            _ => false
        };
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
        else if (channel == "Prompt")
        {
            RenderPrompt(message);
        }

        AppendLog(
            $"[b]{Escape(channel)}[/b] type={message.Type} room={Escape(message.RoomId)} player={Escape(message.PlayerId)} tick={message.ServerTick} payload={PayloadSummary(message.Payload)}");
    }

    private void RenderPrompt(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        try
        {
            var view = BuildPromptView(element);
            QueueMainThread(nameof(ApplyPrompt), view);
            AppendLog(
                $"Prompt rendered: {view["candidateCount"].AsInt32()} candidates, {view["directCount"].AsInt32()} direct, {view["templateCount"].AsInt32()} templates.");
            AppendLog($"Prompt actions: {PromptActionSummary(view)}");
            _ = RunAutoSmokePromptAsync(view);
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Prompt render skipped: {Escape(ex.Message)}[/color]");
        }
    }

    private async Task RunAutoSmokePromptAsync(Godot.Collections.Dictionary view)
    {
        if ((!_autoSmokeMulligan && !_autoSmokeTapRune && !_autoSmokePlayCard && !_autoSmokeFollowups)
            || !view.TryGetValue("actions", out var actionsValue)
            || actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } actions)
        {
            return;
        }

        if (_autoSmokeMulligan
            && TryGetEnabledPromptAction(actions, "MULLIGAN", requireTemplate: false, out var mulliganAction))
        {
            var key = AutoSmokePromptKey(mulliganAction, "MULLIGAN");
            if (!_autoSmokePromptSubmissions.Add(key))
            {
                return;
            }

            AppendLog("Auto smoke: confirming mulligan with 0 selected cards.");
            await SubmitMulliganAsync(mulliganAction, Array.Empty<string>());
            return;
        }

        if (_autoSmokeFollowups)
        {
            foreach (var actionName in AutoSmokeSpecialActions)
            {
                if (await TryRunAutoSmokeSpecialActionAsync(actions, actionName))
                {
                    return;
                }
            }
        }

        if (_autoSmokePlayCard
            && !_autoSmokePlayCardSubmitted
            && (!_autoSmokeTapRune || _autoSmokeTapRuneSubmissions > 0)
            && TryGetEnabledPromptAction(actions, "PLAY_CARD", requireTemplate: true, out var playAction))
        {
            var sourceObjectId = FirstPromptChoiceId(playAction, "sourceChoices");
            if (string.IsNullOrWhiteSpace(sourceObjectId))
            {
                AppendLog("[color=yellow]Auto smoke: PLAY_CARD has no server-provided source choice.[/color]");
                return;
            }

            var key = AutoSmokePromptKey(playAction, $"PLAY_CARD:{sourceObjectId}");
            if (!_autoSmokePromptSubmissions.Add(key))
            {
                return;
            }

            _autoSmokePlayCardSubmitted = true;
            AppendLog($"Auto smoke: submitting PLAY_CARD from server source {Escape(sourceObjectId)}.");
            await SubmitPromptTemplateAsync(playAction, PromptSelection.SourceOnly(sourceObjectId));
            return;
        }

        if (_autoSmokeFollowups && _autoSmokePlayCardSubmitted)
        {
            foreach (var actionName in AutoSmokePostPlayActions)
            {
                if (await TryRunAutoSmokeTemplateActionAsync(actions, actionName))
                {
                    return;
                }
            }
        }

        var tapRuneLimit = _autoSmokePlayCard ? AutoSmokePlayCardTapRuneLimit : 1;
        if (_autoSmokeTapRune
            && _autoSmokeTapRuneSubmissions < tapRuneLimit
            && TryGetEnabledPromptAction(actions, "TAP_RUNE", requireTemplate: true, out var tapRuneAction))
        {
            var sourceObjectId = FirstPromptChoiceId(tapRuneAction, "sourceChoices");
            if (string.IsNullOrWhiteSpace(sourceObjectId))
            {
                AppendLog("[color=yellow]Auto smoke: TAP_RUNE has no server-provided source choice.[/color]");
                return;
            }

            var key = AutoSmokePromptKey(tapRuneAction, $"TAP_RUNE:{sourceObjectId}");
            if (!_autoSmokePromptSubmissions.Add(key))
            {
                return;
            }

            _autoSmokeTapRuneSubmissions++;
            AppendLog($"Auto smoke: submitting TAP_RUNE from server source {Escape(sourceObjectId)}.");
            await SubmitPromptTemplateAsync(tapRuneAction, PromptSelection.SourceOnly(sourceObjectId));
            return;
        }

        if (_autoSmokeFollowups)
        {
            foreach (var actionName in AutoSmokeTempoActions)
            {
                if (await TryRunAutoSmokeTemplateActionAsync(actions, actionName))
                {
                    return;
                }
            }
        }
    }

    private async Task<bool> TryRunAutoSmokeTemplateActionAsync(
        Godot.Collections.Array<Godot.Collections.Dictionary> actions,
        string actionName)
    {
        if (_autoSmokeActionSubmissions.GetValueOrDefault(actionName) >= AutoSmokeActionLimitFor(actionName)
            || !TryGetEnabledPromptAction(actions, actionName, requireTemplate: true, out var action))
        {
            return false;
        }

        if (!TryBuildFirstServerPromptSelection(action, out var selection, out var selectionKey, out var reason))
        {
            AppendLog($"[color=yellow]Auto smoke: {Escape(actionName)} skipped: {Escape(reason)}[/color]");
            return false;
        }

        var key = AutoSmokePromptKey(action, $"{actionName}:{selectionKey}");
        if (!_autoSmokePromptSubmissions.Add(key))
        {
            return false;
        }

        _autoSmokeActionSubmissions[actionName] = _autoSmokeActionSubmissions.GetValueOrDefault(actionName) + 1;
        AppendLog($"Auto smoke: submitting {Escape(actionName)} with server selection {Escape(selectionKey)}.");
        await SubmitPromptTemplateAsync(action, selection);
        return true;
    }

    private async Task<bool> TryRunAutoSmokeSpecialActionAsync(
        Godot.Collections.Array<Godot.Collections.Dictionary> actions,
        string actionName)
    {
        if (_autoSmokeActionSubmissions.GetValueOrDefault(actionName) >= AutoSmokeActionLimitFor(actionName)
            || !TryGetEnabledPromptAction(actions, actionName, requireTemplate: false, out var action))
        {
            return false;
        }

        if (!TryBuildSpecialPromptCommand(action, out var payload, out var payloadKey, out var reason))
        {
            AppendLog($"[color=yellow]Auto smoke: {Escape(actionName)} skipped: {Escape(reason)}[/color]");
            return false;
        }

        var key = AutoSmokePromptKey(action, $"{actionName}:{payloadKey}");
        if (!_autoSmokePromptSubmissions.Add(key))
        {
            return false;
        }

        _autoSmokeActionSubmissions[actionName] = _autoSmokeActionSubmissions.GetValueOrDefault(actionName) + 1;
        AppendLog($"Auto smoke: submitting {Escape(actionName)} with server metadata {Escape(payloadKey)}.");
        await SubmitPromptPayloadAsync(action, payload, actionName.ToLowerInvariant());
        return true;
    }

    private static int AutoSmokeActionLimitFor(string actionName)
    {
        return string.Equals(actionName, "MOVE_UNIT", StringComparison.Ordinal)
            || string.Equals(actionName, "DECLARE_BATTLE", StringComparison.Ordinal)
            ? AutoSmokeBoardActionLimit
            : AutoSmokeTempoActionLimit;
    }

    private static bool TryGetEnabledPromptAction(
        Godot.Collections.Array<Godot.Collections.Dictionary> actions,
        string actionName,
        bool requireTemplate,
        out Godot.Collections.Dictionary action)
    {
        action = [];
        foreach (var candidate in actions)
        {
            var candidateName = candidate.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty;
            var enabled = candidate.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
            var hasTemplate = candidate.TryGetValue("hasTemplate", out var templateValue) && templateValue.AsBool();
            if (enabled
                && string.Equals(candidateName, actionName, StringComparison.Ordinal)
                && (!requireTemplate || hasTemplate))
            {
                action = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildFirstServerPromptSelection(
        Godot.Collections.Dictionary action,
        out PromptSelection selection,
        out string selectionKey,
        out string reason)
    {
        selection = PromptSelection.Empty;
        selectionKey = "none";
        reason = string.Empty;

        if (!action.TryGetValue("selectionSteps", out var stepsValue)
            || stepsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } steps
            || steps.Count == 0)
        {
            selection = PromptSelection.Empty;
            return true;
        }

        var sourceId = string.Empty;
        var destinationId = string.Empty;
        var mode = string.Empty;
        var targets = new List<string>();
        var optionalCosts = new List<string>();
        var keyParts = new List<string>();

        foreach (var step in steps)
        {
            var role = step.TryGetValue("role", out var roleValue) ? roleValue.AsString() : string.Empty;
            var required = step.TryGetValue("required", out var requiredValue) && requiredValue.AsBool();
            var choiceId = FirstPromptStepChoiceId(step);
            if (string.IsNullOrWhiteSpace(choiceId))
            {
                if (required)
                {
                    reason = $"required {role} choice is missing";
                    return false;
                }

                continue;
            }

            if (!required && string.Equals(role, "optionalCost", StringComparison.Ordinal))
            {
                continue;
            }

            switch (role)
            {
                case "source":
                    sourceId = string.IsNullOrWhiteSpace(sourceId) ? choiceId : sourceId;
                    break;
                case "target":
                    targets.Add(choiceId);
                    break;
                case "destination":
                    destinationId = string.IsNullOrWhiteSpace(destinationId) ? choiceId : destinationId;
                    break;
                case "mode":
                    mode = string.IsNullOrWhiteSpace(mode) ? choiceId : mode;
                    break;
                case "optionalCost":
                    optionalCosts.Add(choiceId);
                    break;
                default:
                    if (required)
                    {
                        reason = $"unsupported required selection role {role}";
                        return false;
                    }

                    continue;
            }

            keyParts.Add($"{role}={choiceId}");
        }

        selection = new PromptSelection(
            string.IsNullOrWhiteSpace(sourceId) ? null : sourceId,
            targets,
            string.IsNullOrWhiteSpace(destinationId) ? null : destinationId,
            string.IsNullOrWhiteSpace(mode) ? null : mode,
            optionalCosts);
        selectionKey = keyParts.Count == 0 ? "none" : string.Join(",", keyParts);
        return true;
    }

    private static string FirstPromptStepChoiceId(Godot.Collections.Dictionary step)
    {
        if (!step.TryGetValue("choices", out var choicesValue)
            || choicesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } choices)
        {
            return string.Empty;
        }

        foreach (var choice in choices)
        {
            var choiceId = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(choiceId))
            {
                return choiceId;
            }
        }

        return string.Empty;
    }

    private static string PromptActionSummary(Godot.Collections.Dictionary view)
    {
        if (!view.TryGetValue("actions", out var actionsValue)
            || actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } actions
            || actions.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", actions.Select(action =>
        {
            var name = action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : "?";
            var enabled = action.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
            var hasTemplate = action.TryGetValue("hasTemplate", out var templateValue) && templateValue.AsBool();
            return $"{name}:{(enabled ? "on" : "off")}{(hasTemplate ? ":template" : string.Empty)}";
        }));
    }

    private static bool TryBuildSpecialPromptCommand(
        Godot.Collections.Dictionary action,
        out Dictionary<string, object?> payload,
        out string payloadKey,
        out string reason)
    {
        return SpecialPromptCommandBuilder.TryBuild(action, out payload, out payloadKey, out reason);
    }

    private string AutoSmokePromptKey(Godot.Collections.Dictionary action, string actionName)
    {
        var promptId = action.TryGetValue("promptId", out var promptValue) ? promptValue.AsString() : string.Empty;
        var snapshotTick = action.TryGetValue("snapshotTick", out var tickValue) ? tickValue.AsInt64() : -1L;
        return $"{_session.RoomId}:{_authenticatedHandle}:{promptId}:{snapshotTick}:{actionName}";
    }

    private static string FirstPromptChoiceId(Godot.Collections.Dictionary action, string propertyName)
    {
        if (!action.TryGetValue(propertyName, out var choicesValue)
            || choicesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } choices)
        {
            return string.Empty;
        }

        foreach (var choice in choices)
        {
            var choiceId = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(choiceId))
            {
                return choiceId;
            }
        }

        return string.Empty;
    }

    private static Godot.Collections.Dictionary BuildPromptView(JsonElement prompt)
    {
        var actions = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var promptId = ReadString(prompt, "promptId");
        var snapshotTick = ReadOptionalLong(prompt, "snapshotTick");
        var actionable = ReadBool(prompt, "actionable");
        var reason = ReadString(prompt, "reason");
        var title = "Prompt";
        var message = reason;

        if (prompt.TryGetProperty("view", out var promptView) && promptView.ValueKind == JsonValueKind.Object)
        {
            title = ReadString(promptView, "title");
            message = ReadString(promptView, "message");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Prompt";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = string.IsNullOrWhiteSpace(reason) ? "Waiting for server prompt." : reason;
        }

        if (prompt.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                actions.Add(PromptAction(candidate, promptId, snapshotTick, actionable));
            }
        }

        var directCount = actions.Count(action =>
            action.TryGetValue("submitKind", out var kindValue)
            && !string.Equals(kindValue.AsString(), "unsupported", StringComparison.Ordinal));
        var templateCount = actions.Count(action =>
            action.TryGetValue("hasTemplate", out var templateValue)
            && templateValue.AsBool());

        return new Godot.Collections.Dictionary
        {
            ["summary"] = $"{title}\n{message}\nActionable: {actionable} · {reason}",
            ["actions"] = actions,
            ["candidateCount"] = actions.Count,
            ["directCount"] = directCount,
            ["templateCount"] = templateCount
        };
    }

    private static Godot.Collections.Dictionary PromptAction(
        JsonElement candidate,
        string promptId,
        long? snapshotTick,
        bool promptActionable)
    {
        var action = ReadString(candidate, "action");
        var label = ReadString(candidate, "label");
        var enabled = promptActionable && ReadBool(candidate, "enabled");
        var reason = ReadString(candidate, "reason");
        var submitKind = DirectSubmitKind(action);
        var cmdType = DirectCommandType(action);
        var hasTemplate = candidate.TryGetProperty("commandTemplate", out var template)
            && template.ValueKind == JsonValueKind.Object
            && !string.IsNullOrWhiteSpace(ReadString(template, "cmdType"));

        if (string.IsNullOrWhiteSpace(label))
        {
            label = action;
        }

        return new Godot.Collections.Dictionary
        {
            ["action"] = action,
            ["label"] = label,
            ["enabled"] = enabled,
            ["reason"] = reason,
            ["submitKind"] = submitKind,
            ["cmdType"] = cmdType,
            ["promptId"] = promptId,
            ["snapshotTick"] = snapshotTick ?? -1L,
            ["hasTemplate"] = hasTemplate,
            ["candidateJson"] = candidate.GetRawText(),
            ["selectionSteps"] = PromptSelectionSteps(candidate),
            ["sourceChoices"] = PromptChoices(candidate, "sources"),
            ["minSelectionCount"] = CandidateMetadataInt(candidate, "minSelectionCount") ?? -1,
            ["maxSelectionCount"] = CandidateMetadataInt(candidate, "maxSelectionCount") ?? -1
        };
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> PromptChoices(
        JsonElement candidate,
        string propertyName)
    {
        var choices = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (!candidate.TryGetProperty(propertyName, out var elements)
            || elements.ValueKind != JsonValueKind.Array)
        {
            return choices;
        }

        foreach (var choice in elements.EnumerateArray())
        {
            choices.Add(PromptChoice(choice));
        }

        return choices;
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> PromptSelectionSteps(JsonElement candidate)
    {
        var steps = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (candidate.TryGetProperty("selectionSteps", out var selectionSteps)
            && selectionSteps.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in selectionSteps.EnumerateArray())
            {
                steps.Add(PromptSelectionStep(step));
            }
        }

        if (steps.Count > 0)
        {
            return steps;
        }

        AddLegacySelectionStep(steps, candidate, "sources", "source", "Source", required: false);
        AddLegacySelectionStep(steps, candidate, "targets", "target", "Target", required: false);
        AddLegacySelectionStep(steps, candidate, "destinations", "destination", "Destination", required: false);
        AddLegacySelectionStep(steps, candidate, "modes", "mode", "Mode", required: false);
        AddLegacySelectionStep(steps, candidate, "optionalCosts", "optionalCost", "Optional cost", required: false);
        return steps;
    }

    private static Godot.Collections.Dictionary PromptSelectionStep(JsonElement step)
    {
        var choices = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (step.TryGetProperty("choices", out var choiceElements)
            && choiceElements.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choiceElements.EnumerateArray())
            {
                choices.Add(PromptChoice(choice));
            }
        }

        return new Godot.Collections.Dictionary
        {
            ["role"] = ReadString(step, "role"),
            ["label"] = ReadString(step, "label"),
            ["required"] = ReadBool(step, "required"),
            ["choices"] = choices
        };
    }

    private static Godot.Collections.Dictionary PromptChoice(JsonElement choice)
    {
        var id = ReadString(choice, "id");
        var label = ReadString(choice, "label");
        return new Godot.Collections.Dictionary
        {
            ["id"] = id,
            ["label"] = string.IsNullOrWhiteSpace(label) ? id : label
        };
    }

    private static int? CandidateMetadataInt(JsonElement candidate, string propertyName)
    {
        if (MetadataElement(candidate) is not { ValueKind: JsonValueKind.Object } metadata
            || !metadata.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number,
            _ => null
        };
    }

    private static void AddLegacySelectionStep(
        Godot.Collections.Array<Godot.Collections.Dictionary> steps,
        JsonElement candidate,
        string propertyName,
        string role,
        string label,
        bool required)
    {
        if (!candidate.TryGetProperty(propertyName, out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return;
        }

        var normalizedChoices = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var choice in choices.EnumerateArray())
        {
            normalizedChoices.Add(PromptChoice(choice));
        }

        steps.Add(new Godot.Collections.Dictionary
        {
            ["role"] = role,
            ["label"] = label,
            ["required"] = required,
            ["choices"] = normalizedChoices
        });
    }

    private static string DirectSubmitKind(string action)
    {
        return action switch
        {
            "READY" => "ready",
            "SUBMIT_DECK" => "submitDeck",
            "PASS_PRIORITY" or "PASS_FOCUS" or "PASS" or "END_TURN" or "SURRENDER" => "command",
            _ => "unsupported"
        };
    }

    private static string DirectCommandType(string action)
    {
        return action switch
        {
            "PASS_PRIORITY" => "PASS_PRIORITY",
            "PASS_FOCUS" => "PASS_FOCUS",
            "PASS" => "PASS",
            "END_TURN" => "END_TURN",
            "SURRENDER" => "SURRENDER",
            _ => string.Empty
        };
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
            if (_isShuttingDown && ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }

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

    private static long? ReadOptionalLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(property.GetString(), out var number) => number,
            _ => null
        };
    }

    private static bool? ReadOptionalBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return property.GetBoolean();
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

    public void ApplyPrompt(Godot.Collections.Dictionary view)
    {
        if (_promptSummary is not null)
        {
            _promptSummary.Text = view.TryGetValue("summary", out var summary)
                ? summary.AsString()
                : "No prompt";
        }

        if (_promptActions is null)
        {
            return;
        }

        foreach (var child in _promptActions.GetChildren())
        {
            child.QueueFree();
        }

        var actions = view.TryGetValue("actions", out var actionsValue)
            ? actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        if (actions.Count == 0)
        {
            _promptActions.AddChild(new Label
            {
                Text = "No candidate actions"
            });
            return;
        }

        foreach (var action in actions)
        {
            _promptActions.AddChild(PromptActionNode(action));
        }
    }

    private Control PromptActionNode(Godot.Collections.Dictionary action)
    {
        var row = new VBoxContainer();
        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Action";
        var actionName = action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty;
        var enabled = action.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
        var reason = action.TryGetValue("reason", out var reasonValue) ? reasonValue.AsString() : string.Empty;
        var submitKind = action.TryGetValue("submitKind", out var submitKindValue) ? submitKindValue.AsString() : "unsupported";
        var cmdType = action.TryGetValue("cmdType", out var cmdTypeValue) ? cmdTypeValue.AsString() : string.Empty;
        var promptId = action.TryGetValue("promptId", out var promptIdValue) ? promptIdValue.AsString() : string.Empty;
        var snapshotTick = action.TryGetValue("snapshotTick", out var snapshotTickValue) ? snapshotTickValue.AsInt64() : -1L;
        var hasTemplate = action.TryGetValue("hasTemplate", out var hasTemplateValue) && hasTemplateValue.AsBool();
        if (string.Equals(actionName, "MULLIGAN", StringComparison.Ordinal))
        {
            return PromptMulliganActionNode(action);
        }

        if (string.Equals(actionName, "ORDER_TRIGGERS", StringComparison.Ordinal)
            || string.Equals(actionName, "ASSIGN_COMBAT_DAMAGE", StringComparison.Ordinal))
        {
            return PromptSpecialActionNode(action);
        }

        var canSubmit = enabled && (hasTemplate || !string.Equals(submitKind, "unsupported", StringComparison.Ordinal));
        var selectors = new List<PromptSelector>();

        if (hasTemplate
            && action.TryGetValue("selectionSteps", out var stepValue)
            && stepValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is { } steps)
        {
            foreach (var step in steps)
            {
                var selectorNode = PromptSelectionStepNode(step, enabled);
                selectors.Add(selectorNode.Selector);
                row.AddChild(selectorNode.Node);
            }
        }

        var button = new Button
        {
            Disabled = !canSubmit,
            Text = canSubmit ? label : $"{label} (choose)",
            TooltipText = string.IsNullOrWhiteSpace(reason) ? actionName : reason
        };
        button.Pressed += () =>
        {
            if (hasTemplate)
            {
                _ = SubmitPromptTemplateAsync(action, selectors);
            }
            else
            {
                _ = SubmitPromptActionAsync(submitKind, cmdType, promptId, snapshotTick, label);
            }
        };
        row.AddChild(button);
        row.AddChild(new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = $"{actionName} · {(enabled ? "enabled" : "disabled")} · {(hasTemplate ? "template" : submitKind)} · {reason}"
        });
        return row;
    }

    private Control PromptSpecialActionNode(Godot.Collections.Dictionary action)
    {
        var row = new VBoxContainer();
        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Prompt action";
        var actionName = action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty;
        var enabled = action.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
        var reason = action.TryGetValue("reason", out var reasonValue) ? reasonValue.AsString() : string.Empty;
        var canBuild = TryBuildSpecialPromptCommand(action, out _, out var payloadKey, out var buildReason);
        var button = new Button
        {
            Disabled = !enabled || !canBuild,
            Text = canBuild ? label : $"{label} (waiting for server metadata)",
            TooltipText = !canBuild ? buildReason : string.IsNullOrWhiteSpace(reason) ? actionName : reason
        };
        button.Pressed += () => _ = SubmitSpecialPromptAsync(action);
        row.AddChild(button);
        row.AddChild(new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = $"{actionName} · {(enabled ? "enabled" : "disabled")} · server metadata · {(canBuild ? payloadKey : buildReason)}"
        });
        return row;
    }

    private async Task SubmitSpecialPromptAsync(Godot.Collections.Dictionary action)
    {
        if (!TryBuildSpecialPromptCommand(action, out var payload, out _, out var reason))
        {
            AppendLog($"[color=yellow]Prompt action requires server metadata: {Escape(reason)}[/color]");
            return;
        }

        var actionName = action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : "special";
        await SubmitPromptPayloadAsync(action, payload, actionName.ToLowerInvariant());
    }

    private Control PromptMulliganActionNode(Godot.Collections.Dictionary action)
    {
        var row = new VBoxContainer();
        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Mulligan";
        var enabled = action.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
        var reason = action.TryGetValue("reason", out var reasonValue) ? reasonValue.AsString() : string.Empty;
        var minSelectionCount = action.TryGetValue("minSelectionCount", out var minValue) ? minValue.AsInt32() : -1;
        var maxSelectionCount = action.TryGetValue("maxSelectionCount", out var maxValue) ? maxValue.AsInt32() : -1;
        var choices = action.TryGetValue("sourceChoices", out var choicesValue)
            ? choicesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        var selectedObjectIds = new List<string>();
        var summary = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        var submit = new Button
        {
            TooltipText = string.IsNullOrWhiteSpace(reason) ? "Confirm mulligan" : reason
        };

        void Refresh()
        {
            var hasServerLimit = maxSelectionCount >= 0;
            var min = Math.Max(0, minSelectionCount);
            var canSubmit = enabled
                && hasServerLimit
                && selectedObjectIds.Count >= min
                && selectedObjectIds.Count <= maxSelectionCount;
            summary.Text = hasServerLimit
                ? $"{label} · selected {selectedObjectIds.Count} / {maxSelectionCount}"
                : $"{label} · waiting for server selection limit";
            submit.Text = "Confirm mulligan";
            submit.Disabled = !canSubmit;
        }

        row.AddChild(summary);
        if (choices.Count == 0)
        {
            row.AddChild(new Label
            {
                Text = "No server-provided mulligan choices."
            });
        }
        else
        {
            foreach (var choice in choices)
            {
                var choiceId = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
                var checkBox = new CheckBox
                {
                    Disabled = !enabled || string.IsNullOrWhiteSpace(choiceId),
                    Text = PromptChoiceText(choice)
                };
                checkBox.Toggled += pressed =>
                {
                    if (pressed)
                    {
                        if (maxSelectionCount >= 0 && selectedObjectIds.Count >= maxSelectionCount)
                        {
                            checkBox.SetPressedNoSignal(false);
                            return;
                        }

                        if (!selectedObjectIds.Contains(choiceId, StringComparer.Ordinal))
                        {
                            selectedObjectIds.Add(choiceId);
                        }
                    }
                    else
                    {
                        selectedObjectIds.RemoveAll(objectId => string.Equals(objectId, choiceId, StringComparison.Ordinal));
                    }

                    Refresh();
                };
                row.AddChild(checkBox);
            }
        }

        submit.Pressed += () => _ = SubmitMulliganAsync(action, selectedObjectIds.ToArray());
        row.AddChild(submit);
        row.AddChild(new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = $"MULLIGAN · {(enabled ? "enabled" : "disabled")} · {reason}"
        });
        Refresh();
        return row;
    }

    private static string PromptChoiceText(Godot.Collections.Dictionary choice)
    {
        var label = choice.TryGetValue("label", out var labelValue) ? labelValue.AsString() : string.Empty;
        var id = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
        if (string.IsNullOrWhiteSpace(label))
        {
            return id;
        }

        return string.IsNullOrWhiteSpace(id) || string.Equals(label, id, StringComparison.Ordinal)
            ? label
            : $"{label} ({id})";
    }

    private static PromptSelectorNode PromptSelectionStepNode(Godot.Collections.Dictionary step, bool enabled)
    {
        var role = step.TryGetValue("role", out var roleValue) ? roleValue.AsString() : string.Empty;
        var label = step.TryGetValue("label", out var labelValue) ? labelValue.AsString() : role;
        var required = step.TryGetValue("required", out var requiredValue) && requiredValue.AsBool();
        var choices = step.TryGetValue("choices", out var choicesValue)
            ? choicesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            CustomMinimumSize = new Vector2(76, 0),
            Text = required ? $"{label}*" : label
        });

        var selector = new OptionButton
        {
            Disabled = !enabled || choices.Count == 0,
            CustomMinimumSize = new Vector2(180, 0)
        };
        if (!required)
        {
            selector.AddItem("(none)");
            selector.SetItemMetadata(0, string.Empty);
        }

        foreach (var choice in choices)
        {
            var choiceId = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
            var choiceLabel = choice.TryGetValue("label", out var textValue) ? textValue.AsString() : choiceId;
            selector.AddItem(string.IsNullOrWhiteSpace(choiceLabel) ? choiceId : choiceLabel);
            selector.SetItemMetadata(selector.ItemCount - 1, choiceId);
        }

        if (choices.Count == 0)
        {
            selector.AddItem("(no choices)");
            selector.SetItemMetadata(selector.ItemCount - 1, string.Empty);
        }

        row.AddChild(selector);
        return new PromptSelectorNode(row, new PromptSelector(role, selector));
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

    private sealed record PromptSelector(string Role, OptionButton Control);

    private sealed record PromptSelectorNode(Control Node, PromptSelector Selector);

    private sealed record PromptSelection(
        string? SourceId,
        IReadOnlyList<string> TargetObjectIds,
        string? DestinationId,
        string? Mode,
        IReadOnlyList<string> OptionalCostIds)
    {
        public static PromptSelection Empty { get; } = new(
            null,
            Array.Empty<string>(),
            null,
            null,
            Array.Empty<string>());

        public static PromptSelection SourceOnly(string sourceId)
        {
            return new PromptSelection(
                sourceId,
                Array.Empty<string>(),
                null,
                null,
                Array.Empty<string>());
        }

        public static PromptSelection FromSelectors(IEnumerable<PromptSelector> selectors)
        {
            string? sourceId = null;
            string? destinationId = null;
            string? mode = null;
            var targets = new List<string>();
            var optionalCosts = new List<string>();

            foreach (var selector in selectors)
            {
                var selected = SelectedChoiceId(selector.Control);
                if (string.IsNullOrWhiteSpace(selected))
                {
                    continue;
                }

                switch (selector.Role)
                {
                    case "source":
                        sourceId ??= selected;
                        break;
                    case "target":
                        targets.Add(selected);
                        break;
                    case "destination":
                        destinationId ??= selected;
                        break;
                    case "mode":
                        mode ??= selected;
                        break;
                    case "optionalCost":
                        optionalCosts.Add(selected);
                        break;
                }
            }

            return new PromptSelection(sourceId, targets, destinationId, mode, optionalCosts);
        }

        private static string SelectedChoiceId(OptionButton control)
        {
            var selected = control.Selected;
            if (selected < 0 || selected >= control.ItemCount)
            {
                return string.Empty;
            }

            var metadata = control.GetItemMetadata(selected);
            return metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
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
