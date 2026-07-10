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
    [Export] public bool AutoConnectOnReady { get; set; } = true;
    [Export] public bool UseLegacyCardTableFallback { get; set; } = false;
    [Export] public string OfficialCatalogSnapshotPath { get; set; } = "res://../../data/official/card-catalog.zh-CN.json";
    [Export] public string PreviewCardNo { get; set; } = "UNL-181/219";

    private static readonly JsonSerializerOptions ClientJsonOptions = CreateClientJsonOptions();
    private readonly CancellationTokenSource _shutdown = new();
    private PlayerSessionStore _sessionStore = new();
    private readonly List<PreconstructedDeck> _decks = [];
    private readonly List<PublicMatchDto> _publicMatches = [];
    private readonly OfficialCardImageLoader _cardImageLoader = new();
    private readonly CardViewFactory _cardViewFactory;
    private readonly CardControlRenderer _cardControlRenderer;
    private readonly PromptInteractionController _promptInteractionController = new();
    private readonly object _promptHighlightLock = new();
    private readonly HashSet<string> _promptSourceObjectIds = new(StringComparer.Ordinal);

    private PlayerSessionSettings _session = PlayerSessionSettings.CreateDefault();
    private RichTextLabel? _log;
    private Control? _controls;
    private LobbyScreen? _lobbyScreen;
    private MatchScreen? _matchScreen;
    private CardInspectOverlay? _cardInspectOverlay;
    private ResultOverlay? _resultOverlay;
    private MulliganOverlay? _mulliganOverlay;
    private TriggerOrderOverlay? _triggerOrderOverlay;
    private DamageAssignmentOverlay? _damageAssignmentOverlay;
    private Label? _boardSummary;
    private ScrollContainer? _snapshotScroll;
    private ScrollContainer? _legacyHandScroll;
    private VBoxContainer? _snapshotRows;
    private HBoxContainer? _handRow;
    private TextureRect? _officialCardPreview;
    private Label? _officialCardPreviewSummary;
    private PanelContainer? _officialCardPreviewFrame;
    private PanelContainer? _promptFrame;
    private PanelContainer? _resultFrame;
    private Label? _resultSummary;
    private Label? _promptSummary;
    private VBoxContainer? _promptActions;
    private Button? _returnLobbyButton;
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
    private bool _battleChromeHidden;
    private bool _matchmakingWaiting;
    private bool _lobbyCanSubmitDeckFromPrompt;
    private bool _lobbyCanReadyFromPrompt;
    private bool _ephemeralSession;
    private bool _isShuttingDown;
    private int _snapshotRenderVersion;
    private string _lastJoinedMatchmakingRoom = string.Empty;
    private readonly HashSet<string> _autoSmokePromptSubmissions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _autoSmokeActionSubmissions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _autoSmokeUiStages = new(StringComparer.Ordinal);
    private Task? _officialCatalogLoadTask;
    private IReadOnlyDictionary<string, CardCatalogEntry> _officialCatalog =
        new Dictionary<string, CardCatalogEntry>(StringComparer.Ordinal);
    private Godot.Collections.Array<Godot.Collections.Dictionary>? _lastSnapshotSections;
    private Godot.Collections.Dictionary? _lastAppliedPromptView;
    private Godot.Collections.Dictionary _lastViewerResult = new();

    public Main()
    {
        _cardViewFactory = new CardViewFactory(_cardImageLoader);
        _cardControlRenderer = new CardControlRenderer(ApplyCardPreview, IsPromptSourceObject);
    }

    public override async void _Ready()
    {
        RenderingServer.SetDefaultClearColor(MinimalTheme.AppBackground);
        BindNodes();
        ApplyRunestoneTheme();
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
        ApplySessionToInputs();
        _officialCatalogLoadTask = LoadOfficialCardPreviewAsync();
        _ = LoadDecksAsync();
        _ = LoadPublicMatchesAsync();

        if (AutoConnectOnReady && !_autoSmokeQuickMatch && !_autoSmokePublicMatch && !_autoSmokeJoinPublicMatch)
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
        ReleaseRuntimeUiResources();
        _ = DisconnectAsync();
        _shutdown.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void BindNodes()
    {
        _log = GetNode<RichTextLabel>("Log");
        _controls = GetNode<Control>("Controls");
        _lobbyScreen = GetNode<LobbyScreen>("Controls/LobbyScreen");
        _matchScreen = GetNode<MatchScreen>("MatchScreen");
        _cardInspectOverlay = GetNode<CardInspectOverlay>("CardInspectOverlay");
        _resultOverlay = GetNode<ResultOverlay>("ResultOverlay");
        _mulliganOverlay = GetNode<MulliganOverlay>("MulliganOverlay");
        _triggerOrderOverlay = GetNode<TriggerOrderOverlay>("TriggerOrderOverlay");
        _damageAssignmentOverlay = GetNode<DamageAssignmentOverlay>("DamageAssignmentOverlay");
        _boardSummary = GetNode<Label>("Controls/BoardSummary");
        _snapshotScroll = GetNode<ScrollContainer>("Controls/SnapshotScroll");
        _snapshotRows = GetNode<VBoxContainer>("Controls/SnapshotScroll/SnapshotRows");
        _legacyHandScroll = GetNode<ScrollContainer>("Controls/HandScroll");
        _handRow = GetNode<HBoxContainer>("Controls/HandScroll/HandRow");
        _officialCardPreviewFrame = GetNode<PanelContainer>("OfficialCardPreviewFrame");
        _officialCardPreview = GetNode<TextureRect>("OfficialCardPreviewFrame/OfficialPreviewBox/OfficialCardPreview");
        _officialCardPreviewSummary = GetNode<Label>("OfficialCardPreviewFrame/OfficialPreviewBox/OfficialCardPreviewSummary");
        _resultFrame = GetNode<PanelContainer>("ResultFrame");
        _resultSummary = GetNode<Label>("ResultFrame/ResultBox/ResultSummary");
        _promptFrame = GetNode<PanelContainer>("PromptFrame");
        _promptSummary = GetNode<Label>("PromptFrame/PromptBox/PromptSummary");
        _promptActions = GetNode<VBoxContainer>("PromptFrame/PromptBox/PromptScroll/PromptActions");
        _returnLobbyButton = GetNode<Button>("ResultFrame/ResultBox/ReturnLobbyButton");
    }

    private void InstallRunestoneBackdrop()
    {
        if (GetNodeOrNull<RunestoneBackdrop>("RunestoneBackdrop") is not null)
        {
            return;
        }

        var backdrop = new RunestoneBackdrop
        {
            Name = "RunestoneBackdrop",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = 0,
            OffsetBottom = 0,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(backdrop);
        MoveChild(backdrop, 0);
    }

    private void ApplyRunestoneTheme()
    {
        ApplyMainContentGutter();
        RunestoneTheme.ApplyToTree(this);
        var title = GetNodeOrNull<Label>("Title");
        if (title is not null)
        {
            title.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
        }

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

        if (_boardSummary is not null)
        {
            _boardSummary.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
        }

        if (_officialCardPreviewFrame is not null)
        {
            _officialCardPreviewFrame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(RunestoneSurface.Card, 2));
        }

        if (_promptFrame is not null)
        {
            _promptFrame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(RunestoneSurface.Result, 2));
        }

        if (_resultFrame is not null)
        {
            _resultFrame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(RunestoneSurface.Result, 3));
        }
    }

    private void ApplyMainContentGutter()
    {
        foreach (var path in new[] { "Title", "Controls" })
        {
            var control = GetNodeOrNull<Control>(path);
            if (control is not null)
            {
                control.OffsetLeft = Math.Max(control.OffsetLeft, 24f);
            }
        }
    }

    private void WireButtons()
    {
        _lobbyScreen!.ConnectRequested += () => _ = ConnectAndRequestSnapshotAsync(useReconnectToken: false);
        _lobbyScreen.ReconnectRequested += () => _ = ConnectAndRequestSnapshotAsync(useReconnectToken: true);
        _lobbyScreen.CreatePublicMatchRequested += () => _ = CreatePublicMatchAsync();
        _lobbyScreen.QueueRequested += () => _ = QueueMatchmakingAsync();
        _lobbyScreen.CancelQueueRequested += () => _ = CancelMatchmakingAsync();
        _lobbyScreen.JoinPublicMatchRequested += () => _ = JoinSelectedPublicMatchAsync();
        _lobbyScreen.SubmitDeckRequested += () => _ = SubmitSelectedDeckAsync();
        _lobbyScreen.ReadyRequested += () => _ = ReadyAsync();
        _matchScreen!.CardActivated += HandleMatchCardActivated;
        _matchScreen.ActionBar.ActionSelected += HandlePromptActionSelected;
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
        _returnLobbyButton!.Pressed += () => _ = ReturnToLobbyAsync();
    }

    private void HandleMatchCardActivated(Godot.Collections.Dictionary card)
    {
        var objectId = card.TryGetValue("objectId", out var objectValue)
            ? objectValue.AsString()
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(objectId)
            && _promptInteractionController.TrySelectObject(objectId))
        {
            return;
        }

        ApplyCardPreview(card);
    }

    private void HandlePromptActionSelected(string actionName)
    {
        _promptInteractionController.SelectAction(actionName);
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
            _promptInteractionController.CurrentChoices,
            _promptInteractionController.CurrentStepLabel,
            _promptInteractionController.CurrentStepRequired);
        RefreshPromptInteractionVisuals();
    }

    private void HandlePromptSelectionCleared()
    {
        _matchScreen?.ActionBar.ClearSelectionDisplay();
        RefreshPromptInteractionVisuals();
    }

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
        if (_officialCardPreview is not null)
        {
            _officialCardPreview.Texture = null;
        }

        FreeNodeChildrenImmediately(_snapshotRows);
        FreeNodeChildrenImmediately(_handRow);
        FreeNodeChildrenImmediately(_promptActions);
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

    private static void FreeNodeChildrenImmediately(Node? node)
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

            var imagePath = await _cardImageLoader.LoadOfficialFrontImagePathAsync(card, _shutdown.Token);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                AppendLog($"[color=yellow]No official front image for {Escape(card.CardNo)} {Escape(card.CardName)}[/color]");
                return;
            }

            QueueMainThread(nameof(ApplyOfficialCardPreviewPath), imagePath);
            var preview = new CardViewData(
                string.Empty,
                card.CardNo,
                card.CardName,
                card.CardCategoryName,
                card.Energy ?? -1,
                card.Power ?? -1,
                card.Trait,
                card.EffectText,
                card.RarityName,
                card.ColorText,
                Visible: true,
                FaceDown: false,
                imagePath);
            QueueMainThread(nameof(ApplyOfficialCardPreviewSummary), preview.PreviewSummary);
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

    private async Task<bool> EnsureAuthenticatedConnectionAsync()
    {
        _session = PlayerSessionSettings.WithUsableKey(ReadSessionFromInputs());
        await SaveSessionAsync();

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
            SetStatus($"Authentication rejected: {auth.Status}");
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
        _hub.LogReceived += AppendLog;
        _hub.ServerMessageReceived += LogMessage;
        return _hub;
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

    private void RenderEvents(WsServerMessage message)
    {
        if (message.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var eventKinds = new List<string>();
        foreach (var eventElement in element.EnumerateArray())
        {
            if (eventElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = ReadString(eventElement, "kind");
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

        if (eventKinds.Count > 0)
        {
            AppendLog($"Events received: {Escape(string.Join(", ", eventKinds))}.");
        }
    }

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

                return;
            }

            if (IsStaleSnapshotRender(renderVersion))
            {
                return;
            }

            var table = element.TryGetProperty("table", out var tableElement) && tableElement.ValueKind == JsonValueKind.Object
                ? tableElement
                : default;
            var summary = BuildSnapshotSummary(element, table);
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
            QueueMainThread(nameof(ApplyBoardSummary), summary);
            QueueMainThread(nameof(ApplyHandCards), views);
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
            || _matchFinished
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
            var summary = CardControlRenderer.PreviewSummary(card);
            QueueMainThread(nameof(ApplyCardPreview), card);
            AppendLog($"Auto smoke: previewing first visible card: {Escape(summary.Replace('\n', ' '))}");
            return;
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
            ["viewerPlayerId"] = viewerPlayerId,
            ["turnState"] = ReadString(snapshot, "turnState"),
            ["runeDeckSize"] = runeDeckSize,
            ["self"] = self,
            ["opponent"] = opponent,
            ["lanes"] = lanes
        }, cardCount, officialImageCount);
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
        var handIds = side == "self" ? ReadStringArray(zones, "hand") : [];
        var hand = await BuildWireCardsAsync(handIds, objectIndex);

        foreach (var result in new[] { legend, hero, baseCardViews, baseRunes, graveyard, banished, hand })
        {
            cardCount += result.CardCount;
            officialImageCount += result.OfficialImageCount;
        }

        var hiddenHandCount = side == "opponent"
            ? Math.Max(ReadInt(zones, "handHidden"), ReadArrayCount(zones, "hand"))
            : 0;

        return (new Godot.Collections.Dictionary
        {
            ["side"] = side,
            ["playerId"] = playerId,
            ["label"] = $"{(side == "self" ? "P1 我方" : "P2 对手")} · {playerId}",
            ["missing"] = false,
            ["score"] = ReadSnapshotPlayerScore(snapshot, playerId),
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
            ["hiddenStandbyCount"] = Math.Max(ReadInt(battlefield, "hiddenStandbyCount"), ReadInt(battlefield, "faceDownStandbyCount")),
            ["controllerId"] = ReadString(battlefield, "controllerId"),
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
        var controllerOrOwner = ReadString(card, "controllerId");
        if (string.IsNullOrWhiteSpace(controllerOrOwner))
        {
            controllerOrOwner = ReadString(card, "ownerId");
        }

        return new SnapshotCardRef(objectId, cardNo, !string.IsNullOrWhiteSpace(cardNo), faceDown, controllerOrOwner);
    }

    private async Task<Godot.Collections.Dictionary> BuildCardViewAsync(SnapshotCardRef card)
    {
        var view = await _cardViewFactory.BuildAsync(card, _officialCatalog, _shutdown.Token);
        return view.ToGodotDictionary();
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
        var tone = receipt.Accepted ? "green" : "red";
        AppendLog(
            $"[color={tone}]{Escape(label)} receipt accepted={receipt.Accepted} state={Escape(receipt.State)} message={Escape(receipt.Message)}[/color]");
    }

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
        var connected = IsConnected() || string.Equals(text, "Connected", StringComparison.OrdinalIgnoreCase);
        _lobbyScreen?.SetStatus(text, connected, _matchmakingWaiting);
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

    public void ApplyBoardSummary(string text)
    {
        if (_boardSummary is not null)
        {
            _boardSummary.Text = text;
        }
    }

    public void ApplyMatchResult(Godot.Collections.Dictionary result)
    {
        _matchFinished = true;
        SetBattleChromeVisible(battleActive: true);
        _cardInspectOverlay?.HideCard();
        _lastViewerResult = BuildViewerResult(result);

        if (UseLegacyCardTableFallback)
        {
            _resultOverlay?.HideResult();
            SetRightRailMatchResultVisible(matchResultVisible: true);
            if (_resultFrame is not null)
            {
                FlashResultFrame(_resultFrame);
            }

            if (_resultSummary is not null)
            {
                _resultSummary.Text = MatchResultSummaryForViewer(result);
                _resultSummary.AddThemeColorOverride("font_color", MatchResultFontColor(result));
            }
        }
        else
        {
            SetRightRailMatchResultVisible(matchResultVisible: false);
            if (_resultOverlay is not null)
            {
                _resultOverlay.ShowResult(_lastViewerResult);
            }
        }

        QueueResultScreenshotIfReady();
    }

    private void FlashResultFrame(PanelContainer resultFrame)
    {
        resultFrame.Modulate = new Color(1f, 0.82f, 0.46f, 1f);
        var tween = resultFrame.CreateTween();
        tween.TweenProperty(resultFrame, "modulate", Colors.White, 0.28d)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private Color MatchResultFontColor(Godot.Collections.Dictionary result)
    {
        var winnerPlayerId = ResultString(result, "winnerPlayerId");
        if (string.IsNullOrWhiteSpace(winnerPlayerId))
        {
            return RunestoneTheme.Ivory;
        }

        return string.Equals(winnerPlayerId, _authenticatedHandle, StringComparison.Ordinal)
            ? RunestoneTheme.Brass
            : RunestoneTheme.Warning;
    }

    public void ClearMatchResult()
    {
        _matchFinished = false;
        _battleTableRendered = false;
        _lastAppliedPromptView = null;
        _autoSmokeSurrenderSubmitted = false;
        _resultScreenshotSaved = false;
        _lastViewerResult = new Godot.Collections.Dictionary();
        _cardInspectOverlay?.HideCard();
        _resultOverlay?.HideResult();
        HideSpecialPromptOverlays();
        _promptInteractionController.ClearSelection();
        _matchScreen?.ActionBar.SetWaiting("等待服务端提供下一步行动。");
        SetRightRailMatchResultVisible(matchResultVisible: false);
        SetBattleChromeVisible(battleActive: false);
        _matchScreen?.RenderSections([]);

        if (_resultSummary is not null)
        {
            _resultSummary.Text = "No result";
        }
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

    private string MatchResultSummaryForViewer(Godot.Collections.Dictionary result)
    {
        var winnerPlayerId = ResultString(result, "winnerPlayerId");
        var surrenderedPlayerId = ResultString(result, "surrenderedPlayerId");
        var reason = ResultString(result, "reason");
        var winningScore = ResultInt(result, "winningScore");
        var knownWinner = !string.IsNullOrWhiteSpace(winnerPlayerId);
        var youWon = knownWinner && string.Equals(winnerPlayerId, _authenticatedHandle, StringComparison.Ordinal);
        var lines = new List<string>
        {
            knownWinner ? youWon ? "对局结束 · 胜利" : "对局结束 · 失败" : "对局结束"
        };

        if (knownWinner)
        {
            lines.Add($"胜者：{(youWon ? "你" : "对手")}");
        }

        if (!string.IsNullOrWhiteSpace(surrenderedPlayerId))
        {
            var youSurrendered = string.Equals(surrenderedPlayerId, _authenticatedHandle, StringComparison.Ordinal);
            lines.Add($"投降：{(youSurrendered ? "你" : "对手")}");
        }

        if (winningScore > 0)
        {
            lines.Add($"胜利分数：{winningScore}");
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            lines.Add($"原因：{ResultReasonLabel(reason)}");
        }

        return string.Join("\n", lines);
    }

    private static string ResultString(Godot.Collections.Dictionary result, string key)
    {
        return result.TryGetValue(key, out var value) ? value.AsString() : string.Empty;
    }

    private static int ResultInt(Godot.Collections.Dictionary result, string key)
    {
        return result.TryGetValue(key, out var value) ? value.AsInt32() : -1;
    }

    private static string ResultReasonLabel(string reason)
    {
        return reason switch
        {
            "SURRENDER" => "投降",
            _ => reason
        };
    }

    public void ApplyHandCards(Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        if (!UseLegacyCardTableFallback || _handRow is null)
        {
            return;
        }

        _cardControlRenderer.RenderHandCards(_handRow, cards);
    }

    public void ApplyPrompt(Godot.Collections.Dictionary view)
    {
        if (_promptActions is null)
        {
            return;
        }

        foreach (var child in _promptActions.GetChildren())
        {
            _promptActions.RemoveChild(child);
            child.Free();
        }

        var actions = view.TryGetValue("actions", out var actionsValue)
            ? actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        HideSpecialPromptOverlays();
        _lastAppliedPromptView = view.Duplicate(true);
        _promptInteractionController.Load(view);
        PresentPromptInteraction(view);
        TryStageAutoSmokeUiAction();
        RefreshLobbySetupStateFromPrompt(actions);
        RefreshPromptHighlights(actions);
        var actionable = view.TryGetValue("actionable", out var actionableValue) && actionableValue.AsBool();
        if (_promptFrame is not null)
        {
            _promptFrame.AddThemeStyleboxOverride(
                "panel",
                RunestoneTheme.FrameStyle(actionable ? RunestoneSurface.Result : RunestoneSurface.Chrome, actionable ? 3 : 1));
        }

        if (_promptSummary is not null)
        {
            _promptSummary.Text = PromptGuidanceSummary(view, actions);
            _promptSummary.AddThemeColorOverride("font_color", actionable ? RunestoneTheme.Brass : RunestoneTheme.MutedInk);
        }

        if (!UseLegacyCardTableFallback)
        {
            RedrawLastSnapshotSections();
            return;
        }

        if (actions.Count == 0)
        {
            _promptActions.AddChild(MutedLabel("等待服务端提供可展示的候选。"));
            RunestoneTheme.ApplyToTree(_promptActions);
            RedrawLastSnapshotSections();
            return;
        }

        foreach (var action in actions.OrderBy(PromptActionSortKey).ThenBy(PromptActionLabel))
        {
            _promptActions.AddChild(PromptActionNode(action));
        }

        RunestoneTheme.ApplyToTree(_promptActions);
        RedrawLastSnapshotSections();
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
        if (UseLegacyCardTableFallback && _snapshotRows is not null && _lastSnapshotSections is not null)
        {
            _cardControlRenderer.RenderSnapshotSections(_snapshotRows, _lastSnapshotSections);
            return;
        }

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
                if (_mulliganOverlay is not null && (!_mulliganOverlay.Visible || !_mulliganOverlay.CanUsePrompt))
                {
                    if (!_mulliganOverlay.ShowPrompt(action, VisibleMulliganHandCards(action), out var reason))
                    {
                        AppendLog($"[color=yellow]Mulligan overlay disabled: {Escape(reason)}[/color]");
                    }
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

    private IReadOnlyList<Godot.Collections.Dictionary> VisibleMulliganHandCards(Godot.Collections.Dictionary action)
    {
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
                    ? "请选择一个服务端候选行动。"
                    : "等待对手行动。";
        _matchScreen.SetTurnStatus(
            actionable ? "轮到你行动" : "等待对手行动",
            detail,
            actionable);
        _matchScreen.ActionBar.ShowPrompt(detail, _promptInteractionController.Actions);
        if (_promptInteractionController.Current is { } state)
        {
            _matchScreen.ActionBar.ShowSelection(
                state,
                _promptInteractionController.CurrentChoices,
                _promptInteractionController.CurrentStepLabel,
                _promptInteractionController.CurrentStepRequired);
        }

        RefreshPromptInteractionVisuals();
        if (_promptFrame is not null && !UseLegacyCardTableFallback)
        {
            _promptFrame.Visible = false;
        }

        var actions = view.TryGetValue("actions", out var actionsValue)
            ? actionsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        ShowSpecialPromptOverlays(actions);
    }

    private static string PromptGuidanceSummary(
        Godot.Collections.Dictionary view,
        Godot.Collections.Array<Godot.Collections.Dictionary> actions)
    {
        var actionable = view.TryGetValue("actionable", out var actionableValue) && actionableValue.AsBool();
        var message = view.TryGetValue("message", out var messageValue) ? messageValue.AsString() : string.Empty;
        var reason = view.TryGetValue("reason", out var reasonValue) ? reasonValue.AsString() : string.Empty;
        var enabledCount = actions.Count(action =>
            action.TryGetValue("enabled", out var enabledValue)
            && enabledValue.AsBool()
            && !string.Equals(
                action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty,
                "WAIT",
                StringComparison.Ordinal));
        var detail = !string.IsNullOrWhiteSpace(message)
            ? message
            : !string.IsNullOrWhiteSpace(reason)
                ? reason
                : actionable
                    ? "请选择一个服务端候选行动。"
                    : "对手正在行动，请稍候。";
        var headline = actionable ? "轮到你行动" : "等待对手行动";
        return $"{headline}\n{detail}\n{enabledCount} / {actions.Count} 个服务端候选可提交";
    }

    private static int PromptActionSortKey(Godot.Collections.Dictionary action)
    {
        var actionName = PromptActionName(action);
        var enabled = action.TryGetValue("enabled", out var enabledValue) && enabledValue.AsBool();
        var primary = actionName switch
        {
            "PASS_PRIORITY" or "PASS_FOCUS" or "PASS" or "END_TURN" => 0,
            "MULLIGAN" or "READY" or "SUBMIT_DECK" => 1,
            "TAP_RUNE" or "PLAY_CARD" or "MOVE_UNIT" or "DECLARE_BATTLE" => 2,
            "ASSIGN_COMBAT_DAMAGE" or "ORDER_TRIGGERS" => 3,
            "SURRENDER" => 8,
            "WAIT" => 9,
            _ => 4
        };
        return enabled ? primary : primary + 10;
    }

    private static string PromptActionName(Godot.Collections.Dictionary action)
    {
        return action.TryGetValue("action", out var actionValue) ? actionValue.AsString() : string.Empty;
    }

    private static string PromptActionLabel(Godot.Collections.Dictionary action)
    {
        var label = action.TryGetValue("label", out var labelValue) ? labelValue.AsString() : string.Empty;
        return ActionDisplayName(PromptActionName(action), label);
    }

    private static VBoxContainer PromptCard()
    {
        var row = new VBoxContainer
        {
            Name = "PromptCard",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 5);
        return row;
    }

    private static Control PromptActionHeader(
        string actionName,
        string label,
        bool enabled,
        bool canSubmit,
        bool hasTemplate)
    {
        var row = new VBoxContainer
        {
            Name = "PromptActionHeader",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 1);
        var stateText = canSubmit ? "可提交" : enabled && hasTemplate ? "需选择" : enabled ? "待处理" : "等待";
        var title = new Label
        {
            Text = $"{ActionDisplayName(actionName, label)} · {stateText}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeColorOverride("font_color", enabled ? RunestoneTheme.Ink : RunestoneTheme.MutedInk);
        row.AddChild(title);
        return row;
    }

    private static string PromptSubmitLabel(string actionName, string fallback)
    {
        return actionName switch
        {
            "PASS_PRIORITY" => "让过优先权",
            "PASS_FOCUS" => "让过焦点",
            "PASS" => "让过",
            "END_TURN" => "结束回合",
            "READY" => "准备",
            "SUBMIT_DECK" => "提交构筑",
            "MULLIGAN" => "确认起手",
            "SURRENDER" => "投降",
            _ => ActionDisplayName(actionName, fallback)
        };
    }

    private static string ActionDisplayName(string actionName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback)
            && !string.Equals(fallback, actionName, StringComparison.Ordinal)
            && !fallback.Contains('_', StringComparison.Ordinal))
        {
            return fallback;
        }

        return actionName switch
        {
            "ASSIGN_COMBAT_DAMAGE" => "分配伤害",
            "DECLARE_BATTLE" => "宣战",
            "END_TURN" => "结束回合",
            "HIDE_CARD" => "隐藏卡牌",
            "MOVE_UNIT" => "移动单位",
            "MULLIGAN" => "起手调度",
            "ORDER_TRIGGERS" => "排序触发",
            "PASS" => "让过",
            "PASS_FOCUS" => "让过焦点",
            "PASS_PRIORITY" => "让过优先权",
            "PLAY_CARD" => "打出卡牌",
            "READY" => "准备",
            "RECYCLE_RUNE" => "回收符文",
            "REVEAL_CARD" => "展示卡牌",
            "SUBMIT_DECK" => "提交构筑",
            "SURRENDER" => "投降",
            "TAP_RUNE" => "横置符文",
            "WAIT" => "等待",
            _ => string.IsNullOrWhiteSpace(fallback) ? "服务端行动" : fallback
        };
    }

    private static Label PromptReasonLabel(bool enabled, string reason, string fallback)
    {
        var text = !string.IsNullOrWhiteSpace(reason)
            ? reason
            : enabled
                ? fallback
                : "服务端当前未开放此候选。";
        return MutedLabel(text);
    }

    private static Label MutedLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        label.AddThemeColorOverride("font_color", RunestoneTheme.MutedInk);
        return label;
    }

    private static void FlashActionButton(Button button)
    {
        button.Modulate = new Color(1f, 0.78f, 0.42f, 1f);
        var tween = button.CreateTween();
        tween.TweenProperty(button, "modulate", Colors.White, 0.18d)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private Control PromptActionNode(Godot.Collections.Dictionary action)
    {
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
            return PromptSpecialOverlayNotice(actionName, label);
        }

        if (string.Equals(actionName, "ORDER_TRIGGERS", StringComparison.Ordinal)
            || string.Equals(actionName, "ASSIGN_COMBAT_DAMAGE", StringComparison.Ordinal))
        {
            return PromptSpecialOverlayNotice(actionName, label);
        }

        var row = PromptCard();
        var canSubmit = enabled && (hasTemplate || !string.Equals(submitKind, "unsupported", StringComparison.Ordinal));
        var selectors = new List<PromptSelector>();
        row.AddChild(PromptActionHeader(actionName, label, enabled, canSubmit, hasTemplate));

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
            Text = canSubmit ? PromptSubmitLabel(actionName, label) : "等待服务端候选",
            TooltipText = string.IsNullOrWhiteSpace(reason) ? ActionDisplayName(actionName, label) : reason
        };
        button.Pressed += () =>
        {
            FlashActionButton(button);
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
        row.AddChild(PromptReasonLabel(enabled, reason, hasTemplate ? "选择后将按服务端模板提交。" : "一键提交服务端候选。"));
        return row;
    }

    private static Control PromptSpecialOverlayNotice(string actionName, string label)
    {
        return MutedLabel($"{ActionDisplayName(actionName, label)} 将在专用覆盖层中处理。");
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

    private async Task SubmitSpecialPromptAsync(
        Godot.Collections.Dictionary action,
        Dictionary<string, object?> payload,
        string intentSuffix)
    {
        await SubmitPromptPayloadAsync(action, payload, intentSuffix);
    }

    private static string CompactPromptChoiceLabel(string label)
    {
        var trimmed = label.Trim();
        const int maxLength = 34;
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)]}…";
    }

    private static string ShortPromptChoiceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "(未命名选项)";
        }

        var trimmed = id.Trim();
        const int maxLength = 30;
        return trimmed.Length <= maxLength ? trimmed : $"…{trimmed[^maxLength..]}";
    }

    private static PromptSelectorNode PromptSelectionStepNode(Godot.Collections.Dictionary step, bool enabled)
    {
        var role = step.TryGetValue("role", out var roleValue) ? roleValue.AsString() : string.Empty;
        var label = step.TryGetValue("label", out var labelValue) ? labelValue.AsString() : role;
        var required = step.TryGetValue("required", out var requiredValue) && requiredValue.AsBool();
        var choices = step.TryGetValue("choices", out var choicesValue)
            ? choicesValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        var row = new VBoxContainer
        {
            Name = "PromptSelectionStep",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 2);
        row.AddChild(MutedLabel(required ? $"{label} *" : label));

        var selector = new OptionButton
        {
            Disabled = !enabled || choices.Count == 0,
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        if (!required)
        {
            selector.AddItem("(无)");
            selector.SetItemMetadata(0, string.Empty);
        }

        foreach (var choice in choices)
        {
            var choiceId = choice.TryGetValue("id", out var idValue) ? idValue.AsString() : string.Empty;
            var choiceLabel = choice.TryGetValue("label", out var textValue) ? textValue.AsString() : choiceId;
            selector.AddItem(string.IsNullOrWhiteSpace(choiceLabel)
                ? ShortPromptChoiceId(choiceId)
                : CompactPromptChoiceLabel(choiceLabel));
            selector.SetItemMetadata(selector.ItemCount - 1, choiceId);
        }

        if (choices.Count == 0)
        {
            selector.AddItem("(无可选项)");
            selector.SetItemMetadata(selector.ItemCount - 1, string.Empty);
        }

        row.AddChild(selector);
        return new PromptSelectorNode(row, new PromptSelector(role, selector));
    }

    public void ApplySnapshotSections(Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        if (_snapshotRows is null || _matchScreen is null)
        {
            return;
        }

        _lastSnapshotSections = sections;
        var battleActive = HasWireTableSection(sections);
        SetBattleChromeVisible(_matchFinished || battleActive);
        if (_matchFinished && !battleActive)
        {
            return;
        }

        if (battleActive && !UseLegacyCardTableFallback)
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
                _ = RunAutoSmokePromptAsync(_lastAppliedPromptView);
            }
            return;
        }

        if (!_matchFinished)
        {
            _battleTableRendered = false;
        }
        _matchScreen.RenderSections([]);
        if (UseLegacyCardTableFallback)
        {
            _cardControlRenderer.RenderSnapshotSections(_snapshotRows, sections);
            if (battleActive)
            {
                _battleTableRendered = true;
                if (_lastAppliedPromptView is not null)
                {
                    _ = RunAutoSmokePromptAsync(_lastAppliedPromptView);
                }
            }
        }
        else
        {
            ClearNodeChildren(_snapshotRows);
        }
    }

    private void SetBattleChromeVisible(bool battleActive)
    {
        _battleChromeHidden = battleActive;
        var legacyBattleVisible = battleActive && UseLegacyCardTableFallback;
        var lobbyVisible = !battleActive;
        if (_lobbyScreen is not null)
        {
            _lobbyScreen.SetScreenVisible(lobbyVisible);
        }

        if (_matchScreen is not null)
        {
            _matchScreen.SetScreenVisible(battleActive && !UseLegacyCardTableFallback);
        }

        if (_boardSummary is not null)
        {
            _boardSummary.Visible = legacyBattleVisible;
        }

        if (_snapshotScroll is not null)
        {
            _snapshotScroll.Visible = legacyBattleVisible;
        }

        if (_legacyHandScroll is not null)
        {
            _legacyHandScroll.Visible = legacyBattleVisible;
        }

        if (_officialCardPreviewFrame is not null)
        {
            if (UseLegacyCardTableFallback)
            {
                _officialCardPreviewFrame.Visible = battleActive;
            }
            else
            {
                _officialCardPreviewFrame.Visible = false;
            }
        }

        if (_promptFrame is not null)
        {
            if (UseLegacyCardTableFallback)
            {
                _promptFrame.Visible = battleActive;
            }
            else
            {
                _promptFrame.Visible = false;
            }
        }

        if (_resultFrame is not null && !battleActive)
        {
            _resultFrame.Visible = false;
        }

        if (!battleActive)
        {
            _cardInspectOverlay?.HideCard();
            if (!_matchFinished)
            {
                _resultOverlay?.HideResult();
            }
        }

        if (_controls is not null)
        {
            _controls.OffsetRight = legacyBattleVisible ? -336f : -16f;
        }
    }

    private void SetRightRailMatchResultVisible(bool matchResultVisible)
    {
        if (_resultFrame is not null)
        {
            _resultFrame.Visible = matchResultVisible;
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
        if (UseLegacyCardTableFallback)
        {
            SetRightRailMatchResultVisible(matchResultVisible: true);
            _resultOverlay?.HideResult();
        }
        else
        {
            SetRightRailMatchResultVisible(matchResultVisible: false);
            if (_resultOverlay is not null && _lastViewerResult.Count > 0)
            {
                _resultOverlay.ShowResult(_lastViewerResult);
            }
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
        if (!UseLegacyCardTableFallback)
        {
            return;
        }

        if (_officialCardPreviewSummary is not null)
        {
            _officialCardPreviewSummary.Text = CardControlRenderer.PreviewSummary(card);
        }

        if (_officialCardPreview is null)
        {
            return;
        }

        var imagePath = card.TryGetValue("imagePath", out var imagePathValue) ? imagePathValue.AsString() : string.Empty;
        _officialCardPreview.Texture = CardControlRenderer.LoadTextureFromImagePath(imagePath);
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

    public void ApplyOfficialCardPreviewPath(string imagePath)
    {
        if (_officialCardPreview is not null)
        {
            _officialCardPreview.Texture = CardControlRenderer.LoadTextureFromImagePath(imagePath);
        }
    }

    public void ApplyOfficialCardPreviewSummary(string text)
    {
        if (_officialCardPreviewSummary is not null)
        {
            _officialCardPreviewSummary.Text = text;
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
