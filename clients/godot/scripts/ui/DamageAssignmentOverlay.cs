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
    private readonly Dictionary<(string Source, string Target), int> _damageByPair = new();
    private VBoxContainer _rows = null!;
    private Label _summary = null!;
    private Button _cancelButton = null!;
    private Button _confirmButton = null!;
    private bool _canUsePrompt;

    public IReadOnlyList<DamageAssignmentSelection> RequiredAssignments => _assignments
        .SelectMany(assignment => assignment.Targets
            .Select(target => new DamageAssignmentSelection(
                assignment.SourceObjectId,
                target.TargetObjectId,
                _damageByPair.GetValueOrDefault((assignment.SourceObjectId, target.TargetObjectId))))
            .Where(selection => selection.Damage > 0))
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
                Text = $"来源剩余伤害：{RemainingDamage(assignment)}",
                TooltipText = "服务端提供的可分配伤害"
            });

            for (var targetIndex = 0; targetIndex < assignment.Targets.Count; targetIndex++)
            {
                var target = assignment.Targets[targetIndex];
                var targetRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var targetId = target.TargetObjectId;
                targetRow.AddChild(new Label
                {
                    Text = $"{target.Label} · 致命阈值 {target.LethalDamageThreshold}",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                });
                var spinBox = new SpinBox
                {
                    Name = "DamageAmountStepper",
                    MinValue = 0,
                    MaxValue = targetIndex == assignment.Targets.Count - 1
                        ? assignment.DamagePool
                        : Math.Min(assignment.DamagePool, target.LethalDamageThreshold),
                    Step = 1,
                    AllowGreater = false,
                    Editable = _canUsePrompt,
                    Value = _damageByPair.GetValueOrDefault((assignment.SourceObjectId, targetId))
                };
                spinBox.ValueChanged += value => DamageValueChanged(
                    assignment.SourceObjectId,
                    targetId,
                    (int)Math.Round(value));
                targetRow.AddChild(spinBox);
                row.AddChild(targetRow);
            }

            _rows.AddChild(row);
        }

        var completed = _assignments.Count(assignment => RemainingDamage(assignment) == 0);
        _summary.Text = $"已完成 {completed} / {_assignments.Count} 个伤害来源。";
        _confirmButton.Disabled = !_canUsePrompt || !HasValidServerDistribution();
    }

    private void DamageValueChanged(string sourceObjectId, string targetObjectId, int damage)
    {
        if (!_canUsePrompt)
        {
            return;
        }

        _damageByPair[(sourceObjectId, targetObjectId)] = Math.Max(0, damage);
        RenderRows();
    }

    private int RemainingDamage(DamageAssignmentPromptItem assignment)
    {
        var assignedDamage = assignment.Targets.Sum(target =>
            _damageByPair.GetValueOrDefault((assignment.SourceObjectId, target.TargetObjectId)));
        return assignment.DamagePool - assignedDamage;
    }

    private bool HasValidServerDistribution()
    {
        foreach (var assignment in _assignments)
        {
            if (RemainingDamage(assignment) != 0)
            {
                return false;
            }

            for (var targetIndex = 0; targetIndex < assignment.Targets.Count - 1; targetIndex++)
            {
                var target = assignment.Targets[targetIndex];
                var damage = _damageByPair.GetValueOrDefault((assignment.SourceObjectId, target.TargetObjectId));
                if (damage > target.LethalDamageThreshold)
                {
                    return false;
                }

                var laterHasDamage = assignment.Targets
                    .Skip(targetIndex + 1)
                    .Any(later => _damageByPair.GetValueOrDefault((assignment.SourceObjectId, later.TargetObjectId)) > 0);
                if (laterHasDamage && damage < target.LethalDamageThreshold)
                {
                    return false;
                }
            }
        }

        return _assignments.Count > 0;
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
        _damageByPair.Clear();
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
