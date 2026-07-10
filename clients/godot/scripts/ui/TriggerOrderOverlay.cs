using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class TriggerOrderOverlay : Control
{
    public event Action<IReadOnlyList<string>>? Confirmed;
    public event Action? Cancelled;

    private readonly List<TriggerPromptItem> _triggers = [];
    private VBoxContainer _rows = null!;
    private Label _summary = null!;
    private Button _cancelButton = null!;
    private Button _confirmButton = null!;
    private int _selectedIndex;
    private bool _canUsePrompt;

    public IReadOnlyList<string> OrderedTriggerIds => _triggers.Select(trigger => trigger.TriggerId).ToArray();

    public bool CanUsePrompt => _canUsePrompt;

    public override void _Ready()
    {
        _rows = GetNode<VBoxContainer>("%TriggerRows");
        _summary = GetNode<Label>("%TriggerSummary");
        _cancelButton = GetNode<Button>("%CancelButton");
        _confirmButton = GetNode<Button>("%ConfirmButton");
        _cancelButton.Pressed += Cancel;
        _confirmButton.Pressed += Confirm;

        ApplyTheme();
        HidePrompt();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible || !_canUsePrompt)
        {
            return;
        }

        if (input.IsActionPressed("ui_up"))
        {
            MoveSelected(-1);
            GetViewport().SetInputAsHandled();
        }
        else if (input.IsActionPressed("ui_down"))
        {
            MoveSelected(1);
            GetViewport().SetInputAsHandled();
        }
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        GetNode<PanelContainer>("%TriggerPanel")
            .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.SurfaceRaised));
        GetNode<Label>("%TriggerTitle").AddThemeFontSizeOverride("font_size", 24);
        _summary?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
    }

    public bool ShowPrompt(Godot.Collections.Dictionary action, out string reason)
    {
        Reset();
        if (!SpecialPromptCommandBuilder.TryReadOrderTriggers(action, out var triggers, out reason))
        {
            ShowDisabled();
            return false;
        }

        _triggers.AddRange(triggers);
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
        for (var index = 0; index < _triggers.Count; index++)
        {
            var rowIndex = index;
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var select = new Button
            {
                Text = $"{index + 1}. {_triggers[index].Label}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Disabled = !_canUsePrompt
            };
            select.Pressed += () =>
            {
                _selectedIndex = rowIndex;
                RenderRows();
            };
            if (index == _selectedIndex)
            {
                select.AddThemeStyleboxOverride("normal", MinimalTheme.Outline(OfficialCardVisualState.Selected));
            }

            var up = new Button
            {
                Name = "MoveUpButton",
                Text = "^",
                Disabled = !_canUsePrompt || !CanMoveWithinControllerBlock(index, -1)
            };
            var down = new Button
            {
                Name = "MoveDownButton",
                Text = "v",
                Disabled = !_canUsePrompt || !CanMoveWithinControllerBlock(index, 1)
            };
            up.Pressed += () => Move(rowIndex, -1);
            down.Pressed += () => Move(rowIndex, 1);
            row.AddChild(select);
            row.AddChild(up);
            row.AddChild(down);
            _rows.AddChild(row);
        }

        _summary.Text = _triggers.Count == 0 ? "服务端尚未提供触发顺序。" : "选择一项后可用方向键或上移、下移调整顺序。";
        _confirmButton.Disabled = !_canUsePrompt || _triggers.Count == 0;
    }

    private void MoveSelected(int delta)
    {
        Move(_selectedIndex, delta);
    }

    private void Move(int index, int delta)
    {
        var targetIndex = index + delta;
        if (!_canUsePrompt || !CanMoveWithinControllerBlock(index, delta))
        {
            return;
        }

        (_triggers[index], _triggers[targetIndex]) = (_triggers[targetIndex], _triggers[index]);
        _selectedIndex = targetIndex;
        RenderRows();
    }

    private bool CanMoveWithinControllerBlock(int index, int delta)
    {
        var targetIndex = index + delta;
        return index >= 0
            && targetIndex >= 0
            && targetIndex < _triggers.Count
            && _triggers[index].ControllerBlockIndex == _triggers[targetIndex].ControllerBlockIndex;
    }

    private void Confirm()
    {
        if (!_canUsePrompt || _confirmButton.Disabled)
        {
            return;
        }

        Confirmed?.Invoke(OrderedTriggerIds);
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
        _summary.Text = "服务端尚未提供可用的触发排序数据。";
        _confirmButton.Disabled = true;
    }

    private void Reset()
    {
        _triggers.Clear();
        _selectedIndex = 0;
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
