using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient.Ui;

/// <summary>Multi-unit intent composer using only the destinations supplied by the server.</summary>
public partial class MovementOverlay : Control
{
    public event Action<Dictionary<string, object?>>? Confirmed;
    public event Action? TableSelectionChanged;
    public bool TableMode { get; set; }
    public string PromptId { get; private set; } = string.Empty;
    public long SnapshotTick { get; private set; } = -1;
    public bool IsSubmitting => _submitting;
    private OptionButton _destination = null!;
    private VBoxContainer _units = null!;
    private Label _summary = null!;
    private Button _confirm = null!;
    private Button _cancel = null!;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly List<(string Id, string Name, string[] Destinations, bool Roam)> _sources = [];
    private readonly List<string> _destinations = [];
    private readonly Dictionary<string, int> _powerPerExtraUnit = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);
    private Func<string, Godot.Collections.Dictionary?>? _cardView;
    private bool _submitting;
    private string _feedback = string.Empty;
    public IEnumerable<string> TableSources => _submitting ? [] : _sources.Where(source => source.Destinations.Contains(TableDestination)).Select(source => source.Id);
    public IEnumerable<string> TableDestinations => _submitting ? [] : _destinations;
    public IEnumerable<string> TableSelectedObjects => _selected;
    public string TableDestination => _destination.Selected >= 0 ? _destinations[_destination.Selected] : string.Empty;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        PanelContainer panel;
        if (TableMode)
        {
            panel = new PanelContainer(); AddChild(panel); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        else
        {
            ZIndex = 100;
            var shade = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.88f) };
            AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var center = new CenterContainer(); AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 520) }; center.AddChild(panel);
        }
        var layout = MatchTableLayout.Column(panel, 10);
        MatchTableLayout.Label(layout, "调动单位", 24, MinimalTheme.Selected);
        MatchTableLayout.Label(layout, "点击单位进行多选，再点击战场或基地。\n金色边框表示已选单位。", 13, MinimalTheme.TextSecondary, true);
        _destination = new OptionButton { CustomMinimumSize = new Vector2(0, 40), FitToLongestItem = false }; layout.AddChild(_destination);
        _destination.ItemSelected += _ => { _feedback = ""; RebuildSources(); };
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, TableMode ? 0 : 300), SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll); _units = MatchTableLayout.Column(scroll); _units.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _summary = MatchTableLayout.Label(layout, "请选择要移动的单位。", 14, MinimalTheme.Text, true);
        var actions = MatchTableLayout.Row(layout);
        _cancel = new Button { Text = "取消  Esc", SizeFlagsHorizontal = SizeFlags.ExpandFill }; actions.AddChild(_cancel); _cancel.Pressed += Hide;
        _confirm = new Button { Text = "确认移动", Name = "ConfirmMovementButton", SizeFlagsHorizontal = SizeFlags.ExpandFill }; actions.AddChild(_confirm);
        _confirm.Pressed += () =>
        {
            if (_selected.Count == 0 || _destination.Selected < 0 || _submitting) return;
            var ids = _sources.Where(source => _selected.Contains(source.Id)).Select(source => source.Id).ToArray();
            // Simultaneous movement may have different origins. The server resolves
            // each source's precise location; a shared coarse origin breaks Roam.
            var payload = new Dictionary<string, object?>
            {
                ["cmdType"] = "MOVE_UNIT", ["sourceObjectId"] = ids[0],
                ["sourceObjectIds"] = ids, ["destination"] = TableDestination
            };
            _submitting = true; RefreshSummary(); Confirmed?.Invoke(payload);
        };
        MinimalTheme.Apply(panel); Hide();
    }

    public bool Open(JsonElement candidate, string promptId, long tick, Func<string, Godot.Collections.Dictionary?> cardView,
        Func<string, string> friendlyLabel, string? initialSource = null)
    {
        if (!candidate.TryGetProperty("metadata", out var metadata)
            || !metadata.TryGetProperty("supportsSimultaneousMovement", out var supported) || !supported.GetBoolean()
            || !metadata.TryGetProperty("sourceRequirements", out var requirements)) return false;
        _sources.Clear(); _destinations.Clear(); _selected.Clear(); _destination.Clear(); _cardView = cardView;
        _powerPerExtraUnit.Clear(); _submitting = false; _feedback = "";
        if (metadata.TryGetProperty("powerPerExtraUnitByDestination", out var taxes))
            foreach (var tax in taxes.EnumerateObject()) _powerPerExtraUnit[tax.Name] = tax.Value.GetInt32();
        var destinations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in requirements.EnumerateArray().GroupBy(item => item.GetProperty("sourceObjectId").GetString()!))
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requirement in group)
                foreach (var choice in requirement.GetProperty("destinationChoices").EnumerateArray())
                {
                    var id = choice.GetProperty("id").GetString()!;
                    if (id != "BASE" && (!id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal) || id.EndsWith("-MAIN", StringComparison.Ordinal))) continue;
                    allowed.Add(id); destinations[id] = friendlyLabel(choice.GetProperty("label").GetString() ?? id);
                }
            if (allowed.Count == 0) continue;
            var card = cardView(group.Key);
            var name = card is not null && card.TryGetValue("cardName", out var cardName) ? cardName.AsString() : "单位";
            _sources.Add((group.Key, name, allowed.ToArray(), group.Any(r => r.TryGetProperty("mode", out var mode) && mode.GetString() == "ROAM")));
        }
        foreach (var destination in destinations) { _destinations.Add(destination.Key); _destination.AddItem(destination.Value); }
        if (_destinations.Count == 0) return false;
        if (initialSource is not null && !_sources.Any(source => source.Id == initialSource)) return false;
        PromptId = promptId; SnapshotTick = tick;
        var initial = initialSource is null ? default : _sources.First(source => source.Id == initialSource);
        _destination.Select(initialSource is null ? 0 : _destinations.IndexOf(initial.Destinations[0]));
        if (initialSource is not null) _selected.Add(initialSource);
        RebuildSources(); Show(); _destination.GrabFocus(); return true;
    }

    public bool TrySelectTableDestination(string id)
    {
        if (!Visible || _submitting || !_destinations.Contains(id)) return false;
        _destination.Select(_destinations.IndexOf(id)); _feedback = ""; RebuildSources(); return true;
    }
    public bool TryToggleTableSource(string id)
    {
        if (!Visible || _submitting || !_checks.TryGetValue(id, out var check)) return false;
        check.ButtonPressed = !check.ButtonPressed; return true;
    }
    public void ApplyReceipt(string promptId, long tick, bool accepted, string message)
    {
        if (!Visible || PromptId != promptId || SnapshotTick != tick) return;
        _submitting = false;
        if (accepted) { Hide(); return; }
        _feedback = message; RefreshSummary();
    }
    private void RebuildSources()
    {
        _checks.Clear();
        foreach (var child in _units.GetChildren()) { _units.RemoveChild(child); child.QueueFree(); }
        var valid = _sources.Where(source => source.Destinations.Contains(TableDestination, StringComparer.Ordinal)).ToArray();
        var removed = _selected.RemoveWhere(id => !valid.Any(source => source.Id == id));
        if (removed > 0) _feedback = $"有 {removed} 名单位不能移至此处，已取消选择。";
        foreach (var source in valid)
        {
            var row = MatchTableLayout.Row(_units);
            var check = new CheckBox { Text = source.Name + (source.Roam ? "\n游走 · 可跨战场" : ""), SizeFlagsHorizontal = SizeFlags.ExpandFill, ButtonPressed = _selected.Contains(source.Id) };
            if (_cardView?.Invoke(source.Id) is { } card)
            {
                var preview = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn").Instantiate<OfficialCardView>();
                preview.CustomMinimumSize = new Vector2(42, 58); preview.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                row.AddChild(preview); preview.Display(card, OfficialCardVisualState.Normal);
                preview.Activated += _ => { if (!_submitting) check.ButtonPressed = !check.ButtonPressed; };
            }
            row.AddChild(check); MinimalTheme.Apply(check); _checks[source.Id] = check;
            check.Toggled += selected => { if (selected) _selected.Add(source.Id); else _selected.Remove(source.Id); _feedback = ""; RefreshSummary(); };
        }
        RefreshSummary();
    }
    private void RefreshSummary()
    {
        _confirm.Disabled = _submitting || _selected.Count == 0; _destination.Disabled = _submitting; _cancel.Disabled = _submitting;
        foreach (var check in _checks.Values) check.Disabled = _submitting;
        _confirm.Text = _submitting ? "正在提交…" : $"确认移动{(_selected.Count > 0 ? $" · {_selected.Count}" : "")}";
        _summary.Text = _submitting ? "正在等待对局确认。" : _selected.Count == 0 ? "请选择要移动的单位。" : $"已选 {_selected.Count} 名单位\n移动后进入休眠。";
        if (_powerPerExtraUnit.TryGetValue(TableDestination, out var tax) && tax > 0)
            _summary.Text += $"\n每多移动一名单位，额外支付 {tax} 符能。";
        if (_feedback.Length > 0) _summary.Text = "移动未完成：" + _feedback + "\n已保留你的选择，可以调整后重试。";
        _summary.Modulate = _feedback.Length > 0 ? MinimalTheme.Hostile : Colors.White;
        TableSelectionChanged?.Invoke();
    }
}
