using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient;

public partial class Main : Control
{
    private const int AutoSmokePlayCardTapRuneLimit = 4;
    private const int AutoSmokeBoardActionLimit = 4;
    private const int AutoSmokeTempoActionLimit = 24;
    private const int ResultScreenshotFrameDelay = 12;
    private const string MatchmakingQueued = "QUEUED";
    private const string MatchmakingMatched = "MATCHED";
    private const string MatchmakingCancelled = "CANCELLED";
    private const string MatchmakingIdle = "IDLE";
    private const string MatchmakingRejected = "REJECTED";
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
    [Export] public bool AutoConnectOnReady { get; set; } = false;
    [Export] public string OfficialCatalogSnapshotPath { get; set; } = "res://data/card-catalog.zh-CN.json";

    private static readonly JsonSerializerOptions ClientJsonOptions = CreateClientJsonOptions();
    private readonly CancellationTokenSource _shutdown = new();
    private PlayerSessionStore _sessionStore = new();
    private readonly List<PreconstructedDeck> _decks = [];
    private readonly List<PublicMatchDto> _publicMatches = [];
    private readonly OfficialCardImageLoader _cardImageLoader = new();
    private readonly CardViewFactory _cardViewFactory;
    private readonly PromptInteractionController _promptInteractionController = new();
    private readonly object _promptHighlightLock = new();
    private readonly HashSet<string> _promptSourceObjectIds = new(StringComparer.Ordinal);

    private PlayerSessionSettings _session = PlayerSessionSettings.CreateDefault();
    private RichTextLabel? _log;
    private LobbyScreen? _lobbyScreen;
    private MatchScreen? _matchScreen;
    private CardInspectOverlay? _cardInspectOverlay;
    private ResultOverlay? _resultOverlay;
    private MulliganOverlay? _mulliganOverlay;
    private TriggerOrderOverlay? _triggerOrderOverlay;
    private DamageAssignmentOverlay? _damageAssignmentOverlay;
    private PlayCardOverlay? _playCardOverlay;
    private RuneActionPanel? _runeActionPanel;
    private Godot.Collections.Dictionary? _playCardAction;
    private CancellationTokenSource? _playCostPreviewCancellation;
    private MovementOverlay? _movementOverlay;
    private Godot.Collections.Dictionary? _movementAction;
    private BattleDeclarationOverlay? _battleDeclaration;
    private Godot.Collections.Dictionary? _battleDeclarationAction;
    private bool _promptSubmissionInFlight;
    private RiftboundGameHubClient? _hub;
    private string _authenticatedHandle = string.Empty;
    private string _visualScreenshotPath = string.Empty;
    private bool _autoSmoke;
    private bool _autoSmokeMulligan;
    private bool _autoSmokeTapRune;
    private bool _autoSmokePlayCard;
    private bool _autoSmokeFollowups;
    private bool _autoSmokeQuickMatch;
    private bool _autoSmokePublicMatch;
    private bool _autoSmokeJoinPublicMatch;
    private bool _autoSmokeSurrender;
    private bool _autoSmokePreviewFirstVisibleCard;
    private string _autoSmokeUiAction = string.Empty;
    private bool _autoSmokeUiSubmit;
    private bool _autoSmokeUiCompleted;
    private bool _autoSmokeSubmitted;
    private bool _visualScreenshotSaved;
    private bool _resultScreenshotSaved;
    private int _autoSmokeTapRuneSubmissions;
    private int _visualScreenshotMinTableCards = 1;
    private bool _autoSmokePlayCardSubmitted;
    private bool _autoSmokeSurrenderSubmitted;
    private bool _autoSmokePreviewRendered;
    private bool _matchFinished;
    private volatile bool _battleTableRendered;
    private bool _matchmakingWaiting;
    private bool _lobbyCanSubmitDeckFromPrompt;
    private bool _lobbyCanReadyFromPrompt;
    private bool _ephemeralSession;
    private bool _isShuttingDown;
    private int _snapshotRenderVersion;
    private WsServerMessage? _latestSnapshotMessage;
    private int _imageRefreshRequested;
    private double _imageRefreshElapsed;
    private string _lastJoinedMatchmakingRoom = string.Empty;
    private readonly HashSet<string> _autoSmokePromptSubmissions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _autoSmokeActionSubmissions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _autoSmokeUiStages = new(StringComparer.Ordinal);
    private readonly object _autoSmokePromptQueueLock = new();
    private Godot.Collections.Dictionary? _pendingAutoSmokePromptView;
    private bool _autoSmokePromptQueueRunning;
    private long _latestObservedPromptSnapshotTick = -1;
    private Task? _officialCatalogLoadTask;
    private IReadOnlyDictionary<string, CardCatalogEntry> _officialCatalog =
        new Dictionary<string, CardCatalogEntry>(StringComparer.Ordinal);
    private Godot.Collections.Array<Godot.Collections.Dictionary>? _lastSnapshotSections;
    private Godot.Collections.Dictionary? _lastAppliedPromptView;
    private Godot.Collections.Dictionary _lastViewerResult = new();

    public Main()
    {
        _cardViewFactory = new CardViewFactory(_cardImageLoader);
        _cardImageLoader.ImageAvailable += () => Interlocked.Exchange(ref _imageRefreshRequested, 1);
    }

