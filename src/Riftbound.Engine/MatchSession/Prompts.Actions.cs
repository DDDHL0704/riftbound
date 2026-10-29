using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
internal static partial class ActionPromptBuilder
{

    public static IReadOnlyList<string> ActionsWithLegendActIfAvailable(
        MatchState state,
        string playerId,
        string primaryAction)
    {
        var legendSources = SourcesFor(state, playerId, "LEGEND_ACT");
        return legendSources?.Count > 0
            ? ["LEGEND_ACT", primaryAction]
            : [primaryAction];
    }

    public static IReadOnlyList<string> StackPriorityActions(MatchState state, string playerId)
    {
        var actions = new List<string>();
        if (ResourceActionWindow.CanAct(state, playerId))
        {
            if (SourcesFor(state, playerId, CommandTypes.TapRune)?.Count > 0)
                actions.Add(CommandTypes.TapRune);
            if (SourcesFor(state, playerId, CommandTypes.RecycleRune)?.Count > 0)
                actions.Add(CommandTypes.RecycleRune);
        }
        if (SourcesFor(state, playerId, "PLAY_CARD")?.Count > 0)
        {
            actions.Add("PLAY_CARD");
        }

        if (SourcesFor(state, playerId, "REVEAL_CARD")?.Count > 0)
        {
            actions.Add("REVEAL_CARD");
        }

        if (SourcesFor(state, playerId, "LEGEND_ACT")?.Count > 0)
        {
            actions.Add("LEGEND_ACT");
        }

        if (SourcesFor(state, playerId, "ACTIVATE_ABILITY")?.Count > 0)
        {
            actions.Add("ACTIVATE_ABILITY");
        }

        actions.Add("PASS_PRIORITY");
        return actions;
    }

    private static bool HasOpenBattleResponsePriority(MatchState state)
    {
        return state.StackItems.Count == 0
            && string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            && state.BattleState.IsActive
            && !string.IsNullOrWhiteSpace(state.BattleState.BattlefieldObjectId);
    }

    public static IReadOnlyList<string> SpellDuelFocusActions(MatchState state, string playerId)
    {
        var actions = new List<string>();
        if (ResourceActionWindow.CanAct(state, playerId))
        {
            if (SourcesFor(state, playerId, CommandTypes.TapRune)?.Count > 0)
                actions.Add(CommandTypes.TapRune);
            if (SourcesFor(state, playerId, CommandTypes.RecycleRune)?.Count > 0)
                actions.Add(CommandTypes.RecycleRune);
        }
        if (SourcesFor(state, playerId, "PLAY_CARD")?.Count > 0)
        {
            actions.Add("PLAY_CARD");
        }

        if (SourcesFor(state, playerId, CommandTypes.RevealCard)?.Count > 0)
            actions.Add(CommandTypes.RevealCard);

        if (SourcesFor(state, playerId, "ACTIVATE_ABILITY")?.Count > 0)
        {
            actions.Add("ACTIVATE_ABILITY");
        }

        var legendSources = SourcesFor(state, playerId, "LEGEND_ACT");
        if (legendSources?.Count > 0)
        {
            actions.Add("LEGEND_ACT");
        }

        actions.Add("PASS_FOCUS");
        return actions;
    }

    public static bool CanDeclareBattleForActiveTask(MatchState state, string playerId)
    {
        return DeclareBattleSourceRequirements(state, playerId).Count > 0;
    }

    public static ActionPromptDto Build(
        MatchState state,
        string playerId,
        bool actionable,
        string reason,
        IReadOnlyList<string> actions)
    {
        if (state.UntilEndOfTurnEffects.Contains(CardPermissionKeywordRules.SpellPlayProhibitionPrefix + playerId, StringComparer.Ordinal))
            reason += " · " + CardPermissionKeywordRules.SpellPlayProhibitionReason;
        var normalizedActions = actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())
            .ToArray();
        var promptId = $"{state.RoomId}:{state.Tick}:{playerId}:{string.Join(",", normalizedActions)}";
        var candidates = normalizedActions
            .Select(action => BuildCandidate(state, playerId, action, actionable, reason))
            .ToArray();
        var view = BuildView(state, playerId, actionable, reason, normalizedActions);
        var contract = ContractForPromptView(view);
        var objectContexts = ObjectContextsFor(candidates);
        var serverFlow = ServerFlowFor(state, playerId, actionable, reason, normalizedActions, candidates, view, objectContexts);

