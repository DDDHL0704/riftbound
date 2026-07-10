using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Interaction;

internal sealed class PromptInteractionController
{
    public event Action<PromptSelectionState>? SelectionChanged;
    public event Action? SelectionCleared;

    private readonly Dictionary<string, PromptActionModel> _actions = new(StringComparer.Ordinal);
    private readonly List<string> _actionOrder = [];
    private readonly Dictionary<int, string> _selectedChoiceByStep = [];
    private string _promptId = string.Empty;
    private long _snapshotTick = -1;
    private string? _selectedActionName;

    public PromptSelectionState? Current { get; private set; }

    public string PromptId => _promptId;

    public long SnapshotTick => _snapshotTick;

    public IReadOnlyList<PromptActionOption> Actions => _actionOrder
        .Select(name => _actions[name].Option)
        .ToArray();

    public IReadOnlyList<PromptChoice> CurrentChoices => CurrentStep()?.Choices ?? [];

    public string CurrentStepLabel => CurrentStep()?.Label ?? string.Empty;

    public string CurrentStepRole => CurrentStep()?.Role ?? string.Empty;

    public bool CurrentStepRequired => CurrentStep()?.Required ?? false;

    public bool HasEnabledSpecialAction => _actions.Values.Any(action =>
        action.Option.Enabled && action.Option.IsSpecial);

    public void Load(Godot.Collections.Dictionary promptView)
    {
        var nextPromptId = ReadString(promptView, "promptId");
        var nextSnapshotTick = ReadLong(promptView, "snapshotTick", -1);
        var identityChanged = !string.Equals(nextPromptId, _promptId, StringComparison.Ordinal)
            || nextSnapshotTick != _snapshotTick;
        var retainedAction = identityChanged ? null : _selectedActionName;
        var retainedChoices = identityChanged
            ? new Dictionary<int, string>()
            : new Dictionary<int, string>(_selectedChoiceByStep);

        _promptId = nextPromptId;
        _snapshotTick = nextSnapshotTick;
        _actions.Clear();
        _actionOrder.Clear();
        foreach (var action in ReadDictionaries(promptView, "actions"))
        {
            var model = ParseAction(action);
            if (string.IsNullOrWhiteSpace(model.Option.Name)
                || _actions.ContainsKey(model.Option.Name))
            {
                continue;
            }

            _actions[model.Option.Name] = model;
            _actionOrder.Add(model.Option.Name);
        }

        _selectedActionName = retainedAction;
        _selectedChoiceByStep.Clear();
        foreach (var (stepIndex, choiceId) in retainedChoices)
        {
            _selectedChoiceByStep[stepIndex] = choiceId;
        }

        RevalidateSelection();
        PublishSelection();
    }

    public bool SelectAction(string actionName)
    {
        if (!_actions.TryGetValue(actionName, out var action))
        {
            return false;
        }

        var enabled = action.Option.Enabled;
        if (!enabled
            || action.Option.IsSpecial)
        {
            return false;
        }

        _selectedActionName = actionName;
        _selectedChoiceByStep.Clear();
        AutoSelectForcedRequiredChoices(action, 0);
        PublishSelection();
        return true;
    }

    public bool TrySelectObject(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        if (CurrentAction() is null)
        {
            var matchingActions = _actions.Values
                .Where(action => action.Option.Enabled && !action.Option.IsSpecial)
                .Where(action => action.Steps.Any(step =>
                    string.Equals(step.Role, "source", StringComparison.Ordinal)
                    && step.Choices.Any(choice => choice.MatchesObject(objectId))))
                .ToArray();
            if (matchingActions.Length != 1 || !SelectAction(matchingActions[0].Option.Name))
            {
                return false;
            }
        }

        if (CurrentAction() is not { } action || CurrentStep() is not { } step)
        {
            return false;
        }

        var choice = step.Choices.FirstOrDefault(candidate => candidate.MatchesObject(objectId));
        return choice is not null && SelectChoice(action, step, choice);
    }

