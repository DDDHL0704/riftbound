using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using CardDictionary = Godot.Collections.Dictionary;
using CardArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

namespace Riftbound.GodotClient.Ui;

public partial class MatchScreen : AppScreen
{
    public event Action<CardDictionary>? CardActivated;
    public event Action<CardDictionary>? CardInspectionRequested;
    public event Action<string>? DestinationActivated;
    public event Action<string, CardArray>? PublicPileRequested;
    public event Action? ReconnectRequested;
    public event Action? ReturnToLobbyRequested;
    public Func<CardDictionary, bool>? TableDragRequested { get; set; }
    internal MatchTableLayout TableLayout { get; private set; } = null!;
    public Control ComposerHost => TableLayout.Composer;
    public ActionBar ActionBar => TableLayout.Actions;
    private HBoxContainer _connectionBanner = null!;
    private Label _connectionMessage = null!;
    private Button _reconnectButton = null!;
    private MatchTableRenderer? _renderer;
    private CardArray? _lastSections;
    private CardDictionary? _inspected;
    private readonly Queue<string> _history = new();
    private long _lastEventTick = -1;
    private readonly HashSet<string> _eventKeys = new(StringComparer.Ordinal);
    private string[] _destinations = [];
    private readonly HashSet<string> _legalDropObjects = new(StringComparer.Ordinal);
    private readonly HashSet<string> _legalDropDestinations = new(StringComparer.Ordinal);
    private long _dragGeneration;
    private string _dragSource = "";
    private Variant _activeDrag;
    private long _feedbackBaselineTick = -1, _renderedTick = -1, _animatedTick = -1;
    private long _pendingFeedbackTick = -1;
    private string[] _pendingFeedbackObjects = [];
    private string[] _linkObjects = [];
    private string? _linkDestination;
    private TableTargetLinks _links = null!;

