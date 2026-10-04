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

    public override void _Ready()
    {
        TableLayout = new MatchTableLayout(this);
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
        for (var index = 0; index < TableLayout.Battlefields.Length; index++)
        {
            var captured = index;
            var field = TableLayout.Battlefields[index];
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
        _connectionMessage.Text = recovering ? "连接中断，正在恢复对局… 当前显示断线前的局面。" : "已断开连接。重新连接后同步最新局面。";
        ActionBar.Visible = connected;
        if (!connected) SetTurnStatus(recovering ? "正在恢复连接" : "连接已断开", "同步最新局面后可继续行动。", false);
    }

    public void RenderSections(CardArray sections)
    {
        _lastSections = sections;
        if (_renderer is null) return;
        var table = sections.FirstOrDefault(s => Read(s, "kind") == "wireTable");
        if (table is null) { _renderer.Clear(); SetTurnStatus("等待对局", "准备完成后进入牌桌。", false); return; }
        _renderer.Render(table);
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
        TableLayout.Rail.CustomMinimumSize = new Vector2(visible ? 344 : 268, 0);
        if (!visible) ClearPromptStates();
    }

    public void PreviewCard(CardDictionary card)
    {
        if (!card.ContainsKey("visible") || card["visible"].AsBool())
        {
            if (card.TryGetValue("faceDown", out var faceDown) && faceDown.AsBool()) return;
            if (_inspected is not null && Read(_inspected, "objectId") != Read(card, "objectId")) ClearChildren(TableLayout.CardActions);
            _inspected = card.Duplicate(true);
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
    public void AddBattleEvents(long tick, string[] descriptions)
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
    }
    public void ClearPromptStates()
    {
        _renderer?.ClearPromptStates(); SetDestinationChoices([]);
        foreach (var row in TableLayout.Chain.GetChildren())
            foreach (var button in row.GetChildren().OfType<Button>()) MinimalTheme.Apply(button);
        if (IsNodeReady()) ClearChildren(TableLayout.CardActions);
    }
    public void SetObjectState(string objectId, OfficialCardVisualState state)
    {
        _renderer?.SetObjectState(objectId, state);
        foreach (var row in TableLayout.Chain.GetChildren())
            foreach (var button in row.GetChildren().OfType<Button>())
                if (button.HasMeta("objectId") && button.GetMeta("objectId").AsString() == objectId)
                    button.AddThemeStyleboxOverride("normal", MinimalTheme.Outline(state));
    }
    public void SetDestinationChoices(IEnumerable<string> choices, string? selected = null)
    {
        var legal = choices.ToHashSet(StringComparer.Ordinal);
        TableLayout.BaseDestination.Disabled = !legal.Contains("BASE");
        TableLayout.BaseDestination.Text = selected == "BASE" ? "已选基地" : legal.Contains("BASE") ? "移至 / 选基地" : "我方基地";
        for (var i = 0; i < TableLayout.Battlefields.Length; i++)
        {
            var button = TableLayout.Battlefields[i].Destination;
            var id = i < _destinations.Length ? _destinations[i] : "";
            button.Visible = legal.Contains(id); button.Text = id == selected ? "已选此处" : "选择此处";
        }
    }
    public override void SetScreenVisible(bool visible)
    {
        base.SetScreenVisible(visible);
        if (!visible && IsNodeReady())
        {
            ClearPromptStates(); ClearInspection(); _history.Clear(); _lastEventTick = -1; _eventKeys.Clear();
            ClearChildren(TableLayout.History); ActionBar.SetWaiting("等待下一步行动。");
        }
    }
    private static void ClearChildren(Node parent) { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private static string Read(CardDictionary source, string key) => source.TryGetValue(key, out var value) ? value.AsString() : "";
    private static int Number(CardDictionary source, string key) => source.TryGetValue(key, out var value) ? value.AsInt32() : 0;
}
