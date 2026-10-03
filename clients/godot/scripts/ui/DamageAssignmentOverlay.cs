using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class DamageAssignmentOverlay : Control
{
    public event Action<IReadOnlyList<DamageAssignmentSelection>>? Confirmed;
    public event Action? Cancelled;
    public event Action<Godot.Collections.Dictionary>? CardInspectionRequested;
    public Func<string, Godot.Collections.Dictionary?>? CardViewFor { get; set; }

    private readonly List<DamageAssignmentPromptItem> _assignments = [];
    private readonly Dictionary<string, long> _damageByTarget = new(StringComparer.Ordinal);
    private readonly List<DamageTargetPromptItem> _targets = [];
    private readonly Dictionary<string, SpinBox> _steppers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _targetSummaries = new(StringComparer.Ordinal);
    private VBoxContainer _rows = null!;
    private Label _summary = null!;
    private Label _feedback = null!;
    private Button _cancelButton = null!;
    private Button _confirmButton = null!;
    private Button _suggestButton = null!;
    private bool _canUsePrompt;

    public IReadOnlyList<DamageAssignmentSelection> RequiredAssignments =>
        PooledDamageSelection.TrySplit(_assignments, _damageByTarget, out var selections) ? selections : [];

    public bool CanUsePrompt => _canUsePrompt;

    public override void _Ready()
    {
        _rows = GetNode<VBoxContainer>("%DamageRows");
        _summary = GetNode<Label>("%DamageSummary");
        _feedback = GetNode<Label>("%DamageFeedback");
        _feedback.AddThemeColorOverride("font_color", MinimalTheme.Hostile);
        _cancelButton = GetNode<Button>("%CancelButton");
        _confirmButton = GetNode<Button>("%ConfirmButton");
        _suggestButton = GetNode<Button>("%SuggestButton");
        _suggestButton.Pressed += ApplySuggestedDistribution;
        _cancelButton.Pressed += Cancel;
        _confirmButton.Pressed += Confirm;

        ApplyTheme();
        HidePrompt();
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        GetNode<PanelContainer>("%DamagePanel")
            .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.SurfaceRaised));
        GetNode<Label>("%DamageTitle").AddThemeFontSizeOverride("font_size", 24);
        _summary?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
    }

    public bool ShowPrompt(Godot.Collections.Dictionary action, out string reason)
    {
        Reset();
        if (!SpecialPromptCommandBuilder.TryReadDamageAssignmentPrompt(action, out var assignments, out reason))
        {
            ShowDisabled();
            return false;
        }

        _assignments.AddRange(assignments);
        _targets.AddRange(assignments.SelectMany(source => source.Targets)
            .DistinctBy(target => target.TargetObjectId).OrderBy(target => target.Priority));
        _canUsePrompt = ReadBool(action, "enabled");
        _suggestButton.Disabled = !_canUsePrompt || _targets.Sum(target => target.SuggestedDamage) != TotalDamage;
        Visible = true;
        MoveToFront();
        RenderRows();
        FocusFirstControl();
        return _canUsePrompt;
    }

    public void HidePrompt()
    {
        Reset();
        Visible = false;
    }

    public bool ConfirmCurrent()
    {
        if (!_canUsePrompt || _confirmButton.Disabled)
        {
            return false;
        }

        Confirm();
        return true;
    }

    public void ResetSelection()
    {
        Cancel();
    }

    private long TotalDamage => _assignments.Sum(source => (long)source.DamagePool);
    private long RemainingDamage => TotalDamage - _damageByTarget.Values.Sum();

    private void RenderRows()
    {
        ClearChildren(_rows);
        _steppers.Clear();
        _targetSummaries.Clear();
        foreach (var target in _targets)
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.Surface));
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 20);
            if (CardViewFor?.Invoke(target.TargetObjectId) is { } card
                && card.TryGetValue("visible", out var visible) && visible.AsBool()
                && (!card.TryGetValue("faceDown", out var faceDown) || !faceDown.AsBool()))
            {
                var preview = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn").Instantiate<OfficialCardView>();
                preview.CustomMinimumSize = new Vector2(76, 106);
                preview.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                row.AddChild(preview);
                preview.Display(card, OfficialCardVisualState.Normal);
                preview.Activated += selected => CardInspectionRequested?.Invoke(selected);
            }
            var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var priority = target.Priority switch { 0 => "壁垒 · 优先分配", 2 => "后排 · 最后分配", _ => "普通单位" };
            var heading = new Label { Text = target.Label };
            heading.AddThemeFontSizeOverride("font_size", 18);
            labels.AddChild(heading);
            labels.AddChild(new Label { Text = $"{priority}    致命所需 {target.LethalDamageThreshold}" });
            var status = new Label();
            _targetSummaries[target.TargetObjectId] = status;
            labels.AddChild(status);
            row.AddChild(labels);
            var spinBox = new SpinBox
            {
                Name = "DamageAmountStepper", MinValue = 0, MaxValue = TotalDamage,
                Step = 1, AllowGreater = false, Editable = _canUsePrompt,
                CustomMinimumSize = new Vector2(140, 48),
                Value = _damageByTarget.GetValueOrDefault(target.TargetObjectId),
                TooltipText = "分配给此目标的总伤害"
            };
            spinBox.ValueChanged += value => DamageValueChanged(target.TargetObjectId, (long)Math.Round(value));
            _steppers[target.TargetObjectId] = spinBox;
            row.AddChild(spinBox);
            panel.AddChild(row);
            _rows.AddChild(panel);
        }
        UpdateSummary();
    }

    private void FocusFirstControl()
    {
        var first = _steppers.Values.FirstOrDefault(stepper => stepper.Editable);
        if (first is not null) first.GetLineEdit().GrabFocus();
        else _cancelButton.GrabFocus();
    }

    private void DamageValueChanged(string targetObjectId, long damage)
    {
        if (!_canUsePrompt) return;
        _damageByTarget[targetObjectId] = Math.Max(0, damage);
        _feedback.Text = string.Empty;
        // Keep controls alive while editing; rebuilding them loses keyboard focus.
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var remaining = RemainingDamage;
        _summary.Text = $"总伤害 {TotalDamage}    已分配 {TotalDamage - remaining}    "
            + (remaining < 0 ? $"超出 {-remaining}" : $"剩余 {remaining}");
        _summary.AddThemeColorOverride("font_color", remaining < 0 ? MinimalTheme.Hostile : MinimalTheme.Text);
        foreach (var target in _targets)
        {
            var amount = _damageByTarget.GetValueOrDefault(target.TargetObjectId);
            var status = _targetSummaries[target.TargetObjectId];
            status.Text = amount == 0 ? "尚未分配" : amount >= target.LethalDamageThreshold
                ? $"已分配 {amount} · 达到致命阈值" : $"已分配 {amount} · 距致命还差 {target.LethalDamageThreshold - amount}";
            status.AddThemeColorOverride("font_color", amount >= target.LethalDamageThreshold && amount > 0
                ? MinimalTheme.Selected : MinimalTheme.TextSecondary);
        }
        _confirmButton.Disabled = !_canUsePrompt || !PooledDamageSelection.TrySplit(_assignments, _damageByTarget, out _);
    }

    private void ApplySuggestedDistribution()
    {
        if (_suggestButton.Disabled) return;
        foreach (var target in _targets)
            _steppers[target.TargetObjectId].Value = target.SuggestedDamage;
        UpdateSummary();
    }

    public void ShowServerRejection(string message)
    {
        if (Visible) _feedback.Text = message;
    }

    private void Confirm()
    {
        if (!_canUsePrompt || _confirmButton.Disabled)
        {
            return;
        }

        Confirmed?.Invoke(RequiredAssignments);
    }

    private void Cancel()
    {
        HidePrompt();
        Cancelled?.Invoke();
    }

    private void ShowDisabled()
    {
        _canUsePrompt = false;
        Visible = true;
        MoveToFront();
        _summary.Text = "服务端尚未提供可用的伤害分配数据。";
        _confirmButton.Disabled = true;
        _suggestButton.Disabled = true;
    }

    private void Reset()
    {
        _assignments.Clear();
        _damageByTarget.Clear();
        _targets.Clear();
        _steppers.Clear();
        _targetSummaries.Clear();
        _canUsePrompt = false;
        if (IsNodeReady())
        {
            ClearChildren(_rows);
            _summary.Text = string.Empty;
            _feedback.Text = string.Empty;
            _confirmButton.Disabled = true;
        _suggestButton.Disabled = true;
            _cancelButton.Disabled = false;
        }
    }

    private static bool ReadBool(Godot.Collections.Dictionary source, string key)
    {
        return source.TryGetValue(key, out var value) && value.AsBool();
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
