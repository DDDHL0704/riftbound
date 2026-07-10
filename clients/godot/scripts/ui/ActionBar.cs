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

    private Label _guidance = null!;
    private HBoxContainer _actionChoices = null!;
    private Label _selectionSummary = null!;
    private Label _stepLabel = null!;
    private HBoxContainer _stepChoices = null!;
    private Button _cancelButton = null!;
    private Button _submitButton = null!;
    private PromptSelectionState? _current;
    private bool _pending;

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

        ApplyTheme();
        SetWaiting("等待服务端提供下一步行动。");
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
        _guidance.Text = string.IsNullOrWhiteSpace(guidance)
            ? "请选择服务端提供的行动。"
            : guidance;
        ClearChildren(_actionChoices);
        foreach (var action in actions.Where(option => option.Enabled && !option.IsSpecial))
        {
            var button = new Button
            {
                Text = action.Label,
                TooltipText = string.IsNullOrWhiteSpace(action.Reason) ? action.Label : action.Reason,
                CustomMinimumSize = new Vector2(104, 40),
                FocusMode = FocusModeEnum.All
            };
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
        _selectionSummary.Text = state.Summary;
        _stepLabel.Text = string.IsNullOrWhiteSpace(stepLabel)
            ? state.CanSubmit ? "可以提交" : "等待服务端选项"
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
            var role = choice.Role;
            var choiceId = choice.Id;
            button.Pressed += () => ChoiceSelected?.Invoke(role, choiceId);
            _stepChoices.AddChild(button);
        }

        _cancelButton.Visible = true;
        _cancelButton.Disabled = _pending;
        _submitButton.Visible = true;
        _submitButton.Text = state.CanSubmit ? "确认提交" : "完成选择后提交";
        _submitButton.Disabled = _pending || !state.CanSubmit;
    }

    public void ClearSelectionDisplay()
    {
        if (!IsNodeReady())
        {
            return;
        }

        _current = null;
        _selectionSummary.Text = "选择行动后，桌面会标出服务器允许的对象。";
        _stepLabel.Text = string.Empty;
        ClearChildren(_stepChoices);
        _cancelButton.Visible = false;
        _submitButton.Visible = false;
    }

    public void SetWaiting(string guidance)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _pending = false;
        _guidance.Text = guidance;
        ClearChildren(_actionChoices);
        _actionChoices.AddChild(SecondaryLabel("等待服务端"));
        ClearSelectionDisplay();
    }

    public void SetPending(bool pending)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _pending = pending;
        foreach (var button in _actionChoices.GetChildren().OfType<Button>())
        {
            button.Disabled = pending;
        }

        foreach (var button in _stepChoices.GetChildren().OfType<Button>())
        {
            button.Disabled = pending;
        }

        _cancelButton.Disabled = pending;
        _submitButton.Disabled = pending || _current?.CanSubmit != true;
        if (pending)
        {
            _selectionSummary.Text = "已提交，等待服务器确认。";
        }
    }

    private void SubmitCurrent()
    {
        if (!_pending && _current is { CanSubmit: true } state)
        {
            SubmitRequested?.Invoke(state);
        }
    }

    private static string FriendlyChoiceLabel(string label)
    {
        var value = string.IsNullOrWhiteSpace(label) ? "服务端选项" : label.Trim();
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
            child.Free();
        }
    }
}
