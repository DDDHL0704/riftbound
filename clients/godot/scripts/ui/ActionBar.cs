using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Riftbound.GodotClient.Interaction;

namespace Riftbound.GodotClient.Ui;

public partial class ActionBar : Control
{
    public event Action<string>? ActionSelected;
    public event Action<string, string>? ChoiceSelected;
    public event Action? CancelRequested;
    public event Action<PromptSelectionState>? SubmitRequested;
    public event Action<bool>? SelectionVisibilityChanged;

    private Label _guidance = null!;
    private HBoxContainer _actionChoices = null!;
    private Label _selectionSummary = null!;
    private Label _stepLabel = null!;
    private HBoxContainer _stepChoices = null!;
    private Button _cancelButton = null!;
    private Button _submitButton = null!;
    private Button _primaryAction = null!;
    private string? _primaryActionName;
    private PromptSelectionState? _current;
    private bool _pending;
    private bool _composing;

    public override void _Ready()
    {
        _guidance = GetNode<Label>("%Guidance");
        _actionChoices = GetNode<HBoxContainer>("%ActionChoices");
        _selectionSummary = GetNode<Label>("%SelectionSummary");
        _stepLabel = GetNode<Label>("%StepLabel");
        _stepChoices = GetNode<HBoxContainer>("%StepChoices");
        _cancelButton = GetNode<Button>("%CancelButton");
        _submitButton = GetNode<Button>("%SubmitButton");
        _cancelButton.Pressed += () => CancelRequested?.Invoke();
        _submitButton.Pressed += SubmitCurrent;
        _primaryAction = new Button { CustomMinimumSize = new Vector2(140, 40), FocusMode = FocusModeEnum.All };
        _actionChoices.GetParent().GetParent().AddChild(_primaryAction);
        MinimalTheme.Apply(_primaryAction);
        var primaryStyle = MinimalTheme.Panel(new Color("244858"));
        primaryStyle.BorderColor = MinimalTheme.Selected;
        _primaryAction.AddThemeStyleboxOverride("normal", primaryStyle);
        _primaryAction.Pressed += () => { if (_primaryActionName is { } name) ActionSelected?.Invoke(name); };

        ApplyTheme();
        SetWaiting("正在同步下一步行动。");
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        _guidance?.AddThemeColorOverride("font_color", MinimalTheme.Text);
        _selectionSummary?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
        _stepLabel?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
    }

    public void ShowPrompt(string guidance, IReadOnlyList<PromptActionOption> actions)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _pending = false;
        _guidance.Text = "行动";
        _guidance.TooltipText = guidance;
        var primary = actions.FirstOrDefault(option => option.Enabled && IsPrimary(option.Name));
        _primaryActionName = primary?.Name;
        _primaryAction.Text = primary?.Name switch {
            "PASS_PRIORITY" => "不响应 · 让过",
            "PASS_FOCUS" => "不出牌 · 让过",
            _ => primary?.Label ?? "等待行动"
        };
        _primaryAction.TooltipText = guidance;
        _primaryAction.Disabled = primary is null || _composing;
        ClearChildren(_actionChoices);
        foreach (var action in actions.Where(option => option.Enabled && !option.IsSpecial && !IsPrimary(option.Name)).OrderBy(option => option.Name == "SURRENDER" ? 1 : 0))
        {
            var button = new Button
            {
                Text = action.Label,
                TooltipText = string.IsNullOrWhiteSpace(action.Reason) ? action.Label : action.Reason,
                CustomMinimumSize = new Vector2(90, 40),
                FocusMode = FocusModeEnum.All,
                Disabled = _composing
            };
            MinimalTheme.Apply(button);
            if (string.Equals(action.Name, "SURRENDER", StringComparison.Ordinal))
            {
                button.AddThemeColorOverride("font_color", MinimalTheme.Hostile);
            }

            var actionName = action.Name;
            button.Pressed += () => ActionSelected?.Invoke(actionName);
            _actionChoices.AddChild(button);
        }

        if (_actionChoices.GetChildCount() == 0)
        {
            _actionChoices.AddChild(SecondaryLabel("等待对手或专用选择流程"));
        }