    public override async void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--riftbound-play-card-proof"))
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/debug/PlayCardOverlayProof.tscn");
            return;
        }
        RenderingServer.SetDefaultClearColor(MinimalTheme.AppBackground);
        GetWindow().MinSize = new Vector2I(1280, 720);
        BindNodes();
        ApplyMinimalTheme();
        WireButtons();
        SetBattleChromeVisible(battleActive: false);
        var args = CommandLineArgs();
        ServerUrl = ArgValue(args, "--riftbound-server=") ?? ServerUrl;
        _autoSmoke = args.Contains("--riftbound-smoke-auto-ready");
        _autoSmokeMulligan = args.Contains("--riftbound-smoke-auto-mulligan");
        _autoSmokeTapRune = args.Contains("--riftbound-smoke-auto-tap-rune");
        _autoSmokePlayCard = args.Contains("--riftbound-smoke-auto-play-card");
        _autoSmokeFollowups = args.Contains("--riftbound-smoke-auto-followups");
        _autoSmokeQuickMatch = args.Contains("--riftbound-smoke-auto-quick-match");
        _autoSmokePublicMatch = args.Contains("--riftbound-smoke-auto-public-match");
        _autoSmokeJoinPublicMatch = args.Contains("--riftbound-smoke-auto-join-public-match");
        _autoSmokeSurrender = args.Contains("--riftbound-smoke-auto-surrender");
        _autoSmokePreviewFirstVisibleCard = args.Contains("--riftbound-smoke-preview-first-card");
        _autoSmokeUiAction = (ArgValue(args, "--riftbound-smoke-ui-action=") ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
        _autoSmokeUiSubmit = args.Contains("--riftbound-smoke-ui-submit");
        _visualScreenshotPath = ArgValue(args, "--riftbound-visual-screenshot=") ?? string.Empty;
        _visualScreenshotMinTableCards = Math.Max(
            0,
            ArgInt(args, "--riftbound-visual-screenshot-min-table-cards=", _visualScreenshotMinTableCards));
        _ephemeralSession = args.Contains("--riftbound-ephemeral-session");
        var sessionFile = ArgValue(args, "--riftbound-session-file=");
        if (!string.IsNullOrWhiteSpace(sessionFile))
        {
            _sessionStore = new PlayerSessionStore(sessionFile);
        }

        AppendLog("Client booted. Waiting for server authority.");

        _session = _ephemeralSession
            ? PlayerSessionSettings.CreateDefault()
            : await _sessionStore.LoadAsync();
        _session = ApplyCommandLineOverrides(_session, args);
        ServerUrl = ArgValue(args, "--riftbound-server=") ?? _session.ServerUrl ?? ServerUrl;
        ApplySessionToInputs();
        _officialCatalogLoadTask = LoadOfficialCatalogAsync();
        _ = LoadDecksAsync();
        _ = LoadPublicMatchesAsync();

        if ((AutoConnectOnReady || _autoSmoke) && !_autoSmokeQuickMatch && !_autoSmokePublicMatch && !_autoSmokeJoinPublicMatch)
        {
            await ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        }

        if (_autoSmokePublicMatch)
        {
            await CreatePublicMatchAsync();
        }

        if (_autoSmokeQuickMatch)
        {
            await QueueMatchmakingAsync();
        }

        if (_autoSmokeJoinPublicMatch)
        {
            await JoinFirstPublicMatchSmokeAsync();
        }
    }

    public override void _ExitTree()
    {
        _isShuttingDown = true;
        _shutdown.Cancel();
        _playCostPreviewCancellation?.Dispose();
        ReleaseRuntimeUiResources();
        _ = DisconnectAsync();
        _shutdown.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Echo: true } || !HandleKeyboardAction(input))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    private bool HandleKeyboardAction(InputEvent input)
    {
        if (_runeActionPanel?.Visible == true)
        {
            if (input.IsActionPressed("ui_cancel_selection")) _runeActionPanel.Cancel();
            return input.IsActionPressed("ui_cancel_selection");
        }
        if (_battleDeclaration?.Visible == true)
        {
            if (input.IsActionPressed("ui_cancel_selection") && !_battleDeclaration.IsSubmitting) _battleDeclaration.Hide();
            return input.IsActionPressed("ui_cancel_selection");
        }
        if (input.IsActionPressed("ui_cancel_selection")) _matchScreen?.InvalidateTableGesture();
        if (_playCardOverlay?.IsVisibleInTree() == true)
        {
            if (input.IsActionPressed("ui_cancel_selection") && !_playCardOverlay.IsSubmitting) _playCardOverlay.Hide();
            return input.IsActionPressed("ui_cancel_selection");
        }
        if (_movementOverlay?.IsVisibleInTree() == true)
        {
            if (input.IsActionPressed("ui_cancel_selection") && !_movementOverlay.IsSubmitting) _movementOverlay.Hide();
            return input.IsActionPressed("ui_cancel_selection");
        }
        if (_cardInspectOverlay?.IsVisibleInTree() == true)
        {
            if (input.IsActionPressed("ui_cancel_selection"))
            {
                _cardInspectOverlay.HideCard();
                return true;
            }

            return false;
        }

        if (_resultOverlay?.IsVisibleInTree() == true)
        {
            return false;
        }

        if (_mulliganOverlay?.IsVisibleInTree() == true)
        {
            if (input.IsActionPressed("ui_cancel_selection"))
            {
                _mulliganOverlay.ResetSelection();
                return true;
            }

            return input.IsActionPressed("ui_confirm_action") && _mulliganOverlay.ConfirmCurrent();
        }

        if (_triggerOrderOverlay?.IsVisibleInTree() == true)
        {
            if (input.IsActionPressed("ui_cancel_selection"))
            {
                _triggerOrderOverlay.ResetSelection();
                return true;
            }

            return input.IsActionPressed("ui_confirm_action") && _triggerOrderOverlay.ConfirmCurrent();
        }

        if (_damageAssignmentOverlay?.IsVisibleInTree() == true)
        {
            if (input.IsActionPressed("ui_cancel_selection"))
            {
                _damageAssignmentOverlay.ResetSelection();
                return true;
            }

            return input.IsActionPressed("ui_confirm_action") && _damageAssignmentOverlay.ConfirmCurrent();
        }

        if (input.IsActionPressed("ui_inspect_card")
            && GetViewport().GuiGetFocusOwner() is OfficialCardView focusedCard
            && focusedCard.TryGetVisibleCard(out var card))
        {
            ApplyCardPreview(card);
            return true;
        }

        if (_matchScreen?.IsVisibleInTree() != true)
        {
            return false;
        }

        if (input.IsActionPressed("ui_cancel_selection"))
        {
            return _matchScreen.ActionBar.CancelCurrent();
        }

        if (input.IsActionPressed("ui_confirm_action"))
        {
            return _matchScreen.ActionBar.ConfirmCurrent();
        }

        if (input.IsActionPressed("ui_action_previous"))
        {
            return _matchScreen.ActionBar.FocusAdjacentAction(-1);
        }

        return input.IsActionPressed("ui_action_next")
            && _matchScreen.ActionBar.FocusAdjacentAction(1);
    }

    private void BindNodes()
    {
        _log = GetNode<RichTextLabel>("Log");
        _lobbyScreen = GetNode<LobbyScreen>("LobbyScreen");
        _matchScreen = GetNode<MatchScreen>("MatchScreen");
        _cardInspectOverlay = GetNode<CardInspectOverlay>("CardInspectOverlay");
        _resultOverlay = GetNode<ResultOverlay>("ResultOverlay");
        _mulliganOverlay = GetNode<MulliganOverlay>("MulliganOverlay");
        _triggerOrderOverlay = GetNode<TriggerOrderOverlay>("TriggerOrderOverlay");
        _damageAssignmentOverlay = GetNode<DamageAssignmentOverlay>("DamageAssignmentOverlay");
        _damageAssignmentOverlay.CardViewFor = id => VisibleTableCardView(id, includeOpponents: true);
        _damageAssignmentOverlay.CardInspectionRequested += ApplyCardPreview;
    }

    private void ApplyMinimalTheme()
    {
        MinimalTheme.Apply(this);

        if (_lobbyScreen is not null)
        {
            _lobbyScreen.ApplyTheme();
        }

        if (_matchScreen is not null)
        {
            _matchScreen.ApplyTheme();
        }

        _cardInspectOverlay?.ApplyTheme();
        _resultOverlay?.ApplyTheme();
        _mulliganOverlay?.ApplyTheme();
        _triggerOrderOverlay?.ApplyTheme();
        _damageAssignmentOverlay?.ApplyTheme();
    }

    private void WireButtons()
    {
        _battleDeclaration = new BattleDeclarationOverlay(); _matchScreen!.ComposerHost.AddChild(_battleDeclaration);
        _battleDeclaration.VisibilityChanged += RefreshTableComposer;
        _battleDeclaration.Confirmed += payload =>
        {
            if (_battleDeclarationAction is not null) _ = SubmitTableActionAsync(_battleDeclarationAction, payload, "declare_battle");
        };
        _playCardOverlay = new PlayCardOverlay { TableMode = true }; _matchScreen!.ComposerHost.AddChild(_playCardOverlay);
        _runeActionPanel = new RuneActionPanel(); _matchScreen.ComposerHost.AddChild(_runeActionPanel);
        _runeActionPanel.SelectionChanged += RefreshTableComposer;
        _runeActionPanel.VisibilityChanged += RefreshTableComposer;
        _runeActionPanel.Requested += (action, ids) => _ = SubmitRuneActionAsync(action, ids);
        _playCardOverlay.VisibilityChanged += RefreshTableComposer;
        _playCardOverlay.TableSelectionChanged += RefreshPromptInteractionVisuals;
        _playCardOverlay.PreviewRequested += request => _ = RequestPlayCostPreviewAsync(request);
        _playCardOverlay.Confirmed += payload =>
        {
            if (_playCardAction is not null) _ = SubmitTableActionAsync(_playCardAction, payload, "play_card");
        };
        _movementOverlay = new MovementOverlay { TableMode = true }; _matchScreen!.ComposerHost.AddChild(_movementOverlay);
        _movementOverlay.VisibilityChanged += RefreshTableComposer;
        _movementOverlay.TableSelectionChanged += RefreshPromptInteractionVisuals;
        _movementOverlay.Confirmed += payload =>
        {
            if (_movementAction is not null) _ = SubmitTableActionAsync(_movementAction, payload, "move_units");
        };
        _lobbyScreen!.ConnectRequested += () => _ = ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        _lobbyScreen.ReconnectRequested += () => _ = ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        _lobbyScreen.CreatePublicMatchRequested += () => _ = CreatePublicMatchAsync();
        _lobbyScreen.QueueRequested += () => _ = QueueMatchmakingAsync();
        _lobbyScreen.CancelQueueRequested += () => _ = CancelMatchmakingAsync();
        _lobbyScreen.JoinPublicMatchRequested += () => _ = JoinSelectedPublicMatchAsync();
        _lobbyScreen.SubmitDeckRequested += () => _ = SubmitSelectedDeckAsync();
        _lobbyScreen.ReadyRequested += () => _ = ReadyAsync();
        _lobbyScreen.RefreshPublicMatchesRequested += () => _ = LoadPublicMatchesAsync();
        _lobbyScreen.DeckSelectionChanged += () => _ = RefreshDeckPreviewAsync();
        _matchScreen!.CardActivated += HandleMatchCardActivated;
        _matchScreen.RuneRecycleRequested += id =>
        {
            if (_playCardOverlay?.Visible != true && _movementOverlay?.Visible != true && _battleDeclaration?.Visible != true)
                _runeActionPanel?.Request("RECYCLE_RUNE", [id]);
        };
        _matchScreen.RuneBatchRequested += () => HandlePromptActionSelected("TAP_RUNE");
        _matchScreen.CardInspectionRequested += ApplyCardPreview;
        _matchScreen.DestinationActivated += HandleTableDestination;
        _matchScreen.TableDragRequested = card =>
        {
            if (_promptSubmissionInFlight || _runeActionPanel?.Visible == true || _playCardOverlay?.IsSubmitting == true || _movementOverlay?.IsSubmitting == true || _battleDeclaration?.Visible == true) return false;
            var id = card.TryGetValue("objectId", out var value) ? value.AsString() : "";
            if (_playCardOverlay?.Visible == true && _playCardOverlay.TableSelectedObjects.FirstOrDefault() == id) return true;
            if (_movementOverlay?.Visible == true && _movementOverlay.TableSelectedObjects.Contains(id)) return true;
            _playCardOverlay?.Hide(); _movementOverlay?.Hide();
            return TryOpenPlayCard(id) || TryOpenMovement(id);
        };
        _matchScreen.PublicPileRequested += (title, cards) => _cardInspectOverlay?.ShowPile(title, cards);
        _matchScreen.ActionBar.ActionSelected += HandlePromptActionSelected;
        _matchScreen.ReconnectRequested += () => _ = RetryConnectionAsync();
        _matchScreen.ReturnToLobbyRequested += () => _ = ReturnToLobbyAsync();
        _matchScreen.ActionBar.ChoiceSelected += HandlePromptChoiceSelected;
        _matchScreen.ActionBar.CancelRequested += _promptInteractionController.ClearSelection;
        _matchScreen.ActionBar.SubmitRequested += state => _ = SubmitPromptSelectionAsync(state);
        _promptInteractionController.SelectionChanged += HandlePromptSelectionChanged;
        _promptInteractionController.SelectionCleared += HandlePromptSelectionCleared;
        _mulliganOverlay!.Confirmed += sourceIds => _ = SubmitCurrentMulliganAsync(sourceIds);
        _mulliganOverlay.Cancelled += ReopenSpecialPromptOverlay;
        _triggerOrderOverlay!.Confirmed += triggerIds => _ = SubmitCurrentTriggerOrderAsync(triggerIds);
        _triggerOrderOverlay.Cancelled += ReopenSpecialPromptOverlay;
        _damageAssignmentOverlay!.Confirmed += assignments => _ = SubmitCurrentDamageAssignmentsAsync(assignments);
        _damageAssignmentOverlay.Cancelled += ReopenSpecialPromptOverlay;
        _resultOverlay!.ReturnLobbyRequested += () => _ = ReturnToLobbyAsync();
    }

    private void HandleMatchCardActivated(Godot.Collections.Dictionary card)
    {
        if (_promptSubmissionInFlight || _runeActionPanel?.IsSubmitting == true) return;
        if (_battleDeclaration?.Visible == true) { _matchScreen?.PreviewCard(card); return; }
        var objectId = card.TryGetValue("objectId", out var objectValue)
            ? objectValue.AsString()
            : string.Empty;
        if (_playCardOverlay?.Visible == true)
        {
            if (!_playCardOverlay.TrySelectTableObject(objectId)) _matchScreen?.PreviewCard(card);
            return;
        }
        if (_movementOverlay?.Visible == true)
        {
            if (!_movementOverlay.TryToggleTableSource(objectId)) _matchScreen?.PreviewCard(card);
            return;
        }
        if (_runeActionPanel?.OwnsSource(objectId) == true)
        {
            if (_runeActionPanel.Visible || Input.IsKeyPressed(Key.Shift)) _runeActionPanel.Toggle(objectId);
            else if (_runeActionPanel.TapSources.Contains(objectId)) _runeActionPanel.Request("TAP_RUNE", [objectId]);
            else _matchScreen?.PreviewCard(card);
            return;
        }
        if (!string.IsNullOrWhiteSpace(objectId) && _promptInteractionController.Current is null)
        {
            if (TryOpenPlayCard(objectId)) return;
            var options = _promptInteractionController.ActionsForObject(objectId);
            if (options.Count > 1)
            {
                _matchScreen?.ShowCardActions(card, options.Select(option => (option.Label, (Action)(() => SelectTableAction(option.Name, objectId)))));
                return;
            }
            if (options.Count == 1) { SelectTableAction(options[0].Name, objectId); return; }
        }
        if (!string.IsNullOrWhiteSpace(objectId) && _promptInteractionController.TrySelectObject(objectId)) return;
        _matchScreen?.PreviewCard(card);
    }

    private void SelectTableAction(string action, string objectId)
    {
        if (action == "MOVE_UNIT" && TryOpenMovement(objectId)) return;
        if (_promptInteractionController.SelectAction(action)) _promptInteractionController.TrySelectSource(objectId);
    }

    private void HandleTableDestination(string id)
    {
        if (_playCardOverlay?.Visible == true) { _playCardOverlay.TrySelectTableDestination(id); return; }
        if (_movementOverlay?.Visible == true) { _movementOverlay.TrySelectTableDestination(id); return; }
        _promptInteractionController.TrySelectChoice("destination", id);
    }

    private void RefreshTableComposer()
    {
        var composing = _runeActionPanel?.Visible == true || _playCardOverlay?.Visible == true || _movementOverlay?.Visible == true || _battleDeclaration?.Visible == true;
        _matchScreen?.SetComposerVisible(composing);
        _matchScreen?.ActionBar.SetComposerActive(composing);
        RefreshPromptInteractionVisuals();
    }

    private async Task RequestPlayCostPreviewAsync(PlayCostPreviewRequestDto request)
    {
        _playCostPreviewCancellation?.Cancel();
        _playCostPreviewCancellation?.Dispose();
        _playCostPreviewCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var token = _playCostPreviewCancellation.Token;
        try
        {
            await Task.Delay(120, token);
            if (!IsConnected() || _hub is null) throw new InvalidOperationException("连接尚未恢复。");
            var quote = await _hub.PreviewPlayCardAsync(_session.RoomId, request, token).WaitAsync(TimeSpan.FromSeconds(10), token);
            if (!token.IsCancellationRequested) QueueMainThread(nameof(ApplyPlayCostQuote), JsonSerializer.Serialize(quote));
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!token.IsCancellationRequested)
                QueueMainThread(nameof(ApplyPlayCostQuote), JsonSerializer.Serialize(PlayCostQuoteDto.Rejected(request,
                    request.SnapshotTick, "PREVIEW_UNAVAILABLE", "费用暂时无法核对，请检查连接后重新选择。")));
        }
    }

    public void ApplyPlayCostQuote(string json)
    {
        if (JsonSerializer.Deserialize<PlayCostQuoteDto>(json) is { } quote) _playCardOverlay?.ApplyQuote(quote);
    }

    private bool TryOpenPlayCard(string? sourceId = null)
    {
        if (!TryGetCurrentSpecialAction("PLAY_CARD", out var action)) return false;
        using var document = JsonDocument.Parse(action["candidateJson"].AsString());
        var privateSources = new Dictionary<string,string>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("metadata", out var metadata)
            && metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("sourceRequirements", out var requirements))
            foreach (var requirement in requirements.EnumerateArray())
                if (ReadString(requirement, "sourceZone") == "MAIN_DECK")
                    privateSources[ReadString(requirement, "sourceObjectId")] = ReadString(requirement, "cardNo");
        Godot.Collections.Dictionary? CardView(string objectId)
        {
            if (VisibleTableCardView(objectId, includeOpponents: true) is { } visible) return visible;
            if (!privateSources.TryGetValue(objectId, out var cardNo)) return null;
            // Only the owner's server-authorized effect-play sources may reveal a
            // deck card. Cached rendering never waits for the image CDN.
            var card = _cardViewFactory.BuildAsync(new SnapshotCardRef(objectId, cardNo, true, false),
                _officialCatalog, _shutdown.Token, waitForImage: false);
            return card.IsCompletedSuccessfully ? card.Result.ToGodotDictionary() : null;
        }
        if (_playCardOverlay?.Open(document.RootElement, action["promptId"].AsString(),
                action["snapshotTick"].AsInt64(), CardView, sourceId) != true) return false;
        _playCardAction = action;
        _promptInteractionController.ClearSelection();
        return true;
    }

    private void HandlePromptActionSelected(string actionName)
    {
        if (_promptSubmissionInFlight) return;
        _playCardOverlay?.Hide(); _movementOverlay?.Hide(); _battleDeclaration?.Hide();
        _runeActionPanel?.Cancel();
        if (actionName is "TAP_RUNE" or "RECYCLE_RUNE") { _promptInteractionController.ClearSelection(); _runeActionPanel?.Open(); return; }
        if (actionName == "DECLARE_BATTLE" && TryGetCurrentSpecialAction(actionName, out var battleAction))
        {
            using var battle = JsonDocument.Parse(battleAction["candidateJson"].AsString());
            string CardName(string id) => VisibleTableCardView(id, true) is { } view && view.TryGetValue("cardName", out var name) ? name.AsString() : "公开卡牌";
            if (_battleDeclaration?.Open(battle.RootElement, battleAction["promptId"].AsString(), battleAction["snapshotTick"].AsInt64(), CardName) == true)
            { _battleDeclarationAction = battleAction; _promptInteractionController.ClearSelection(); return; }
        }
        if (actionName == "PLAY_CARD" && TryOpenPlayCard()) return;
        if (actionName == "MOVE_UNIT" && TryOpenMovement()) return;
        if (!_promptInteractionController.SelectAction(actionName)) return;
        if (actionName is "PASS_PRIORITY" or "PASS_FOCUS" or "END_TURN"
            && _promptInteractionController.Current is { CanSubmit: true } state)
            _ = SubmitPromptSelectionAsync(state);
    }

    private bool TryOpenMovement(string? sourceId = null)
    {
        if (!TryGetCurrentSpecialAction("MOVE_UNIT", out var action)) return false;
        using var document = JsonDocument.Parse(action["candidateJson"].AsString());
        if (_movementOverlay?.Open(document.RootElement, action["promptId"].AsString(), action["snapshotTick"].AsInt64(),
            VisibleMovementCardView, MovementDestinationLabel, sourceId) != true) return false;
        _movementAction = action; _promptInteractionController.ClearSelection(); RefreshTableComposer(); return true;
    }

    private string MovementDestinationLabel(string label)
    {
        var cardNo = label.Split(" / ", StringSplitOptions.None)[0];
        return _officialCatalog.TryGetValue(cardNo, out var entry) ? entry.CardName
            : label.Contains(" / ", StringComparison.Ordinal) ? "战场" : label;
    }

    private Godot.Collections.Dictionary? VisibleMovementCardView(string objectId)
        => VisibleTableCardView(objectId, includeOpponents: false);

    private Godot.Collections.Dictionary? VisibleTableCardView(string objectId, bool includeOpponents)
    {
        if (_lastSnapshotSections is null) return null;
        foreach (var section in _lastSnapshotSections)
        {
            if (!section.TryGetValue("kind", out var kind) || kind.AsString() != "wireTable") continue;
            var zones = new List<Godot.Collections.Dictionary> { section["self"].AsGodotDictionary() };
            if (includeOpponents) zones.Add(section["opponent"].AsGodotDictionary());
            zones.AddRange(section["lanes"].As<Godot.Collections.Array<Godot.Collections.Dictionary>>());
            foreach (var zone in zones)
                foreach (var key in includeOpponents
                    ? new[] { "base", "baseRunes", "hand", "legend", "hero", "graveyard", "banished", "selfUnits", "opponentUnits", "site", "selfStandby", "opponentStandby" }
                    : new[] { "base", "selfUnits" })
                    if (zone.TryGetValue(key, out var cards))
                        foreach (var card in cards.As<Godot.Collections.Array<Godot.Collections.Dictionary>>())
                            if (card.TryGetValue("objectId", out var id) && id.AsString() == objectId
                                && (!card.TryGetValue("visible", out var visible) || visible.AsBool())
                                && (!card.TryGetValue("faceDown", out var faceDown) || !faceDown.AsBool())) return card;
        }
        return null;
    }

    private void HandlePromptChoiceSelected(string role, string choiceId)
    {
        _promptInteractionController.TrySelectChoice(role, choiceId);
    }

    private async Task SubmitCurrentMulliganAsync(IReadOnlyList<string> sourceIds)
    {
        if (TryGetCurrentSpecialAction("MULLIGAN", out var action))
        {
            await SubmitMulliganAsync(action, sourceIds);
        }
    }

    private async Task SubmitCurrentTriggerOrderAsync(IReadOnlyList<string> triggerIds)
    {
        if (!TryGetCurrentSpecialAction("ORDER_TRIGGERS", out var action))
        {
            return;
        }

        if (!SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action, triggerIds, out var payload, out _, out var reason))
        {
            AppendLog($"[color=yellow]Prompt action requires server metadata: {Escape(reason)}[/color]");
            return;
        }

        await SubmitSpecialPromptAsync(action, payload, "order_triggers");
    }

    private async Task SubmitCurrentDamageAssignmentsAsync(IReadOnlyList<DamageAssignmentSelection> assignments)
    {
        if (!TryGetCurrentSpecialAction("ASSIGN_COMBAT_DAMAGE", out var action))
        {
            return;
        }

        if (!SpecialPromptCommandBuilder.TryBuildDamageAssignmentPayload(action, assignments, out var payload, out _, out var reason))
        {
            AppendLog($"[color=yellow]Prompt action requires server metadata: {Escape(reason)}[/color]");
            return;
        }

        await SubmitSpecialPromptAsync(action, payload, "assign_combat_damage");
    }

    private bool TryGetCurrentSpecialAction(string actionName, out Godot.Collections.Dictionary action)
    {
        action = new Godot.Collections.Dictionary();
        if (_lastAppliedPromptView is null
            || !_lastAppliedPromptView.TryGetValue("actions", out var actionsValue)
            || actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } actions)
        {
            return false;
        }

        foreach (var candidate in actions)
        {
            if (string.Equals(ReadActionName(candidate), actionName, StringComparison.Ordinal)
                && candidate.TryGetValue("enabled", out var enabledValue)
                && enabledValue.AsBool())
            {
                action = candidate.Duplicate(true);
                return true;
            }
        }

        return false;
    }

    private static string ReadActionName(Godot.Collections.Dictionary action)
    {
        return action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty;
    }

    private void HandlePromptSelectionChanged(PromptSelectionState state)
    {
        if (_matchScreen is null)
        {
            return;
        }

        _matchScreen.ActionBar.ShowSelection(
            state,
            FriendlyPromptChoices(),
            _promptInteractionController.CurrentStepLabel,
            _promptInteractionController.CurrentStepRequired);
        RefreshPromptInteractionVisuals();
    }

    private void HandlePromptSelectionCleared()
    {
        _matchScreen?.ActionBar.ClearSelectionDisplay();
        RefreshPromptInteractionVisuals();
    }

    private IReadOnlyList<PromptChoice> FriendlyPromptChoices() => _promptInteractionController.CurrentChoices
        .Select(choice => _officialCatalog.TryGetValue(choice.Label, out var entry)
            ? choice with { Label = entry.CardName } : choice).ToArray();

    private void TryStageAutoSmokeUiAction()
    {
        if (string.IsNullOrWhiteSpace(_autoSmokeUiAction)
            || _autoSmokeUiCompleted
            || !_battleTableRendered
            || _matchFinished
            || !_promptInteractionController.Actions.Any(action =>
                action.Enabled
                && string.Equals(action.Name, _autoSmokeUiAction, StringComparison.Ordinal)))
        {
            return;
        }

        var stageKey = $"{_promptInteractionController.PromptId}:{_promptInteractionController.SnapshotTick}:{_autoSmokeUiAction}";
        if (_autoSmokeUiStages.Contains(stageKey))
        {
            return;
        }

        if (!_promptInteractionController.SelectAction(_autoSmokeUiAction))
        {
            return;
        }

        var selectionCount = 0;
        while (_promptInteractionController.CurrentChoices.Count > 0 && selectionCount < 12)
        {
            var choice = _promptInteractionController.CurrentChoices[0];
            if (!_promptInteractionController.TrySelectChoice(choice.Role, choice.Id))
            {
                break;
            }

            selectionCount++;
            if (!_autoSmokeUiSubmit)
            {
                break;
            }
        }

        _autoSmokeUiStages.Add(stageKey);
        _autoSmokeUiCompleted = true;
        var state = _promptInteractionController.Current;
        AppendLog(
            $"UI smoke staged {Escape(_autoSmokeUiAction)} with {selectionCount} server choice(s); "
            + $"canSubmit={state?.CanSubmit == true} submit={_autoSmokeUiSubmit}.");
        if (_autoSmokeUiSubmit && state is { CanSubmit: true })
        {
            _ = SubmitPromptSelectionAsync(state);
        }
    }

    private async Task SubmitPromptSelectionAsync(PromptSelectionState state)
    {
        if (_promptSubmissionInFlight) return;
        var current = _promptInteractionController.Current;
        var action = _promptInteractionController.CurrentActionDictionary();
        if (current is null
            || action is null
            || !current.CanSubmit
            || !string.Equals(current.PromptId, state.PromptId, StringComparison.Ordinal)
            || current.SnapshotTick != state.SnapshotTick
            || !string.Equals(current.ActionName, state.ActionName, StringComparison.Ordinal))
        {
            return;
        }

        _promptSubmissionInFlight = true;
        _matchScreen?.ActionBar.SetPending(true);
        try
        {
            var hasTemplate = action.TryGetValue("hasTemplate", out var templateValue)
                && templateValue.AsBool();
            if (hasTemplate)
            {
                await SubmitPromptTemplateAsync(
                    action,
                    new PromptSelection(
                        state.SourceId,
                        state.TargetIds,
                        state.DestinationId,
                        state.Mode,
                        state.OptionalCostIds));
            }
            else
            {
                var submitKind = action.TryGetValue("submitKind", out var submitValue)
                    ? submitValue.AsString()
                    : "unsupported";
                var cmdType = action.TryGetValue("cmdType", out var commandValue)
                    ? commandValue.AsString()
                    : string.Empty;
                var label = action.TryGetValue("label", out var labelValue)
                    ? labelValue.AsString()
                    : state.ActionName;
                await SubmitPromptActionAsync(
                    submitKind,
                    cmdType,
                    state.PromptId,
                    state.SnapshotTick,
                    label);
            }
        }
        finally
        {
            _promptSubmissionInFlight = false;
            _matchScreen?.ActionBar.SetPending(false);
            _promptInteractionController.ClearSelection();
        }
    }

    private void RefreshPromptInteractionVisuals()
    {
        if (_matchScreen is null)
        {
            return;
        }

        _matchScreen.ClearPromptStates();
        if (_runeActionPanel is { } runes)
            _matchScreen.SetRuneActions(runes.TapSources, runes.RecycleSources,
                runes.IsSubmitting || _promptSubmissionInFlight || _playCardOverlay?.Visible == true || _movementOverlay?.Visible == true || _battleDeclaration?.Visible == true);
        if (_runeActionPanel?.Visible == true)
        {
            foreach (var id in _runeActionPanel.RecycleSources.Concat(_runeActionPanel.TapSources).Distinct()) _matchScreen.SetObjectState(id, OfficialCardVisualState.Selectable);
            foreach (var id in _runeActionPanel.Selected) _matchScreen.SetObjectState(id, OfficialCardVisualState.Selected);
            return;
        }
        if (_playCardOverlay?.Visible == true)
        {
            foreach (var id in _playCardOverlay.TableTargets) _matchScreen.SetObjectState(id, OfficialCardVisualState.LegalTarget);
            foreach (var id in _playCardOverlay.TableSelectedObjects) _matchScreen.SetObjectState(id, OfficialCardVisualState.Selected);
            _matchScreen.SetDestinationChoices(_playCardOverlay.TableDestinations, _playCardOverlay.TableDestination);
            _matchScreen.SetSelectionLinks(_playCardOverlay.TableSelectedObjects, _playCardOverlay.TableDestination);
            return;
        }
        if (_movementOverlay?.Visible == true)
        {
            foreach (var id in _movementOverlay.TableSources) _matchScreen.SetObjectState(id, OfficialCardVisualState.Selectable);
            foreach (var id in _movementOverlay.TableSelectedObjects) _matchScreen.SetObjectState(id, OfficialCardVisualState.Selected);
            _matchScreen.SetDestinationChoices(_movementOverlay.TableDestinations, _movementOverlay.TableDestination);
            _matchScreen.SetSelectionLinks(_movementOverlay.TableSelectedObjects, _movementOverlay.TableDestination);
            return;
        }
        _matchScreen.SetDestinationChoices(_promptInteractionController.CurrentStepRole == "destination"
            ? _promptInteractionController.CurrentChoices.Select(choice => choice.Id) : [], _promptInteractionController.Current?.DestinationId);
        var nextState = _promptInteractionController.CurrentStepRole == "source"
            ? OfficialCardVisualState.Selectable
            : OfficialCardVisualState.LegalTarget;
        foreach (var objectId in _promptInteractionController.SelectableObjectIds())
        {
            _matchScreen.SetObjectState(objectId, nextState);
        }

        foreach (var objectId in _promptInteractionController.SelectedObjectIds())
        {
            _matchScreen.SetObjectState(objectId, OfficialCardVisualState.Selected);
        }
    }

    private void ReleaseRuntimeUiResources()
    {
        ReleaseTextureReferences(this);
    }

    private static void ReleaseTextureReferences(Node? node)
    {
        if (node is null)
        {
            return;
        }

        if (node is TextureRect textureRect)
        {
            textureRect.Texture = null;
        }

        foreach (var child in node.GetChildren())
        {
            ReleaseTextureReferences(child);
        }
    }

    private static void ClearNodeChildren(Node? node)
    {
        if (node is null)
        {
            return;
        }

        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.Free();
        }
    }

    private void ApplySessionToInputs()
    {
        if (_lobbyScreen is not null)
        {
            _lobbyScreen.HandleText = _session.Handle;
            _lobbyScreen.RoomText = _session.RoomId;
            _lobbyScreen.ServerText = ServerUrl;
        }
    }

    private PlayerSessionSettings ReadSessionFromInputs()
    {
        var handle = _lobbyScreen?.HandleText.Trim() ?? PlayerSessionSettings.DefaultHandle;
        var room = _lobbyScreen?.RoomText.Trim() ?? PlayerSessionSettings.DefaultRoomId;
        if (string.IsNullOrWhiteSpace(handle))
        {
            handle = PlayerSessionSettings.DefaultHandle;
        }

        if (string.IsNullOrWhiteSpace(room))
        {
            room = PlayerSessionSettings.DefaultRoomId;
        }

        return PlayerSessionSettings.WithConnectionTarget(_session, handle, room,
            _lobbyScreen?.ServerText.Trim().TrimEnd('/') ?? ServerUrl);
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

    private async Task LoadPublicMatchesAsync()
    {
        try
        {
            var matches = await new RiftboundApiClient(ServerUrl).GetPublicMatchesAsync(_shutdown.Token);
            _publicMatches.Clear();
            _publicMatches.AddRange(matches);
            QueueMainThread(nameof(ApplyPublicMatchOptions));
            AppendLog($"Public matches loaded: {_publicMatches.Count}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Unable to load public matches: {Escape(ex.Message)}[/color]");
        }
    }

    private async Task LoadOfficialCatalogAsync()
    {
        try
        {
            var catalog = await new OfficialCardCatalogService()
                .LoadSnapshotAsync(OfficialCatalogSnapshotPath, _shutdown.Token);
            _officialCatalog = catalog;
            AppendLog($"Official catalog loaded: {catalog.Count} cards.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Official card catalog unavailable: {Escape(ex.Message)}[/color]");
        }
    }

    private async Task ConnectAndRequestSnapshotAsync(bool useReconnectToken)
    {
        ResetLobbyPromptState();
        try
        {
            if (!await EnsureAuthenticatedConnectionAsync())
            {
                return;
            }

            await JoinCurrentRoomAndRequestSnapshotAsync(useReconnectToken);
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

    private async Task RetryConnectionAsync()
    {
        try
        {
            await DisconnectAsync();
            await ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        }
        catch (Exception ex)
        {
            SetStatus("Connection error");
            AppendLog($"Reconnect failed: {Escape(ex.Message)}");
        }
    }

    private async Task<bool> EnsureAuthenticatedConnectionAsync()
    {
        _session = PlayerSessionSettings.WithUsableKey(ReadSessionFromInputs());
        if (!Uri.TryCreate(_session.ServerUrl, UriKind.Absolute, out var serverUri)
            || (serverUri.Scheme != "http" && serverUri.Scheme != "https")
            || !string.IsNullOrEmpty(serverUri.UserInfo) || !string.IsNullOrEmpty(serverUri.Query))
        {
            SetStatus("请输入有效的服务器地址，例如 https://game.example.com");
            return false;
        }
        var serverChanged = ServerUrl != _session.ServerUrl;
        if (_hub is not null && (serverChanged || (!string.IsNullOrEmpty(_authenticatedHandle)
            && _authenticatedHandle != _session.Handle.Trim().ToLowerInvariant())))
        {
            await DisconnectAsync();
            _authenticatedHandle = string.Empty;
            _session = _session with { ReconnectToken = null };
        }
        ServerUrl = _session.ServerUrl!;
        await SaveSessionAsync();
        if (serverChanged)
        {
            await LoadDecksAsync();
            await LoadPublicMatchesAsync();
        }

        SetStatus("Connecting");
        var hub = EnsureHubClient();
        var started = await hub.StartAsync(_shutdown.Token);
        if (started)
        {
            AppendLog($"Connected to {ServerUrl}/hubs/game.");
        }

        SetStatus("Connected");
        var auth = await hub.AuthenticateAsync(
            _session.Handle,
            _session.PlayerKey,
            _shutdown.Token);
        AppendLog($"Authenticate: {auth.Status} ({auth.Handle}).");
        if (!auth.Authenticated)
        {
            await DisconnectAsync();
            _authenticatedHandle = string.Empty;
            SetStatus(auth.Status == "HandleClaimed"
                ? "这个玩家名已被使用，请更换名字后重新连接。"
                : "身份验证失败，请检查玩家名后重新连接。");
            return false;
        }

        _authenticatedHandle = auth.Handle;
        return true;
    }

    private RiftboundGameHubClient EnsureHubClient()
    {
        if (_hub is not null)
        {
            return _hub;
        }

        _hub = new RiftboundGameHubClient(ServerUrl);
        _hub.StatusChanged += SetStatus;
        _hub.RestoreSession += RestoreSessionAfterReconnectAsync;
        _hub.LogReceived += AppendLog;
        _hub.ServerMessageReceived += LogMessage;
        return _hub;
    }

    private async Task RestoreSessionAfterReconnectAsync()
    {
        var hub = _hub ?? throw new InvalidOperationException("连接已关闭。");
        var auth = await hub.AuthenticateAsync(_session.Handle, _session.PlayerKey, _shutdown.Token);
        if (!auth.Authenticated) throw new InvalidOperationException("身份验证失败，请返回大厅重新连接。");
        _authenticatedHandle = auth.Handle;
        if (!string.IsNullOrWhiteSpace(_session.ReconnectToken))
            await hub.ReconnectAsync(_session.RoomId, auth.Handle, _session.ReconnectToken, _shutdown.Token);
        else
            await hub.JoinRoomAsync(_session.RoomId, auth.Handle, null, _shutdown.Token);
        await hub.RequestSnapshotAsync(_session.RoomId, auth.Handle, _shutdown.Token);
    }

    private async Task CreatePublicMatchAsync()
    {
        ResetLobbyPromptState();
        try
        {
            if (!await EnsureAuthenticatedConnectionAsync())
            {
                return;
            }

            SetMatchmakingStatus("Creating public match...");
            var result = await _hub!.CreatePublicMatchAsync(
                _authenticatedHandle,
                _shutdown.Token);
            if (result is null)
            {
                SetMatchmakingStatus("Create public match rejected");
                AppendLog("[color=yellow]Create public match returned no room.[/color]");
                return;
            }

            ApplyPublicMatchResult(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetMatchmakingStatus("Create public match error");
            AppendLog($"[color=red]Create public match failed: {Escape(ex.Message)}[/color]");
            GD.PushError(ex.ToString());
        }
    }

    private async Task QueueMatchmakingAsync()
    {
        ResetLobbyPromptState();
        try
        {
            if (!await EnsureAuthenticatedConnectionAsync())
            {
                return;
            }

            SetMatchmakingStatus("Queueing...");
            var status = await _hub!.EnqueueMatchmakingAsync(
                _authenticatedHandle,
                _shutdown.Token);
            await ApplyMatchmakingStatusAsync(status, "EnqueueMatchmaking");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetMatchmakingStatus("Queue error");
            AppendLog($"[color=red]Queue matchmaking failed: {Escape(ex.Message)}[/color]");
            GD.PushError(ex.ToString());
        }
    }

    private async Task CancelMatchmakingAsync()
    {
        try
        {
            if (!await EnsureAuthenticatedConnectionAsync())
            {
                return;
            }

            SetMatchmakingStatus("Cancelling queue...");
            var status = await _hub!.CancelMatchmakingAsync(
                _authenticatedHandle,
                _shutdown.Token);
            await ApplyMatchmakingStatusAsync(status, "CancelMatchmaking");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetMatchmakingStatus("Cancel queue error");
            AppendLog($"[color=red]Cancel matchmaking failed: {Escape(ex.Message)}[/color]");
            GD.PushError(ex.ToString());
        }
    }

    private async Task JoinSelectedPublicMatchAsync()
    {
        try
        {
            var match = SelectedPublicMatch();
            if (match is null)
            {
                SetMatchmakingStatus("No public match selected");
                AppendLog("[color=yellow]Join public match skipped: no open public match selected.[/color]");
                return;
            }

            await JoinPublicMatchAsync(match);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetMatchmakingStatus("Join public match error");
            AppendLog($"[color=red]Join public match failed: {Escape(ex.Message)}[/color]");
            GD.PushError(ex.ToString());
        }
    }

    private async Task JoinFirstPublicMatchSmokeAsync()
    {
        for (var attempt = 1; attempt <= 20 && !_shutdown.IsCancellationRequested; attempt++)
        {
            await LoadPublicMatchesAsync();
            var match = _publicMatches.FirstOrDefault();
            if (match is not null)
            {
                AppendLog($"Auto smoke: joining public match {Escape(match.RoomId)}.");
                await JoinPublicMatchAsync(match);
                return;
            }

            SetMatchmakingStatus($"Waiting for public match... {attempt}/20");
            await Task.Delay(TimeSpan.FromMilliseconds(500), _shutdown.Token);
        }

        AppendLog("[color=yellow]Auto smoke: no public match became available.[/color]");
    }

    private async Task JoinPublicMatchAsync(PublicMatchDto match)
    {
        ResetLobbyPromptState();
        if (!await EnsureAuthenticatedConnectionAsync())
        {
            return;
        }

        _session = _session with { RoomId = match.RoomId, ReconnectToken = null };
        QueueMainThread(nameof(ApplyRoomInput), match.RoomId);
        await SaveSessionAsync();

        SetMatchmakingStatus($"正在加入公开房间 {match.RoomId}…");
        await JoinCurrentRoomAndRequestSnapshotAsync(useReconnectToken: false);
        SetMatchmakingStatus($"已加入公开房间 {match.RoomId}");
        AppendLog($"Public match joined: room={Escape(match.RoomId)}, host={Escape(match.HostPlayerId)}.");
        await RunAutoSmokeSetupIfReadyAsync();
    }

    private void ApplyPublicMatchResult(CreatePublicMatchResultDto result)
    {
        var roomId = result.Match.RoomId;
        _lastJoinedMatchmakingRoom = roomId;
        _session = _session with
        {
            RoomId = roomId,
            ReconnectToken = result.PlayerSession.ReconnectToken
        };
        QueueMainThread(nameof(ApplyRoomInput), roomId);
        _ = SaveSessionAsync();

        SetMatchmakingStatus(
            $"公开房间 {roomId} · {result.Match.SeatCount}/{result.Match.Capacity} 人 · 等待加入");
        AppendLog($"Public match created: room={Escape(roomId)}, seat={Escape(result.PlayerSession.Seat)}.");
        _ = RunAutoSmokeSetupIfReadyAsync();
    }

    private async Task ApplyMatchmakingStatusAsync(MatchmakingStatusDto status, string source)
    {
        var summary = MatchmakingSummary(status);
        SetMatchmakingStatus(summary);
        AppendLog($"{Escape(source)}: {Escape(summary)}.");

        if (!string.Equals(status.State, MatchmakingMatched, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(status.RoomId)
            || string.Equals(_lastJoinedMatchmakingRoom, status.RoomId, StringComparison.Ordinal))
        {
            return;
        }

        _lastJoinedMatchmakingRoom = status.RoomId;
        ResetLobbyPromptState();
        _session = _session with
        {
            RoomId = status.RoomId,
            ReconnectToken = status.PlayerSession?.ReconnectToken ?? _session.ReconnectToken
        };
        QueueMainThread(nameof(ApplyRoomInput), status.RoomId);
        await SaveSessionAsync();
        await JoinCurrentRoomAndRequestSnapshotAsync(useReconnectToken: false);
        await RunAutoSmokeSetupIfReadyAsync();
    }

    private async Task JoinCurrentRoomAndRequestSnapshotAsync(bool useReconnectToken)
    {
        if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
        {
            AppendLog("[color=yellow]Join skipped: not connected/authenticated.[/color]");
            return;
        }

        var reconnectToken = _session.ReconnectToken;
        if (useReconnectToken && !string.IsNullOrWhiteSpace(reconnectToken))
        {
            await _hub!.ReconnectAsync(
                _session.RoomId,
                _authenticatedHandle,
                reconnectToken,
                _shutdown.Token);
            AppendLog($"Reconnect requested: room={_session.RoomId}, player={_authenticatedHandle}.");
        }
        else
        {
            await _hub!.JoinRoomAsync(
                _session.RoomId,
                _authenticatedHandle,
                null,
                _shutdown.Token);
            AppendLog($"JoinRoom requested: room={_session.RoomId}, player={_authenticatedHandle}.");
        }

        await _hub!.RequestSnapshotAsync(_session.RoomId, _authenticatedHandle, _shutdown.Token);
        AppendLog("RequestSnapshot submitted.");
    }

    private static string MatchmakingSummary(MatchmakingStatusDto status)
    {
        return status.State switch
        {
            MatchmakingQueued => "正在匹配对手…",
            MatchmakingMatched => $"已匹配 · 房间 {status.RoomId ?? "?"}",
            MatchmakingCancelled => "已取消匹配",
            MatchmakingIdle => "尚未开始匹配",
            MatchmakingRejected => $"匹配失败 · {status.Message ?? status.ErrorCode ?? "未知错误"}",
            _ => "匹配状态已更新"
        };
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
        var receipt = await _hub!.SubmitIntentAsync(
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

        var receipt = await _hub!.ReadyAsync(
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId("ready"),
            _shutdown.Token);
        AppendReceipt("Ready", receipt);
    }

    private async Task ReturnToLobbyAsync()
    {
        await DisconnectAsync();
        ResetLobbyPromptState();
        SetStatus("Lobby");
        SetMatchmakingStatus("Returned to lobby");
        QueueMainThread(nameof(ClearMatchResult));
        await LoadPublicMatchesAsync();
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
        var receipt = await _hub!.SubmitIntentAsync(
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
        var receipt = await _hub!.SubmitIntentAsync(
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId($"prompt-{intentSuffix}"),
            cmd,
            _shutdown.Token).WaitAsync(TimeSpan.FromSeconds(10), _shutdown.Token).ConfigureAwait(false);
        AppendReceipt(label, receipt);
        if (intentSuffix is "play_card" or "move_units" or "declare_battle")
            QueueMainThread(nameof(ApplyPlayCardReceipt), new Godot.Collections.Dictionary
            {
                ["promptId"] = promptId, ["tick"] = snapshotTick,
                ["accepted"] = receipt.Accepted,
                ["message"] = receipt.ErrorCode == ErrorCodes.InsufficientCost
                    ? "资源不足以支付所选费用，请补充法力或符能，或调整额外费用。"
                    : receipt.Message
            });
        if (intentSuffix == "runes")
            QueueMainThread(nameof(ApplyRuneReceipt), new Godot.Collections.Dictionary
            { ["promptId"] = promptId, ["tick"] = snapshotTick, ["accepted"] = receipt.Accepted, ["message"] = receipt.Message });
    }

    private async Task SubmitRuneActionAsync(string actionName, string[] ids)
    {
        var prompt = _runeActionPanel!.PromptId; var tick = _runeActionPanel.SnapshotTick;
        if (!IsConnected() || _promptSubmissionInFlight || !TryGetCurrentSpecialAction(actionName, out var action)
            || action["promptId"].AsString() != prompt || action["snapshotTick"].AsInt64() != tick
            || ids.Any(id => !PromptChoiceIds(action, "sourceChoices").Contains(id)))
        { ApplyRuneReceipt(new() { ["promptId"] = prompt, ["tick"] = tick, ["accepted"] = false, ["message"] = "符文选择已失效，请重新选择。" }); return; }
        _promptSubmissionInFlight = true; _matchScreen?.ActionBar.SetPending(true);
        try
        {
            await SubmitPromptPayloadAsync(action, new() { ["cmdType"] = actionName, ["sourceObjectId"] = ids[0], ["sourceObjectIds"] = ids }, "runes").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Rune action failed: {Escape(ex.Message)}[/color]");
            QueueMainThread(nameof(ApplyRuneReceipt), new Godot.Collections.Dictionary
            { ["promptId"] = prompt, ["tick"] = tick, ["accepted"] = false, ["message"] = "操作未完成，请检查连接后重试。" });
        }
    }

    public void ApplyRuneReceipt(Godot.Collections.Dictionary receipt)
    {
        if (_runeActionPanel?.MatchesReceipt(receipt["promptId"].AsString(), receipt["tick"].AsInt64()) != true) return;
        _promptSubmissionInFlight = false; _matchScreen?.ActionBar.SetPending(false);
        _runeActionPanel?.ApplyReceipt(receipt["promptId"].AsString(), receipt["tick"].AsInt64(), receipt["accepted"].AsBool(), receipt["message"].AsString());
    }

    public void ApplyPlayCardReceipt(Godot.Collections.Dictionary receipt)
    {
        _playCardOverlay?.ApplyReceipt(receipt["promptId"].AsString(), receipt["tick"].AsInt64(), receipt["accepted"].AsBool(), receipt["message"].AsString());
        _movementOverlay?.ApplyReceipt(receipt["promptId"].AsString(), receipt["tick"].AsInt64(), receipt["accepted"].AsBool(), receipt["message"].AsString());
        _battleDeclaration?.ApplyReceipt(receipt["promptId"].AsString(), receipt["tick"].AsInt64(), receipt["accepted"].AsBool(), receipt["message"].AsString());
    }

    private async Task SubmitTableActionAsync(Godot.Collections.Dictionary action, Dictionary<string, object?> payload, string suffix)
    {
        try
        {
            if (!IsConnected() || string.IsNullOrWhiteSpace(_authenticatedHandle))
                throw new InvalidOperationException("连接已断开，请等待恢复后重试。");
            await SubmitSpecialPromptAsync(action, payload, suffix);
        }
        catch (Exception error)
        {
            AppendLog($"[color=yellow]Table action failed: {Escape(error.Message)}[/color]");
            QueueMainThread(nameof(ApplyPlayCardReceipt), new Godot.Collections.Dictionary
            {
                ["promptId"] = action["promptId"], ["tick"] = action["snapshotTick"], ["accepted"] = false,
                ["message"] = "未能确认行动结果，请等待局面同步后重试。"
            });
        }
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
        var receipt = await _hub!.SubmitIntentAsync(
            _session.RoomId,
            _authenticatedHandle,
            NewIntentId("prompt-mulligan"),
            cmd,
            _shutdown.Token);
        AppendReceipt(label, receipt);
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
            var receipt = await _hub!.SubmitIntentAsync(
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
            "selectedOptionalCosts" => selection.OptionalCostIds.Concat(requirement is { } required
                ? ReadStringArray(required, "requiredOptionalCosts") : []).Distinct(StringComparer.Ordinal).ToArray(),
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

        var selected = _lobbyScreen?.SelectedDeckIndex ?? 0;
        return selected < _decks.Count ? _decks[selected] : _decks[0];
    }

    private PublicMatchDto? SelectedPublicMatch()
    {
        if (_publicMatches.Count == 0)
        {
            return null;
        }

        var selected = _lobbyScreen?.SelectedPublicMatchIndex ?? 0;
        return selected < _publicMatches.Count ? _publicMatches[selected] : _publicMatches[0];
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
            _latestSnapshotMessage = message;
            var renderVersion = Interlocked.Increment(ref _snapshotRenderVersion);
            _ = RenderSnapshotAsync(message, renderVersion);
        }
        else if (channel == "Prompt")
        {
            RenderPrompt(message);
        }
        else if (channel == "Events")
        {
            RenderEvents(message);
        }
        else if (channel == "Matchmaking")
        {
            _ = HandleMatchmakingMessageAsync(message);
        }

        AppendLog(
            $"[b]{Escape(channel)}[/b] type={message.Type} room={Escape(message.RoomId)} player={Escape(message.PlayerId)} tick={message.ServerTick} payload={PayloadSummary(message.Payload)}");
    }

    private async Task HandleMatchmakingMessageAsync(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        try
        {
            var status = element.Deserialize<MatchmakingStatusDto>(ClientJsonOptions);
            if (status is not null)
            {
                await ApplyMatchmakingStatusAsync(status, "Matchmaking");
            }
        }
        catch (JsonException ex)
        {
            AppendLog($"[color=yellow]Matchmaking payload skipped: {Escape(ex.Message)}[/color]");
        }
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
            ObserveAutoSmokePromptTick(view);
            QueueMainThread(nameof(ApplyPrompt), view);
            AppendLog(
                $"Prompt rendered: {view["candidateCount"].AsInt32()} candidates, {view["directCount"].AsInt32()} direct, {view["templateCount"].AsInt32()} templates.");
            AppendLog($"Prompt actions: {PromptActionSummary(view)}");
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Prompt render skipped: {Escape(ex.Message)}[/color]");
        }
    }

    private void RenderEvents(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var eventKinds = new List<string>();
        var descriptions = new Godot.Collections.Array<string>();
        var feedbackObjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var eventElement in element.EnumerateArray())
        {
            if (eventElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = ReadString(eventElement, "kind");
            if (eventElement.TryGetProperty("objectRefs", out var feedbackRefs) && feedbackRefs.ValueKind == JsonValueKind.Array)
                foreach (var reference in feedbackRefs.EnumerateArray())
                    if (!ReadBool(reference, "isHidden") && !ReadBool(reference, "isFaceDown")
                        && ReadString(reference, "objectId") is { Length: > 0 } id)
                        feedbackObjects.Add(id);
            var description = BattleEventPresenter.Describe(eventElement, _authenticatedHandle,
                cardNo => _officialCatalog.TryGetValue(cardNo, out var card) ? card.CardName : null);
            if (!string.IsNullOrWhiteSpace(description) && kind != "DEV_SCENARIO_SEEDED") descriptions.Add(description);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                eventKinds.Add(kind);
            }

            if (string.Equals(kind, "MATCH_WON", StringComparison.Ordinal)
                && MatchResultView(eventElement, message.ServerTick) is { } result)
            {
                _matchFinished = true;
                var summary = result.TryGetValue("summary", out var summaryValue)
                    ? summaryValue.AsString().Replace('\n', ' ')
                    : "Match finished";
                AppendLog($"Match result rendered: {Escape(summary)}");
                QueueMainThread(nameof(ApplyMatchResult), result);
            }
        }

        if (descriptions.Count > 0) QueueMainThread(nameof(ApplyBattleEvents), new Godot.Collections.Dictionary
        { ["tick"] = message.ServerTick, ["objects"] = new Godot.Collections.Array<string>(feedbackObjects), ["descriptions"] = new Godot.Collections.Array<string>(descriptions
            .GroupBy(description => description).Select(group => group.Count() > 1 ? $"{group.Key} × {group.Count()}" : group.Key)) });
        if (eventKinds.Count > 0)
        {
            AppendLog($"Events received: {Escape(string.Join(", ", eventKinds))}.");
        }
    }

    public void ApplyBattleEvents(Godot.Collections.Dictionary events)
        => _matchScreen?.AddBattleEvents(events["tick"].AsInt64(), events["descriptions"].As<Godot.Collections.Array<string>>().ToArray(),
            events["objects"].As<Godot.Collections.Array<string>>().ToArray());

    private static Godot.Collections.Dictionary? MatchResultView(JsonElement eventElement, long serverTick)
    {
        var payload = eventElement.TryGetProperty("payload", out var payloadElement)
            ? payloadElement
            : default;
        var winnerPlayerId = ReadObjectString(payload, "winnerPlayerId");
        var surrenderedPlayerId = ReadObjectString(payload, "surrenderedPlayerId");
        var reason = ReadObjectString(payload, "reason");
        var winningScore = ReadObjectInt(payload, "winningScore");
        var description = ReadString(eventElement, "description");

        if (string.IsNullOrWhiteSpace(winnerPlayerId) && string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(winnerPlayerId)
                ? "Match finished"
                : $"Match finished · winner {winnerPlayerId}"
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            lines.Add(description);
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            lines.Add($"Reason: {reason}");
        }

        if (!string.IsNullOrWhiteSpace(surrenderedPlayerId))
        {
            lines.Add($"Surrendered: {surrenderedPlayerId}");
        }

        if (winningScore > 0)
        {
            lines.Add($"Winning score: {winningScore}");
        }

        lines.Add($"Server tick: {serverTick}");

        return new Godot.Collections.Dictionary
        {
            ["summary"] = string.Join("\n", lines),
            ["winnerPlayerId"] = winnerPlayerId,
            ["reason"] = reason,
            ["surrenderedPlayerId"] = surrenderedPlayerId,
            ["winningScore"] = winningScore,
            ["serverTick"] = serverTick,
            ["source"] = "MATCH_WON"
        };
    }

    private async Task RunAutoSmokePromptAsync(Godot.Collections.Dictionary view)
    {
        if ((!_autoSmokeMulligan && !_autoSmokeTapRune && !_autoSmokePlayCard && !_autoSmokeFollowups && !_autoSmokeSurrender)
            || !view.TryGetValue("actions", out var actionsValue)
            || actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } actions)
        {
            return;
        }

        if (_matchFinished)
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

        if (_autoSmokeSurrender
            && !_autoSmokeSurrenderSubmitted
            && _battleTableRendered
            && TryGetEnabledPromptAction(actions, "SURRENDER", requireTemplate: false, out var surrenderAction))
        {
            var label = surrenderAction.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Surrender";
            var promptId = surrenderAction.TryGetValue("promptId", out var promptIdValue) ? promptIdValue.AsString() : string.Empty;
            var snapshotTick = surrenderAction.TryGetValue("snapshotTick", out var snapshotTickValue) ? snapshotTickValue.AsInt64() : -1L;
            var key = AutoSmokePromptKey(surrenderAction, "SURRENDER");
            if (!_autoSmokePromptSubmissions.Add(key))
            {
                return;
            }

            _autoSmokeSurrenderSubmitted = true;
            AppendLog("Auto smoke: submitting SURRENDER from a server-enabled prompt.");
            await SubmitPromptCommandAsync("SURRENDER", promptId, snapshotTick, label);
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

        var preparingUiPlayCard = string.Equals(_autoSmokeUiAction, "PLAY_CARD", StringComparison.Ordinal);
        var tapRuneLimit = _autoSmokePlayCard || preparingUiPlayCard
            ? AutoSmokePlayCardTapRuneLimit
            : 1;
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

    private void ScheduleAutoSmokePrompt(Godot.Collections.Dictionary view)
    {
        lock (_autoSmokePromptQueueLock)
        {
            _pendingAutoSmokePromptView = view.Duplicate(true);
            if (_autoSmokePromptQueueRunning)
            {
                return;
            }

            _autoSmokePromptQueueRunning = true;
        }

        _ = DrainAutoSmokePromptQueueAsync();
    }

    private async Task DrainAutoSmokePromptQueueAsync()
    {
        while (true)
        {
            Godot.Collections.Dictionary? view;
            lock (_autoSmokePromptQueueLock)
            {
                view = _pendingAutoSmokePromptView;
                _pendingAutoSmokePromptView = null;
                if (view is null)
                {
                    _autoSmokePromptQueueRunning = false;
                    return;
                }
            }

            try
            {
                if (IsAutoSmokePromptStale(view))
                {
                    continue;
                }

                await RunAutoSmokePromptAsync(view);
            }
            catch (Exception ex) when (!_isShuttingDown)
            {
                AppendLog($"[color=yellow]Auto smoke prompt skipped: {Escape(ex.Message)}[/color]");
            }
        }
    }

    private void ObserveAutoSmokePromptTick(Godot.Collections.Dictionary view)
    {
        var tick = view.TryGetValue("snapshotTick", out var tickValue) ? tickValue.AsInt64() : -1L;
        if (tick < 0)
        {
            return;
        }

        while (true)
        {
            var observed = Volatile.Read(ref _latestObservedPromptSnapshotTick);
            if (tick <= observed
                || Interlocked.CompareExchange(ref _latestObservedPromptSnapshotTick, tick, observed) == observed)
            {
                return;
            }
        }
    }

    private bool IsAutoSmokePromptStale(Godot.Collections.Dictionary view)
    {
        var tick = view.TryGetValue("snapshotTick", out var tickValue) ? tickValue.AsInt64() : -1L;
        return tick >= 0 && tick < Volatile.Read(ref _latestObservedPromptSnapshotTick);
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

        if (string.Equals(actionName, "DECLARE_BATTLE", StringComparison.Ordinal))
        {
            return await TryRunAutoSmokePayloadActionAsync(action, actionName);
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

    private async Task<bool> TryRunAutoSmokePayloadActionAsync(
        Godot.Collections.Dictionary action,
        string actionName)
    {
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

    private async Task<bool> TryRunAutoSmokeSpecialActionAsync(
        Godot.Collections.Array<Godot.Collections.Dictionary> actions,
        string actionName)
    {
        if (_autoSmokeActionSubmissions.GetValueOrDefault(actionName) >= AutoSmokeActionLimitFor(actionName)
            || !TryGetEnabledPromptAction(actions, actionName, requireTemplate: false, out var action))
        {
            return false;
        }

        return await TryRunAutoSmokePayloadActionAsync(action, actionName);
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

            if (!required
                && string.Equals(role, "optionalCost", StringComparison.Ordinal)
                && !ShouldAutoIncludeOptionalCost(choiceId))
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

    private static bool ShouldAutoIncludeOptionalCost(string choiceId)
    {
        return string.Equals(choiceId, "COMBAT_ASSIGNMENT", StringComparison.Ordinal);
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
                if (candidate.TryGetProperty("metadata", out var choiceMetadata)
                    && choiceMetadata.ValueKind == JsonValueKind.Object
                    && choiceMetadata.TryGetProperty("viewedCards", out var viewedCards))
                    message = ReadString(choiceMetadata, "reason") + (choiceMetadata.TryGetProperty("viewedCardsPublic", out var publicCards) && publicCards.ValueKind == JsonValueKind.True ? "\n已公开展示：" : "\n仅你可见：") + string.Join("、", viewedCards.EnumerateArray().Select(card => ReadString(card, "label")));
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
            ["summary"] = $"{title}\n{message}",
            ["title"] = title,
            ["message"] = message,
            ["reason"] = reason,
            ["promptId"] = promptId,
            ["snapshotTick"] = snapshotTick ?? -1L,
            ["actionable"] = actionable,
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
        var objectIds = new Godot.Collections.Array<string>();
        if (choice.TryGetProperty("objectIds", out var objectIdElements)
            && objectIdElements.ValueKind == JsonValueKind.Array)
        {
            foreach (var objectId in objectIdElements.EnumerateArray())
            {
                if (objectId.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(objectId.GetString()))
                {
                    objectIds.Add(objectId.GetString()!);
                }
            }
        }

        return new Godot.Collections.Dictionary
        {
            ["id"] = id,
            ["label"] = string.IsNullOrWhiteSpace(label) ? id : label,
            ["objectIds"] = objectIds
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

    private async Task RenderSnapshotAsync(WsServerMessage message, int renderVersion)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        try
        {
            if (SnapshotMatchResultView(element) is { } snapshotMatchResult)
            {
                if (!_matchFinished)
                {
                    _matchFinished = true;
                    var resultSummary = snapshotMatchResult.TryGetValue("summary", out var summaryValue)
                        ? summaryValue.AsString().Replace('\n', ' ')
                        : "Match finished";
                    AppendLog($"Match result rendered from snapshot: {Escape(resultSummary)}");
                    QueueMainThread(nameof(ApplyMatchResult), snapshotMatchResult);
                }

            }

            if (IsStaleSnapshotRender(renderVersion))
            {
                return;
            }

            var table = element.TryGetProperty("table", out var tableElement) && tableElement.ValueKind == JsonValueKind.Object
                ? tableElement
                : default;
            var handCards = VisibleHandCards(element, table);
            if (_officialCatalogLoadTask is { IsCompleted: false } catalogLoadTask)
            {
                await catalogLoadTask;
                if (IsStaleSnapshotRender(renderVersion))
                {
                    return;
                }
            }

            var views = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            var officialImageCount = 0;
            foreach (var handCard in handCards.Take(12))
            {
                var view = await BuildCardViewAsync(handCard);
                if (view.ContainsKey("imagePath"))
                {
                    officialImageCount++;
                }

                views.Add(view);
            }

            TryRunAutoSmokePreview(views);

            var objectIndex = VisibleObjectIndex(element, table);
            var tableSections = await BuildTableSectionsAsync(element, table, objectIndex);
            if (IsStaleSnapshotRender(renderVersion))
            {
                return;
            }

            var hiddenBoundaryLogLine = HiddenInfoBoundaryLogLine(tableSections.Sections);
            QueueMainThread(nameof(ApplySnapshotSections), tableSections.Sections);
            AppendLog(
                $"Snapshot table rendered: visibleHand={views.Count}, handOfficialImages={officialImageCount}, tableCards={tableSections.CardCount}, tableOfficialImages={tableSections.OfficialImageCount}.");
            AppendLog(hiddenBoundaryLogLine);
            QueueVisualScreenshotIfReady(tableSections.CardCount);
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

    private bool IsStaleSnapshotRender(int renderVersion)
    {
        return _isShuttingDown
            || Volatile.Read(ref _snapshotRenderVersion) != renderVersion;
    }

    private void TryRunAutoSmokePreview(Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        if (!_autoSmokePreviewFirstVisibleCard || _autoSmokePreviewRendered)
        {
            return;
        }

        foreach (var card in cards)
        {
            var isVisible = card.TryGetValue("visible", out var visibleValue) && visibleValue.AsBool();
            if (!isVisible)
            {
                continue;
            }

            _autoSmokePreviewRendered = true;
            var summary = card.TryGetValue("previewSummary", out var summaryValue)
                ? summaryValue.AsString()
                : "可见卡牌";
            QueueMainThread(nameof(ApplyCardPreview), card);
            AppendLog($"Auto smoke: previewing first visible card: {Escape(summary.Replace('\n', ' '))}");
            return;
        }
    }

    private static Godot.Collections.Dictionary? SnapshotMatchResultView(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("timing", out var timing)
            || timing.ValueKind != JsonValueKind.Object
            || !string.Equals(ReadString(timing, "roomStatus"), "FINISHED", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(ReadString(timing, "winnerPlayerId")))
        {
            return null;
        }

        var winnerPlayerId = ReadString(timing, "winnerPlayerId");
        var winningScore = ReadInt(timing, "winningScore");
        var lines = new List<string>
        {
            $"Match finished · winner {winnerPlayerId}",
            "Source: snapshot timing"
        };
        if (winningScore > 0)
        {
            lines.Add($"Winning score: {winningScore}");
        }

        return new Godot.Collections.Dictionary
        {
            ["summary"] = string.Join("\n", lines),
            ["winnerPlayerId"] = winnerPlayerId,
            ["reason"] = string.Empty,
            ["surrenderedPlayerId"] = string.Empty,
            ["winningScore"] = winningScore,
            ["serverTick"] = ReadLong(snapshot, "tick"),
            ["source"] = "snapshot"
        };
    }

    private async Task<(Godot.Collections.Array<Godot.Collections.Dictionary> Sections, int CardCount, int OfficialImageCount)> BuildTableSectionsAsync(
        JsonElement snapshot,
        JsonElement table,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var sections = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var wireTable = await BuildWireTableSectionAsync(snapshot, table, objectIndex);
        sections.Add(wireTable.Section);
        return (sections, wireTable.CardCount, wireTable.OfficialImageCount);
    }

    private static string HiddenInfoBoundaryLogLine(Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        if (sections.Count != 1
            || !sections[0].TryGetValue("kind", out var kind)
            || !string.Equals(kind.AsString(), "wireTable", StringComparison.Ordinal)
            || !sections[0].TryGetValue("opponent", out var opponentValue))
        {
            return "Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=0 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0";
        }

        var opponent = opponentValue.AsGodotDictionary();
        var opponentHand = CardArray(opponent, "hand");
        var opponentHandFaces = CountFaceCards(opponentHand);
        var opponentHandBacks = GodotInt(opponent, "handHiddenCount") + CountHiddenCards(opponentHand);
        var opponentStandbyFaces = 0;
        var opponentStandbyBacks = 0;
        var hiddenCardIdentityLeaks = CountHiddenIdentityLeaks(opponentHand);

        if (sections[0].TryGetValue("lanes", out var lanesValue)
            && lanesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is { } lanes)
        {
            foreach (var lane in lanes)
            {
                var opponentStandby = CardArray(lane, "opponentStandby");
                opponentStandbyFaces += CountFaceCards(opponentStandby);
                opponentStandbyBacks += GodotInt(lane, "hiddenStandbyCount") + CountHiddenCards(opponentStandby);
                hiddenCardIdentityLeaks += CountHiddenIdentityLeaks(opponentStandby);
            }
        }

        var status = opponentHandFaces == 0
            && opponentStandbyFaces == 0
            && hiddenCardIdentityLeaks == 0
            ? "ok"
            : "VIOLATION";
        return $"Hidden info boundary {status}: opponentHandFaces={opponentHandFaces} opponentHandBacks={opponentHandBacks} opponentStandbyFaces={opponentStandbyFaces} opponentStandbyBacks={opponentStandbyBacks} hiddenCardIdentityLeaks={hiddenCardIdentityLeaks}";
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> CardArray(
        Godot.Collections.Dictionary container,
        string key)
    {
        return container.TryGetValue(key, out var value)
            ? value.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
    }

    private static int GodotInt(Godot.Collections.Dictionary container, string key)
    {
        return container.TryGetValue(key, out var value)
            ? Math.Max(0, value.AsInt32())
            : 0;
    }

    private static int CountFaceCards(Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        return cards.Count(card => IsFaceCard(card) && HasVisibleIdentityFields(card));
    }

    private static int CountHiddenCards(Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        return cards.Count(IsHiddenCard);
    }

    private static int CountHiddenIdentityLeaks(Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        return cards.Count(card => IsHiddenCard(card) && HasVisibleIdentityFields(card));
    }

    private static bool IsFaceCard(Godot.Collections.Dictionary card)
    {
        return (!card.TryGetValue("visible", out var visibleValue) || visibleValue.AsBool())
            && (!card.TryGetValue("faceDown", out var faceDownValue) || !faceDownValue.AsBool());
    }

    private static bool IsHiddenCard(Godot.Collections.Dictionary card)
    {
        return (card.TryGetValue("visible", out var visibleValue) && !visibleValue.AsBool())
            || (card.TryGetValue("faceDown", out var faceDownValue) && faceDownValue.AsBool());
    }

    private static bool HasVisibleIdentityFields(Godot.Collections.Dictionary card)
    {
        foreach (var key in new[] { "cardNo", "cardName", "category", "trait", "effectText", "rarityName", "colorText", "imagePath" })
        {
            if (card.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.AsString()))
            {
                return true;
            }
        }

        return (card.TryGetValue("energy", out var energy) && energy.AsInt32() >= 0)
            || (card.TryGetValue("power", out var power) && power.AsInt32() >= 0);
    }

    private async Task<(Godot.Collections.Dictionary Section, int CardCount, int OfficialImageCount)> BuildWireTableSectionAsync(
        JsonElement snapshot,
        JsonElement table,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var viewerPlayerId = ReadString(table, "viewerPlayerId");
        var runeDeckSize = Math.Max(12, ReadInt(table, "runeDeckSize"));
        var cardCount = 0;
        var officialImageCount = 0;

        var self = new Godot.Collections.Dictionary
        {
            ["side"] = "self",
            ["label"] = "P1 我方",
            ["missing"] = true,
            ["runeDeckSize"] = runeDeckSize
        };
        var opponent = new Godot.Collections.Dictionary
        {
            ["side"] = "opponent",
            ["label"] = "P2 对手",
            ["missing"] = true,
            ["runeDeckSize"] = runeDeckSize
        };

        if (table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("players", out var players)
            && players.ValueKind == JsonValueKind.Array)
        {
            foreach (var player in players.EnumerateArray())
            {
                var side = WirePlayerSide(player, viewerPlayerId);
                var entry = await BuildWirePlayerAsync(snapshot, player, side, objectIndex, runeDeckSize);
                cardCount += entry.CardCount;
                officialImageCount += entry.OfficialImageCount;
                if (side == "self")
                {
                    self = entry.Player;
                }
                else
                {
                    opponent = entry.Player;
                }
            }
        }

        var lanes = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("battlefields", out var battlefields)
            && battlefields.ValueKind == JsonValueKind.Array)
        {
            foreach (var battlefield in battlefields
                .EnumerateArray()
                .OrderBy(field => ReadInt(field, "index")))
            {
                var lane = await BuildWireBattlefieldLaneAsync(battlefield, ReadInt(battlefield, "index"), viewerPlayerId, objectIndex);
                lanes.Add(lane.Lane);
                cardCount += lane.CardCount;
                officialImageCount += lane.OfficialImageCount;
            }
        }

        for (var index = lanes.Count; index < 2; index++)
        {
            lanes.Add(EmptyWireBattlefieldLane(index));
        }

        return (new Godot.Collections.Dictionary
        {
            ["kind"] = "wireTable",
            ["tick"] = ReadInt(snapshot, "tick"),
            ["viewerPlayerId"] = viewerPlayerId,
            ["turnState"] = ReadString(snapshot, "turnState"),
            ["turnNumber"] = ReadInt(snapshot, "turnNumber"),
            ["winningScore"] = snapshot.TryGetProperty("timing", out var timing) ? ReadInt(timing, "winningScore") : 0,
            ["chain"] = BuildTableChain(snapshot, viewerPlayerId, objectIndex),
            ["phaseWindow"] = MatchPhasePresentation.Build(snapshot, viewerPlayerId,
                id => objectIndex.TryGetValue(id, out var reference) && _officialCatalog.TryGetValue(reference.CardNo, out var card)
                    ? card.CardName : "当前战场"),
            ["runeDeckSize"] = runeDeckSize,
            ["self"] = self,
            ["opponent"] = opponent,
            ["lanes"] = lanes
        }, cardCount, officialImageCount);
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> BuildTableChain(JsonElement snapshot, string viewerId,
        IReadOnlyDictionary<string, SnapshotCardRef> objects)
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (!snapshot.TryGetProperty("stack", out var stack) || stack.ValueKind != JsonValueKind.Array) return result;
        string Name(string id) => objects.TryGetValue(id, out var card) && _officialCatalog.TryGetValue(card.CardNo, out var entry)
            ? entry.CardName : "卡牌";
        var items = stack.EnumerateArray().Reverse().ToArray();
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index]; var cardNo = ReadString(item, "cardNo");
            var name = _officialCatalog.TryGetValue(cardNo, out var card) ? card.CardName : cardNo == "HIDDEN" ? "隐藏行动" : "卡牌效果";
            if (ReadBool(item, "playAbility")) name += " · 打出技能";
            var abilityLabel = ReadString(item, "abilityLabel");
            if (!string.IsNullOrWhiteSpace(abilityLabel)) name += " · " + abilityLabel;
            var targets = ReadStringArray(item, "targetObjectIds").Select(Name).ToArray();
            result.Add(new Godot.Collections.Dictionary
            {
                ["objectId"] = ReadString(item, "stackItemId"), ["title"] = (index == 0 ? "下一项  " : $"{index + 1}.  ") + name,
                ["detail"] = (ReadString(item, "controllerId") == viewerId ? "我方" : "对手")
                    + (targets.Length > 0 ? " → " + string.Join("、", targets) : " · 等待响应结束")
            });
        }
        return result;
    }

    private async Task<(Godot.Collections.Dictionary Player, int CardCount, int OfficialImageCount)> BuildWirePlayerAsync(
        JsonElement snapshot,
        JsonElement player,
        string side,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex,
        int runeDeckSize)
    {
        var playerId = ReadString(player, "playerId");
        var zones = player.TryGetProperty("zones", out var zoneElement) && zoneElement.ValueKind == JsonValueKind.Object
            ? zoneElement
            : default;
        var cardCount = 0;
        var officialImageCount = 0;

        var legend = await BuildWireCardsAsync(ReadStringArray(zones, "legendZone"), objectIndex);
        var hero = await BuildWireCardsAsync(ReadStringArray(zones, "championZone"), objectIndex);
        var baseCards = ReadStringArray(zones, "baseCards");
        if (baseCards.Count == 0)
        {
            var baseRuneSet = new HashSet<string>(ReadStringArray(zones, "baseRunes"), StringComparer.Ordinal);
            baseCards = ReadStringArray(zones, "base")
                .Where(objectId => !baseRuneSet.Contains(objectId))
                .ToArray();
        }

        var baseCardViews = await BuildWireCardsAsync(baseCards, objectIndex);
        var baseRunes = await BuildWireCardsAsync(ReadStringArray(zones, "baseRunes"), objectIndex);
        var graveyard = await BuildWireCardsAsync(ReadStringArray(zones, "graveyard"), objectIndex);
        var banished = await BuildWireCardsAsync(ReadStringArray(zones, "banished"), objectIndex);
        var handIds = ReadStringArray(zones, "hand"); // Server supplies only visible hand identities.
        var hand = await BuildWireCardsAsync(handIds, objectIndex);

        foreach (var result in new[] { legend, hero, baseCardViews, baseRunes, graveyard, banished, hand })
        {
            cardCount += result.CardCount;
            officialImageCount += result.OfficialImageCount;
        }

        var hiddenHandCount = side == "opponent"
            ? Math.Max(0, ReadInt(zones, "handHidden"))
            : 0;

        return (new Godot.Collections.Dictionary
        {
            ["side"] = side,
            ["playerId"] = playerId,
            ["label"] = $"{(side == "self" ? "P1 我方" : "P2 对手")} · {playerId}",
            ["missing"] = false,
            ["score"] = ReadSnapshotPlayerScore(snapshot, playerId),
            ["resources"] = ReadSnapshotPlayerResources(snapshot, playerId),
            ["mainDeckCount"] = Math.Max(0, ReadInt(zones, "mainDeckCount")),
            ["runeDeckCount"] = Math.Max(0, ReadInt(zones, "runeDeckCount")),
            ["runeDeckSize"] = runeDeckSize,
            ["handHiddenCount"] = hiddenHandCount,
            ["legend"] = legend.Cards,
            ["hero"] = hero.Cards,
            ["base"] = baseCardViews.Cards,
            ["baseRunes"] = baseRunes.Cards,
            ["graveyard"] = graveyard.Cards,
            ["banished"] = banished.Cards,
            ["hand"] = hand.Cards
        }, cardCount, officialImageCount);
    }

    private async Task<(Godot.Collections.Dictionary Lane, int CardCount, int OfficialImageCount)> BuildWireBattlefieldLaneAsync(
        JsonElement battlefield,
        int fallbackIndex,
        string viewerPlayerId,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var index = Math.Max(0, ReadInt(battlefield, "index"));
        if (index == 0 && fallbackIndex > 0)
        {
            index = fallbackIndex;
        }

        var battlefieldId = ReadString(battlefield, "battlefieldObjectId");
        var cardNo = ReadString(battlefield, "cardNo");
        var cardCount = 0;
        var officialImageCount = 0;
        var siteCards = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (!string.IsNullOrWhiteSpace(battlefieldId) && !string.IsNullOrWhiteSpace(cardNo))
        {
            var siteView = await BuildCardViewAsync(new SnapshotCardRef(battlefieldId, cardNo, true, false, ReadString(battlefield, "controllerId")));
            siteView["rotated"] = true;
            siteCards.Add(siteView);
            cardCount++;
            if (siteView.ContainsKey("imagePath"))
            {
                officialImageCount++;
            }
        }

        var occupants = ReadStringArray(battlefield, "occupantObjectIds");
        var ownOccupants = Array.Empty<string>();
        var opposingOccupants = Array.Empty<string>();
        if (battlefield.TryGetProperty("unitsBySide", out var unitsBySide)
            && unitsBySide.ValueKind == JsonValueKind.Object)
        {
            var occupantSet = new HashSet<string>(occupants, StringComparer.Ordinal);
            ownOccupants = unitsBySide.TryGetProperty(viewerPlayerId, out var ownSide)
                ? ReadStringArray(ownSide).Where(occupantSet.Contains).ToArray()
                : [];
            var ownSet = new HashSet<string>(ownOccupants, StringComparer.Ordinal);
            opposingOccupants = occupants.Where(objectId => !ownSet.Contains(objectId)).ToArray();
        }
        else
        {
            ownOccupants = occupants
                .Where(objectId => objectIndex.TryGetValue(objectId, out var card)
                    && string.Equals(card.ControllerOrOwner, viewerPlayerId, StringComparison.Ordinal))
                .ToArray();
            var ownSet = new HashSet<string>(ownOccupants, StringComparer.Ordinal);
            opposingOccupants = occupants.Where(objectId => !ownSet.Contains(objectId)).ToArray();
        }

        var selfUnits = await BuildWireCardsAsync(ownOccupants, objectIndex);
        var opponentUnits = await BuildWireCardsAsync(opposingOccupants, objectIndex);
        var standby = await BuildWireStandbyCardsAsync(battlefield, viewerPlayerId, objectIndex);

        foreach (var result in new[] { selfUnits, opponentUnits, standby.Self, standby.Opponent })
        {
            cardCount += result.CardCount;
            officialImageCount += result.OfficialImageCount;
        }

        return (new Godot.Collections.Dictionary
        {
            ["index"] = index,
            ["battlefieldId"] = battlefieldId,
            ["site"] = siteCards,
            ["selfUnits"] = selfUnits.Cards,
            ["opponentUnits"] = opponentUnits.Cards,
            ["selfStandby"] = standby.Self.Cards,
            ["opponentStandby"] = standby.Opponent.Cards,
            ["hiddenStandbyCount"] = ReadInt(battlefield, "hiddenStandbyCount"),
            ["controllerId"] = ReadString(battlefield, "controllerId"),
            ["contested"] = ReadBool(battlefield, "contested"),
            ["scoredThisTurn"] = ReadBool(battlefield, "scoredThisTurn")
        }, cardCount, officialImageCount);
    }

    private Godot.Collections.Dictionary EmptyWireBattlefieldLane(int index)
    {
        return new Godot.Collections.Dictionary
        {
            ["index"] = index,
            ["battlefieldId"] = $"empty-battlefield-{index}",
            ["site"] = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            ["selfUnits"] = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            ["opponentUnits"] = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            ["selfStandby"] = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            ["opponentStandby"] = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
            ["hiddenStandbyCount"] = 0,
            ["controllerId"] = string.Empty,
            ["scoredThisTurn"] = false
        };
    }

    private async Task<(Godot.Collections.Array<Godot.Collections.Dictionary> Cards, int CardCount, int OfficialImageCount)> BuildWireStandbySideCardsAsync(
        IEnumerable<SnapshotCardRef> cards)
    {
        var refs = cards.ToArray();
        var views = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var officialImageCount = 0;
        foreach (var card in refs)
        {
            var view = await BuildCardViewAsync(card);
            view["standby"] = true;
            if (view.ContainsKey("imagePath"))
            {
                officialImageCount++;
            }

            views.Add(view);
        }

        return (views, views.Count, officialImageCount);
    }

    private async Task<(
        (Godot.Collections.Array<Godot.Collections.Dictionary> Cards, int CardCount, int OfficialImageCount) Self,
        (Godot.Collections.Array<Godot.Collections.Dictionary> Cards, int CardCount, int OfficialImageCount) Opponent)> BuildWireStandbyCardsAsync(
        JsonElement battlefield,
        string viewerPlayerId,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var self = new List<SnapshotCardRef>();
        var opponent = new List<SnapshotCardRef>();
        if (battlefield.TryGetProperty("standbySlots", out var slots)
            && slots.ValueKind == JsonValueKind.Array)
        {
            foreach (var slot in slots.EnumerateArray())
            {
                var visible = ReadBool(slot, "visible");
                var objectId = ReadString(slot, "objectId");
                var slotId = ReadString(slot, "slotId");
                var card = visible && objectIndex.TryGetValue(objectId, out var visibleCard)
                    ? visibleCard
                    : new SnapshotCardRef(string.IsNullOrWhiteSpace(slotId) ? objectId : slotId, string.Empty, false, true);
                var sidePlayerId = ReadString(slot, "sidePlayerId");
                if (string.IsNullOrWhiteSpace(sidePlayerId))
                {
                    sidePlayerId = ReadString(slot, "controllerId");
                }

                if (string.Equals(sidePlayerId, viewerPlayerId, StringComparison.Ordinal))
                {
                    self.Add(card);
                }
                else
                {
                    opponent.Add(card);
                }
            }
        }
        else
        {
            foreach (var objectId in ReadStringArray(battlefield, "standbyObjectIds"))
            {
                var card = objectIndex.TryGetValue(objectId, out var visibleCard)
                    ? visibleCard
                    : new SnapshotCardRef(objectId, string.Empty, false, true);
                if (string.Equals(card.ControllerOrOwner, viewerPlayerId, StringComparison.Ordinal))
                {
                    self.Add(card);
                }
                else
                {
                    opponent.Add(card);
                }
            }
        }

        return (await BuildWireStandbySideCardsAsync(self), await BuildWireStandbySideCardsAsync(opponent));
    }

    private async Task<(Godot.Collections.Array<Godot.Collections.Dictionary> Cards, int CardCount, int OfficialImageCount)> BuildWireCardsAsync(
        IReadOnlyList<string> objectIds,
        IReadOnlyDictionary<string, SnapshotCardRef> objectIndex)
    {
        var views = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var officialImageCount = 0;
        foreach (var objectId in objectIds)
        {
            var view = await BuildCardViewAsync(objectIndex.TryGetValue(objectId, out var card)
                ? card
                : new SnapshotCardRef(objectId, string.Empty, false, true));
            if (view.ContainsKey("imagePath"))
            {
                officialImageCount++;
            }

            views.Add(view);
        }

        return (views, views.Count, officialImageCount);
    }

    private static string WirePlayerSide(JsonElement player, string viewerPlayerId)
    {
        var perspective = ReadString(player, "perspective");
        if (perspective == "self" || perspective == "opponent")
        {
            return perspective;
        }

        return string.Equals(ReadString(player, "playerId"), viewerPlayerId, StringComparison.Ordinal)
            ? "self"
            : "opponent";
    }

    private static int ReadSnapshotPlayerScore(JsonElement snapshot, string playerId)
    {
        if (!snapshot.TryGetProperty("players", out var players)
            || players.ValueKind != JsonValueKind.Object
            || !players.TryGetProperty(playerId, out var player)
            || player.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return ReadInt(player, "score");
    }

    private static string ReadSnapshotPlayerResources(JsonElement snapshot, string playerId)
    {
        if (!snapshot.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Object
            || !players.TryGetProperty(playerId, out var player) || player.ValueKind != JsonValueKind.Object
            || !player.TryGetProperty("runePool", out var pool) || pool.ValueKind != JsonValueKind.Object)
            return string.Empty;
        var summary = $"法力 {ReadInt(pool, "mana")} · 符能 {ReadInt(pool, "power")}";
        if (pool.TryGetProperty("powerByTrait", out var traits) && traits.ValueKind == JsonValueKind.Object)
        {
            var colors = traits.EnumerateObject().Where(trait => trait.Value.TryGetInt32(out var count) && count > 0)
                .Select(trait => $"{trait.Name switch { "red" => "红", "green" => "绿", "blue" => "蓝", "yellow" => "黄", "orange" => "橙", "purple" => "紫", _ => trait.Name }} {trait.Value.GetInt32()}");
            var details = string.Join(" · ", colors);
            if (details.Length > 0) summary += "\n" + details;
        }
        return summary;
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
            if (view.ContainsKey("imagePath"))
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

    private IReadOnlyDictionary<string, SnapshotCardRef> VisibleObjectIndex(JsonElement snapshot, JsonElement table)
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

    private SnapshotCardRef CardRefFor(string objectId, IReadOnlyDictionary<string, JsonElement> objects)
    {
        if (!objects.TryGetValue(objectId, out var card) || card.ValueKind != JsonValueKind.Object)
        {
            return new SnapshotCardRef(objectId, string.Empty, false, true);
        }

        return CardRefFromObject(objectId, card);
    }

    private SnapshotCardRef CardRefFromObject(string objectId, JsonElement card)
        => SnapshotCardRef.FromSnapshot(objectId, card, _authenticatedHandle);

    private async Task<Godot.Collections.Dictionary> BuildCardViewAsync(SnapshotCardRef card)
    {
        var view = await _cardViewFactory.BuildAsync(card, _officialCatalog, _shutdown.Token, waitForImage: false);
        return view.ToGodotDictionary();
    }

    public override void _Process(double delta)
    {
        _imageRefreshElapsed += delta;
        if (_isShuttingDown || _imageRefreshElapsed < 0.2
            || Interlocked.Exchange(ref _imageRefreshRequested, 0) == 0) return;
        _imageRefreshElapsed = 0;
        if (_lobbyScreen?.Visible == true) _ = RefreshDeckPreviewAsync();
        if (_latestSnapshotMessage is not null)
            _ = RenderSnapshotAsync(_latestSnapshotMessage, Volatile.Read(ref _snapshotRenderVersion));
    }

    private async Task RefreshDeckPreviewAsync()
    {
        if (_officialCatalogLoadTask is not null) await _officialCatalogLoadTask;
        var deck = SelectedDeck();
        if (deck is null || _isShuttingDown) return;
        var card = await _cardViewFactory.BuildAsync(new SnapshotCardRef("deck-preview", deck.ChampionCardNo,
            true, false, string.Empty), _officialCatalog, _shutdown.Token, waitForImage: false);
        QueueMainThread(nameof(ApplyDeckPreview), new Godot.Collections.Dictionary
        {
            ["name"] = deck.Name, ["description"] = deck.Description, ["card"] = card.ToGodotDictionary()
        });
    }

    public void ApplyDeckPreview(Godot.Collections.Dictionary preview)
    {
        _lobbyScreen?.SetDeckPreview(preview["name"].AsString(), preview["description"].AsString(),
            preview["card"].AsGodotDictionary());
    }

    private void UpdateJoinedSession(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var token = ReadString(element, "reconnectToken");
        var roomId = string.IsNullOrWhiteSpace(message.RoomId) ? _session.RoomId : message.RoomId;
        if (string.IsNullOrWhiteSpace(token) && string.Equals(roomId, _session.RoomId, StringComparison.Ordinal))
        {
            return;
        }

        _session = _session with
        {
            RoomId = roomId,
            ReconnectToken = string.IsNullOrWhiteSpace(token) ? _session.ReconnectToken : token
        };
        QueueMainThread(nameof(ApplyRoomInput), roomId);
        _ = SaveSessionAsync();
    }

    private void HandleServerError(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var code = ReadString(element, "code");
        if (code == "RECOVERY_INCONSISTENT")
            SetStatus("这局暂时无法恢复，已保存记录仍保留。请检查服务端后重试。");
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
        var target = PlayerSessionSettings.WithConnectionTarget(session,
            ArgValue(args, "--riftbound-handle=") ?? session.Handle,
            ArgValue(args, "--riftbound-room=") ?? session.RoomId,
            ArgValue(args, "--riftbound-server=") ?? session.ServerUrl);
        return target with
        {
            PlayerKey = ArgValue(args, "--riftbound-player-key=") ?? session.PlayerKey,
            ReconnectToken = args.Contains("--riftbound-ignore-reconnect") ? null : target.ReconnectToken
        };
    }

    private static string? ArgValue(IReadOnlyList<string> args, string prefix)
    {
        return args
            .FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))
            ?[prefix.Length..]
            .Trim();
    }

    private static int ArgInt(IReadOnlyList<string> args, string prefix, int defaultValue)
    {
        var value = ArgValue(args, prefix);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static IReadOnlyList<string> CommandLineArgs()
    {
        return OS.GetCmdlineArgs()
            .Concat(OS.GetCmdlineUserArgs())
            .ToArray();
    }

    private static JsonSerializerOptions CreateClientJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string ReadObjectString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            ? ReadString(element, propertyName)
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

    private static int ReadObjectInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            ? ReadInt(element, propertyName)
            : 0;
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
        QueueMainThread(nameof(ApplyCommandFeedback), receipt.Accepted ? "" : receipt.Message);
        if (!receipt.Accepted && receipt.CmdType == CommandTypes.AssignCombatDamage)
            QueueMainThread(nameof(ApplyDamageRejection), receipt.Message);
        var tone = receipt.Accepted ? "green" : "red";
        AppendLog(
            $"[color={tone}]{Escape(label)} receipt accepted={receipt.Accepted} state={Escape(receipt.State)} message={Escape(receipt.Message)}[/color]");
    }

    public void ApplyCommandFeedback(string message) => _matchScreen?.SetCommandFeedback(message);

    public void ApplyDamageRejection(string message) => _damageAssignmentOverlay?.ShowServerRejection(message);

    private async Task DisconnectAsync()
    {
        if (_hub is null)
        {
            return;
        }

        await _hub.DisposeAsync();
        _hub = null;
    }

    private bool IsConnected()
    {
        return _hub?.IsConnected == true;
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

    private void SetMatchmakingStatus(string text)
    {
        _matchmakingWaiting = text.StartsWith("Queued", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Queueing", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Waiting", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("正在匹配", StringComparison.Ordinal);
        QueueMainThread(nameof(ApplyMatchmakingStatus), text);
    }

    private void RefreshLobbySetupState(bool? connected = null)
    {
        if (_lobbyScreen is null)
        {
            return;
        }

        var isConnected = connected ?? IsConnected();
        var hasDecks = _decks.Count > 0;
        var canSubmitDeck = isConnected && hasDecks && _lobbyCanSubmitDeckFromPrompt;
        var canReady = isConnected && _lobbyCanReadyFromPrompt;
        var guidance = !isConnected
            ? "连接服务器后即可选择房间和卡组。"
            : !hasDecks
                ? "没有可用的预组卡组。"
                : canReady
                    ? "卡组已提交，可以准备开始。"
                    : canSubmitDeck
                        ? "请选择预组卡组并提交。"
                        : "等待服务器更新房间状态。";
        _lobbyScreen.SetSetupState(
            canSubmitDeck,
            canReady,
            guidance);
    }

    private void RefreshLobbySetupStateFromPrompt(
        Godot.Collections.Array<Godot.Collections.Dictionary> actions)
    {
        _lobbyCanSubmitDeckFromPrompt = HasEnabledPromptAction(actions, "SUBMIT_DECK");
        _lobbyCanReadyFromPrompt = HasEnabledPromptAction(actions, "READY");
        GD.Print(
            $"[Riftbound] Lobby prompt availability: submitDeck={_lobbyCanSubmitDeckFromPrompt} ready={_lobbyCanReadyFromPrompt}.");
        RefreshLobbySetupState();
    }

    private void ResetLobbyPromptState()
    {
        _latestSnapshotMessage = null;
        Interlocked.Increment(ref _snapshotRenderVersion);
        _lobbyCanSubmitDeckFromPrompt = false;
        _lobbyCanReadyFromPrompt = false;
        QueueMainThread(nameof(ApplyLobbySetupState));
    }

    private static bool HasEnabledPromptAction(
        Godot.Collections.Array<Godot.Collections.Dictionary> actions,
        string actionName)
    {
        return actions.Any(action =>
            action.TryGetValue("action", out var actionValue)
            && string.Equals(actionValue.AsString(), actionName, StringComparison.Ordinal)
            && action.TryGetValue("enabled", out var enabledValue)
            && enabledValue.AsBool());
    }

    private void AppendLog(string text)
    {
        GD.Print($"[Riftbound] {text}");
        QueueMainThread(nameof(ApplyLog), text);
    }

    public void ApplyStatus(string text)
    {
        var connected = IsConnected();
        _lobbyScreen?.SetStatus(text, connected, _matchmakingWaiting);
        var recovering = text is "Connecting" or "Reconnecting" or "Restoring";
        _matchScreen?.SetConnectionStatus(connected, recovering);
        if (!connected)
        {
            _runeActionPanel?.Load("", -1, [], []);
            _battleDeclaration?.Hide();
            _promptInteractionController.ClearSelection();
            _movementOverlay?.Hide();
            _playCardOverlay?.Hide();
            HideSpecialPromptOverlays();
            _matchScreen?.ClearPromptStates();
        }
        else if (_lastAppliedPromptView is not null)
        {
            PresentPromptInteraction(_lastAppliedPromptView);
        }
        RefreshLobbySetupState(connected);
    }

    public void ApplyLobbySetupState()
    {
        RefreshLobbySetupState();
    }

    public void ApplyMatchmakingStatus(string text)
    {
        _lobbyScreen?.SetMatchmakingStatus(text, _matchmakingWaiting);
    }

    public void ApplyRoomInput(string roomId)
    {
        if (_lobbyScreen is not null)
        {
            _lobbyScreen.RoomText = roomId;
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

    public void ApplyMatchResult(Godot.Collections.Dictionary result)
    {
        _matchFinished = true;
        SetBattleChromeVisible(battleActive: true);
        _cardInspectOverlay?.HideCard();
        _lastViewerResult = BuildViewerResult(result);

        if (_resultOverlay is not null)
        {
            _resultOverlay.ShowResult(_lastViewerResult);
        }

        QueueResultScreenshotIfReady();
    }

    public void ClearMatchResult()
    {
        _matchFinished = false;
        _battleTableRendered = false;
        _lastAppliedPromptView = null;
        Interlocked.Exchange(ref _latestObservedPromptSnapshotTick, -1L);
        _autoSmokeSurrenderSubmitted = false;
        _resultScreenshotSaved = false;
        _lastViewerResult = new Godot.Collections.Dictionary();
        _cardInspectOverlay?.HideCard();
        _resultOverlay?.HideResult();
        HideSpecialPromptOverlays();
        _promptInteractionController.ClearSelection();
        _matchScreen?.ActionBar.SetWaiting("等待服务端提供下一步行动。");
        SetBattleChromeVisible(battleActive: false);
        _matchScreen?.RenderSections([]);
    }

    private Godot.Collections.Dictionary BuildViewerResult(Godot.Collections.Dictionary result)
    {
        var winnerPlayerId = ResultString(result, "winnerPlayerId");
        var surrenderedPlayerId = ResultString(result, "surrenderedPlayerId");
        var reason = ResultString(result, "reason");
        var winningScore = Math.Max(0, ResultInt(result, "winningScore"));
        var knownWinner = !string.IsNullOrWhiteSpace(winnerPlayerId);
        var youWon = knownWinner
            && string.Equals(winnerPlayerId, _authenticatedHandle, StringComparison.Ordinal);

        return new Godot.Collections.Dictionary
        {
            ["outcome"] = knownWinner ? youWon ? "胜利" : "失败" : "对局结束",
            ["winner"] = knownWinner ? youWon ? "你" : "对手" : string.Empty,
            ["score"] = winningScore,
            ["reason"] = ViewerResultReason(reason, surrenderedPlayerId, winningScore)
        };
    }

    private string ViewerResultReason(string reason, string surrenderedPlayerId, int winningScore)
    {
        if (!string.IsNullOrWhiteSpace(surrenderedPlayerId))
        {
            var youSurrendered = string.Equals(
                surrenderedPlayerId,
                _authenticatedHandle,
                StringComparison.Ordinal);
            return youSurrendered ? "你投降" : "对手投降";
        }

        return reason.ToUpperInvariant() switch
        {
            "SURRENDER" => "投降",
            "SCORE" or "SCORE_THRESHOLD" or "VICTORY_POINTS" => "达到胜利分数",
            "TIMEOUT" => "对局超时",
            "DISCONNECT" => "连接中断判定",
            _ when winningScore > 0 => "达到胜利分数",
            _ => "服务端确认对局结束"
        };
    }


    private static string ResultString(Godot.Collections.Dictionary result, string key)
    {
        return result.TryGetValue(key, out var value) ? value.AsString() : string.Empty;
    }

    private static int ResultInt(Godot.Collections.Dictionary result, string key)
    {
        return result.TryGetValue(key, out var value) ? value.AsInt32() : -1;
    }

    public void ApplyPrompt(Godot.Collections.Dictionary view)
    {
        if (view.TryGetValue("snapshotTick", out var nextTick)
            && nextTick.AsInt64() != _promptInteractionController.SnapshotTick)
            _cardInspectOverlay?.HidePile();
        var actions = view.TryGetValue("actions", out var actionsValue)
            ? actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        // Rejected damage commands resend the same prompt. Keep the editable
        // totals and feedback; a new prompt/tick still invalidates all selections.
        var preserveDamageSelection = _damageAssignmentOverlay is { Visible: true, CanUsePrompt: true }
            && _lastAppliedPromptView is not null
            && view.TryGetValue("promptId", out var promptId)
            && _lastAppliedPromptView.TryGetValue("promptId", out var previousId) && promptId.AsString() == previousId.AsString()
            && view.TryGetValue("snapshotTick", out var tick)
            && _lastAppliedPromptView.TryGetValue("snapshotTick", out var previousTick) && tick.AsInt64() == previousTick.AsInt64();
        if (!preserveDamageSelection) HideSpecialPromptOverlays();
        _lastAppliedPromptView = view.Duplicate(true);
        if (_runeActionPanel?.IsSubmitting == true
            && (_runeActionPanel.PromptId != view["promptId"].AsString() || _runeActionPanel.SnapshotTick != view["snapshotTick"].AsInt64()))
        { _promptSubmissionInFlight = false; _matchScreen?.ActionBar.SetPending(false); }
        var tap = TryGetCurrentSpecialAction("TAP_RUNE", out var tapAction) ? PromptChoiceIds(tapAction, "sourceChoices") : [];
        var recycle = TryGetCurrentSpecialAction("RECYCLE_RUNE", out var recycleAction) ? PromptChoiceIds(recycleAction, "sourceChoices") : [];
        _runeActionPanel?.Load(view["promptId"].AsString(), view["snapshotTick"].AsInt64(), tap, recycle);
        _promptInteractionController.Load(view);
        PresentPromptInteraction(view);
        TryStageAutoSmokeUiAction();
        RefreshLobbySetupStateFromPrompt(actions);
        RefreshPromptHighlights(actions);
        RedrawLastSnapshotSections();
        ScheduleAutoSmokePrompt(view);
    }

    private void RefreshPromptHighlights(Godot.Collections.Array<Godot.Collections.Dictionary> actions)
    {
        var next = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            var enabled = action.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
            if (!enabled)
            {
                continue;
            }

            foreach (var objectId in PromptChoiceIds(action, "sourceChoices"))
            {
                next.Add(objectId);
            }
        }

        lock (_promptHighlightLock)
        {
            _promptSourceObjectIds.Clear();
            foreach (var objectId in next)
            {
                _promptSourceObjectIds.Add(objectId);
            }
        }
    }

    private static IEnumerable<string> PromptChoiceIds(Godot.Collections.Dictionary action, string propertyName)
    {
        if (!action.TryGetValue(propertyName, out var choicesValue)
            || choicesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } choices)
        {
            yield break;
        }

        foreach (var choice in choices)
        {
            var choiceId = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(choiceId))
            {
                yield return choiceId;
            }

            if (choice.TryGetValue("objectIds", out var objectIdsValue)
                && objectIdsValue.As<Godot.Collections.Array<string>>() is { } objectIds)
            {
                foreach (var objectId in objectIds)
                {
                    if (!string.IsNullOrWhiteSpace(objectId))
                    {
                        yield return objectId;
                    }
                }
            }
        }
    }

    private bool IsPromptSourceObject(string objectId)
    {
        lock (_promptHighlightLock)
        {
            return _promptSourceObjectIds.Contains(objectId);
        }
    }

    private void RedrawLastSnapshotSections()
    {
        RefreshPromptInteractionVisuals();
    }

    private void ShowSpecialPromptOverlays(Godot.Collections.Array<Godot.Collections.Dictionary> actions)
    {
        var action = actions.FirstOrDefault(candidate =>
            candidate.TryGetValue("enabled", out var enabledValue)
            && enabledValue.AsBool()
            && candidate.TryGetValue("action", out var actionValue)
            && actionValue.AsString() is "MULLIGAN" or "ORDER_TRIGGERS" or "ASSIGN_COMBAT_DAMAGE");
        if (action is null)
        {
            HideSpecialPromptOverlays();
            return;
        }

        switch (ReadActionName(action))
        {
            case "MULLIGAN":
                _triggerOrderOverlay?.HidePrompt();
                _damageAssignmentOverlay?.HidePrompt();
                var visibleHandCards = VisibleMulliganHandCards(action, out var hasHandSnapshot);
                if (!hasHandSnapshot)
                {
                    _mulliganOverlay?.HidePrompt();
                    break;
                }

                if (_mulliganOverlay is not null && (!_mulliganOverlay.Visible || !_mulliganOverlay.CanUsePrompt))
                {
                    if (!_mulliganOverlay.ShowPrompt(action, visibleHandCards, out var reason))
                    {
                        AppendLog($"[color=yellow]Mulligan overlay disabled: {Escape(reason)}[/color]");
                    }
                }
                else
                {
                    _mulliganOverlay?.RefreshVisibleCards(visibleHandCards);
                }

                break;
            case "ORDER_TRIGGERS":
                _mulliganOverlay?.HidePrompt();
                _damageAssignmentOverlay?.HidePrompt();
                if (_triggerOrderOverlay is not null && (!_triggerOrderOverlay.Visible || !_triggerOrderOverlay.CanUsePrompt))
                {
                    if (!_triggerOrderOverlay.ShowPrompt(action, out var reason))
                    {
                        AppendLog($"[color=yellow]Trigger-order overlay disabled: {Escape(reason)}[/color]");
                    }
                }

                break;
            case "ASSIGN_COMBAT_DAMAGE":
                _mulliganOverlay?.HidePrompt();
                _triggerOrderOverlay?.HidePrompt();
                if (_damageAssignmentOverlay is not null && (!_damageAssignmentOverlay.Visible || !_damageAssignmentOverlay.CanUsePrompt))
                {
                    if (!_damageAssignmentOverlay.ShowPrompt(action, out var reason))
                    {
                        AppendLog($"[color=yellow]Damage-assignment overlay disabled: {Escape(reason)}[/color]");
                    }
                }

                break;
        }
    }

    private void HideSpecialPromptOverlays()
    {
        _mulliganOverlay?.HidePrompt();
        _triggerOrderOverlay?.HidePrompt();
        _damageAssignmentOverlay?.HidePrompt();
    }

    private void ReopenSpecialPromptOverlay()
    {
        if (_lastAppliedPromptView is null
            || !_lastAppliedPromptView.TryGetValue("actions", out var actionsValue)
            || actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } actions)
        {
            HideSpecialPromptOverlays();
            return;
        }

        // The server prompt is unchanged: discard only the local overlay state.
        HideSpecialPromptOverlays();
        ShowSpecialPromptOverlays(actions);
    }

    private IReadOnlyList<Godot.Collections.Dictionary> VisibleMulliganHandCards(
        Godot.Collections.Dictionary action,
        out bool hasHandSnapshot)
    {
        hasHandSnapshot = false;
        var sourceIds = new HashSet<string>(PromptChoiceIds(action, "sourceChoices"), StringComparer.Ordinal);
        if (_lastSnapshotSections is null || sourceIds.Count == 0)
        {
            return [];
        }

        foreach (var section in _lastSnapshotSections)
        {
            if (!section.TryGetValue("kind", out var kindValue)
                || !string.Equals(kindValue.AsString(), "wireTable", StringComparison.Ordinal)
                || !section.TryGetValue("self", out var selfValue))
            {
                continue;
            }

            var self = selfValue.AsGodotDictionary();
            if (!self.TryGetValue("hand", out var handValue)
                || handValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is not { } hand)
            {
                return [];
            }

            hasHandSnapshot = hand.Count > 0;
            return hand
                .Where(card => card.TryGetValue("objectId", out var objectIdValue)
                    && sourceIds.Contains(objectIdValue.AsString())
                    && (!card.TryGetValue("visible", out var visibleValue) || visibleValue.AsBool())
                    && (!card.TryGetValue("faceDown", out var faceDownValue) || !faceDownValue.AsBool()))
                .Select(card => card.Duplicate(true))
                .ToArray();
        }

        return [];
    }

    private void PresentPromptInteraction(Godot.Collections.Dictionary view)
    {
        if (!IsConnected()) return;
        if (_battleDeclaration?.Visible == true && (_battleDeclaration.PromptId != view["promptId"].AsString()
            || _battleDeclaration.SnapshotTick != view["snapshotTick"].AsInt64())) _battleDeclaration.Hide();
        if (_playCardOverlay?.Visible == true
            && (_playCardOverlay.PromptId != view["promptId"].AsString()
                || _playCardOverlay.SnapshotTick != view["snapshotTick"].AsInt64()))
            _playCardOverlay.Hide();
        if (_movementOverlay?.Visible == true
            && (_movementOverlay.PromptId != view["promptId"].AsString()
                || _movementOverlay.SnapshotTick != view["snapshotTick"].AsInt64()))
            _movementOverlay.Hide();
        if (_matchScreen is null)
        {
            return;
        }

        var actionable = view.TryGetValue("actionable", out var actionableValue) && actionableValue.AsBool();
        var message = view.TryGetValue("message", out var messageValue) ? messageValue.AsString() : string.Empty;
        var reason = view.TryGetValue("reason", out var reasonValue) ? reasonValue.AsString() : string.Empty;
        var detail = !string.IsNullOrWhiteSpace(message)
            ? message
            : !string.IsNullOrWhiteSpace(reason)
                ? reason
                : actionable
                    ? "选择手牌或场上的卡牌行动，也可以结束回合。"
                    : "等待对手行动。";
        detail = detail switch
        {
            "当前玩家可让过焦点" => "战场正在争夺：可继续行动，或让过焦点推进战斗。",
            "当前玩家可让过优先权" or "当前玩家可让过先行动权" => "可打出响应，或让过优先行动权继续结算。",
            "当前玩家普通开环行动" => "选择手牌或场上的卡牌行动，也可以结束回合。",
            "等待普通开行动玩家" or "等待对手行动" or "等待对手行动。" => "对手正在行动，你可以查看手牌和公开卡牌。",
            _ => detail
        };
        var responding = actionable && _promptInteractionController.Actions.Any(action => action.Enabled && action.Name == "PASS_PRIORITY");
        if (responding) detail = "可打出响应，或让过优先行动权继续结算。";
        var waitingForResponse = !actionable && (detail.Contains("优先行动", StringComparison.Ordinal)
            || detail.Contains("优先权", StringComparison.Ordinal));
        var settling = !actionable && detail.StartsWith("等待服务端处理", StringComparison.Ordinal);
        if (waitingForResponse) detail = "对手正在选择是否响应，结算链暂时保留。";
        _matchScreen.SetTurnStatus(
            responding ? "轮到你响应" : actionable && _promptInteractionController.Actions.Any(action => action.Enabled && action.Name == "PASS_FOCUS")
                    ? "战场争夺中" : actionable ? "轮到你行动" : settling ? "正在结算" : waitingForResponse ? "等待对手响应" : "等待对手行动",
            detail,
            actionable,
            useWindowDetail: responding || waitingForResponse
                || _promptInteractionController.Actions.Any(action => action.Enabled && action.Name == "PASS_FOCUS")
                || reason.Contains("焦点", StringComparison.Ordinal));
        _matchScreen.ActionBar.ShowPrompt(detail, _promptInteractionController.Actions);
        if (_promptInteractionController.Current is { } state)
        {
            _matchScreen.ActionBar.ShowSelection(
                state,
                FriendlyPromptChoices(),
                _promptInteractionController.CurrentStepLabel,
                _promptInteractionController.CurrentStepRequired);
        }

        RefreshPromptInteractionVisuals();
        var actions = view.TryGetValue("actions", out var actionsValue)
            ? actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        ShowSpecialPromptOverlays(actions);
    }

    private async Task SubmitSpecialPromptAsync(
        Godot.Collections.Dictionary action,
        Dictionary<string, object?> payload,
        string intentSuffix)
    {
        await SubmitPromptPayloadAsync(action, payload, intentSuffix);
    }

    public void ApplySnapshotSections(Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        if (_matchScreen is null)
        {
            return;
        }

        _lastSnapshotSections = sections;
        if (_playCardOverlay?.Visible == true) _playCardOverlay.RefreshCardPreview();
        var battleActive = HasWireTableSection(sections);
        SetBattleChromeVisible(_matchFinished || battleActive);
        if (_matchFinished && !battleActive)
        {
            return;
        }

        if (battleActive)
        {
            _matchScreen.RenderSections(sections);
            _battleTableRendered = true;
            if (_lastAppliedPromptView is not null)
            {
                PresentPromptInteraction(_lastAppliedPromptView);
            }
            TryStageAutoSmokeUiAction();
            if (_lastAppliedPromptView is not null)
            {
                ScheduleAutoSmokePrompt(_lastAppliedPromptView);
            }
            return;
        }

        _battleTableRendered = false;
        _matchScreen.RenderSections([]);
    }

    private void SetBattleChromeVisible(bool battleActive)
    {
        var lobbyVisible = !battleActive;
        if (_lobbyScreen is not null)
        {
            _lobbyScreen.SetScreenVisible(lobbyVisible);
        }

        if (_matchScreen is not null)
        {
            _matchScreen.SetScreenVisible(battleActive);
        }

        if (!battleActive)
        {
            _cardInspectOverlay?.HideCard();
            if (!_matchFinished)
            {
                _resultOverlay?.HideResult();
            }
        }

    }

    private static bool HasWireTableSection(Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        if (sections.Count != 1
            || !sections[0].TryGetValue("kind", out var kind)
            || !string.Equals(kind.AsString(), "wireTable", StringComparison.Ordinal))
        {
            return false;
        }

        var turnState = sections[0].TryGetValue("turnState", out var turnStateValue)
            ? turnStateValue.AsString()
            : string.Empty;
        return !string.IsNullOrWhiteSpace(turnState)
            && !string.Equals(turnState, "ROOM", StringComparison.OrdinalIgnoreCase);
    }

    private void QueueVisualScreenshotIfReady(int tableCardCount)
    {
        if (_visualScreenshotSaved
            || string.IsNullOrWhiteSpace(_visualScreenshotPath)
            || tableCardCount < _visualScreenshotMinTableCards)
        {
            return;
        }

        _visualScreenshotSaved = true;
        QueueMainThread(nameof(CaptureVisualScreenshot), _visualScreenshotPath);
    }

    private void QueueResultScreenshotIfReady()
    {
        if (_resultScreenshotSaved || string.IsNullOrWhiteSpace(_visualScreenshotPath))
        {
            return;
        }

        _resultScreenshotSaved = true;
        CaptureResultScreenshot(ResultScreenshotPath(_visualScreenshotPath));
    }

    private static string ResultScreenshotPath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        var extension = Path.GetExtension(path);
        var fileName = Path.GetFileNameWithoutExtension(path);
        var resultFileName = string.IsNullOrWhiteSpace(extension)
            ? $"{fileName}-result.png"
            : $"{fileName}-result{extension}";
        return string.IsNullOrWhiteSpace(directory)
            ? resultFileName
            : Path.Combine(directory, resultFileName);
    }

    public async void CaptureVisualScreenshot(string path)
    {
        await CaptureVisualScreenshotAsync(path, forceResultChrome: false);
    }

    private async void CaptureResultScreenshot(string path)
    {
        await CaptureVisualScreenshotAsync(path, forceResultChrome: true);
    }

    private async Task CaptureVisualScreenshotAsync(string path, bool forceResultChrome)
    {
        try
        {
            if (forceResultChrome)
            {
                ForceResultScreenshotChrome();
            }

            var frameDelay = forceResultChrome ? ResultScreenshotFrameDelay : 2;
            for (var frame = 0; frame < frameDelay; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (forceResultChrome)
                {
                    ForceResultScreenshotChrome();
                }
            }

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            RenderingServer.ForceDraw();
            RenderingServer.ForceSync();
            if (forceResultChrome)
            {
                LogResultScreenshotLayout();
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var image = GetViewport().GetTexture().GetImage();
            var error = image.SavePng(path);
            if (error == Error.Ok)
            {
                AppendLog($"Visual screenshot saved: {Escape(path)}");
            }
            else
            {
                AppendLog($"[color=yellow]Visual screenshot failed: {error} {Escape(path)}[/color]");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Visual screenshot failed: {Escape(ex.Message)}[/color]");
        }
    }

    private void LogResultScreenshotLayout()
    {
        var panel = GetNodeOrNull<PanelContainer>("ResultOverlay/ResultCenter/ResultPanel");
        var button = GetNodeOrNull<Button>("ResultOverlay/ResultCenter/ResultPanel/ContentMargin/ResultContent/ReturnButton");
        var styleType = panel is null
            ? "missing"
            : panel.GetThemeStylebox("panel").GetType().Name;
        AppendLog(
            $"Result screenshot layout: overlayVisible={_resultOverlay?.IsVisibleInTree()} "
            + $"overlaySize={_resultOverlay?.Size} panelVisible={panel?.IsVisibleInTree()} "
            + $"panelPosition={panel?.GlobalPosition} panelSize={panel?.Size} panelStyle={styleType} "
            + $"buttonVisible={button?.IsVisibleInTree()} buttonPosition={button?.GlobalPosition} buttonSize={button?.Size}");
    }

    private void ForceResultScreenshotChrome()
    {
        _matchFinished = true;
        SetBattleChromeVisible(battleActive: true);
        if (_resultOverlay is not null && _lastViewerResult.Count > 0)
        {
            _resultOverlay.ShowResult(_lastViewerResult);
        }
    }

    public void ApplyCardPreview(Godot.Collections.Dictionary card)
    {
        var visible = card.TryGetValue("visible", out var visibleValue) && visibleValue.AsBool();
        var faceDown = card.TryGetValue("faceDown", out var faceDownValue) && faceDownValue.AsBool();
        if (!visible || faceDown || _cardInspectOverlay is null)
        {
            return;
        }

        _cardInspectOverlay.ShowCard(card);
    }

    public void ApplyDeckOptions()
    {
        if (_lobbyScreen is null)
        {
            return;
        }

        var decks = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var selected = 0;
        for (var i = 0; i < _decks.Count; i++)
        {
            var deck = _decks[i];
            decks.Add(new Godot.Collections.Dictionary
            {
                ["name"] = deck.Name,
                ["description"] = deck.Description
            });
            if (string.Equals(deck.Id, _session.LastDeckId, StringComparison.Ordinal))
            {
                selected = i;
            }
        }

        _lobbyScreen.SetDeckOptions(decks, selected);
        _ = RefreshDeckPreviewAsync();
        RefreshLobbySetupState();
    }

    public void ApplyPublicMatchOptions()
    {
        if (_lobbyScreen is null)
        {
            return;
        }

        var matches = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var match in _publicMatches)
        {
            matches.Add(new Godot.Collections.Dictionary
            {
                ["roomId"] = match.RoomId,
                ["seats"] = $"{match.SeatCount}/{match.Capacity}",
                ["status"] = match.Status
            });
        }

        _lobbyScreen.SetPublicMatches(matches);
    }

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