    public bool TrySelectChoice(string role, string choiceId)
    {
        if (string.IsNullOrWhiteSpace(role)
            || string.IsNullOrWhiteSpace(choiceId)
            || CurrentAction() is not { } action)
        {
            return false;
        }

        var step = action.Steps.FirstOrDefault(candidate =>
            !_selectedChoiceByStep.ContainsKey(candidate.Index)
            && string.Equals(candidate.Role, role, StringComparison.Ordinal)
            && candidate.Choices.Any(choice => string.Equals(choice.Id, choiceId, StringComparison.Ordinal)));
        step ??= action.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.Role, role, StringComparison.Ordinal)
            && candidate.Choices.Any(choice => string.Equals(choice.Id, choiceId, StringComparison.Ordinal)));
        if (step is null)
        {
            return false;
        }

        var choice = step.Choices.First(candidate => string.Equals(candidate.Id, choiceId, StringComparison.Ordinal));
        return SelectChoice(action, step, choice);
    }

    public void ClearSelection()
    {
        _selectedActionName = null;
        _selectedChoiceByStep.Clear();
        Current = null;
        SelectionCleared?.Invoke();
    }

    public Godot.Collections.Dictionary? CurrentActionDictionary()
    {
        return CurrentAction()?.Source.Duplicate(true);
    }

    public IReadOnlyCollection<string> SelectableObjectIds()
    {
        IEnumerable<PromptChoice> choices;
        if (CurrentAction() is null)
        {
            choices = _actions.Values
                .Where(action => action.Option.Enabled && !action.Option.IsSpecial)
                .SelectMany(action => action.Steps
                    .Where(step => string.Equals(step.Role, "source", StringComparison.Ordinal))
                    .SelectMany(step => step.Choices));
        }
        else
        {
            choices = CurrentStep()?.Choices ?? [];
        }

        return choices
            .SelectMany(choice => choice.SelectableObjectIds())
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyCollection<string> SelectedObjectIds()
    {
        if (CurrentAction() is not { } action)
        {
            return [];
        }

        return action.Steps
            .Where(step => _selectedChoiceByStep.ContainsKey(step.Index))
            .Select(step => step.Choices.FirstOrDefault(choice =>
                string.Equals(choice.Id, _selectedChoiceByStep[step.Index], StringComparison.Ordinal)))
            .Where(choice => choice is not null)
            .SelectMany(choice => choice!.SelectableObjectIds())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private bool SelectChoice(PromptActionModel action, PromptStep step, PromptChoice choice)
    {
        if (!action.Option.Enabled
            || !step.Choices.Any(candidate => string.Equals(candidate.Id, choice.Id, StringComparison.Ordinal)))
        {
            return false;
        }

        _selectedChoiceByStep[step.Index] = choice.Id;
        foreach (var laterStep in action.Steps.Where(candidate => candidate.Index > step.Index))
        {
            _selectedChoiceByStep.Remove(laterStep.Index);
        }

        AutoSelectForcedRequiredChoices(action, step.Index + 1);
        PublishSelection();
        return true;
    }

    private void RevalidateSelection()
    {
        if (string.IsNullOrWhiteSpace(_selectedActionName)
            || !_actions.TryGetValue(_selectedActionName, out var action)
            || !action.Option.Enabled
            || action.Option.IsSpecial)
        {
            _selectedActionName = null;
            _selectedChoiceByStep.Clear();
            Current = null;
            return;
        }

        var firstInvalidStep = int.MaxValue;
        foreach (var (stepIndex, choiceId) in _selectedChoiceByStep.OrderBy(entry => entry.Key))
        {
            var step = action.Steps.FirstOrDefault(candidate => candidate.Index == stepIndex);
            if (step is null
                || !step.Choices.Any(choice => string.Equals(choice.Id, choiceId, StringComparison.Ordinal)))
            {
                firstInvalidStep = Math.Min(firstInvalidStep, stepIndex);
            }
        }

        foreach (var stepIndex in _selectedChoiceByStep.Keys.Where(index => index >= firstInvalidStep).ToArray())
        {
            _selectedChoiceByStep.Remove(stepIndex);
        }

        AutoSelectForcedRequiredChoices(action, 0);
        Current = BuildSelectionState(action);
    }

    private void AutoSelectForcedRequiredChoices(PromptActionModel action, int startIndex)
    {
        foreach (var step in action.Steps.Where(candidate => candidate.Index >= startIndex))
        {
            if (_selectedChoiceByStep.ContainsKey(step.Index))
            {
                continue;
            }

            if (!step.Required || step.Choices.Count != 1)
            {
                break;
            }

            _selectedChoiceByStep[step.Index] = step.Choices[0].Id;
        }
    }

    private void PublishSelection()
    {
        if (CurrentAction() is not { } action)
        {
            Current = null;
            SelectionCleared?.Invoke();
            return;
        }

        Current = BuildSelectionState(action);
        SelectionChanged?.Invoke(Current);
    }

    private PromptSelectionState BuildSelectionState(PromptActionModel action)
    {
        var selected = action.Steps
            .Where(step => _selectedChoiceByStep.TryGetValue(step.Index, out _))
            .Select(step => (Step: step, Choice: step.Choices.First(choice =>
                string.Equals(choice.Id, _selectedChoiceByStep[step.Index], StringComparison.Ordinal))))
            .ToArray();
        var requiredComplete = action.Steps
            .Where(step => step.Required)
            .All(step => step.Choices.Count > 0 && _selectedChoiceByStep.ContainsKey(step.Index));
        var canSubmitCandidate = action.Option.HasTemplate
            || !string.Equals(action.Option.SubmitKind, "unsupported", StringComparison.Ordinal);
        var canSubmit = action.Option.Enabled && canSubmitCandidate && requiredComplete;
        var next = action.Steps.FirstOrDefault(step => !_selectedChoiceByStep.ContainsKey(step.Index));
        var summary = selected.Length > 0
            ? $"{action.Option.Label} · 已选择 {selected.Length} 项"
            : next is not null
                ? $"{action.Option.Label} · 请选择{next.Label}"
                : action.Option.Label;

        return new PromptSelectionState(
            _promptId,
            _snapshotTick,
            action.Option.Name,
            selected.FirstOrDefault(entry => string.Equals(entry.Step.Role, "source", StringComparison.Ordinal)).Choice?.Id,
            selected.Where(entry => string.Equals(entry.Step.Role, "target", StringComparison.Ordinal)).Select(entry => entry.Choice.Id).ToArray(),
            selected.FirstOrDefault(entry => string.Equals(entry.Step.Role, "destination", StringComparison.Ordinal)).Choice?.Id,
            selected.FirstOrDefault(entry => string.Equals(entry.Step.Role, "mode", StringComparison.Ordinal)).Choice?.Id,
            selected.Where(entry => string.Equals(entry.Step.Role, "optionalCost", StringComparison.Ordinal)).Select(entry => entry.Choice.Id).ToArray(),
            canSubmit,
            summary);
    }

    private PromptActionModel? CurrentAction()
    {
        return !string.IsNullOrWhiteSpace(_selectedActionName)
            && _actions.TryGetValue(_selectedActionName, out var action)
                ? action
                : null;
    }

    private PromptStep? CurrentStep()
    {
        return CurrentAction()?.Steps.FirstOrDefault(step => !_selectedChoiceByStep.ContainsKey(step.Index));
    }

    private static PromptActionModel ParseAction(Godot.Collections.Dictionary action)
    {
        var actionName = ReadString(action, "action");
        var label = ReadString(action, "label");
        var enabled = ReadBool(action, "enabled");
        var option = new PromptActionOption(
            actionName,
            FriendlyActionLabel(actionName, label),
            ReadString(action, "reason"),
            enabled,
            ReadBool(action, "hasTemplate"),
            IsSpecialAction(actionName),
            ReadString(action, "submitKind", "unsupported"));
        var steps = new List<PromptStep>();
        var stepIndex = 0;
        foreach (var step in ReadDictionaries(action, "selectionSteps"))
        {
            var role = ReadString(step, "role");
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            var choices = ReadDictionaries(step, "choices")
                .Select(choice => ParseChoice(choice, role, stepIndex))
                .Where(choice => !string.IsNullOrWhiteSpace(choice.Id))
                .GroupBy(choice => choice.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            steps.Add(new PromptStep(
                stepIndex,
                role,
                FriendlyStepLabel(role, ReadString(step, "label")),
                ReadBool(step, "required"),
                choices));
            stepIndex++;
        }

        return new PromptActionModel(option, action.Duplicate(true), steps);
    }

    private static PromptChoice ParseChoice(
        Godot.Collections.Dictionary choice,
        string role,
        int stepIndex)
    {
        var id = ReadString(choice, "id");
        return new PromptChoice(
            role,
            id,
            ReadString(choice, "label", "服务端选项"),
            ReadStrings(choice, "objectIds"),
            stepIndex);
    }

    private static bool IsSpecialAction(string actionName)
    {
        return actionName is "MULLIGAN" or "ORDER_TRIGGERS" or "ASSIGN_COMBAT_DAMAGE";
    }

    private static string FriendlyActionLabel(string actionName, string label)
    {
        if (!string.IsNullOrWhiteSpace(label)
            && !string.Equals(label, actionName, StringComparison.Ordinal)
            && !label.Contains('_', StringComparison.Ordinal))
        {
            return label;
        }

        return actionName switch
        {
            "ACTIVATE_ABILITY" => "激活技能",
            "ASSEMBLE_EQUIPMENT" => "装配装备",
            "CHOOSE_HAND_CARDS" => "选择手牌",
            "DECLARE_BATTLE" => "宣战",
            "END_TURN" => "结束回合",
            "HIDE_CARD" => "布置待命",
            "LEGEND_ACT" => "传奇行动",
            "MOVE_UNIT" => "移动单位",
            "PAY_COST" => "支付费用",
            "PASS" => "让过",
            "PASS_FOCUS" => "让过焦点",
            "PASS_PRIORITY" => "让过优先权",
            "PLAY_CARD" => "打出卡牌",
            "RECYCLE_RUNE" => "回收符文",
            "REVEAL_CARD" => "翻开待命",
            "SURRENDER" => "投降",
            "TAP_RUNE" => "横置符文",
            _ => "服务端行动"
        };
    }

    private static string FriendlyStepLabel(string role, string label)
    {
        if (!string.IsNullOrWhiteSpace(label)
            && !label.Contains('_', StringComparison.Ordinal))
        {
            return label;
        }

        return role switch
        {
            "source" => "来源",
            "target" => "目标",
            "destination" => "位置",
            "mode" => "模式",
            "optionalCost" => "额外费用",
            _ => "选项"
        };
    }

    private static IReadOnlyList<Godot.Collections.Dictionary> ReadDictionaries(
        Godot.Collections.Dictionary source,
        string key)
    {
        return source.TryGetValue(key, out var value)
            ? value.As<Godot.Collections.Array<Godot.Collections.Dictionary>>().ToArray()
            : [];
    }

    private static IReadOnlyList<string> ReadStrings(Godot.Collections.Dictionary source, string key)
    {
        return source.TryGetValue(key, out var value)
            ? value.As<Godot.Collections.Array<string>>()
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    private static string ReadString(
        Godot.Collections.Dictionary source,
        string key,
        string fallback = "")
    {
        return source.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.AsString())
            ? value.AsString()
            : fallback;
    }

    private static bool ReadBool(Godot.Collections.Dictionary source, string key)
    {
        return source.TryGetValue(key, out var value) && value.AsBool();
    }

    private static long ReadLong(Godot.Collections.Dictionary source, string key, long fallback)
    {
        return source.TryGetValue(key, out var value) ? value.AsInt64() : fallback;
    }

    private sealed record PromptStep(
        int Index,
        string Role,
        string Label,
        bool Required,
        IReadOnlyList<PromptChoice> Choices);

    private sealed record PromptActionModel(
        PromptActionOption Option,
        Godot.Collections.Dictionary Source,
        IReadOnlyList<PromptStep> Steps);
}
