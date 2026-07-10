using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class DamageAssignmentOverlay : Control
{
    public event Action<IReadOnlyList<DamageAssignmentSelection>>? Confirmed;
    public event Action? Cancelled;

    private readonly List<DamageAssignmentPromptItem> _assignments = [];
    private readonly Dictionary<string, string> _selectedTargets = new(StringComparer.Ordinal);
    private VBoxContainer _rows = null!;
    private Label _summary = null!;
    private Button _cancelButton = null!;
    private Button _confirmButton = null!;
    private bool _canUsePrompt;

    public IReadOnlyList<DamageAssignmentSelection> RequiredAssignments => _assignments
        .Where(assignment => _selectedTargets.ContainsKey(assignment.SourceObjectId))
        .Select(assignment => new DamageAssignmentSelection(
            assignment.SourceObjectId,
            _selectedTargets[assignment.SourceObjectId],
            assignment.RemainingDamage))
        .ToArray();

    public bool CanUsePrompt => _canUsePrompt;

    public override void _Ready()
    {
        _rows = GetNode<VBoxContainer>("%DamageRows");
        _summary = GetNode<Label>("%DamageSummary");
        _cancelButton = GetNode<Button>("%CancelButton");
        _confirmButton = GetNode<Button>("%ConfirmButton");
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
        _canUsePrompt = ReadBool(action, "enabled");
        Visible = true;
        MoveToFront();
        RenderRows();
        _cancelButton.GrabFocus();
        return _canUsePrompt;
    }

    public void HidePrompt()
    {
        Reset();
        Visible = false;
    }

    private void RenderRows()
    {
        ClearChildren(_rows);
        foreach (var assignment in _assignments)
        {
            var row = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = assignment.SourceLabel });
            row.AddChild(new Label
            {
                Name = "RemainingDamage",
                Text = $"剩余伤害：{assignment.RemainingDamage}",
                TooltipText = "服务端提供的可分配伤害"
            });

            var targets = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var target in assignment.Targets)
            {
                var targetId = target.TargetObjectId;
                var button = new Button
                {
                    Name = "DamageTargetButton",
                    Text = target.Label,
                    Disabled = !_canUsePrompt,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                if (_selectedTargets.TryGetValue(assignment.SourceObjectId, out var selectedTarget)
                    && string.Equals(selectedTarget, targetId, StringComparison.Ordinal))
                {
                    button.AddThemeStyleboxOverride("normal", MinimalTheme.Outline(OfficialCardVisualState.Selected));
                }

                button.Pressed += () => SelectTarget(assignment.SourceObjectId, targetId);
                targets.AddChild(button);
            }

            row.AddChild(targets);
            _rows.AddChild(row);
        }

        var completed = RequiredAssignments.Count;
        _summary.Text = $"已指定 {completed} / {_assignments.Count} 个伤害来源。";
        _confirmButton.Disabled = !_canUsePrompt || completed != _assignments.Count;
    }

    private void SelectTarget(string sourceObjectId, string targetObjectId)
    {
        if (!_canUsePrompt)
        {
            return;
        }

        _selectedTargets[sourceObjectId] = targetObjectId;
        RenderRows();
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
    }

    private void Reset()
    {
        _assignments.Clear();
        _selectedTargets.Clear();
        _canUsePrompt = false;
        if (IsNodeReady())
        {
            ClearChildren(_rows);
            _summary.Text = string.Empty;
            _confirmButton.Disabled = true;
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
