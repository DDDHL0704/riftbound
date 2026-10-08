using Godot;

namespace Riftbound.GodotClient.Ui;

/// <summary>Selects only rune objects authorized by the current server prompt.</summary>
public partial class RuneActionPanel : Control
{
    public event Action<string, string[]>? Requested;
    public event Action? SelectionChanged;
    public string PromptId { get; private set; } = "";
    public long SnapshotTick { get; private set; } = -1;
    public bool IsSubmitting { get; private set; }
    public IReadOnlyCollection<string> Selected => _selected;
    public IReadOnlyCollection<string> TapSources => _tapSources;
    public IReadOnlyCollection<string> RecycleSources => _recycleSources;
    private readonly HashSet<string> _tapSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recycleSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private Label _summary = null!;
    private Label _status = null!;
    private Button _tap = null!;
    private Button _recycle = null!;
    private Button _allTap = null!;
    private Button _allRecycle = null!;
    private Button _cancel = null!;
    private string _submissionPrompt = "";
    private long _submissionTick = -1;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer(); AddChild(panel); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = MatchTableLayout.Column(panel, 12);
        MatchTableLayout.Label(column, "符文资源", 22, MinimalTheme.Selected);
        MatchTableLayout.Label(column, "点击牌桌符文多选，再一次提交。\n平时点击符文直接横置，点牌下“回收”获得符能。", 13, MinimalTheme.TextSecondary, true);
        _summary = MatchTableLayout.Label(column, "尚未选择符文", 16, MinimalTheme.Text, true);
        _allTap = Button(column, "选择全部可横置", () => SelectAll(false));
        _allRecycle = Button(column, "选择全部可回收", () => SelectAll(true));
        _tap = Button(column, "横置所选", () => Request("TAP_RUNE", _selected.ToArray()));
        _recycle = Button(column, "回收所选", () => Request("RECYCLE_RUNE", _selected.ToArray()));
        _status = MatchTableLayout.Label(column, "", 13, MinimalTheme.TextSecondary, true);
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _cancel = Button(column, "取消  Esc", Cancel);
        MinimalTheme.Apply(panel); Hide(); Refresh();
    }

    private static Button Button(Node parent, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
        parent.AddChild(button); button.Pressed += pressed; return button;
    }

    public void Load(string promptId, long tick, IEnumerable<string> tap, IEnumerable<string> recycle)
    {
        var changed = PromptId != promptId || SnapshotTick != tick;
        PromptId = promptId; SnapshotTick = tick;
        _tapSources.Clear(); _tapSources.UnionWith(tap);
        _recycleSources.Clear(); _recycleSources.UnionWith(recycle);
        if (changed) { IsSubmitting = false; _selected.Clear(); _status.Text = ""; Hide(); }
        _selected.RemoveWhere(id => !_tapSources.Contains(id) && !_recycleSources.Contains(id));
        Refresh(); SelectionChanged?.Invoke();
    }

    public bool OwnsSource(string id) => _tapSources.Contains(id) || _recycleSources.Contains(id);

    public bool Toggle(string id)
    {
        if (IsSubmitting || !OwnsSource(id)) return false;
        Show();
        if (!_selected.Add(id)) _selected.Remove(id);
        _status.Text = ""; Refresh(); SelectionChanged?.Invoke(); return true;
    }

    public void Open()
    {
        if (IsSubmitting || _tapSources.Count + _recycleSources.Count == 0) return;
        Show(); Refresh(); SelectionChanged?.Invoke();
    }

    public void SelectAll(bool recycle)
    {
        if (IsSubmitting) return;
        _selected.Clear(); _selected.UnionWith(recycle ? _recycleSources : _tapSources);
        Show(); _status.Text = ""; Refresh(); SelectionChanged?.Invoke();
    }

    public bool Request(string action, string[] ids)
    {
        var legal = action == "TAP_RUNE" ? _tapSources : action == "RECYCLE_RUNE" ? _recycleSources : [];
        if (IsSubmitting || ids.Length == 0 || ids.Length > 12 || ids.Distinct().Count() != ids.Length || ids.Any(id => !legal.Contains(id))) return false;
        _submissionPrompt = PromptId; _submissionTick = SnapshotTick;
        IsSubmitting = true; _status.Text = "正在结算…"; Refresh(); SelectionChanged?.Invoke();
        Requested?.Invoke(action, ids); return true;
    }

    public void ApplyReceipt(string promptId, long tick, bool accepted, string message)
    {
        if (!MatchesReceipt(promptId, tick)) return;
        IsSubmitting = false;
        if (PromptId == promptId && SnapshotTick == tick)
        {
            if (accepted) Cancel();
            else { _status.Text = message; Refresh(); }
        }
        Refresh(); SelectionChanged?.Invoke();
    }

    public bool MatchesReceipt(string promptId, long tick)
        => IsSubmitting && _submissionPrompt == promptId && _submissionTick == tick;

    public void Cancel()
    {
        if (IsSubmitting) return;
        _selected.Clear(); Hide(); Refresh(); SelectionChanged?.Invoke();
    }

    private void Refresh()
    {
        if (!IsNodeReady()) return;
        var count = _selected.Count;
        _summary.Text = count == 0 ? "尚未选择符文" : $"已选 {count} 枚符文";
        _tap.Text = count == 0 ? "横置所选" : $"横置 {count} 枚 · 获得 {count} 法力";
        _recycle.Text = count == 0 ? "回收所选" : $"回收 {count} 枚 · 获得对应符能";
        _tap.Disabled = IsSubmitting || count == 0 || !_selected.All(_tapSources.Contains);
        _recycle.Disabled = IsSubmitting || count == 0 || !_selected.All(_recycleSources.Contains);
        _allTap.Disabled = IsSubmitting || _tapSources.Count == 0;
        _allRecycle.Disabled = IsSubmitting || _recycleSources.Count == 0;
        _cancel.Disabled = IsSubmitting;
    }
}
