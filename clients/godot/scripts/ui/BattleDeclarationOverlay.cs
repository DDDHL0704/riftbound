using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient.Ui;

// Keeps participants, battlefield and effect targets distinct. All choices come from the prompt.
public partial class BattleDeclarationOverlay : Control
{
    public event Action<Dictionary<string, object?>>? Confirmed;
    public string PromptId { get; private set; } = "";
    public long SnapshotTick { get; private set; }
    public bool IsSubmitting { get; private set; }
    private VBoxContainer _choices = null!;
    private Label _status = null!;
    private Button _confirm = null!, _cancel = null!;
    private OptionButton _source = null!;
    private readonly List<JsonElement> _requirements = [];
    private readonly List<(string Field, OptionButton Picker, string[] Ids, bool Required)> _picks = [];
    private readonly Dictionary<string, CheckBox> _costs = new(StringComparer.Ordinal);
    private string[] _requiredCosts = [];
    private Func<string, string> _name = id => id;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer(); AddChild(panel); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layout = MatchTableLayout.Column(panel, 10);
        MatchTableLayout.Label(layout, "声明战斗", 24, MinimalTheme.Selected);
        MatchTableLayout.Label(layout, "确认参战单位；有额外效果时选择其目标。", 13, MinimalTheme.TextSecondary, true);
        _source = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 44) };
        layout.AddChild(_source); _source.ItemSelected += _ => Rebuild();
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll); _choices = MatchTableLayout.Column(scroll, 6); _choices.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status = MatchTableLayout.Label(layout, "", 13, MinimalTheme.TextSecondary, true);
        var footer = MatchTableLayout.Row(layout);
        _cancel = new Button { Text = "取消  Esc", SizeFlagsHorizontal = SizeFlags.ExpandFill }; footer.AddChild(_cancel); _cancel.Pressed += Hide;
        _confirm = new Button { Name = "ConfirmBattleButton", Text = "确认战斗", SizeFlagsHorizontal = SizeFlags.ExpandFill }; footer.AddChild(_confirm);
        _confirm.Pressed += Submit;
        MinimalTheme.Apply(panel); Hide();
    }

    public bool Open(JsonElement candidate, string promptId, long tick, Func<string, string> name)
    {
        if (!candidate.TryGetProperty("metadata", out var meta) || !meta.TryGetProperty("sourceRequirements", out var requirements)) return false;
        _requirements.Clear(); _source.Clear(); _name = name;
        foreach (var requirement in requirements.EnumerateArray())
        {
            if (requirement.TryGetProperty("composable", out var composable) && !composable.GetBoolean()) continue;
            _requirements.Add(requirement.Clone()); _source.AddItem(name(requirement.GetProperty("sourceObjectId").GetString()!));
        }
        if (_requirements.Count == 0) return false;
        PromptId = promptId; SnapshotTick = tick; IsSubmitting = false;
        _source.Select(0); Rebuild(); Show(); return true;
    }

    private void Rebuild()
    {
        foreach (var child in _choices.GetChildren()) { _choices.RemoveChild(child); child.QueueFree(); }
        _picks.Clear(); _costs.Clear();
        var req = _requirements[_source.Selected];
        AddPicker("battlefieldId", "战场", req.GetProperty("battlefieldChoices"), true);
        AddIndexed(req, "attackerChoicesByIndex", "attackerObjectIds", "进攻单位", "minAttackerCount");
        AddIndexed(req, "targetChoicesByIndex", "defenderObjectIds", "防守单位", "minDefenderCount");
        AddIndexed(req, "battlefieldTargetChoicesByIndex", "battlefieldTargetObjectIds", "额外效果目标", "minBattlefieldTargetCount");
        _requiredCosts = req.TryGetProperty("requiredOptionalCosts", out var required)
            ? required.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
        foreach (var key in new[] { "optionalCostChoices", "paymentResourceChoices" })
            if (req.TryGetProperty(key, out var choices)) foreach (var choice in choices.EnumerateArray())
            {
                var id = choice.GetProperty("id").GetString()!;
                if (_requiredCosts.Contains(id) || _costs.ContainsKey(id)) continue;
                var check = new CheckBox { Text = choice.GetProperty("label").GetString(), CustomMinimumSize = new Vector2(0, 44) };
                _choices.AddChild(check); _costs[id] = check;
            }
        MinimalTheme.Apply(_choices); Refresh();
    }

    private void AddIndexed(JsonElement req, string key, string field, string label, string minimumKey)
    {
        if (!req.TryGetProperty(key, out var choices)) return;
        var minimum = req.TryGetProperty(minimumKey, out var min) ? min.GetInt32() : 0;
        foreach (var entry in choices.EnumerateObject().OrderBy(x => int.Parse(x.Name)))
            AddPicker(field, label + " " + (int.Parse(entry.Name) + 1), entry.Value, int.Parse(entry.Name) < minimum);
    }

    private void AddPicker(string field, string label, JsonElement choices, bool required)
    {
        var rows = choices.EnumerateArray().ToArray();
        MatchTableLayout.Label(_choices, label, 14);
        var picker = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 44) };
        _choices.AddChild(picker); picker.AddItem(required ? "请选择" : "不选择");
        foreach (var choice in rows) picker.AddItem(_name(choice.GetProperty("id").GetString()!));
        _picks.Add((field, picker, rows.Select(x => x.GetProperty("id").GetString()!).Prepend("").ToArray(), required));
        picker.Select(required && rows.Length == 1 ? 1 : 0); picker.ItemSelected += _ => Refresh();
    }

    private void Refresh()
    {
        _confirm.Disabled = IsSubmitting || _picks.Any(p => p.Required && p.Picker.Selected <= 0);
        _cancel.Disabled = _source.Disabled = IsSubmitting;
        foreach (var pick in _picks) pick.Picker.Disabled = IsSubmitting;
        foreach (var cost in _costs.Values) cost.Disabled = IsSubmitting;
        _confirm.Text = IsSubmitting ? "正在提交…" : "确认战斗";
        _status.Text = IsSubmitting ? "正在等待对局确认。" : _confirm.Disabled ? "请完成参战与目标选择。" : "确认后进入战斗响应。";
    }

    private void Submit()
    {
        if (_confirm.Disabled) return;
        var payload = new Dictionary<string, object?> { ["cmdType"] = "DECLARE_BATTLE", ["optionalCosts"] = _requiredCosts.Concat(_costs.Where(x => x.Value.ButtonPressed).Select(x => x.Key)).Distinct().ToArray() };
        foreach (var group in _picks.GroupBy(p => p.Field))
        {
            var ids = group.Where(p => p.Picker.Selected > 0).Select(p => p.Ids[p.Picker.Selected]).ToArray();
            if (group.Key == "battlefieldId") payload[group.Key] = ids.Single();
            else payload[group.Key] = ids;
        }
        IsSubmitting = true; Refresh(); Confirmed?.Invoke(payload);
    }

    public void ApplyReceipt(string promptId, long tick, bool accepted, string message)
    {
        if (!Visible || promptId != PromptId || tick != SnapshotTick) return;
        IsSubmitting = false; if (accepted) { Hide(); return; }
        Refresh(); _status.Text = message;
    }
}