        return new ActionPromptDto(
            playerId,
            actionable,
            reason,
            normalizedActions,
            promptId,
            state.Tick,
            candidates,
            view,
            contract,
            objectContexts,
            PromptInspectionFor(actionable, normalizedActions, candidates, view, contract, objectContexts),
            serverFlow);
    }

    private static ActionPromptServerFlowDto ServerFlowFor(
        MatchState state,
        string playerId,
        bool actionable,
        string reason,
        IReadOnlyList<string> actions,
        IReadOnlyList<ActionPromptCandidateDto> candidates,
        PromptViewDto view,
        IReadOnlyList<ActionPromptObjectContextDto>? objectContexts)
    {
        var responsibility = view.Responsibility ?? PromptResponsibilityFor(state, playerId, view.Type, actionable, reason);
        var relatedObjects = ServerFlowRelatedObjectsWithCandidateSummary(
            PromptResponsibilityRelatedObjects(state, playerId, responsibility.PromptType),
            objectContexts);
        var relatedObjectIds = relatedObjects
            .Select(item => item.ObjectId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var flowState = ServerFlowStateFor(state, actionable, responsibility);
        var nextStep = ServerFlowNextStepFor(flowState, responsibility);
        var stateLabel = ServerFlowStateLabel(flowState);
        var enabledCandidateCount = candidates.Count(candidate => candidate.Enabled);
        var disabledCandidateCount = Math.Max(0, candidates.Count - enabledCandidateCount);
        return new ActionPromptServerFlowDto(
            flowState,
            stateLabel,
            ServerFlowTone(flowState),
            ServerFlowPrimaryLabel(flowState, view, responsibility),
            $"{view.Title} / {stateLabel} / {nextStep}",
            nextStep,
            ServerFlowReason(flowState, state, responsibility, reason),
            responsibility.PromptType,
            playerId,
            responsibility.ResponsiblePlayerId,
            responsibility.IsResponsiblePlayer,
            responsibility.ActionableForPromptPlayer,
            candidates.Count,
            enabledCandidateCount,
            disabledCandidateCount,
            responsibility.QueueCounts,
            ServerFlowLanes(state),
            ServerFlowSteps(state, actions, candidates, view, responsibility),
            relatedObjectIds,
            relatedObjects,
            view.RelatedStackItemId,
            view.RelatedBattlefieldId,
            view.RelatedSpellDuelId);
    }

    private static IReadOnlyList<ActionPromptServerFlowObjectRefDto> ServerFlowRelatedObjectsWithCandidateSummary(
        IReadOnlyList<ActionPromptServerFlowObjectRefDto> relatedObjects,
        IReadOnlyList<ActionPromptObjectContextDto>? objectContexts)
    {
        if (objectContexts is null || objectContexts.Count == 0)
        {
            return relatedObjects;
        }

        var contextsByObjectId = objectContexts
            .Where(context => !string.IsNullOrWhiteSpace(context.ObjectId))
            .GroupBy(context => context.ObjectId.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return relatedObjects
            .Select(relatedObject =>
            {
                if (!contextsByObjectId.TryGetValue(relatedObject.ObjectId, out var context))
                {
                    return relatedObject;
                }

                var candidateRoles = ActionPromptObjectContextCandidateRoles(context);
                var candidateActions = ActionPromptObjectContextCandidateActions(context);

                return relatedObject with
                {
                    CandidateRoles = candidateRoles.Length == 0 ? null : candidateRoles,
                    EnabledCandidateCount = context.EnabledCandidateCount,
                    DisabledCandidateCount = context.DisabledCandidateCount,
                    CandidateSource = string.IsNullOrWhiteSpace(context.Source) ? null : context.Source,
                    CandidateBoundary = string.IsNullOrWhiteSpace(context.Boundary) ? null : context.Boundary,
                    CandidateSteps = ActionPromptObjectContextCandidateSteps(context),
                    CandidateActions = candidateActions.Length == 0 ? null : candidateActions
                };
            })
            .Concat(objectContexts
                .Where(context => !string.IsNullOrWhiteSpace(context.ObjectId))
                .Select(context =>
                {
                    var objectId = context.ObjectId.Trim();
                    var candidateRoles = ActionPromptObjectContextCandidateRoles(context);
                    var candidateActions = ActionPromptObjectContextCandidateActions(context);
                    return new ActionPromptServerFlowObjectRefDto(
                        objectId,
                        ServerFlowCandidateRelatedObjectRole(candidateRoles),
                        candidateRoles.Length == 0 ? null : candidateRoles,
                        context.EnabledCandidateCount,
                        context.DisabledCandidateCount,
                        string.IsNullOrWhiteSpace(context.Source) ? null : context.Source,
                        string.IsNullOrWhiteSpace(context.Boundary) ? null : context.Boundary,
                        ActionPromptObjectContextCandidateSteps(context),
                        candidateActions.Length == 0 ? null : candidateActions);
                }))
            .GroupBy(item => $"{item.Role}\u001f{item.ObjectId}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static string[] ActionPromptObjectContextCandidateActions(ActionPromptObjectContextDto context)
    {
        return context.Candidates
            .Select(candidate => candidate.Action)
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(action => action, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ActionPromptObjectContextCandidateRoles(ActionPromptObjectContextDto context)
    {
        return context.Candidates
            .SelectMany(candidate => candidate.Roles)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptObjectCandidateStepDto>? ActionPromptObjectContextCandidateSteps(
        ActionPromptObjectContextDto context)
    {
        var steps = context.Candidates
            .SelectMany(candidate => candidate.SelectionSteps ?? [])
            .Where(step => !string.IsNullOrWhiteSpace(step.Role))
            .GroupBy(step => step.Role.Trim(), StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.OrderBy(step => step.Index).ToArray();
                var first = ordered[0];
                return new ActionPromptObjectCandidateStepDto(
                    first.Role,
                    first.Label,
                    ordered.Any(step => step.Required),
                    ordered.Max(step => step.ChoiceCount),
                    ordered.Max(step => step.ObjectChoiceCount),
                    ordered.Min(step => step.Index));
            })
            .OrderBy(step => step.Index)
            .ThenBy(step => step.Role, StringComparer.Ordinal)
            .ToArray();

        return steps.Length == 0 ? null : steps;
    }

    private static string ServerFlowCandidateRelatedObjectRole(IReadOnlyList<string> candidateRoles)
    {
        return candidateRoles.Count == 0
            ? "候选对象"
            : $"候选{string.Join("/", candidateRoles)}";
    }

    private static string ServerFlowStateFor(
        MatchState state,
        bool actionable,
        PromptResponsibilityDto responsibility)
    {
        if (string.Equals(responsibility.State, "MATCH_COMPLETE", StringComparison.Ordinal)
            || string.Equals(state.Status, MatchStatuses.Finished, StringComparison.Ordinal))
        {
            return "history";
        }

        if (ResolutionResult.HasBlockingPendingTaskQueue(state))
        {
            return "blocked";
        }

        if (state.StackItems.Count > 0 || HasOpenBattleResponsePriority(state))
        {
            return "respond";
        }

        if (actionable)
        {
            return "ready";
        }

        return "waiting";
    }

    private static string ServerFlowStateLabel(string flowState)
    {
        return flowState switch
        {
            "blocked" => "阻塞",
            "history" => "回看",
            "ready" => "可提交",
            "respond" => "响应",
            _ => "等待"
        };
    }

    private static string ServerFlowTone(string flowState)
    {
        return flowState switch
        {
            "blocked" => "warn",
            "history" => "info",
            "ready" => "good",
            "respond" => "info",
            _ => "neutral"
        };
    }

    private static string ServerFlowPrimaryLabel(
        string flowState,
        PromptViewDto view,
        PromptResponsibilityDto responsibility)
    {
        return flowState switch
        {
            "blocked" => "规则任务阻塞",
            "history" => "规则事件回看",
            "ready" => "提交给服务端",
            "respond" => "响应结算链",
            _ when !string.IsNullOrWhiteSpace(responsibility.ResponsiblePlayerId) =>
                $"等待 {responsibility.ResponsiblePlayerId}",
            _ => view.Title
        };
    }

    private static string ServerFlowNextStepFor(
        string flowState,
        PromptResponsibilityDto responsibility)
    {
        return flowState switch
        {
            "blocked" => "等待服务端推进规则队列。",
            "history" => "选择近期规则事件查看详情。",
            "ready" => responsibility.NextStep,
            "respond" => "按服务端 prompt 选择响应或让过。",
            _ => responsibility.NextStep
        };
    }

    private static string ServerFlowReason(
        string flowState,
        MatchState state,
        PromptResponsibilityDto responsibility,
        string reason)
    {
        return flowState switch
        {
            "blocked" => ResolutionResult.BlockingPendingTaskQueueReason(state),
            "history" => "近期战场、战斗或规则事件可用于核对桌面状态。",
            "respond" => "结算链项目存在时，合法响应和提交字段由服务端候选决定。",
            _ => string.IsNullOrWhiteSpace(responsibility.NextStep)
                ? string.IsNullOrWhiteSpace(reason) ? "等待服务端确认下一步。" : reason
                : responsibility.NextStep
        };
    }

    private static IReadOnlyList<ActionPromptServerFlowLaneDto> ServerFlowLanes(MatchState state)
    {
        var taskCount = state.PendingTaskQueue.Tasks.Count + state.BattlefieldTasks.Count;
        var resolutionCount = state.BattlefieldResolutions.Count + state.BattleResolutions.Count;
        return
        [
            new("stack", "结算链", state.StackItems.Count, state.StackItems.Count > 0 ? "active" : "empty", ServerFlowStackHeadline(state)),
            new("task", "任务", taskCount, taskCount > 0 ? ResolutionResult.HasBlockingPendingTaskQueue(state) ? "blocked" : "active" : "empty", ServerFlowTaskHeadline(state)),
            new("trigger", "触发", state.TriggerQueue.Count, state.TriggerQueue.Count > 0 ? "active" : "empty", ServerFlowTriggerHeadline(state)),
            new("resolution", "结算", resolutionCount, resolutionCount > 0 ? "history" : "empty", ServerFlowResolutionHeadline(state))
        ];
    }

    private static string ServerFlowStackHeadline(MatchState state)
    {
        if (state.StackItems.Count == 0)
        {
            return "无项目";
        }

        var item = state.StackItems[^1];
        var parts = new[]
        {
            item.EffectKind,
            item.CardNo,
            string.IsNullOrWhiteSpace(item.ControllerId) ? string.Empty : $"控制 {item.ControllerId}"
        };
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string ServerFlowTaskHeadline(MatchState state)
    {
        var task = state.PendingTaskQueue.Tasks.FirstOrDefault(task =>
                string.Equals(task.TaskId, state.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal))
            ?? state.PendingTaskQueue.Tasks.FirstOrDefault();
        if (task is not null)
        {
            return state.PendingTaskQueue.IsBlocking ? "等待服务端清理任务" : "服务端规则任务";
        }

        var battlefieldTask = state.BattlefieldTasks.FirstOrDefault();
        return battlefieldTask is null
            ? "无任务"
            : "战场规则任务";
    }

    private static string ServerFlowTriggerHeadline(MatchState state)
    {
        return state.TriggerQueue.Count == 0 ? "无触发" : "触发等待结算";
    }

    private static string ServerFlowResolutionHeadline(MatchState state)
    {
        if (state.BattlefieldResolutions.Count > 0)
        {
            return "战场结算记录";
        }

        return state.BattleResolutions.Count > 0 ? "战斗结算记录" : "无结算记录";
    }

    private static IReadOnlyList<ActionPromptServerFlowStepDto> ServerFlowSteps(
        MatchState state,
        IReadOnlyList<string> actions,
        IReadOnlyList<ActionPromptCandidateDto> candidates,
        PromptViewDto view,
        PromptResponsibilityDto responsibility)
    {
        var steps = new List<ActionPromptServerFlowStepDto>
        {
            new(
                "prompt",
                "行动窗口",
                responsibility.ActionableForPromptPlayer ? "ready" : "waiting",
                responsibility.ActionableForPromptPlayer ? "可行动" : "等待",
                view.Title,
                view.Message,
                "window"),
            new(
                "responsibility",
                "责任方",
                responsibility.IsResponsiblePlayer ? "ready" : "waiting",
                responsibility.State,
                string.IsNullOrWhiteSpace(responsibility.ResponsiblePlayerId) ? "服务端" : responsibility.ResponsiblePlayerId,
                responsibility.NextStep,
                "responsibility")
        };

        if (state.StackItems.Count > 0)
        {
            steps.Add(new(
                "stack",
                "结算链",
                "respond",
                "响应",
                $"{state.StackItems.Count} 项",
                ServerFlowStackHeadline(state),
                "stack"));
        }

        if (state.PendingTaskQueue.Tasks.Count > 0 || state.BattlefieldTasks.Count > 0)
        {
            steps.Add(new(
                "task",
                "规则任务",
                ResolutionResult.HasBlockingPendingTaskQueue(state) ? "blocked" : "server",
                ResolutionResult.HasBlockingPendingTaskQueue(state) ? "阻塞" : "服务端",
                $"{state.PendingTaskQueue.Tasks.Count + state.BattlefieldTasks.Count} 项",
                ServerFlowTaskHeadline(state),
                "task"));
        }

        if (state.TriggerQueue.Count > 0)
        {
            steps.Add(new(
                "trigger",
                "触发队列",
                "server",
                "服务端",
                $"{state.TriggerQueue.Count} 项",
                ServerFlowTriggerHeadline(state),
                "trigger"));
        }

        steps.Add(new(
            "candidate",
            "候选",
            candidates.Any(candidate => candidate.Enabled) ? "ready" : "waiting",
            $"{candidates.Count(candidate => candidate.Enabled)} 可提交",
            actions.Count == 0 ? "无行动" : string.Join(" / ", actions),
            "前端只提交服务端候选，不重算合法性。",
            "candidate"));

        return steps.ToArray();
    }

    private static ActionPromptInspectionDto PromptInspectionFor(
        bool actionable,
        IReadOnlyList<string> actions,
        IReadOnlyList<ActionPromptCandidateDto> candidates,
        PromptViewDto view,
        ActionPromptContractDto? contract,
        IReadOnlyList<ActionPromptObjectContextDto>? objectContexts)
    {
        var enabledCount = candidates.Count(candidate => candidate.Enabled);
        var disabledCount = candidates.Count(candidate => !candidate.Enabled);
        var objectContextCount = objectContexts?.Count ?? 0;
        return new ActionPromptInspectionDto(
            ActionPromptContextSources.ServerActionPrompt,
            "服务端只公开当前行动窗口的类型、候选、命令契约和对象索引摘要；隐藏 metadata、隐藏区内容和未公开卡牌身份不进入提示检查。",
            [
                new("kind", "提示类型", view.Type),
                new("state", "窗口", actionable ? "可行动" : "等待", actionable ? "good" : "neutral"),
                new("candidate", "候选", $"{enabledCount} 可提交 / {disabledCount} 阻断"),
                new("objects", "对象索引", $"{objectContextCount} 个对象")
            ],
            [
                new(
                    "candidate",
                    "服务端候选",
                    candidates
                        .Select((candidate, index) => new ActionPromptInspectionRowDto(
                            $"candidate-{index}",
                            candidate.Enabled ? "可提交" : "阻断",
                            PromptCandidateInspectionValue(candidate),
                            candidate.Enabled ? "good" : "warn"))
                        .ToArray(),
                    "当前窗口没有公开候选。"),
                new(
                    "contract",
                    "提交契约",
                    PromptContractInspectionRows(contract, actions),
                    "当前窗口没有专用提交契约。"),
                new(
                    "object-index",
                    "对象索引",
                    [
                        new("count", "索引", $"{objectContextCount} 个对象", objectContextCount > 0 ? "info" : "neutral"),
                        new("source", "来源", "服务端选择步骤与命令模板", "info")
                    ]),
                new(
                    "safe-boundary",
                    "信息边界",
                    [
                        new("candidate-source", "合法性", "仅以服务端候选和提交校验为准", "neutral"),
                        new("hidden-metadata", "隐藏字段", "只公开计数或安全标签，不展开隐藏 metadata", "neutral"),
                        new("frontend", "前端职责", "展示与提交，不重算规则", "neutral")
                    ])
            ]);
    }

    private static string PromptCandidateInspectionValue(ActionPromptCandidateDto candidate)
    {
        var commandType = candidate.CommandTemplate?.CmdType;
        var sourceCount = candidate.Sources?.Count ?? 0;
        var targetCount = candidate.Targets?.Count ?? 0;
        var destinationCount = candidate.Destinations?.Count ?? 0;
        var modeCount = candidate.Modes?.Count ?? 0;
        var costCount = candidate.OptionalCosts?.Count ?? 0;
        var stepCount = candidate.SelectionSteps?.Count ?? 0;
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(commandType) ? candidate.Action : commandType,
            $"来源 {sourceCount}",
            targetCount > 0 ? $"目标 {targetCount}" : string.Empty,
            destinationCount > 0 ? $"位置 {destinationCount}" : string.Empty,
            modeCount > 0 ? $"模式 {modeCount}" : string.Empty,
            costCount > 0 ? $"费用 {costCount}" : string.Empty,
            stepCount > 0 ? $"步骤 {stepCount}" : string.Empty,
            ComposerInspectionLabel(candidate.Composer),
            candidate.Enabled ? string.Empty : candidate.Reason
        };
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static IReadOnlyList<ActionPromptInspectionRowDto> PromptContractInspectionRows(
        ActionPromptContractDto? contract,
        IReadOnlyList<string> actions)
    {
        if (contract is null)
        {
            return
            [
                new("actions", "行动", actions.Count == 0 ? "无行动" : string.Join(" / ", actions), "neutral"),
                new("contract", "契约", "无专用契约，仍由服务端命令校验裁定", "neutral")
            ];
        }

        return
        [
            new("candidate-action", "命令", contract.CandidateAction, "info"),
            new("required", "必填", $"{contract.RequiredPayload.Count} 项", contract.RequiredPayload.Count > 0 ? "warn" : "neutral"),
            new("legal", "合法选择", $"{contract.LegalChoices.Count} 组", "info"),
            new("visible", "公开 metadata", $"{contract.VisibleMetadata.Count} 项", "info"),
            new("hidden", "隐藏 metadata", $"{contract.HiddenMetadata.Count} 项", contract.HiddenMetadata.Count > 0 ? "warn" : "neutral")
        ];
    }

    private static IReadOnlyList<ActionPromptObjectContextDto>? ObjectContextsFor(IReadOnlyList<ActionPromptCandidateDto> candidates)
    {
        var rowsByObjectId = new Dictionary<string, List<ActionPromptObjectCandidateDto>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            foreach (var (objectId, roles) in CandidateObjectRoles(candidate))
            {
                if (!rowsByObjectId.TryGetValue(objectId, out var rows))
                {
                    rows = [];
                    rowsByObjectId[objectId] = rows;
                }

                rows.Add(new ActionPromptObjectCandidateDto(
                    candidate.Action,
                    candidate.Label,
                    candidate.Enabled,
                    candidate.Reason,
                    roles,
                    candidate.CommandTemplate?.CmdType ?? candidate.Action,
                    CommandFieldLabels(candidate.CommandTemplate, requiredOnly: true),
                    CommandFieldLabels(candidate.CommandTemplate, requiredOnly: false),
                    candidate.Composer,
                    ObjectCandidateSelectionSteps(candidate, objectId),
                    candidate.Presentation));
            }
        }

        if (rowsByObjectId.Count == 0)
        {
            return null;
        }

        return rowsByObjectId
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry =>
            {
                var rows = entry.Value
                    .OrderByDescending(candidate => candidate.Enabled)
                    .ThenBy(candidate => candidate.Presentation?.Priority ?? 700)
                    .ThenBy(candidate => candidate.Action, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Label, StringComparer.Ordinal)
                    .ToArray();
                return new ActionPromptObjectContextDto(
                    entry.Key,
                    rows.Count(candidate => candidate.Enabled),
                    rows.Count(candidate => !candidate.Enabled),
                    rows,
                    ObjectInspectionFor(entry.Key, rows));
            })
            .ToArray();
    }

    private static ActionPromptObjectInspectionDto ObjectInspectionFor(
        string objectId,
        IReadOnlyList<ActionPromptObjectCandidateDto> candidates)
    {
        var enabledCount = candidates.Count(candidate => candidate.Enabled);
        var disabledCount = candidates.Count(candidate => !candidate.Enabled);
        var requiredFields = candidates
            .SelectMany(candidate => candidate.RequiredCommandFields ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        var commandFields = candidates
            .SelectMany(candidate => candidate.CommandFields ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        var commandTypes = candidates
            .Select(candidate => string.IsNullOrWhiteSpace(candidate.CommandType) ? candidate.Action : candidate.CommandType)
            .Where(commandType => !string.IsNullOrWhiteSpace(commandType))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(commandType => commandType, StringComparer.Ordinal)
            .ToArray();

        return new ActionPromptObjectInspectionDto(
            ActionPromptContextSources.ServerActionPrompt,
            ActionPromptContextSources.ObjectContextBoundary,
            [
                new("object", "对象", objectId),
                new("candidate", "候选", $"{enabledCount} 可提交 / {disabledCount} 阻断"),
                new("commands", "命令", commandTypes.Length == 0 ? "无命令" : string.Join(" / ", commandTypes)),
                new("fields", "字段", $"{requiredFields.Length} 必填 / {commandFields.Length} 公开")
            ],
            [
                new(
                    "candidate",
                    "服务端候选",
                    candidates
                        .Select((candidate, index) => new ActionPromptObjectInspectionRowDto(
                            $"candidate-{index}",
                            candidate.Enabled ? "可提交" : "阻断",
                            ObjectCandidateInspectionValue(candidate),
                            candidate.Enabled ? "good" : "warn"))
                        .ToArray(),
                    "当前行动提示没有公开候选。"),
                new(
                    "selection-steps",
                    "选择步骤",
                    ObjectSelectionStepInspectionRows(candidates),
                    "当前候选没有公开选择步骤。"),
                new(
                    "command-fields",
                    "命令字段",
                    ObjectCommandFieldInspectionRows(requiredFields, commandFields),
                    "当前候选没有公开命令字段。"),
                new(
                    "safe-boundary",
                    "信息边界",
                    [
                        new("source", "来源", "服务端行动提示", "info"),
                        new("hidden-metadata", "隐藏字段", "不公开具体 metadata key/value", "neutral"),
                        new("rules", "规则判断", "由服务端候选与后续校验裁定，前端不重算", "neutral")
                    ])
            ]);
    }

    private static string ObjectCandidateInspectionValue(ActionPromptObjectCandidateDto candidate)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(candidate.CommandType) ? candidate.Action : candidate.CommandType,
            candidate.Roles.Count > 0 ? string.Join("/", candidate.Roles) : string.Empty,
            candidate.SelectionSteps?.Count > 0 ? $"步骤 {ObjectSelectionStepSummary(candidate.SelectionSteps)}" : string.Empty,
            candidate.RequiredCommandFields?.Count > 0 ? $"需 {string.Join("/", candidate.RequiredCommandFields)}" : string.Empty,
            ComposerInspectionLabel(candidate.Composer),
            candidate.Enabled ? string.Empty : candidate.Reason
        };
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string ComposerInspectionLabel(ActionPromptComposerDto? composer)
    {
        if (composer is null)
        {
            return "组合 未公开";
        }

        return composer.Supported ? "组合 服务端声明" : "组合 服务端阻断";
    }

    private static IReadOnlyList<ActionPromptObjectCandidateStepDto>? ObjectCandidateSelectionSteps(
        ActionPromptCandidateDto candidate,
        string objectId)
    {
        var steps = (candidate.SelectionSteps ?? [])
            .Select((step, index) => ObjectCandidateSelectionStep(step, objectId, index))
            .Where(step => step.Required || step.ChoiceCount > 0 || step.ObjectChoiceCount > 0)
            .ToArray();

        return steps.Length == 0 ? null : steps;
    }

    private static ActionPromptObjectCandidateStepDto ObjectCandidateSelectionStep(
        ActionPromptSelectionStepDto step,
        string objectId,
        int index)
    {
        var normalizedObjectId = objectId.Trim();
        var choiceCount = step.Choices
            .Select(choice => choice.Id)
            .Where(choiceId => !string.IsNullOrWhiteSpace(choiceId))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var objectChoiceCount = step.Choices
            .Count(choice => choice.ObjectIds.Any(candidateObjectId =>
                string.Equals(candidateObjectId, normalizedObjectId, StringComparison.Ordinal)));

        return new ActionPromptObjectCandidateStepDto(
            step.Role,
            step.Label,
            step.Required,
            choiceCount,
            objectChoiceCount,
            index);
    }

    private static IReadOnlyList<ActionPromptObjectInspectionRowDto> ObjectSelectionStepInspectionRows(
        IReadOnlyList<ActionPromptObjectCandidateDto> candidates)
    {
        return candidates
            .SelectMany((candidate, candidateIndex) => (candidate.SelectionSteps ?? [])
                .Select((step, stepIndex) => new ActionPromptObjectInspectionRowDto(
                    $"step-{candidateIndex}-{stepIndex}",
                    step.Required ? "必选" : "可选",
                    $"{candidate.Label} / {step.Label} / {step.ObjectChoiceCount}/{step.ChoiceCount}",
                    step.Required ? "warn" : "info")))
            .ToArray();
    }

    private static string ObjectSelectionStepSummary(IReadOnlyList<ActionPromptObjectCandidateStepDto> steps)
    {
        return string.Join(
            " / ",
            steps
                .OrderBy(step => step.Index)
                .ThenBy(step => step.Role, StringComparer.Ordinal)
                .Select(step => $"{step.Label}{(step.Required ? "*" : string.Empty)} {step.ObjectChoiceCount}/{step.ChoiceCount}"));
    }

    private static IReadOnlyList<ActionPromptObjectInspectionRowDto> ObjectCommandFieldInspectionRows(
        IReadOnlyList<string> requiredFields,
        IReadOnlyList<string> commandFields)
    {
        var rows = new List<ActionPromptObjectInspectionRowDto>();
        rows.AddRange(requiredFields.Select((field, index) => new ActionPromptObjectInspectionRowDto(
            $"required-{index}",
            "必填",
            field,
            "warn")));
        rows.AddRange(commandFields
            .Where(field => !requiredFields.Contains(field, StringComparer.Ordinal))
            .Select((field, index) => new ActionPromptObjectInspectionRowDto(
                $"optional-{index}",
                "公开",
                field,
                "neutral")));
        return rows;
    }

    private static IEnumerable<(string ObjectId, IReadOnlyList<string> Roles)> CandidateObjectRoles(ActionPromptCandidateDto candidate)
    {
        var rolesByObjectId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var step in candidate.SelectionSteps ?? [])
        {
            var role = string.IsNullOrWhiteSpace(step.Label) ? step.Role : step.Label;
            foreach (var choice in step.Choices)
            {
                foreach (var objectId in choice.ObjectIds)
                {
                    if (string.IsNullOrWhiteSpace(objectId))
                    {
                        continue;
                    }

                    if (!rolesByObjectId.TryGetValue(objectId, out var roles))
                    {
                        roles = new HashSet<string>(StringComparer.Ordinal);
                        rolesByObjectId[objectId] = roles;
                    }

                    roles.Add(role.Trim());
                }
            }
        }

        return rolesByObjectId
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => (entry.Key, (IReadOnlyList<string>)entry.Value.OrderBy(role => role, StringComparer.Ordinal).ToArray()));
    }

    private static IReadOnlyList<string>? CommandFieldLabels(
        ActionPromptCommandTemplateDto? template,
        bool requiredOnly)
    {
        var labels = (template?.Bindings ?? [])
            .Where(binding => !requiredOnly || binding.Required)
            .Select(CommandBindingLabel)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return labels.Length == 0 ? null : labels;
    }

    private static string CommandBindingLabel(ActionPromptCommandBindingDto binding)
    {
        if (string.Equals(binding.Source, "requirementMetadata", StringComparison.Ordinal))
        {
            return $"服务端字段{(binding.Required ? "*" : "")}";
        }

        var label = string.IsNullOrWhiteSpace(binding.Label) ? binding.Source : binding.Label.Trim();
        return $"{label}:{binding.Field}{(binding.Required ? "*" : "")}";
    }

    private static ActionPromptContractDto? ContractForPromptView(PromptViewDto view)
    {
        return ActionPromptContracts.ByPromptKind.TryGetValue(view.Type, out var contract)
            ? contract
            : null;
    }

    private static PromptViewDto BuildView(
        MatchState state,
        string playerId,
        bool actionable,
        string reason,
        IReadOnlyList<string> actions)
    {
        var type = PromptTypeFor(state, playerId, actionable, actions);
        return new PromptViewDto(
            type,
            PromptTitleFor(type),
            PromptMessageFor(type, state, playerId, reason),
            RelatedBattlefieldIdFor(state, type),
            RelatedStackItemIdFor(state, type),
            RelatedBattleIdFor(state, type),
            RelatedSpellDuelIdFor(state, type),
            type == PromptTypes.Mulligan && actions.Contains("MULLIGAN", StringComparer.Ordinal) ? 0 : null,
            type == PromptTypes.Mulligan && actions.Contains("MULLIGAN", StringComparer.Ordinal)
                ? OfficialDeckValidator.MaximumMulliganCount
                : null,
            PromptViewMetadata(state, playerId, type),
            PromptResponsibilityFor(state, playerId, type, actionable, reason));
    }

    private static string PromptTypeFor(
        MatchState state,
        string playerId,
        bool actionable,
        IReadOnlyList<string> actions)
    {
        if (string.Equals(state.Status, MatchStatuses.Finished, StringComparison.Ordinal))
        {
            return PromptTypes.MatchResult;
        }

        if (!string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal))
        {
            return PromptTypes.RoomSetup;
        }

        if (string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal))
        {
            return PromptTypes.Mulligan;
        }

        if (state.PendingPayment is not null)
        {
            return PromptTypes.PayCost;
        }

        if (state.PendingHandChoice is not null)
        {
            return PromptTypes.HandChoice;
        }

        if (state.PendingCardChoice is not null)
        {
            return PromptTypes.CardChoice;
        }

        if ((state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId))
            || HasOpenBattleResponsePriority(state))
        {
            return PromptTypes.StackPriority;
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId))
        {
            return PromptTypes.SpellDuelFocus;
        }

        if (ResolutionResult.HasOpenOrderTriggersWindow(state))
        {
            return PromptTypes.OrderTriggers;
        }

        if (ResolutionResult.HasOpenBattleDamageAssignmentWindow(state))
        {
            return PromptTypes.AssignCombatDamage;
        }

        if (ResolutionResult.ActiveStartBattleTask(state) is not null)
        {
            return PromptTypes.BattleDeclaration;
        }

        if (ResolutionResult.HasBlockingPendingTaskQueue(state))
        {
            return PromptTypes.TaskQueue;
        }

        return actionable && actions.Any(action => !string.Equals(action, "WAIT", StringComparison.Ordinal))
            ? PromptTypes.MainAction
            : PromptTypes.Wait;
    }

    private static string PromptTitleFor(string type)
    {
        return type switch
        {
            PromptTypes.RoomSetup => "房间准备",
            PromptTypes.Mulligan => "起手调度",
            PromptTypes.MainAction => "主行动",
            PromptTypes.PayCost => "支付费用",
            PromptTypes.HandChoice => "选择手牌",
            PromptTypes.CardChoice => "选择卡牌",
            PromptTypes.StackPriority => "优先行动",
            PromptTypes.SpellDuelFocus => "法术对决",
            PromptTypes.BattleDeclaration => "声明战斗",
            PromptTypes.AssignCombatDamage => "分配战斗伤害",
            PromptTypes.OrderTriggers => "触发排序",
            PromptTypes.TaskQueue => "任务队列",
            PromptTypes.MatchResult => "对局结束",
            _ => "等待"
        };
    }

    private static string PromptMessageFor(
        string type,
        MatchState state,
        string playerId,
        string reason)
    {
        if (type == PromptTypes.HandChoice && state.PendingHandChoice is { } choice)
            return choice.PlayerId == playerId
                ? $"请选择 {choice.RequiredCount} 张手牌弃置，随后抽 {choice.DrawCount} 张牌。"
                : "等待对手选择要弃置的手牌。";
        if (string.Equals(type, PromptTypes.MatchResult, StringComparison.Ordinal))
        {
            if (string.Equals(state.WinnerPlayerId, playerId, StringComparison.Ordinal))
            {
                return "对局结束，你已获胜。";
            }

            return string.IsNullOrWhiteSpace(state.WinnerPlayerId)
                ? "对局已结束。"
                : "对局结束，对手已获胜。";
        }

        return string.IsNullOrWhiteSpace(reason)
            ? "等待服务端确认下一步。"
            : reason;
    }

    private static PromptResponsibilityDto PromptResponsibilityFor(
        MatchState state,
        string playerId,
        string type,
        bool actionable,
        string reason)
    {
        var responsibilityType = PromptResponsibilityTypeFor(state, type);
        var responsiblePlayerId = ResponsiblePlayerIdFor(state, type, actionable, playerId);
        var isResponsiblePlayer = !string.IsNullOrWhiteSpace(responsiblePlayerId)
            && string.Equals(responsiblePlayerId, playerId, StringComparison.Ordinal);
        var responsibilityState = PromptResponsibilityStateFor(state, type, actionable, responsiblePlayerId, isResponsiblePlayer);
        return new PromptResponsibilityDto(
            responsibilityType,
            playerId,
            string.IsNullOrWhiteSpace(responsiblePlayerId) ? null : responsiblePlayerId,
            isResponsiblePlayer,
            actionable,
            responsibilityState,
            PromptResponsibilityNextStep(responsibilityType, reason, actionable, responsiblePlayerId, responsibilityState),
            PromptResponsibilityQueueCounts(state),
            PromptResponsibilityRelatedObjectIds(state, playerId, type));
    }

    private static string PromptResponsibilityTypeFor(MatchState state, string fallbackType)
    {
        if (!string.Equals(fallbackType, PromptTypes.Wait, StringComparison.Ordinal))
        {
            return fallbackType;
        }

        if (string.Equals(state.Status, MatchStatuses.Finished, StringComparison.Ordinal))
        {
            return PromptTypes.MatchResult;
        }

        if (!string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal))
        {
            return PromptTypes.RoomSetup;
        }

        if (string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal))
        {
            return PromptTypes.Mulligan;
        }

        if (state.PendingPayment is not null)
        {
            return PromptTypes.PayCost;
        }

        if (state.PendingHandChoice is not null)
        {
            return PromptTypes.HandChoice;
        }

        if (state.PendingCardChoice is not null)
        {
            return PromptTypes.CardChoice;
        }

        if ((state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId))
            || HasOpenBattleResponsePriority(state))
        {
            return PromptTypes.StackPriority;
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId))
        {
            return PromptTypes.SpellDuelFocus;
        }

        if (ResolutionResult.HasOpenOrderTriggersWindow(state))
        {
            return PromptTypes.OrderTriggers;
        }

        if (ResolutionResult.HasOpenBattleDamageAssignmentWindow(state))
        {
            return PromptTypes.AssignCombatDamage;
        }

        if (ResolutionResult.ActiveStartBattleTask(state) is not null)
        {
            return PromptTypes.BattleDeclaration;
        }

        if (ResolutionResult.HasBlockingPendingTaskQueue(state))
        {
            return PromptTypes.TaskQueue;
        }

        return PromptTypes.MainAction;
    }

    private static string? ResponsiblePlayerIdFor(
        MatchState state,
        string type,
        bool actionable,
        string playerId)
    {
        if (string.Equals(type, PromptTypes.PayCost, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.PendingPayment?.PlayerId);
        }

        if (string.Equals(type, PromptTypes.HandChoice, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.PendingHandChoice?.PlayerId);
        }

        if (string.Equals(type, PromptTypes.CardChoice, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.PendingCardChoice?.PlayerId);
        }

        if (string.Equals(type, PromptTypes.StackPriority, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.PriorityPlayerId);
        }

        if (string.Equals(type, PromptTypes.SpellDuelFocus, StringComparison.Ordinal)
            || string.Equals(type, PromptTypes.SpellDuelAction, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.FocusPlayerId);
        }

        if (string.Equals(type, PromptTypes.OrderTriggers, StringComparison.Ordinal))
        {
            return EmptyAsNull(ResolutionResult.OrderTriggersOrderingPlayerId(state));
        }

        if (string.Equals(type, PromptTypes.AssignCombatDamage, StringComparison.Ordinal))
        {
            return EmptyAsNull(ResolutionResult.BattleDamageAssigningPlayerId(state));
        }

        if (string.Equals(type, PromptTypes.BattleDeclaration, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.ActivePlayerId);
        }

        if (string.Equals(type, PromptTypes.Mulligan, StringComparison.Ordinal))
        {
            return EmptyAsNull(ResolutionResult.OpeningMulliganPlayerId(state));
        }

        if (string.Equals(type, PromptTypes.MainAction, StringComparison.Ordinal)
            || string.Equals(type, PromptTypes.Wait, StringComparison.Ordinal))
        {
            return EmptyAsNull(state.ActivePlayerId);
        }

        if (string.Equals(type, PromptTypes.RoomSetup, StringComparison.Ordinal) && actionable)
        {
            return EmptyAsNull(playerId);
        }

        return null;
    }

    private static string PromptResponsibilityStateFor(
        MatchState state,
        string type,
        bool actionable,
        string? responsiblePlayerId,
        bool isResponsiblePlayer)
    {
        if (string.Equals(type, PromptTypes.MatchResult, StringComparison.Ordinal)
            || string.Equals(state.Status, MatchStatuses.Finished, StringComparison.Ordinal))
        {
            return "MATCH_COMPLETE";
        }

        if (actionable)
        {
            return "PLAYER_ACTION";
        }

        if (!string.IsNullOrWhiteSpace(responsiblePlayerId))
        {
            return isResponsiblePlayer ? "PLAYER_WAIT" : "WAITING_PLAYER";
        }

        if (ResolutionResult.HasBlockingPendingTaskQueue(state)
            || state.PendingTaskQueue.Tasks.Count > 0
            || state.StackItems.Count > 0
            || state.TriggerQueue.Count > 0)
        {
            return "SERVER_RESOLVING";
        }

        if (!string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal))
        {
            return "ROOM_SETUP";
        }

        return "SERVER_WAIT";
    }

    private static string PromptResponsibilityNextStep(
        string type,
        string reason,
        bool actionable,
        string? responsiblePlayerId,
        string responsibilityState)
    {
        if (string.Equals(responsibilityState, "MATCH_COMPLETE", StringComparison.Ordinal))
        {
            return "查看对局结果。";
        }

        if (actionable)
        {
            return $"根据服务端候选处理{PromptTitleFor(type)}。";
        }

        if (!string.IsNullOrWhiteSpace(responsiblePlayerId))
        {
            return $"等待 {responsiblePlayerId} 处理{PromptTitleFor(type)}。";
        }

        if (string.Equals(responsibilityState, "SERVER_RESOLVING", StringComparison.Ordinal))
        {
            return "等待服务端推进规则队列。";
        }

        return string.IsNullOrWhiteSpace(reason)
            ? "等待服务端确认下一步。"
            : reason;
    }

    private static IReadOnlyDictionary<string, int> PromptResponsibilityQueueCounts(MatchState state)
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["stack"] = state.StackItems.Count,
            ["pendingTasks"] = state.PendingTaskQueue.Tasks.Count,
            ["triggerQueue"] = state.TriggerQueue.Count,
            ["pendingPayment"] = state.PendingPayment is null ? 0 : 1,
            ["pendingHandChoice"] = state.PendingHandChoice is null ? 0 : 1,
            ["pendingCardChoice"] = state.PendingCardChoice is null ? 0 : 1,
            ["activeBattle"] = state.BattleState.IsActive ? 1 : 0
        };
    }

    private static IReadOnlyList<string> PromptResponsibilityRelatedObjectIds(
        MatchState state,
        string playerId,
        string type)
    {
        return PromptResponsibilityRelatedObjects(state, playerId, type)
            .Select(item => item.ObjectId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptServerFlowObjectRefDto> PromptResponsibilityRelatedObjects(
        MatchState state,
        string playerId,
        string type)
    {
        var relatedObjects = new List<ActionPromptServerFlowObjectRefDto>();
        AddRelatedObjectRef(state, playerId, relatedObjects, RelatedBattlefieldIdFor(state, type), "相关战场");
        AddStackRelatedObjectIds(state, playerId, relatedObjects);
        AddTaskRelatedObjectIds(state, playerId, relatedObjects);
        AddBattleRelatedObjectIds(state, playerId, relatedObjects);
        AddPendingPaymentRelatedObjectIds(state, playerId, relatedObjects);
        AddPendingHandChoiceRelatedObjectIds(state, playerId, relatedObjects);
        AddPendingCardChoiceRelatedObjectIds(state, playerId, relatedObjects);
        AddTriggerRelatedObjectIds(state, playerId, relatedObjects);

        return relatedObjects
            .GroupBy(item => $"{item.Role}\u001f{item.ObjectId}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static void AddStackRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        foreach (var stackItem in state.StackItems)
        {
            AddRelatedObjectRef(state, playerId, relatedObjects, stackItem.SourceObjectId, "结算来源");
            AddRelatedObjectRefs(state, playerId, relatedObjects, stackItem.TargetObjectIds, "结算目标");
            AddPaymentResourceRelatedObjectIds(state, playerId, relatedObjects, stackItem.OptionalCosts);
        }
    }

    private static void AddTaskRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        foreach (var task in state.PendingTaskQueue.Tasks)
        {
            AddRelatedObjectRef(state, playerId, relatedObjects, task.BattlefieldObjectId, "任务战场");
        }

        foreach (var task in state.BattlefieldTasks)
        {
            AddRelatedObjectRef(state, playerId, relatedObjects, task.BattlefieldObjectId, "战场任务");
            AddRelatedObjectRefs(state, playerId, relatedObjects, task.ParticipantObjectIds, "任务单位");
        }
    }

    private static void AddBattleRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        var battle = state.BattleState;
        if (!battle.IsActive)
        {
            return;
        }

        AddRelatedObjectRef(state, playerId, relatedObjects, battle.BattlefieldObjectId, "战斗战场");
        AddRelatedObjectRefs(state, playerId, relatedObjects, battle.AttackerObjectIds, "攻击单位");
        AddRelatedObjectRefs(state, playerId, relatedObjects, battle.DefenderObjectIds, "防守单位");
    }

    private static void AddPendingPaymentRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        if (state.PendingPayment is not { } payment)
        {
            return;
        }

        AddPaymentResourceRelatedObjectIds(state, playerId, relatedObjects, PendingPaymentResourceActionIds(state, payment));
    }

    private static void AddPendingHandChoiceRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        if (state.PendingHandChoice is not { } choice)
        {
            return;
        }

        AddRelatedObjectRef(state, playerId, relatedObjects, choice.SourceObjectId, "选择来源");
        if (string.Equals(choice.PlayerId, playerId, StringComparison.Ordinal))
        {
            AddRelatedObjectRefs(state, playerId, relatedObjects, choice.LegalObjectIds, "可选手牌");
        }
    }

    private static void AddPendingCardChoiceRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        if (state.PendingCardChoice is not { } choice)
        {
            return;
        }

        AddRelatedObjectRef(state, playerId, relatedObjects, choice.SourceObjectId, "选择来源");
        if (string.Equals(choice.PlayerId, playerId, StringComparison.Ordinal))
        {
            AddRelatedObjectRefs(state, playerId, relatedObjects, choice.LegalObjectIds, "可选卡牌");
        }
    }

    private static void AddTriggerRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects)
    {
        foreach (var trigger in state.TriggerQueue)
        {
            if (IsHiddenRelatedTriggerSourceForViewer(state, trigger, playerId))
            {
                continue;
            }

            AddRelatedObjectRef(state, playerId, relatedObjects, trigger.SourceObjectId, "触发来源");
        }
    }

    private static void AddPaymentResourceRelatedObjectIds(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects,
        IReadOnlyList<string> resourceActionIds)
    {
        foreach (var actionId in resourceActionIds)
        {
            AddPaymentResourceRelatedObjectId(state, playerId, relatedObjects, actionId);
        }
    }

    private static void AddPaymentResourceRelatedObjectId(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects,
        string resourceActionId)
    {
        if (TryParseRecycleRunePaymentActionId(resourceActionId, out var runeObjectId))
        {
            AddRelatedObjectRef(state, playerId, relatedObjects, runeObjectId, "费用资源");
            return;
        }



        AddRelatedObjectRef(state, playerId, relatedObjects, resourceActionId, "费用资源");
    }

    private static void AddRelatedObjectRefs(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects,
        IReadOnlyList<string> objectIds,
        string role)
    {
        foreach (var objectId in objectIds)
        {
            AddRelatedObjectRef(state, playerId, relatedObjects, objectId, role);
        }
    }

    private static void AddRelatedObjectRef(
        MatchState state,
        string playerId,
        List<ActionPromptServerFlowObjectRefDto> relatedObjects,
        string? objectId,
        string role)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var normalizedObjectId = objectId.Trim();
        if (string.Equals(normalizedObjectId, "HIDDEN", StringComparison.OrdinalIgnoreCase)
            || !CanExposeRelatedObjectIdForViewer(state, normalizedObjectId, playerId))
        {
            return;
        }

        var normalizedRole = string.IsNullOrWhiteSpace(role) ? "服务端关联" : role.Trim();
        relatedObjects.Add(new ActionPromptServerFlowObjectRefDto(normalizedObjectId, normalizedRole));
    }

    private static bool CanExposeRelatedObjectIdForViewer(
        MatchState state,
        string objectId,
        string viewerPlayerId)
    {
        if (!state.CardObjects.ContainsKey(objectId)
            || !state.ObjectLocations.TryGetValue(objectId, out var location)
            || IsHiddenRelatedBattlefieldStandbyForViewer(state, objectId, viewerPlayerId))
        {
            return false;
        }

        if (!string.Equals(location.Zone, "HAND", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(location.PlayerId, viewerPlayerId, StringComparison.Ordinal);
    }

    private static bool IsHiddenRelatedTriggerSourceForViewer(
        MatchState state,
        TriggerQueueItemState trigger,
        string viewerPlayerId)
    {
        return !string.IsNullOrWhiteSpace(trigger.SourceObjectId)
            && IsHiddenRelatedBattlefieldStandbyForViewer(state, trigger.SourceObjectId, viewerPlayerId);
    }

    private static bool IsHiddenRelatedBattlefieldStandbyForViewer(
        MatchState state,
        string objectId,
        string viewerPlayerId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || !cardObject.IsFaceDown
            || !state.ObjectLocations.TryGetValue(objectId, out var location)
            || !string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            return false;
        }

        return !CoreRuleEngine.CanInspectFaceDown(state, objectId, viewerPlayerId);
    }

    private static string? EmptyAsNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyDictionary<string, object?>? PromptViewMetadata(
        MatchState state,
        string playerId,
        string type)
    {
        var payment = state.PendingPayment;
        if (payment is not null && string.Equals(type, PromptTypes.PayCost, StringComparison.Ordinal))
        {
            return new Dictionary<string, object?>
            {
                ["paymentId"] = payment.PaymentId,
                ["paymentWindow"] = payment.PaymentWindow,
                ["cost"] = PendingPaymentCostView(payment),
                ["paymentChoices"] = PendingPaymentChoiceDtos(state, payment),
                ["paymentResourceChoices"] = PendingPaymentResourceChoiceDtos(state, payment),
                ["paymentResourcePowerByChoice"] = PendingPaymentResourcePowerByChoice(state, payment)
            };
        }

        if (string.Equals(type, PromptTypes.HandChoice, StringComparison.Ordinal))
        {
            return HandChoiceMetadataFor(state, playerId);
        }

        if (string.Equals(type, PromptTypes.CardChoice, StringComparison.Ordinal))
        {
            return CardChoiceMetadataFor(state, playerId);
        }

        if (string.Equals(type, PromptTypes.AssignCombatDamage, StringComparison.Ordinal))
        {
            return AssignCombatDamageMetadataFor(state, playerId);
        }

        if (string.Equals(type, PromptTypes.OrderTriggers, StringComparison.Ordinal))
        {
            return OrderTriggersMetadataFor(state, playerId);
        }

        return null;
    }

    private static string? RelatedBattlefieldIdFor(MatchState state, string type)
    {
        if (string.Equals(type, PromptTypes.BattleDeclaration, StringComparison.Ordinal))
        {
            return FirstNonEmpty(
                ResolutionResult.ActiveStartBattleTask(state)?.BattlefieldObjectId,
                state.BattleState.BattlefieldObjectId);
        }

        if (string.Equals(type, PromptTypes.StackPriority, StringComparison.Ordinal)
            && (HasOpenBattleResponsePriority(state) || state.BattleState.IsActive && CoreRuleEngine.IsInitialBattleDuel(state, out _)))
        {
            return FirstNonEmpty(state.BattleState.BattlefieldObjectId);
        }

        if (string.Equals(type, PromptTypes.AssignCombatDamage, StringComparison.Ordinal))
        {
            return FirstNonEmpty(state.BattleState.BattlefieldObjectId);
        }

        if (string.Equals(type, PromptTypes.TaskQueue, StringComparison.Ordinal))
        {
            var activeTask = ActivePendingTask(state);
            return FirstNonEmpty(activeTask?.BattlefieldObjectId);
        }

        if (string.Equals(type, PromptTypes.SpellDuelFocus, StringComparison.Ordinal))
        {
            return FirstNonEmpty(
                state.SpellDuelState.BattlefieldObjectId,
                state.BattlefieldTasks.FirstOrDefault(task =>
                    string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                    && string.Equals(task.Status, "ACTIVE", StringComparison.Ordinal))?.BattlefieldObjectId);
        }

        return null;
    }

    private static string? RelatedBattleIdFor(MatchState state, string type)
    {
        if (string.Equals(type, PromptTypes.SpellDuelFocus, StringComparison.Ordinal) && state.BattleState.IsActive)
            return FirstNonEmpty(state.BattleState.BattleId);

        if (string.Equals(type, PromptTypes.BattleDeclaration, StringComparison.Ordinal))
        {
            return FirstNonEmpty(
                BattleLifecycleIds.BattleIdForBattlefield(ResolutionResult.ActiveStartBattleTask(state)?.BattlefieldObjectId),
                state.BattleState.BattleId);
        }

        if (string.Equals(type, PromptTypes.StackPriority, StringComparison.Ordinal)
            && (HasOpenBattleResponsePriority(state) || state.BattleState.IsActive && CoreRuleEngine.IsInitialBattleDuel(state, out _)))
        {
            return FirstNonEmpty(state.BattleState.BattleId);
        }

        if (string.Equals(type, PromptTypes.AssignCombatDamage, StringComparison.Ordinal))
        {
            return FirstNonEmpty(state.BattleState.BattleId);
        }

        return null;
    }

    private static string? RelatedSpellDuelIdFor(MatchState state, string type)
    {
        if (string.Equals(type, PromptTypes.SpellDuelFocus, StringComparison.Ordinal))
        {
            return FirstNonEmpty(
                state.SpellDuelState.SpellDuelId,
                BattleLifecycleIds.SpellDuelIdForBattlefield(state.BattlefieldTasks.FirstOrDefault(task =>
                    string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                    && string.Equals(task.Status, "ACTIVE", StringComparison.Ordinal))?.BattlefieldObjectId));
        }

        return null;
    }

    private static string? RelatedStackItemIdFor(MatchState state, string type)
    {
        if (!string.Equals(type, PromptTypes.StackPriority, StringComparison.Ordinal)
            && !string.Equals(type, PromptTypes.SpellDuelFocus, StringComparison.Ordinal))
        {
            return null;
        }

        return FirstNonEmpty(state.StackItems.LastOrDefault()?.StackItemId);
    }

    private static CleanupTaskState? ActivePendingTask(MatchState state)
    {
        var queue = state.PendingTaskQueue;
        return queue.Tasks.FirstOrDefault(task => string.Equals(task.TaskId, queue.ActiveTaskId, StringComparison.Ordinal))
            ?? queue.Tasks.FirstOrDefault();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static ActionPromptCandidateDto BuildCandidate(
        MatchState state,
        string playerId,
        string action,
        bool promptActionable,
        string promptReason)
    {
        var sources = AnnotatePromptChoiceObjectIds(SourcesFor(state, playerId, action));
        var targets = AnnotatePromptChoiceObjectIds(TargetsFor(state, playerId, action));
        var destinations = AnnotatePromptChoiceObjectIds(DestinationsFor(state, playerId, action));
        var modes = AnnotatePromptChoiceObjectIds(ModesFor(state, playerId, action));
        var optionalCosts = AnnotatePromptChoiceObjectIds(OptionalCostsFor(state, playerId, action));
        var requiresSourceChoices = string.Equals(action, "PLAY_CARD", StringComparison.Ordinal)
            || string.Equals(action, "MOVE_UNIT", StringComparison.Ordinal)
            || string.Equals(action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal)
            || string.Equals(action, "LEGEND_ACT", StringComparison.Ordinal)
            || string.Equals(action, "ACTIVATE_ABILITY", StringComparison.Ordinal)
            || string.Equals(action, "HIDE_CARD", StringComparison.Ordinal)
            || string.Equals(action, "REVEAL_CARD", StringComparison.Ordinal)
            || string.Equals(action, "TAP_RUNE", StringComparison.Ordinal)
            || string.Equals(action, "RECYCLE_RUNE", StringComparison.Ordinal)
            || string.Equals(action, "DECLARE_BATTLE", StringComparison.Ordinal);
        var requiresTargetChoices = string.Equals(action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal)
            || string.Equals(action, "DECLARE_BATTLE", StringComparison.Ordinal);
        var hasRequiredChoices = !requiresSourceChoices
            || sources?.Count > 0;
        hasRequiredChoices = hasRequiredChoices
            && (!requiresTargetChoices || targets?.Count > 0);
        var isSurrender = string.Equals(action, "SURRENDER", StringComparison.Ordinal);
        var enabled = (promptActionable
                || (isSurrender
                    && string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
                    && state.Seats.ContainsKey(playerId)))
            && !string.Equals(action, "WAIT", StringComparison.Ordinal)
            && hasRequiredChoices;
        var commandTemplate = CommandTemplateFor(action);
        if (action == CommandTypes.ActivateAbility && commandTemplate is not null
            && ActivateAbilitySourceRequirements(state, playerId).Any(r => r.AbilityInstanceId is not null))
            commandTemplate = commandTemplate with { Bindings = commandTemplate.Bindings
                .Append(RequirementMetadataBinding("abilityInstanceId", required: false, "abilityInstanceId")).ToArray() };
        var selectionSteps = SelectionStepsFor(
            requiresSourceChoices,
            requiresTargetChoices,
            sources,
            targets,
            destinations,
            modes,
            optionalCosts,
            commandTemplate);
        if (action == CommandTypes.ChooseHandCards && state.PendingHandChoice is { } handChoice
            && handChoice.PlayerId == playerId)
        {
            var handChoices = AnnotatePromptChoiceObjectIds(PendingHandChoiceDtos(state, handChoice));
            var steps = new List<ActionPromptSelectionStepDto>();
            for (var index = 0; index < handChoice.RequiredCount; index++)
                AddSelectionStep(steps, "target", $"第 {index + 1} 张弃牌", true, handChoices);
            selectionSteps = steps;
        }
        if (action == CommandTypes.ChooseCards && state.PendingCardChoice is { } cardChoice
            && cardChoice.PlayerId == playerId)
        {
            var cards = AnnotatePromptChoiceObjectIds(PendingCardChoiceDtos(state, cardChoice));
            var steps = new List<ActionPromptSelectionStepDto>();
            for (var index = 0; index < cardChoice.MaxCount; index++)
                AddSelectionStep(steps, "target", cardChoice.ChoiceWindow == CoreRuleEngine.AzirAttachmentWindow ? "选择已确认的武装；不选则跳过改贴" : cardChoice.ChoiceWindow == CoreRuleEngine.TemperedConfirmationWindow ? "选择你的武装；不选则放弃百炼" : cardChoice.ChoiceWindow == CoreRuleEngine.TemperedCostWindow ? "选择支付装配费用的卡牌" : cardChoice.ChoiceWindow == CoreRuleEngine.TokenReplacementWindow ? "选择替换来源；不选则保留次数" : cardChoice.ChoiceWindow == "INSIGHT" ? $"第 {index + 1} 张回收牌；不选则保留" : cardChoice.ChoiceWindow == "INSIGHT_ORDER" ? $"牌库顶第 {index + 1} 张" : cardChoice.ChoiceWindow == CoreRuleEngine.TriggerCostWindow ? cardChoice.EffectKind == CoreRuleEngine.BattlefieldStartDrawEffect ? "选择支付摧毁费用的单位；不选则放弃" : cardChoice.EffectKind == "HOLD_LEBLANC_DISCARD" ? "弃置所选手牌并横置传奇；不选则放弃" : "横置所选传奇；不选则放弃" : cardChoice.ChoiceWindow == CoreRuleEngine.OptionalTriggerWindow ? cardChoice.EffectKind == "HOLD_CHANNEL_OPTIONAL" ? "确认召出符文；不选则放弃" : "选择换回原战场；不选则保留草丛" : cardChoice.EffectKind == CoreRuleEngine.ReflexiveCopyEffect ? "选择此战场的复制对象" : cardChoice.EffectKind == CoreRuleEngine.CreatedTokenReadyEffect ? "选择本次打出的至多两名士兵" : cardChoice.ChoiceWindow == "TRIGGER_CONFIRMATION" ? cardChoice.RequiredCount == 0 ? "选择目标；不选则放弃技能" : "选择技能目标" : $"第 {index + 1} 张卡牌", index < cardChoice.RequiredCount, cards);
            selectionSteps = steps;
        }
        if (action == CommandTypes.PayCost && state.PendingPayment is { } payment && payment.PlayerId == playerId
            && (payment.PaymentWindow is CoreRuleEngine.RuleChoiceWindow or CoreRuleEngine.TemperedPaymentWindow or CoreRuleEngine.MightyCostWindow or "TRIGGER_PAYMENT" || payment.LegalPaymentChoiceIds.SequenceEqual(["PAY", "DECLINE"])))
        {
            var steps = new List<ActionPromptSelectionStepDto>();
            AddSelectionStep(steps, "target", payment.PaymentWindow == CoreRuleEngine.RuleChoiceWindow ? state.PendingRuleChoice?.Request.Kind == "TOKEN_HASTE_PAYMENT" ? "为即将打出的单位支付急速费用，或不支付" : state.PendingRuleChoice?.Request.Kind == CoreRuleEngine.ResourceConversionAmountKind ? "选择本次资源转换的数量，确认前不横置或支付" : state.PendingRuleChoice?.Request.Kind == CoreRuleEngine.ImposedMovementPaymentKind ? "支付群体移动的施加费用；可先获得资源或放弃此次移动" : state.PendingRuleChoice?.Request.Kind == CoreRuleEngine.InstructionPowerPaymentKind ? "结算时选择符能数量；可先获得资源或支付 0" : state.PendingRuleChoice?.Request.Kind == "OPTIONAL_SPELL_CONTROL_PAYMENT" ? "结算中支付符能或尝试无效化" : state.PendingRuleChoice?.Request.Kind == "COUNTER_TARGET_MANA_PAYMENT" ? "支付法力保留法术，或不支付" : state.PendingRuleChoice?.Request.Kind == "SPELL_CHOICE_REMAKE" ? "修改目标或模式，再确认全部合法；可保留全部原选择" : state.PendingRuleChoice?.Request.Kind == "DECK_OBSERVATION_BANISH" ? state.DeckObservationView?.Public == true ? "选择是否放逐当前展示的牌" : "选择是否放逐当前查看的牌" : state.PendingRuleChoice?.Request.Kind == "MOVEMENT_SELECTION" ? "选择要移动的单位" : state.PendingRuleChoice?.Request.Kind == "DECK_REFLEXIVE_CREATE" ? "创建强化技能，或跳过": state.PendingRuleChoice?.Request.Kind == "ATTACHMENT_COPY_SELECTION" ? "选择复制单位" : "选择替换对象与支付方式，或不替换" : payment.PaymentWindow == CoreRuleEngine.TemperedPaymentWindow ? "支付装配费用，或确认无法支付" : "支付或放弃此效果", true,
                payment.PaymentWindow is CoreRuleEngine.RuleChoiceWindow or CoreRuleEngine.TemperedPaymentWindow or CoreRuleEngine.MightyCostWindow or "TRIGGER_PAYMENT" ? PendingPaymentChoiceDtos(state, payment) : [new("PAY", payment.ManaCost > 0 ? $"支付 {payment.ManaCost} 法力" : $"支付 {payment.PowerCost} 符能"), new("DECLINE", "放弃此效果")]);
            if (payment.PaymentWindow == CoreRuleEngine.MightyCostWindow) {
                var resources = PendingPaymentResourceChoiceDtos(state, payment);
                for (var index = 0; index < resources.Count; index++)
                    AddSelectionStep(steps, "target", "可选回收符文以支付符能；放弃时不选", false, resources);
            }
            selectionSteps = steps;
            commandTemplate = CommandTemplate(CommandTypes.PayCost,
                CandidateMetadataBinding("paymentId", required: true, metadataKeys: ["paymentId"]),
                CandidateMetadataBinding("paymentWindow", required: true, metadataKeys: ["paymentWindow"]),
                SelectedTargetsBinding("paymentChoiceIds", required: true));
        }
        return new ActionPromptCandidateDto(
            action,
            action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow == CoreRuleEngine.AzirAttachmentWindow ? "决定是否改贴武装" : action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow == CoreRuleEngine.AttachmentModeWindow ? "确认技能模式" : action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow == CoreRuleEngine.AttachmentModeTargetWindow ? "确认技能目标" : action == CommandTypes.PayCost && state.PendingRuleChoice is not null ? (state.PendingRuleChoice.Request.Kind switch { "DECK_REFLEXIVE_CREATE" => "决定是否创建强化技能", "DECK_OBSERVATION_BANISH" => "选择是否放逐", "ATTACHMENT_COPY_SELECTION" => "选择复制单位", "MOVEMENT_SELECTION" => "选择移动单位", "TARGET_GROUP" => "重选合法目标", "FIRST_DEATH_OCCURRENCE" => "选择首次死亡事件", "TOKEN_HASTE_PAYMENT" => "指示物急速支付", "COUNTER_TARGET_MANA_PAYMENT" => "决定是否支付法力", "OPTIONAL_SPELL_CONTROL_PAYMENT" => "支付符能或无效化", CoreRuleEngine.ResourceConversionAmountKind => "选择资源转换数量", CoreRuleEngine.ImposedMovementPaymentKind => "支付群体移动费用", CoreRuleEngine.InstructionPowerPaymentKind => "支付结算指令费用", "SPELL_CHOICE_REMAKE" => "为法术做出新的选择", _ => "选择摧毁替换" }) : action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow == CoreRuleEngine.TokenReplacementWindow ? "确认指示物替换" : action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow == CoreRuleEngine.TriggerCostWindow ? "确认触发费用" : action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow is "INSIGHT" or "INSIGHT_ORDER" ? "完成洞察" : action == CommandTypes.ChooseCards && state.PendingCardChoice?.ChoiceWindow is "TRIGGER_CONFIRMATION" or CoreRuleEngine.OptionalTriggerWindow or CoreRuleEngine.TemperedConfirmationWindow ? "确认触发技能" : LabelFor(action),
            enabled,
            enabled ? promptReason : DisabledReasonFor(action, promptReason, hasRequiredChoices),
            sources,
            targets,
            destinations,
            modes,
            optionalCosts,
            MetadataFor(state, playerId, action),
            selectionSteps,
            commandTemplate,
            ComposerFor(selectionSteps, commandTemplate),
            PresentationFor(action));
    }

    private static ActionPromptCandidatePresentationDto PresentationFor(string action)
    {
        return action switch
        {
            CommandTypes.Ready => CandidatePresentation("setup", "ready", "primary", 20),
            CommandTypes.SubmitDeck => CandidatePresentation("setup", "submit-deck", "primary", 10),
            CommandTypes.Mulligan => CandidatePresentation("setup", "mulligan", "primary", 30),
            CommandTypes.PayCost => CandidatePresentation("payment", "pay-cost", "payment", 40),
            CommandTypes.AssignCombatDamage => CandidatePresentation("battle", "assign-damage", "battle", 50),
            CommandTypes.AssignEffectDamage => CandidatePresentation("choice", "assign-effect-damage", "choice", 50),
            CommandTypes.OrderTriggers => CandidatePresentation("choice", "order-triggers", "choice", 60),
            CommandTypes.ChooseHandCards => CandidatePresentation("choice", "choose-hand", "choice", 70),
            CommandTypes.ChooseCards => CandidatePresentation("choice", "choose-cards", "choice", 75),
            CommandTypes.PlayCard => CandidatePresentation("play", "play-card", "card-action", 100),
            CommandTypes.HideCard => CandidatePresentation("play", "hide-card", "card-action", 110),
            CommandTypes.RevealCard => CandidatePresentation("play", "reveal-card", "card-action", 120),
            CommandTypes.DeclareBattle => CandidatePresentation("battle", "declare-battle", "battle", 130),
            CommandTypes.MoveUnit => CandidatePresentation("movement", "move-unit", "card-action", 140),
            CommandTypes.AssembleEquipment => CandidatePresentation("ability", "assemble-equipment", "card-action", 150),
            CommandTypes.ActivateAbility => CandidatePresentation("ability", "activate-ability", "card-action", 160),
            CommandTypes.LegendAct => CandidatePresentation("ability", "legend-act", "card-action", 170),
            CommandTypes.TapRune => CandidatePresentation("resource", "tap-rune", "resource", 180),
            CommandTypes.RecycleRune => CandidatePresentation("resource", "recycle-rune", "resource", 190),
            CommandTypes.PassPriority => CandidatePresentation("tempo", "pass-priority", "secondary", 500),
            CommandTypes.PassFocus => CandidatePresentation("tempo", "pass-focus", "secondary", 510),
            CommandTypes.Pass => CandidatePresentation("tempo", "pass", "secondary", 520),
            CommandTypes.EndTurn => CandidatePresentation("tempo", "end-turn", "secondary", 530),
            CommandTypes.Surrender => CandidatePresentation("concede", "surrender", "danger", 900),
            "WAIT" => CandidatePresentation("system", "wait", "readonly", 990),
            _ => CandidatePresentation("custom", action.ToLowerInvariant().Replace('_', '-'), "card-action", 700)
        };
    }

    private static ActionPromptCandidatePresentationDto CandidatePresentation(
        string category,
        string intent,
        string uiHint,
        int priority)
    {
        return new ActionPromptCandidatePresentationDto(category, intent, uiHint, priority);
    }

    private static ActionPromptComposerDto? ComposerFor(
        IReadOnlyList<ActionPromptSelectionStepDto>? selectionSteps,
        ActionPromptCommandTemplateDto? commandTemplate)
    {
        if (commandTemplate is null)
        {
            return null;
        }

        var steps = selectionSteps ?? Array.Empty<ActionPromptSelectionStepDto>();
        return new ActionPromptComposerDto(
            true,
            "服务端已公开命令模板；前端只负责组装候选选择并提交给服务端规则校验。",
            steps.Select(step => step.Role).Distinct(StringComparer.Ordinal).OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            steps.Where(step => step.Required).Select(step => step.Role).Distinct(StringComparer.Ordinal).OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            commandTemplate.Bindings.Select(binding => binding.Field).Distinct(StringComparer.Ordinal).OrderBy(field => field, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<ActionPromptSelectionStepDto>? SelectionStepsFor(
        bool requiresSourceChoices,
        bool requiresTargetChoices,
        IReadOnlyList<ActionPromptChoiceDto>? sources,
        IReadOnlyList<ActionPromptChoiceDto>? targets,
        IReadOnlyList<ActionPromptChoiceDto>? destinations,
        IReadOnlyList<ActionPromptChoiceDto>? modes,
        IReadOnlyList<ActionPromptChoiceDto>? optionalCosts,
        ActionPromptCommandTemplateDto? commandTemplate)
    {
        var steps = new List<ActionPromptSelectionStepDto>();
        AddSelectionStep(
            steps,
            "source",
            "来源",
            requiresSourceChoices || CommandTemplateRequiresRole(commandTemplate, "selectedSource"),
            sources);
        AddSelectionStep(
            steps,
            "target",
            "目标",
            requiresTargetChoices
                || CommandTemplateRequiresRole(commandTemplate, "selectedTarget", "selectedTargets"),
            targets);
        AddSelectionStep(
            steps,
            "destination",
            "位置",
            CommandTemplateRequiresRole(commandTemplate, "selectedDestination"),
            destinations);
        AddSelectionStep(
            steps,
            "mode",
            "模式",
            CommandTemplateRequiresRole(commandTemplate, "selectedMode"),
            modes);
        AddSelectionStep(
            steps,
            "optionalCost",
            "费用",
            CommandTemplateRequiresRole(commandTemplate, "selectedOptionalCosts"),
            optionalCosts);
        return steps.Count == 0 ? null : steps;
    }

    private static bool CommandTemplateRequiresRole(
        ActionPromptCommandTemplateDto? commandTemplate,
        params string[] bindingSources)
    {
        return commandTemplate?.Bindings.Any(binding =>
            binding.Required
            && bindingSources.Contains(binding.Source, StringComparer.Ordinal)) == true;
    }

    private static ActionPromptCommandTemplateDto? CommandTemplateFor(string action)
    {
        return action switch
        {
            CommandTypes.ChooseCards => CommandTemplate(
                CommandTypes.ChooseCards,
                CandidateMetadataBinding("choiceId", required: true, metadataKeys: ["choiceId"]),
                CandidateMetadataBinding("choiceWindow", required: true, metadataKeys: ["choiceWindow"]),
                SelectedTargetsBinding("chosenObjectIds", required: false, omitEmpty: false)),
            CommandTypes.ChooseHandCards => CommandTemplate(
                CommandTypes.ChooseHandCards,
                CandidateMetadataBinding("choiceId", required: true, metadataKeys: ["choiceId"]),
                CandidateMetadataBinding("choiceWindow", required: true, metadataKeys: ["choiceWindow"]),
                SelectedTargetsBinding("chosenObjectIds", required: true, omitEmpty: false)),
            CommandTypes.PassPriority => CommandTemplate(CommandTypes.PassPriority),
            CommandTypes.PassFocus => CommandTemplate(CommandTypes.PassFocus),
            CommandTypes.Pass => CommandTemplate(CommandTypes.Pass),
            CommandTypes.EndTurn => CommandTemplate(CommandTypes.EndTurn),
            CommandTypes.Surrender => CommandTemplate(CommandTypes.Surrender),
            CommandTypes.PlayCard => CommandTemplate(
                CommandTypes.PlayCard,
                SourceBinding("sourceObjectId", required: true),
                RequirementMetadataBinding("cardNo", required: true, "cardNo", "equipmentCardNo"),
                SelectedTargetsBinding("targetObjectIds", required: false, omitEmpty: false),
                SelectedModeBinding("mode"),
                SelectedDestinationBinding("destination"),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.HideCard => CommandTemplate(
                CommandTypes.HideCard,
                SourceBinding("sourceObjectId", required: true),
                RequirementMetadataBinding("cardNo", required: true, "cardNo", "equipmentCardNo"),
                SelectedDestinationBinding("destination", required: true),
                SelectedOptionalCostsBinding("optionalCosts", required: true)),
            CommandTypes.RevealCard => CommandTemplate(
                CommandTypes.RevealCard,
                SourceBinding("sourceObjectId", required: true),
                RequirementMetadataBinding("cardNo", required: true, "cardNo", "equipmentCardNo"),
                SelectedTargetsBinding("targetObjectIds", required: false, omitEmpty: false),
                SelectedModeBinding("mode"),
                SelectedDestinationBinding("destination"),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.TapRune => CommandTemplate(
                CommandTypes.TapRune,
                SourceBinding("sourceObjectId", required: true)),
            CommandTypes.RecycleRune => CommandTemplate(
                CommandTypes.RecycleRune,
                SourceBinding("sourceObjectId", required: true)),
            CommandTypes.MoveUnit => CommandTemplate(
                CommandTypes.MoveUnit,
                SourceBinding("sourceObjectId", required: true),
                RequirementMetadataBinding("origin", required: true, "origin"),
                SelectedDestinationBinding("destination"),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.AssembleEquipment => CommandTemplate(
                CommandTypes.AssembleEquipment,
                SourceBinding("sourceObjectId", required: true),
                SelectedTargetBinding("targetObjectId", required: true),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.DeclareBattle => CommandTemplate(
                CommandTypes.DeclareBattle,
                SourceBinding("attackerObjectIds", required: true, asArray: true),
                SelectedDestinationBinding("battlefieldId", required: true),
                SelectedTargetsBinding("defenderObjectIds", required: true),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.ActivateAbility => CommandTemplate(
                CommandTypes.ActivateAbility,
                SourceBinding("sourceObjectId", required: true),
                RequirementMetadataBinding("abilityId", required: true, "abilityId"),
                SelectedTargetsBinding("targetObjectIds", required: false, omitEmpty: false),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.LegendAct => CommandTemplate(
                CommandTypes.LegendAct,
                SourceBinding("sourceObjectId", required: true),
                RequirementMetadataBinding("abilityId", required: true, "abilityId"),
                SelectedTargetsBinding("targetObjectIds", required: false, omitEmpty: false),
                SelectedOptionalCostsBinding("optionalCosts")),
            CommandTypes.PayCost => CommandTemplate(
                CommandTypes.PayCost,
                CandidateMetadataBinding("paymentId", required: true, metadataKeys: ["paymentId"]),
                CandidateMetadataBinding("paymentWindow", required: true, metadataKeys: ["paymentWindow"]),
                CandidateMetadataBinding("paymentChoiceIds", required: true, asArray: true, metadataKeys: ["paymentChoiceIds"])),
            _ => null
        };
    }

    private static ActionPromptCommandTemplateDto CommandTemplate(
        string cmdType,
        params ActionPromptCommandBindingDto[] bindings)
    {
        return new ActionPromptCommandTemplateDto(cmdType, bindings);
    }

    private static ActionPromptCommandBindingDto SourceBinding(
        string field,
        bool required = false,
        bool asArray = false)
    {
        return CommandBinding(field, "selectedSource", required, asArray);
    }

    private static ActionPromptCommandBindingDto SelectedTargetBinding(
        string field,
        bool required = false)
    {
        return CommandBinding(field, "selectedTarget", required);
    }

    private static ActionPromptCommandBindingDto SelectedTargetsBinding(
        string field,
        bool required = false,
        bool omitEmpty = true)
    {
        return CommandBinding(field, "selectedTargets", required, asArray: true, omitEmpty);
    }

    private static ActionPromptCommandBindingDto SelectedDestinationBinding(
        string field,
        bool required = false,
        bool asArray = false)
    {
        return CommandBinding(field, "selectedDestination", required, asArray);
    }

    private static ActionPromptCommandBindingDto SelectedModeBinding(string field)
    {
        return CommandBinding(field, "selectedMode");
    }

    private static ActionPromptCommandBindingDto SelectedOptionalCostsBinding(string field, bool required = false)
    {
        return CommandBinding(field, "selectedOptionalCosts", required, asArray: true);
    }

    private static ActionPromptCommandBindingDto RequirementMetadataBinding(
        string field,
        bool required,
        params string[] metadataKeys)
    {
        return CommandBinding(
            field,
            "requirementMetadata",
            required,
            metadataKey: metadataKeys.Length == 1 ? metadataKeys[0] : null,
            metadataKeys: metadataKeys.Length > 1 ? metadataKeys : null);
    }

    private static ActionPromptCommandBindingDto CandidateMetadataBinding(
        string field,
        bool required,
        bool asArray = false,
        params string[] metadataKeys)
    {
        return CommandBinding(
            field,
            "candidateMetadata",
            required,
            asArray,
            metadataKey: metadataKeys.Length == 1 ? metadataKeys[0] : null,
            metadataKeys: metadataKeys.Length > 1 ? metadataKeys : null);
    }

    private static ActionPromptCommandBindingDto CommandBinding(
        string field,
        string source,
        bool required = false,
        bool asArray = false,
        bool omitEmpty = true,
        string? metadataKey = null,
        IReadOnlyList<string>? metadataKeys = null)
    {
        return new ActionPromptCommandBindingDto(
            field,
            source,
            required,
            asArray,
            omitEmpty,
            metadataKey,
            metadataKeys,
            LabelForCommandBindingSource(source));
    }

    private static string? LabelForCommandBindingSource(string source)
    {
        return source switch
        {
            "selectedSource" => "来源",
            "selectedTarget" => "目标",
            "selectedTargets" => "目标",
            "selectedDestination" => "位置",
            "selectedMode" => "模式",
            "selectedOptionalCosts" => "费用",
            "requirementMetadata" => "服务端",
            "candidateMetadata" => "候选数据",
            _ => null
        };
    }

    private static void AddSelectionStep(
        ICollection<ActionPromptSelectionStepDto> steps,
        string role,
        string label,
        bool required,
        IReadOnlyList<ActionPromptChoiceDto>? choices)
    {
        if ((choices is null || choices.Count == 0) && !required)
        {
            return;
        }

        steps.Add(new ActionPromptSelectionStepDto(
            role,
            label,
            required,
            choices?.Select(SelectionChoiceFor).ToArray() ?? []));
    }

    private static ActionPromptSelectionChoiceDto SelectionChoiceFor(ActionPromptChoiceDto choice)
    {
        return new ActionPromptSelectionChoiceDto(
            choice.Id,
            choice.Label,
            choice.ObjectIds is { Count: > 0 } ? choice.ObjectIds : SelectionChoiceObjectIds(choice.Id),
            choice.Reason);
    }

    private static IReadOnlyList<ActionPromptChoiceDto>? AnnotatePromptChoiceObjectIds(IReadOnlyList<ActionPromptChoiceDto>? choices)
    {
        if (choices is null)
        {
            return null;
        }

        return choices
            .Select(choice => choice.ObjectIds is { Count: > 0 }
                ? choice
                : choice with { ObjectIds = SelectionChoiceObjectIds(choice.Id) })
            .ToArray();
    }

    private static IReadOnlyList<string> SelectionChoiceObjectIds(string choiceId)
    {
        var cleaned = choiceId.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return [];
        }

        var objectIds = new List<string> { cleaned };
        var segments = cleaned.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lastSegment = segments.Length > 0 ? segments[^1] : "";
        if (!string.IsNullOrWhiteSpace(lastSegment)
            && !string.Equals(lastSegment, cleaned, StringComparison.Ordinal))
        {
            objectIds.Add(lastSegment);
        }

        return objectIds.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void AddRevealCardSourceRequirement(
        MatchState state,
        string playerId,
        string objectId,
        string mode,
        string modeLabel,
        string destination,
        string destinationLabel,
        string optionalCostLabel,
        List<RevealCardPromptRequirement> requirements)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || string.IsNullOrWhiteSpace(cardObject.CardNo)
            || !CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior))
        {
            return;
        }

        var maxTargetCount = RevealCardMaxTargetCountForMode(behavior, mode);
        var targetScope = maxTargetCount > 0 ? behavior.StandbyReactionTargetScope : string.Empty;
        var targetChoicesByIndex = RevealCardTargetChoicesByIndex(
            state,
            playerId,
            objectId,
            behavior,
            mode,
            maxTargetCount);
        requirements.Add(new RevealCardPromptRequirement(
            objectId,
            behavior.CardNo,
            behavior.DisplayName,
            mode,
            modeLabel,
            [new ActionPromptChoiceDto(destination, destinationLabel, "服务端待命翻开目的地")],
            [new ActionPromptChoiceDto(StandbyRevealOptionalCost, optionalCostLabel)],
            targetChoicesByIndex,
            mode == StandbyReactionMode ? behavior.StandbyReactionMinTargetCount : 0,
            maxTargetCount,
            targetScope,
            [StandbyRevealOptionalCost],
            true,
            null));
    }

    private static int RevealCardMaxTargetCountForMode(
        CardBehaviorDefinition behavior,
        string mode)
    {
        return string.Equals(mode, StandbyReactionMode, StringComparison.Ordinal)
            ? Math.Max(0, behavior.StandbyReactionMaxTargetCount)
            : 0;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> RevealCardTargetChoicesByIndex(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior,
        string mode,
        int maxTargetCount)
    {
        if (maxTargetCount <= 0
            || !string.Equals(mode, StandbyReactionMode, StringComparison.Ordinal))
        {
            return new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        }

        var choicesByIndex = new Dictionary<string, IReadOnlyList<ActionPromptChoiceDto>>(StringComparer.Ordinal);
        for (var targetIndex = 0; targetIndex < maxTargetCount; targetIndex++)
        {
            choicesByIndex[targetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)] = RevealCardStandbyReactionTargetChoices(
                state,
                playerId,
                sourceObjectId,
                behavior);
        }

        return choicesByIndex;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> RevealCardStandbyReactionTargetChoices(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.StandbyReactionTargetScope == CardTargetScopes.Battlefield)
            return PromptTargetChoicesForIndex(state, playerId, behavior, 0);

        if (string.Equals(
                behavior.StandbyReactionTargetScope,
                CardTargetScopes.EnemyUnitAtSourceBattlefield,
                StringComparison.Ordinal))
        {
            return RevealCardStandbyReactionEnemyUnitAtSourceBattlefieldTargetChoices(
                state,
                playerId,
                sourceObjectId);
        }

        if (string.Equals(
                behavior.StandbyReactionTargetScope,
                CardTargetScopes.FriendlyUnit,
                StringComparison.Ordinal))
        {
            return RevealCardStandbyReactionFriendlyUnitTargetChoices(
                state,
                playerId,
                sourceObjectId,
                behavior);
        }

        return [];
    }

    private static IReadOnlyList<ActionPromptChoiceDto> RevealCardStandbyReactionEnemyUnitAtSourceBattlefieldTargetChoices(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        if (!TryResolveStandbyReactionBattlefieldContext(state, playerId, sourceObjectId, out var battlefieldObjectId))
        {
            return [];
        }

        return state.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
                && !string.Equals(objectId, battlefieldObjectId, StringComparison.Ordinal)
                && IsPromptEnemyBattlefieldObject(state, playerId, objectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId => ObjectChoice(state, objectId, "同一战场敌方单位"))
            .ToArray();
    }

    private static IReadOnlyList<ActionPromptChoiceDto> RevealCardStandbyReactionFriendlyUnitTargetChoices(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior)
    {
        return PublicBoardObjects(state)
            .Where(objectId => !string.Equals(objectId, sourceObjectId, StringComparison.Ordinal)
                && IsPromptControlledFieldObject(state, playerId, objectId)
                && IsPromptTargetRequiredTagAllowed(state, objectId, behavior)
                && IsPromptTargetForbiddenTagAllowed(state, objectId, behavior)
                && IsPromptTargetPowerAllowed(state, objectId, behavior)
                && (!behavior.SwapsSourceWithFirstTargetLocation
                    || PromptCanSwapSourceWithTargetLocation(state, sourceObjectId, objectId)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .Select(objectId => ObjectChoice(state, objectId, "友方单位"))
            .ToArray();
    }

    private static bool PromptCanSwapSourceWithTargetLocation(
        MatchState state,
        string sourceObjectId,
        string targetObjectId)
    {
        if (string.Equals(sourceObjectId, targetObjectId, StringComparison.Ordinal)
            || !TryFindLegendActionFieldObjectLocation(state.PlayerZones, sourceObjectId, out var sourceLocation)
            || !TryFindLegendActionFieldObjectLocation(state.PlayerZones, targetObjectId, out var targetLocation)
            || !state.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
            || !state.CardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return false;
        }

        return (!string.Equals(sourceLocation.PlayerId, targetLocation.PlayerId, StringComparison.Ordinal)
                || !string.Equals(sourceLocation.Zone, targetLocation.Zone, StringComparison.Ordinal))
            && SourceObjectControlledByPlayerOrLegacyOwned(sourceState, sourceLocation.PlayerId)
            && SourceObjectControlledByPlayerOrLegacyOwned(targetState, targetLocation.PlayerId);
    }

    private static bool CanRevealStandbyInBase(MatchState state, string playerId)
    {
        return string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            && string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
            && state.StackItems.Count == 0;
    }

    private static bool CanRevealStandbyReactionToStack(MatchState state, string playerId)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            || !string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal))
        {
            return false;
        }

        return state.StackItems.Count > 0
            ? string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
                || string.Equals(state.TimingState, TimingStates.SpellDuelClosed, StringComparison.Ordinal)
            : HasOpenBattleResponsePriority(state);
    }

    private static bool IsImplementedStandbyRevealSource(MatchState state, string playerId, string objectId)
    {
        if (!state.CardObjects.TryGetValue(objectId, out var cardObject)
            || !cardObject.IsFaceDown
            || string.IsNullOrWhiteSpace(cardObject.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
            || !CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo, out var behavior))
        {
            return false;
        }

        if (CardPermissionKeywordRules.IsSpellPlayProhibited(state, playerId, behavior)) return false;

        return cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            || HasDelimitedTag(behavior.SourceUnitTags, CardObjectTags.Standby);
    }

    private static bool TryResolveBattlefieldStandbyRevealDestination(
        MatchState state,
        string playerId,
        string objectId,
        out string battlefieldObjectId)
    {
        battlefieldObjectId = string.Empty;
        if (!state.ObjectLocations.TryGetValue(objectId, out var location)
            || !string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(location.BattlefieldObjectId)
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(location.BattlefieldObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(location.BattlefieldObjectId, out var battlefieldObject)
            || !battlefieldObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            || !SourceObjectControlledByPlayerOrLegacyOwned(battlefieldObject, playerId))
        {
            return false;
        }

        battlefieldObjectId = location.BattlefieldObjectId;
        return true;
    }

    private static bool HasFreeStandbyHidePermission(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            $"{FreeStandbyHideEffectPrefix}{playerId}",
            StringComparer.Ordinal);
    }

    private static bool HasTeemoStandbyHidePermission(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(legendObjectId =>
                state.CardObjects.TryGetValue(legendObjectId, out var legendState)
                && SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                && HasImplementedLegendActionAbility(legendState.CardNo, TeemoLegendAbilityId));
    }

    private static bool IsMovableUnitSource(MatchState state, string playerId, string objectId)
    {
        return IsControlledObjectWithTag(state, playerId, objectId, CardObjectTags.UnitCard)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && MovementRestrictionRules.CanMove(cardObject, playerId)
            && !cardObject.IsFaceDown
            && !cardObject.IsExhausted
            && !cardObject.IsAttacking
            && !cardObject.IsDefending;
    }
}
