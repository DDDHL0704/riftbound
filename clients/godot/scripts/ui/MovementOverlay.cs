using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient.Ui;

/// <summary>Selection UI for the destinations and sources supplied by the server.</summary>
public partial class MovementOverlay : Control
{
    public event Action<string, IReadOnlyList<string>>? Confirmed;
    public string PromptId { get; private set; } = string.Empty;
    public long SnapshotTick { get; private set; } = -1;
    private OptionButton _destination = null!;
    private VBoxContainer _units = null!;
    private Label _summary = null!;
    private Button _confirm = null!;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly List<(string Id, string Name, string[] Destinations)> _sources = [];
    private readonly List<string> _destinations = [];
    private readonly Dictionary<string, int> _powerPerExtraUnit = new(StringComparer.Ordinal);
    private Func<string, Godot.Collections.Dictionary?>? _cardView;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Stop;
        var shade = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.88f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer(); AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 520) }; center.AddChild(panel);
        var margin = new MarginContainer(); panel.AddChild(margin);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 20);
        var layout = new VBoxContainer(); margin.AddChild(layout);
        var title = new Label { Text = "调动单位" }; title.AddThemeFontSizeOverride("font_size", 26); layout.AddChild(title);
        layout.AddChild(new Label { Text = "先选目的地，再选择一名或多名单位，同时移动。" });
        _destination = new OptionButton { CustomMinimumSize = new Vector2(0, 40) }; layout.AddChild(_destination);
        _destination.ItemSelected += _ => RebuildSources();
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll);
        _units = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_units);
        _summary = new Label { Text = "请选择要移动的单位。" }; layout.AddChild(_summary);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; layout.AddChild(actions);
        var cancel = new Button { Text = "取消", CustomMinimumSize = new Vector2(100, 40) }; actions.AddChild(cancel); cancel.Pressed += Hide;
        _confirm = new Button { Text = "确认移动", CustomMinimumSize = new Vector2(150, 40) }; actions.AddChild(_confirm);
        _confirm.Pressed += () =>
        {
            if (_selected.Count == 0 || _destination.Selected < 0) return;
            var ids = _sources.Where(source => _selected.Contains(source.Id)).Select(source => source.Id).ToArray();
            Confirmed?.Invoke(_destinations[_destination.Selected], ids);
            Hide();
        };
        MinimalTheme.Apply(panel);
        Hide();
    }

    public bool Open(JsonElement candidate, string promptId, long tick, Func<string, Godot.Collections.Dictionary?> cardView,
        Func<string, string> friendlyLabel)
    {
        if (!candidate.TryGetProperty("metadata", out var metadata)
            || !metadata.TryGetProperty("supportsSimultaneousMovement", out var supported) || !supported.GetBoolean()
            || !metadata.TryGetProperty("sourceRequirements", out var requirements)) return false;
        _sources.Clear(); _destinations.Clear(); _selected.Clear(); _destination.Clear(); _cardView = cardView;
        _powerPerExtraUnit.Clear();
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
                    // Abstract destinations belong to the old single-unit protocol.
                    if (id != "BASE" && (!id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal) || id.EndsWith("-MAIN", StringComparison.Ordinal))) continue;
                    allowed.Add(id);
                    destinations[id] = friendlyLabel(choice.GetProperty("label").GetString() ?? id);
                }
            if (allowed.Count == 0) continue;
            var card = cardView(group.Key);
            var name = card is not null && card.TryGetValue("cardName", out var cardName) ? cardName.AsString() : "单位";
            _sources.Add((group.Key, name, allowed.ToArray()));
        }
        foreach (var destination in destinations) { _destinations.Add(destination.Key); _destination.AddItem(destination.Value); }
        if (_destinations.Count == 0) return false;
        PromptId = promptId; SnapshotTick = tick;
        _destination.Select(0); RebuildSources(); Show(); _destination.GrabFocus();
        return true;
    }

    private void RebuildSources()
    {
        _selected.Clear();
        foreach (var child in _units.GetChildren()) { _units.RemoveChild(child); child.QueueFree(); }
        var destination = _destination.Selected >= 0 ? _destinations[_destination.Selected] : string.Empty;
        foreach (var source in _sources.Where(source => source.Destinations.Contains(destination, StringComparer.Ordinal)))
        {
            var row = new HBoxContainer(); _units.AddChild(row);
            var check = new CheckBox { Text = source.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            if (_cardView?.Invoke(source.Id) is { } card)
            {
                var preview = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn").Instantiate<OfficialCardView>();
                preview.CustomMinimumSize = new Vector2(54, 75); preview.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                row.AddChild(preview); preview.Display(card, OfficialCardVisualState.Normal);
                preview.Activated += _ => check.ButtonPressed = !check.ButtonPressed;
                check.TooltipText = card.TryGetValue("previewSummary", out var text) ? text.AsString() : source.Name;
            }
            row.AddChild(check); MinimalTheme.Apply(check);
            check.Toggled += selected => { if (selected) _selected.Add(source.Id); else _selected.Remove(source.Id); RefreshSummary(); };
        }
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        _confirm.Disabled = _selected.Count == 0;
        _summary.Text = _selected.Count == 0 ? "请选择要移动的单位。" : $"已选 {_selected.Count} 名单位 · 移动后进入休眠。";
        if (_destination.Selected >= 0 && _powerPerExtraUnit.TryGetValue(_destinations[_destination.Selected], out var tax) && tax > 0)
            _summary.Text += $"\n此处每多移动一名单位，需要额外支付 {tax} 符能。";
    }
}