        ClearSelectionDisplay();
        ConfigureFocusLoop();
    }

    public void ShowSelection(
        PromptSelectionState state,
        IReadOnlyList<PromptChoice> choices,
        string stepLabel,
        bool stepRequired)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _current = state;
        _primaryAction.Visible = false;
        _selectionSummary.GetParent<Control>().Visible = true;
        SelectionVisibilityChanged?.Invoke(true);
        _selectionSummary.Text = state.Summary;
        _selectionSummary.TooltipText = state.Summary;
        _stepLabel.Text = string.IsNullOrWhiteSpace(stepLabel)
            ? state.CanSubmit ? "可以提交" : "等待可选行动"
            : stepRequired ? $"{stepLabel}（必选）" : $"{stepLabel}（可选）";
        ClearChildren(_stepChoices);
        foreach (var choice in choices)
        {
            var button = new Button
            {
                Text = FriendlyChoiceLabel(choice.Label),
                TooltipText = FriendlyChoiceLabel(choice.Label),
                CustomMinimumSize = new Vector2(92, 36),
                FocusMode = FocusModeEnum.All,
                Disabled = _pending
            };
            MinimalTheme.Apply(button);
            var role = choice.Role;
            var choiceId = choice.Id;
            button.Pressed += () => ChoiceSelected?.Invoke(role, choiceId);
            _stepChoices.AddChild(button);
        }

        _cancelButton.Visible = true;
        _cancelButton.Disabled = _pending;
        _submitButton.Visible = true;
        _submitButton.Text = state.CanSubmit ? state.ActionName == "END_TURN" ? "确认结束回合" : state.ActionName == "PASS_PRIORITY" ? "确认让过响应" : state.ActionName == "SURRENDER" ? "确认投降" : "确认行动" : "请完成选择";
        _submitButton.Disabled = _pending || !state.CanSubmit;
        ConfigureFocusLoop();
    }

    public void ClearSelectionDisplay()
    {
        if (!IsNodeReady())
        {
            return;
        }

        _current = null;
        _primaryAction.Visible = true;
        _selectionSummary.GetParent<Control>().Visible = false;
        SelectionVisibilityChanged?.Invoke(false);
        _selectionSummary.Text = _composing ? "在右侧完成选择并确认 · Esc 返回牌桌" : "点牌行动 · 右键查看 · Esc 取消选择";
        _stepLabel.Text = string.Empty;
        ClearChildren(_stepChoices);
        _cancelButton.Visible = false;
        _submitButton.Visible = false;
        ConfigureFocusLoop();
    }

    public void SetWaiting(string guidance)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _pending = false;
        _primaryActionName = null;
        _primaryAction.Text = "等待对手";
        _primaryAction.Disabled = true;
        _guidance.Text = guidance;
        ClearChildren(_actionChoices);
        _actionChoices.AddChild(SecondaryLabel("可查看卡牌和战况"));
        ClearSelectionDisplay();
    }

    public void SetComposerActive(bool active)
    {
        _composing = active;
        _primaryAction.Disabled = active || _pending || _primaryActionName is null;
        foreach (var button in _actionChoices.GetChildren().OfType<Button>()) button.Disabled = active || _pending;
        if (_current is null) ClearSelectionDisplay();
    }

    public void SetPending(bool pending)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _pending = pending;
        _primaryAction.Disabled = pending || _composing || _primaryActionName is null;
        foreach (var button in _actionChoices.GetChildren().OfType<Button>())
        {
            button.Disabled = pending || _composing;
        }

        foreach (var button in _stepChoices.GetChildren().OfType<Button>())
        {
            button.Disabled = pending || _composing;
        }

        _cancelButton.Disabled = pending;
        _submitButton.Disabled = pending || _current?.CanSubmit != true;
        if (pending)
        {
            _selectionSummary.Text = "已提交，等待服务器确认。";
        }

        ConfigureFocusLoop();
    }

    public bool FocusAdjacentAction(int direction)
    {
        var controls = FocusableControls();
        if (controls.Count == 0)
        {
            return false;
        }

        var focused = GetViewport().GuiGetFocusOwner();
        var currentIndex = controls.FindIndex(control => control == focused);
        var nextIndex = currentIndex < 0
            ? direction < 0 ? controls.Count - 1 : 0
            : (currentIndex + Math.Sign(direction) + controls.Count) % controls.Count;
        controls[nextIndex].GrabFocus();
        return true;
    }

    public bool ConfirmCurrent()
    {
        if (GetViewport().GuiGetFocusOwner() is not Button focused
            || !IsAncestorOf(focused)
            || focused.Disabled
            || !focused.IsVisibleInTree())
        {
            return false;
        }

        focused.EmitSignal(Button.SignalName.Pressed);
        return true;
    }

    public bool CancelCurrent()
    {
        if (_pending || _current is null)
        {
            return false;
        }

        CancelRequested?.Invoke();
        return true;
    }

    private void SubmitCurrent()
    {
        if (!_pending && _current is { CanSubmit: true } state)
        {
            SubmitRequested?.Invoke(state);
        }
    }

    private void ConfigureFocusLoop()
    {
        var controls = FocusableControls();
        for (var index = 0; index < controls.Count; index++)
        {
            var previous = controls[(index - 1 + controls.Count) % controls.Count];
            var next = controls[(index + 1) % controls.Count];
            controls[index].FocusPrevious = controls[index].GetPathTo(previous);
            controls[index].FocusNext = controls[index].GetPathTo(next);
        }
    }

    private List<Button> FocusableControls()
    {
        var controls = _actionChoices.GetChildren().OfType<Button>()
            .Concat(_stepChoices.GetChildren().OfType<Button>())
            .Append(_primaryAction)
            .Where(button => button.Visible && !button.Disabled)
            .ToList();
        if (_cancelButton.Visible && !_cancelButton.Disabled)
        {
            controls.Add(_cancelButton);
        }

        if (_submitButton.Visible && !_submitButton.Disabled)
        {
            controls.Add(_submitButton);
        }

        return controls;
    }

    private static bool IsPrimary(string name) => name is "END_TURN" or "PASS_PRIORITY" or "PASS_FOCUS";

    private static string FriendlyChoiceLabel(string label)
    {
        var value = string.IsNullOrWhiteSpace(label) ? "可选行动" : label.Trim();
        const int maxLength = 24;
        return value.Length <= maxLength ? value : $"{value[..(maxLength - 1)]}…";
    }

    private static Label SecondaryLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
        return label;
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