    public override void _Ready()
    {
        TableLayout = new MatchTableLayout(this);
        _links = new TableTargetLinks(); AddChild(_links); _links.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _links.Segments = SelectionSegments;
        TableLayout.ReduceMotion.Toggled += reduced =>
        {
            if (reduced) foreach (var view in FindChildren("*", "", true, false).OfType<OfficialCardView>()) view.CancelFeedback();
        };
        _connectionBanner = new HBoxContainer { Visible = false };
        _connectionMessage = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _connectionMessage.AddThemeColorOverride("font_color", MinimalTheme.Selected);
        _connectionBanner.AddChild(_connectionMessage);
        _reconnectButton = new Button { Text = "重新连接", CustomMinimumSize = new Vector2(110, 40) };
        _reconnectButton.Pressed += () => ReconnectRequested?.Invoke();
        _connectionBanner.AddChild(_reconnectButton);
        var back = new Button { Text = "返回大厅", CustomMinimumSize = new Vector2(110, 40) };
        back.Pressed += () => ReturnToLobbyRequested?.Invoke(); _connectionBanner.AddChild(back);
        TableLayout.Root.AddChild(_connectionBanner); TableLayout.Root.MoveChild(_connectionBanner, 1);
        TableLayout.BaseDestination.Pressed += () => DestinationActivated?.Invoke("BASE");
        TableLayout.BaseZone.CanDrop = data => CanDropOnDestination(data, "BASE");
        TableLayout.BaseZone.Dropped = data => DropOnDestination(data, "BASE");
        for (var index = 0; index < TableLayout.Battlefields.Length; index++)
        {
            var captured = index;
            var field = TableLayout.Battlefields[index];
            field.Panel.CanDrop = data => captured < _destinations.Length && CanDropOnDestination(data, _destinations[captured]);
            field.Panel.Dropped = data => { if (captured < _destinations.Length) DropOnDestination(data, _destinations[captured]); };
            field.Panel.MouseFilter = MouseFilterEnum.Stop;
            field.Panel.GuiInput += input =>
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                    && field.Destination.Visible && captured < _destinations.Length)
                {
                    field.Panel.AcceptEvent(); DestinationActivated?.Invoke(_destinations[captured]);
                }
            };
            field.Destination.Pressed += () =>
            {
                if (captured < _destinations.Length) DestinationActivated?.Invoke(_destinations[captured]);
            };
        }
        _renderer = new MatchTableRenderer(this, card => CardActivated?.Invoke(card),
            (title, cards) => PublicPileRequested?.Invoke(title, cards));
        ApplyTheme(); RenderSections(_lastSections ?? []);
    }

    public void ApplyTheme()
    {
        // Panel colors carry semantic hierarchy; do not overwrite them with one global surface.
        if (!IsNodeReady()) return;
        MinimalTheme.Apply(TableLayout.Actions);
        MinimalTheme.Apply(TableLayout.BaseDestination);
        TableLayout.BaseDestination.CustomMinimumSize = new Vector2(108, 28);
        TableLayout.BaseDestination.AddThemeFontSizeOverride("font_size", 13);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var compact = (StyleBoxFlat)TableLayout.BaseDestination.GetThemeStylebox(state).Duplicate();
            compact.SetContentMarginAll(3); TableLayout.BaseDestination.AddThemeStyleboxOverride(state, compact);
        }
        MinimalTheme.Apply(_connectionBanner);
        foreach (var field in TableLayout.Battlefields) MinimalTheme.Apply(field.Destination);
    }

    public void SetConnectionStatus(bool connected, bool recovering)
    {
        if (!IsNodeReady()) return;
        _connectionBanner.Visible = !connected; _reconnectButton.Disabled = recovering;
        if (!connected) InvalidateTableGesture();
        _connectionMessage.Text = recovering ? "连接中断，正在恢复对局… 当前显示断线前的局面。" : "已断开连接。重新连接后同步最新局面。";
        ActionBar.Visible = connected;
        if (!connected) SetTurnStatus(recovering ? "正在恢复连接" : "连接已断开", "同步最新局面后可继续行动。", false);
    }

    public void RenderSections(CardArray sections)
    {
        InvalidateTableGesture();
        _lastSections = sections;
        if (_renderer is null) return;
        var table = sections.FirstOrDefault(s => Read(s, "kind") == "wireTable");
        if (table is null) { _renderer.Clear(); SetTurnStatus("等待对局", "准备完成后进入牌桌。", false); return; }
        _renderer.Render(table);
        _renderedTick = Number(table, "tick");
        if (_feedbackBaselineTick < 0) _feedbackBaselineTick = _renderedTick;
        ApplyPendingFeedback();
    }

    public void RenderMatchStatus(CardDictionary table)
    {
        var self = table["self"].AsGodotDictionary(); var opponent = table["opponent"].AsGodotDictionary();
        TableLayout.Score.Text = $"我方  {Number(self, "score")}    :    {Number(opponent, "score")}  对手";
        var goal = Number(table, "winningScore");
        TableLayout.Round.Text = $"第 {Number(table, "turnNumber")} 回合" + (goal > 0 ? $"  ·  {goal} 分获胜" : "");
        _destinations = table["lanes"].As<CardArray>().Select(lane => "BATTLEFIELD:" + Read(lane, "battlefieldId")).ToArray();
        ClearChildren(TableLayout.Chain);
        var chain = table.TryGetValue("chain", out var chainValue) ? chainValue.As<CardArray>() : [];
        TableLayout.ChainPanel.Visible = chain.Count > 0;
        if (chain.Count == 0) MatchTableLayout.Label(TableLayout.Chain, "当前没有待结算行动", 13, MinimalTheme.TextSecondary, true);
        foreach (var entry in chain)
        {
            var row = MatchTableLayout.Column(TableLayout.Chain, 2);
            var button = new Button { Text = Read(entry, "title"), Alignment = HorizontalAlignment.Left,
                ClipText = true, CustomMinimumSize = new Vector2(0, 34), TooltipText = Read(entry, "detail") };
            row.AddChild(button); MinimalTheme.Apply(button);
            var id = Read(entry, "objectId"); button.SetMeta("objectId", id);
            button.Pressed += () => CardActivated?.Invoke(new CardDictionary { ["objectId"] = id, ["visible"] = true, ["cardName"] = Read(entry, "title") });
            MatchTableLayout.Label(row, Read(entry, "detail"), 12, MinimalTheme.TextSecondary, true);
        }
        if (_inspected is not null && Read(_inspected, "objectId") is { Length: > 0 } inspectedId)
        {
            var current = _renderer?.VisibleCard(inspectedId);
            if (current is null) ClearInspection(); else PreviewCard(current);
        }
    }

    public void SetTurnStatus(string headline, string detail, bool actionable)
    {
        if (!IsNodeReady()) return;
        TableLayout.TurnHeadline.Text = headline; TableLayout.TurnDetail.Text = detail;
        TableLayout.TurnHeadline.AddThemeColorOverride("font_color", actionable ? MinimalTheme.Selectable : MinimalTheme.Waiting);
        if (!actionable) ActionBar.SetWaiting(detail);
    }

    public void SetComposerVisible(bool visible)
    {
        TableLayout.Composer.Visible = visible; TableLayout.Intel.GetParent<ScrollContainer>().Visible = !visible;
        TableLayout.Rail.CustomMinimumSize = new Vector2(visible ? 320 : 248, 0);
        if (!visible) ClearPromptStates();
    }

    public void PreviewCard(CardDictionary card)
    {
        if (!card.ContainsKey("visible") || card["visible"].AsBool())
        {
            if (card.TryGetValue("faceDown", out var faceDown) && faceDown.AsBool()) return;
            if (_inspected is not null && Read(_inspected, "objectId") != Read(card, "objectId")) ClearChildren(TableLayout.CardActions);
            _inspected = card.Duplicate(true);
            TableLayout.InspectPanel.Visible = true;
            TableLayout.InspectName.Text = Read(card, "cardName");
            TableLayout.InspectArt.Texture = CardTextureLoader.Load(Read(card, "imagePath"), card.TryGetValue("rotated", out var rotated) && rotated.AsBool());
            var summary = Read(card, "previewSummary");
            TableLayout.InspectText.Text = summary.Length > 150 ? summary[..150] + "…\n右键查看完整卡牌" : summary;
        }
    }
    public void InspectCard(CardDictionary card) => CardInspectionRequested?.Invoke(card);
    private void ClearInspection()
    {
        _inspected = null; TableLayout.InspectArt.Texture = null;
        TableLayout.InspectPanel.Visible = false;
        TableLayout.InspectName.Text = "卡牌详情"; TableLayout.InspectText.Text = "悬停查看卡牌\n点选卡牌可直接行动";
        ClearChildren(TableLayout.CardActions);
    }
    public void ShowCardActions(CardDictionary card, IEnumerable<(string Label, Action Select)> actions)
    {
        PreviewCard(card); ClearChildren(TableLayout.CardActions);
        foreach (var action in actions)
        {
            var button = new Button { Text = action.Label }; TableLayout.CardActions.AddChild(button); MinimalTheme.Apply(button);
            button.Pressed += () => { ClearChildren(TableLayout.CardActions); action.Select(); };
        }
    }
    public void AddBattleEvents(long tick, string[] descriptions, string[]? objects = null)
    {
        if (tick < _lastEventTick) return;
        if (tick != _lastEventTick) { _lastEventTick = tick; _eventKeys.Clear(); }
        foreach (var description in descriptions.Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            if (!_eventKeys.Add(description)) continue;
            _history.Enqueue(description); while (_history.Count > 12) _history.Dequeue();
        }
        ClearChildren(TableLayout.History);
        foreach (var (text, index) in _history.Reverse().Select((text, index) => (text, index)))
            MatchTableLayout.Label(TableLayout.History, text, 12, index == 0 ? MinimalTheme.Text : MinimalTheme.TextSecondary, true);
        if (tick > _animatedTick && tick > _feedbackBaselineTick)
        { _pendingFeedbackTick = tick; _pendingFeedbackObjects = objects ?? []; ApplyPendingFeedback(); }
    }

    private void ApplyPendingFeedback()
    {
        if (_pendingFeedbackTick < 0 || _renderedTick < _pendingFeedbackTick) return;
        if (_renderedTick == _pendingFeedbackTick && !TableLayout.ReduceMotion.ButtonPressed)
            foreach (var id in _pendingFeedbackObjects.Distinct()) _renderer?.CardControl(id)?.PulseAcceptedEvent();
        _animatedTick = Math.Max(_animatedTick, _pendingFeedbackTick); _pendingFeedbackTick = -1; _pendingFeedbackObjects = [];
    }
    public void ClearPromptStates()
    {
        _renderer?.ClearPromptStates(); SetDestinationChoices([]);
        _legalDropObjects.Clear(); _linkObjects = []; _linkDestination = null;
        foreach (var row in TableLayout.Chain.GetChildren())
            foreach (var button in row.GetChildren().OfType<Button>()) MinimalTheme.Apply(button);
        if (IsNodeReady()) ClearChildren(TableLayout.CardActions);
    }
    public void SetObjectState(string objectId, OfficialCardVisualState state)
    {
        _renderer?.SetObjectState(objectId, state);
        if (state == OfficialCardVisualState.LegalTarget) _legalDropObjects.Add(objectId);
        foreach (var row in TableLayout.Chain.GetChildren())
            foreach (var button in row.GetChildren().OfType<Button>())
                if (button.HasMeta("objectId") && button.GetMeta("objectId").AsString() == objectId)
                    button.AddThemeStyleboxOverride("normal", MinimalTheme.Outline(state));
    }
    public void SetDestinationChoices(IEnumerable<string> choices, string? selected = null)
    {
        var legal = choices.ToHashSet(StringComparer.Ordinal);
        _legalDropDestinations.Clear(); _legalDropDestinations.UnionWith(legal);
        TableLayout.BaseDestination.Disabled = !legal.Contains("BASE");
        TableLayout.BaseDestination.Text = selected == "BASE" ? "已选基地" : legal.Contains("BASE") ? "移至 / 选基地" : "我方基地";
        for (var i = 0; i < TableLayout.Battlefields.Length; i++)
        {
            var button = TableLayout.Battlefields[i].Destination;
            var id = i < _destinations.Length ? _destinations[i] : "";
            button.Visible = legal.Contains(id); button.Text = id == selected ? "已选此处" : "选择此处";
        }
    }
    public Variant BeginTableDrag(CardDictionary card)
    {
        var id = Read(card, "objectId");
        if (id.Length == 0 || TableDragRequested?.Invoke(card) != true) return default;
        _dragGeneration++;
        _dragSource = id;
        _activeDrag = new CardDictionary { ["tableDrag"] = true, ["generation"] = _dragGeneration, ["sourceId"] = id };
        GD.Print("[Table] Drag began.");
        return _activeDrag;
    }
    private bool CurrentDrag(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var value = data.AsGodotDictionary();
        return value.TryGetValue("tableDrag", out var marker) && marker.AsBool()
            && value.TryGetValue("generation", out var generation) && generation.AsInt64() == _dragGeneration
            && Read(value, "sourceId") == _dragSource && _dragSource.Length > 0;
    }
    public bool CanDropOnObject(Variant data, string id) => CurrentDrag(data) && _legalDropObjects.Contains(id);
    public bool CanDropOnDestination(Variant data, string id) => CurrentDrag(data) && _legalDropDestinations.Contains(id);
    public void DropOnObject(Variant data, string id)
    { if (CanDropOnObject(data, id) && _renderer?.VisibleCard(id) is { } card) { CardActivated?.Invoke(card); GD.Print("[Table] Target drop selected."); } }
    public void DropOnDestination(Variant data, string id)
    { if (CanDropOnDestination(data, id)) { DestinationActivated?.Invoke(id); GD.Print("[Table] Destination drop selected."); } }
    public void InvalidateTableGesture()
    { _dragGeneration++; _dragSource = ""; _linkObjects = []; _linkDestination = null; }
    public void SetSelectionLinks(IEnumerable<string> objects, string? destination = null)
    { _linkObjects = objects.ToArray(); _linkDestination = destination; }
    public override void _Notification(int what)
    { if (what == NotificationDragEnd) { _dragGeneration++; _dragSource = ""; } }
    public override void _Input(InputEvent input)
    {
        // Resolve the release against table geometry as well as native drop controls.
        // Scroll/preview children must not intercept a valid battlefield drop.
        if (!IsVisibleInTree() || !CurrentDrag(_activeDrag)
            || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mouse) return;
        foreach (var id in _legalDropObjects)
            if (_renderer?.CardControl(id) is { } card && card.GetGlobalRect().HasPoint(mouse.GlobalPosition))
            { DropOnObject(_activeDrag, id); _dragSource = ""; return; }
        for (var i = 0; i < _destinations.Length; i++)
            if (TableLayout.Battlefields[i].Panel.GetGlobalRect().HasPoint(mouse.GlobalPosition))
            { DropOnDestination(_activeDrag, _destinations[i]); _dragSource = ""; return; }
        if (TableLayout.BaseZone.GetGlobalRect().HasPoint(mouse.GlobalPosition)) DropOnDestination(_activeDrag, "BASE");
        _dragSource = "";
    }
    private (Vector2 From, Vector2 To)[] SelectionSegments()
    {
        if (!IsVisibleInTree() || _linkObjects.Length == 0) return [];
        Vector2? Center(string id) => _renderer?.CardControl(id) is { } card && GodotObject.IsInstanceValid(card)
            ? _links.GetGlobalTransform().AffineInverse() * card.GetGlobalRect().GetCenter() : null;
        var points = new List<(Vector2, Vector2)>();
        if (_linkDestination is { Length: > 0 } destination)
        {
            var index = Array.IndexOf(_destinations, destination);
            Control? target = destination == "BASE" ? TableLayout.BaseZone : index >= 0 ? TableLayout.Battlefields[index].Panel : null;
            if (target is not null)
                foreach (var id in _linkObjects) if (Center(id) is { } from)
                    points.Add((from, _links.GetGlobalTransform().AffineInverse() * target.GetGlobalRect().GetCenter()));
        }
        else if (Center(_linkObjects[0]) is { } source)
            foreach (var id in _linkObjects.Skip(1)) if (Center(id) is { } target) points.Add((source, target));
        return points.ToArray();
    }
    public override void SetScreenVisible(bool visible)
    {
        base.SetScreenVisible(visible);
        if (!visible && IsNodeReady())
        {
            ClearPromptStates(); ClearInspection(); _history.Clear(); _lastEventTick = -1; _eventKeys.Clear();
            _feedbackBaselineTick = _renderedTick = _animatedTick = _pendingFeedbackTick = -1; _pendingFeedbackObjects = [];
            ClearChildren(TableLayout.History); ActionBar.SetWaiting("等待下一步行动。");
        }
    }
    private static void ClearChildren(Node parent) { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private static string Read(CardDictionary source, string key) => source.TryGetValue(key, out var value) ? value.AsString() : "";
    private static int Number(CardDictionary source, string key) => source.TryGetValue(key, out var value) ? value.AsInt32() : 0;
}
