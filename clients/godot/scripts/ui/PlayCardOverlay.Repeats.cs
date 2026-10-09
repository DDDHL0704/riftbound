using System.Text.Json;
using Godot;
using Riftbound.Contracts;

namespace Riftbound.GodotClient.Ui;

public partial class PlayCardOverlay
{
    private sealed class RepeatSelection
    {
        public VBoxContainer Panel = null!;
        public OptionButton Mode = null!;
        public JsonElement[] Modes = [];
        public VBoxContainer TargetsPanel = null!;
        public List<(OptionButton Picker, string[] Ids, bool Required)> Targets = [];
    }
    private readonly List<RepeatSelection> _repeats = [];
    private VBoxContainer? _repeatPanel;
    private OptionButton? _focusedTarget;
    private IEnumerable<(OptionButton Picker, string[] Ids, bool Required)> AllExecutionTargets
        => _targets.Concat(_repeats.SelectMany(repeat => repeat.Targets));

    private void UpdateRepeatChoices()
    {
        if (_repeatPanel is null || _source.Selected < 0) return;
        var requirement = _requirements[_source.Selected];
        var enabled = requirement.TryGetProperty("supportsSeparateExecutions", out var support) && support.ValueKind == JsonValueKind.True;
        var modes = _requirements.Where(r => Text(r, "cardNo") == Text(requirement, "cardNo")
            && r.TryGetProperty("supportsSeparateExecutions", out var allow) && allow.ValueKind == JsonValueKind.True).ToArray();
        var count = enabled && (modes.Length > 1 || Number(requirement, "maxTargetCount") > 0)
            ? _optional.Count(entry => entry.Value.ButtonPressed && (entry.Key == "ECHO" || entry.Key.StartsWith("ECHO:", StringComparison.Ordinal))) : 0;
        while (_repeats.Count > count)
        {
            var last = _repeats[^1]; _repeatPanel.RemoveChild(last.Panel); last.Panel.QueueFree(); _repeats.RemoveAt(_repeats.Count - 1);
        }
        while (_repeats.Count < count)
        {
            var entry = new RepeatSelection { Panel = new VBoxContainer(), Modes = modes };
            _repeatPanel.AddChild(entry.Panel);
            entry.Panel.AddChild(new Label { Text = $"第 {_repeats.Count + 2} 次执行 · 回响" });
            entry.Mode = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 38) };
            entry.Panel.AddChild(entry.Mode);
            foreach (var mode in modes) entry.Mode.AddItem(Text(mode, "modeLabel"));
            entry.Mode.Select(Math.Max(0, Array.FindIndex(modes, m => Text(m, "mode") == Text(requirement, "mode"))));
            entry.TargetsPanel = new VBoxContainer(); entry.Panel.AddChild(entry.TargetsPanel);
            _repeats.Add(entry);
            BuildRepeatTargets(entry);
            entry.Mode.ItemSelected += _ => { BuildRepeatTargets(entry); Refresh(); };
            MinimalTheme.Apply(entry.Panel);
        }
    }

    private void BuildRepeatTargets(RepeatSelection entry)
    {
        foreach (var node in entry.TargetsPanel.GetChildren()) { entry.TargetsPanel.RemoveChild(node); node.QueueFree(); }
        entry.Targets.Clear();
        var mode = entry.Modes[entry.Mode.Selected];
        if (!mode.TryGetProperty("targetChoicesByIndex", out var byIndex)) return;
        foreach (var target in byIndex.EnumerateObject().OrderBy(x => int.Parse(x.Name)))
        {
            var choices = ReadChoices(target.Value);
            var index = int.Parse(target.Name);
            var required = index < Number(mode, "minTargetCount");
            entry.TargetsPanel.AddChild(new Label { Text = $"目标 {index + 1}" + (required ? " · 必选" : " · 可选") });
            var picker = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 38) };
            picker.AddItem("请选择目标");
            foreach (var choice in choices) picker.AddItem(choice.Label);
            entry.TargetsPanel.AddChild(picker);
            var ids = choices.Select(x => x.Id).Prepend("").ToArray();
            var initial = index < _targets.Count && _targets[index].Picker.Selected > 0
                ? _targets[index].Ids[_targets[index].Picker.Selected] : "";
            picker.Select(Math.Max(0, Array.IndexOf(ids, initial)));
            entry.Targets.Add((picker, ids, required));
            picker.FocusEntered += () => { _focusedTarget = picker; TableSelectionChanged?.Invoke(); };
            picker.ItemSelected += _ => Refresh();
        }
    }

    private IReadOnlyList<SpellRepeatChoice>? RepeatCommandChoices()
        => _repeats.Count == 0 ? null : _repeats.Select(entry => new SpellRepeatChoice(Text(entry.Modes[entry.Mode.Selected], "mode"),
            entry.Targets.Where(t => t.Picker.Selected > 0).Select(t => t.Ids[t.Picker.Selected]).ToArray())).ToArray();
}
