using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        if (_selectedActionName is null && (_actions.TryGetValue("CHOOSE_HAND_CARDS", out var handChoice) || _actions.TryGetValue("CHOOSE_CARDS", out handChoice))
            && handChoice.Option.Enabled)
        {
            _selectedActionName = handChoice.Option.Name;
            AutoSelectForcedRequiredChoices(handChoice, 0);
        }
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

    public IReadOnlyList<PromptActionOption> ActionsForObject(string objectId) => _actions.Values
        .Where(action => action.Option.Enabled && !action.Option.IsSpecial
            && action.Steps.Any(step => step.Role == "source" && step.Choices.Any(choice => choice.MatchesObject(objectId))))
        .Select(action => action.Option).ToArray();

    public bool TrySelectSource(string objectId)
    {
        if (CurrentAction() is not { } action) return false;
        var step = action.Steps.FirstOrDefault(item => item.Role == "source" && item.Choices.Any(choice => choice.MatchesObject(objectId)));
        if (step is null) return false;
        var matches = step.Choices.Where(choice => choice.MatchesObject(objectId)).ToArray();
        return matches.Length == 1 && SelectChoice(action, step, matches[0]);
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

        var matches = step.Choices.Where(candidate => candidate.MatchesObject(objectId)).ToArray();
        return matches.Length == 1 && SelectChoice(action, step, matches[0]);
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

        if (action.Option.Name is "CHOOSE_HAND_CARDS" or "CHOOSE_CARDS" && _selectedChoiceByStep.Any(selected =>
                selected.Key < step.Index && selected.Value == choice.Id))
            return false;

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

        if (action.Option.Name == "PAY_COST" && selected.Length > 0)
        {
            summary = "已选择：" + string.Join("、", selected.Select(entry => entry.Choice.Label));
            using var paymentCandidate = JsonDocument.Parse(ReadString(action.Source, "candidateJson", "{}"));
            if (paymentCandidate.RootElement.TryGetProperty("metadata", out var paymentMetadata)
                && paymentMetadata.TryGetProperty("paymentWindow", out var paymentWindow)
                && paymentWindow.GetString() == "TRIGGER_COST_CONFIRMATION")
                summary += selected.Any(entry => entry.Choice.Id == "PAY")
                    ? " · 先支付费用，双方响应后结算" : " · 移除触发技能，不支付费用";
        }

        if (action.Option.Name == "CHOOSE_CARDS")
        {
            using var candidate = JsonDocument.Parse(ReadString(action.Source, "candidateJson", "{}"));
            if (candidate.RootElement.TryGetProperty("metadata", out var metadata)
                && metadata.TryGetProperty("choiceWindow", out var window))
            {
                if (window.GetString() == "INSIGHT_ORDER")
                    summary = selected.Length == 0 ? "从牌库顶开始选择顺序"
                        : "牌库顶 → " + string.Join(" → ", selected.Select((entry, index) => $"{index + 1}. {entry.Choice.Label}"));
                else if (window.GetString() is "TRIGGER_CONFIRMATION" or "SPELL_TRIGGER_CONFIRMATION")
                    summary = selected.Length == 0 ? requiredComplete ? "放弃触发技能" : "请选择技能目标"
                        : "技能目标：" + string.Join("、", selected.Select(entry => entry.Choice.Label));
                else if (window.GetString() == "EFFECT_PLAY")
                    summary = action.Option.Label;
                else if (window.GetString() == "RECYCLE_FOR_EFFECT_PLAY")
                    summary = selected.Length == 0 ? "放弃回收与再次打出"
                        : "支付回收费用：" + string.Join("、", selected.Select(entry => entry.Choice.Label));
                else if (window.GetString() == "TRIGGER_COST_CONFIRMATION")
                {
                    var discard = metadata.TryGetProperty("effectKind", out var effect) && effect.GetString() == "HOLD_LEBLANC_DISCARD";
                    summary = selected.Length == 0 ? discard ? "放弃创建映像 · 不弃牌、不横置传奇" : "放弃触发技能 · 不横置传奇"
                        : discard ? "弃置：" + string.Join("、", selected.Select(entry => entry.Choice.Label)) + " · 横置传奇，先等待响应；进场后再选复制对象"
                        : "横置：" + string.Join("、", selected.Select(entry => entry.Choice.Label)) + " · 先支付费用，双方响应后结算";
                }
                else if (window.GetString() == "TOKEN_ENTRY_REPLACEMENT")
                    summary = selected.Length == 0 ? "跳过本次替换 · 保留未使用的回合次数"
                        : "应用替换：" + string.Join("、", selected.Select(entry => entry.Choice.Label)) + " · 多打出一个复制体";
                else if (window.GetString() == "INSIGHT")
                    summary = selected.Length == 0 ? "全部保留 · 不回收"
                        : "回收：" + string.Join("、", selected.Select(entry => entry.Choice.Label));
            }
        }

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
        if (string.IsNullOrWhiteSpace(_selectedActionName)
            || !_actions.TryGetValue(_selectedActionName, out var action)) return null;
        if (action.Option.Name != "ACTIVATE_ABILITY"
            || !_selectedChoiceByStep.TryGetValue(0, out var sourceId)) return action;

        // The server supplies source-specific costs and targets. The candidate's
        // union is only useful before a source is selected.
        using var json = JsonDocument.Parse(ReadString(action.Source, "candidateJson"));
        if (!json.RootElement.TryGetProperty("metadata", out var metadata)
            || !metadata.TryGetProperty("sourceRequirements", out var requirements)
            || requirements.ValueKind != JsonValueKind.Array) return action;
        foreach (var requirement in requirements.EnumerateArray())
        {
            if (requirement.GetProperty("sourceObjectId").GetString() != sourceId) continue;
            var steps = new List<PromptStep> { action.Steps.First(s => s.Role == "source") };
            var minimum = requirement.GetProperty("minTargetCount").GetInt32();
            var maximum = requirement.GetProperty("maxTargetCount").GetInt32();
            for (var targetIndex = 0; targetIndex < maximum; targetIndex++)
            {
                var choices = new List<PromptChoice>();
                var index = steps.Count;
                if (requirement.TryGetProperty("targetChoicesByIndex", out var targets)
                    && targets.TryGetProperty(targetIndex.ToString(), out var targetChoices))
                    foreach (var choice in targetChoices.EnumerateArray())
                    {
                        var id = choice.GetProperty("id").GetString()!;
                        choices.Add(new("target", id, choice.GetProperty("label").GetString() ?? id, [id], index));
                    }
                steps.Add(new(index, "target", minimum > 0 ? "目标／费用" : "目标", targetIndex < minimum, choices));
            }
            if (requirement.TryGetProperty("optionalCostChoices", out var costs) && costs.GetArrayLength() > 0)
            {
                var index = steps.Count;
                var choices = costs.EnumerateArray().Select(choice => {
                    var id = choice.GetProperty("id").GetString()!;
                    return new PromptChoice("optionalCost", id, choice.GetProperty("label").GetString() ?? id, [id], index);
                }).ToArray();
                steps.Add(new(index, "optionalCost", "可选费用", false, choices));
            }
            return action with { Steps = steps };
        }
        return action;
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
            "CHOOSE_CARDS" => "选择卡牌",
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
