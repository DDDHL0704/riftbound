using System.Collections.Immutable;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    public bool RewindAllowed => options.RewindAllowed;

    private sealed record RewindPoint(RewindTargetDto Target, MatchState Before,
        bool Selectable, string? ActorDescription);
    private sealed record RewindFrame(MatchState State, long TickOffset, RewindRequestDto? Request,
        ImmutableSortedDictionary<long, RewindPoint> Points)
    {
        public static RewindFrame Start(MatchState state)
            => new(state, 0, null, ImmutableSortedDictionary<long, RewindPoint>.Empty);

        public long Tick => State.Tick + TickOffset;
    }
    private RewindFrame? rewindFrame;

    private long ServerTick => state.Tick + (rewindFrame?.TickOffset ?? 0);

    private void RestoreRewindFrame(MatchRecoveryFrame recovery, MatchSession replayed)
    {
        if (replayed.ServerTick != recovery.CurrentTick || replayed.lastEventSequence != recovery.LastEventSequence)
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "rewind replay position differs from stored room position");
        if (replayed.rewindFrame is { } frame)
            rewindFrame = frame with { State = state };
        foreach (var view in recovery.PlayerViews.Values)
        {
            if (!view.Snapshot.RewindAllowed || view.Snapshot.RewindRequest != rewindFrame?.Request
                || view.Prompt?.PromptId != replayed.PromptFor(view.PlayerId).PromptId)
                throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "stored rewind projection differs from command replay");
        }
        foreach (var (intent, result) in replayed.intentCache)
            intentCache.Add(intent, result);
        foreach (var (intent, result) in replayed.tournamentDecisions)
            tournamentDecisions.Add(intent, result);
    }

    private static bool CanRewind(MatchState frame)
        => frame.Status == MatchStatuses.InProgress && frame.WinnerPlayerId is null;

    private static int RewindGameNumber(MatchState frame)
        => frame.SeriesState?.Games.FirstOrDefault(game => !game.Completed)?.GameNumber
            ?? frame.SeriesState?.Games.LastOrDefault()?.GameNumber ?? 1;

    private ResolutionResult ResolveRewind(string playerId, RewindCommand command, bool replayAcceptedCommand,
        out RewindRequestDto? nextRequest)
    {
        var pending = rewindFrame?.Request;
        nextRequest = pending;
        if (!RewindAllowed)
            return ResolutionResult.Rejected(state, "该房间未开放回退。", ErrorCodes.PhaseNotAllowed);
        if (state.Status == MatchStatuses.Finished || state.WinnerPlayerId is not null)
            return ResolutionResult.Rejected(state, "对局已经结束，不能回退。", ErrorCodes.MatchFinished);
        if (!CanRewind(state))
            return ResolutionResult.Rejected(state, "当前没有进行中的对局。", ErrorCodes.MatchNotStarted);

        var points = rewindFrame!.Points;
        MatchState nextState = state;
        GameEvent gameEvent;
        if (command is RequestRewindCommand request)
        {
            if (pending is not null)
                return ResolutionResult.Rejected(state, "已有待处理的回退请求。", ErrorCodes.PhaseNotAllowed);
            // Existing journals may already contain an accepted request for a control step.
            if (!points.TryGetValue(request.TargetStep, out var point) || (!point.Selectable && !replayAcceptedCommand))
                return ResolutionResult.Rejected(state, "回退目标不属于当前对局。", ErrorCodes.InvalidTarget);
            nextRequest = new RewindRequestDto(lastEventSequence + 1, playerId, request.TargetStep);
            gameEvent = RewindEvent("REWIND_REQUESTED", "玩家发起回退请求", nextRequest);
        }
        else
        {
            var response = (AnswerRewindCommand)command;
            if (pending is null || pending.RequestStep != response.RequestStep)
                return ResolutionResult.Rejected(state, "回退请求已过期。", ErrorCodes.InvalidTarget);
            if ((response is CancelRewindCommand) != (pending.RequesterPlayerId == playerId))
                return ResolutionResult.Rejected(state, "当前座位不能执行此回退操作。", ErrorCodes.PhaseNotAllowed);

            nextRequest = null;
            if (response is AcceptRewindCommand)
            {
                var point = points[pending.TargetStep];
                nextState = point.Before;
                gameEvent = RewindEvent("MATCH_REWOUND", $"双方同意回退到第 {point.Target.TurnNumber} 回合的第 {point.Target.Step} 步之前",
                    pending, point.Target);
            }
            else if (response is RejectRewindCommand)
                gameEvent = RewindEvent("REWIND_REJECTED", "对手拒绝了回退请求", pending);
            else
                gameEvent = RewindEvent("REWIND_CANCELLED", "玩家撤回了回退请求", pending);
        }

        return new ResolutionResult(true, null, nextState, [gameEvent],
            ResolutionResult.BuildSnapshots(nextState), ResolutionResult.BuildPrompts(nextState));
    }

    private static GameEvent RewindEvent(string kind, string description, RewindRequestDto request, RewindTargetDto? target = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["requestStep"] = request.RequestStep,
            ["requesterPlayerId"] = request.RequesterPlayerId,
            ["targetStep"] = request.TargetStep
        };
        if (target is not null)
        {
            payload["gameNumber"] = target.GameNumber;
            payload["turnNumber"] = target.TurnNumber;
        }
        return new GameEvent(kind, description, payload);
    }

    private async ValueTask<ResolutionResult> RecordCommandAsync(string playerId, string intentId, string commandType,
        JsonElement? raw, TypedCommandInput? typed, ResolutionResult result, CancellationToken cancellationToken,
        RewindRequestDto? nextRequest = null, GameCommand? command = null)
    {
        var startedTick = ServerTick;
        var startedSequence = lastEventSequence;
        var completedSequence = startedSequence + result.Events.Count;
        var next = rewindFrame;
        if (next is not null && result.Accepted)
        {
            var isControl = IsRewindCommandType(commandType);
            var tickOffset = next.TickOffset + (isControl ? state.Tick + 1 - result.State.Tick : 0);
            var points = next.Points;
            var gameNumber = RewindGameNumber(result.State);
            if (gameNumber != RewindGameNumber(state))
                points = points.Clear();
            else if (result.Events.Count != 0 && (CanRewind(state) || CanRewind(result.State)))
            {
                var step = startedSequence + 1;
                var description = RewindDescription(commandType);
                string? actorDescription = null;
                if (command is AcceptRewindCommand && next.Request is { } request)
                    description = $"撤销回到第 {points[request.TargetStep].Target.TurnNumber} 回合的回退";
                else if (command is not null && !isControl)
                    (description, actorDescription) = DescribeRewindCommand(state, playerId, command, result.Events, description);
                var target = new RewindTargetDto(step, gameNumber, state.TurnNumber, playerId, description);
                points = points.Add(step, new RewindPoint(target, state,
                    !isControl || commandType == CommandTypes.AcceptRewind, actorDescription));
            }
            next = new RewindFrame(result.State, tickOffset, nextRequest, points);
        }

        var stored = ProjectRewindResult(result, next, includeTargets: false);
        await journal.RecordAsync(new MatchJournalEntry(RoomId, playerId, intentId, commandType, raw,
            startedTick, stored.ServerTick, startedSequence, completedSequence, stored.Accepted, stored.ErrorMessage,
            stored.State, stored.Events, stored.Snapshots, stored.Prompts, DateTimeOffset.UtcNow)
            { OpeningSeed = openingSeed, ErrorCode = stored.ErrorCode, TypedCommand = raw is null ? typed : null },
            cancellationToken).ConfigureAwait(false);

        if (result.Accepted)
        {
            state = result.State;
            Volatile.Write(ref rewindFrame, next);
        }
        lastEventSequence = completedSequence;
        return next is null ? stored : stored with
        {
            Snapshots = stored.Snapshots.ToDictionary(pair => pair.Key,
                pair => ProjectRewindSnapshot(pair.Value, pair.Key, next, includeTargets: true), StringComparer.Ordinal)
        };
    }

    private ResolutionResult Rejected(MatchState frame, string error, string errorCode)
        => ProjectRewindResult(ResolutionResult.Rejected(frame, error, errorCode), rewindFrame, includeTargets: true);

    private static ResolutionResult ProjectRewindResult(ResolutionResult result, RewindFrame? frame, bool includeTargets)
    {
        if (frame is null) return result;
        var tick = result.State.Tick + frame.TickOffset;
        return result with
        {
            TimelineTick = tick,
            Snapshots = result.Snapshots.ToDictionary(pair => pair.Key,
                pair => ProjectRewindSnapshot(pair.Value, pair.Key, frame, includeTargets), StringComparer.Ordinal),
            Prompts = result.Prompts.ToDictionary(pair => pair.Key,
                pair => ProjectRewindPrompt(pair.Value, frame), StringComparer.Ordinal)
        };
    }

    private static SnapshotDto ProjectRewindSnapshot(SnapshotDto snapshot, string viewerPlayerId,
        RewindFrame? frame, bool includeTargets)
    {
        if (frame is null) return snapshot;
        return snapshot with
        {
            Tick = frame.Tick,
            RewindRequest = frame.Request,
            RewindTargets = includeTargets && CanRewind(frame.State)
                ? frame.Points.Values.Where(point => point.Selectable).Select(point =>
                    point.Target.PlayerId == viewerPlayerId && point.ActorDescription is { } description
                        ? point.Target with { Description = description } : point.Target)
                : null
        };
    }

    private static ActionPromptDto ProjectRewindPrompt(ActionPromptDto prompt, RewindFrame? frame)
    {
        if (frame is null) return prompt;
        var tick = frame.Tick;
        IReadOnlyList<RewindCandidateDto>? candidates = null;
        if (CanRewind(frame.State) && frame.Points.Count != 0)
        {
            candidates = frame.Request switch
            {
                null => [new(CommandTypes.RequestRewind)],
                { } request when request.RequesterPlayerId == prompt.PlayerId
                    => [new(CommandTypes.CancelRewind, request.RequestStep)],
                { } request => [new(CommandTypes.AcceptRewind, request.RequestStep), new(CommandTypes.RejectRewind, request.RequestStep)]
            };
        }
        return prompt with
        {
            PromptId = frame.TickOffset == 0 || prompt.PromptId is null || prompt.SnapshotTick == tick
                ? prompt.PromptId : $"rewind:{tick}:{prompt.PromptId}",
            SnapshotTick = tick,
            RewindCandidates = candidates
        };
    }

    internal static bool IsRewindCommandType(string commandType)
        => commandType is CommandTypes.RequestRewind or CommandTypes.AcceptRewind
            or CommandTypes.RejectRewind or CommandTypes.CancelRewind;

    private static string RewindDescription(string commandType) => commandType switch
    {
        CommandTypes.Ready => "开始对局",
        CommandTypes.Mulligan => "调整起手牌",
        CommandTypes.Pass or CommandTypes.PassPriority or CommandTypes.PassFocus => "让过行动",
        CommandTypes.EndTurn => "结束回合",
        CommandTypes.PlayCard => "打出牌",
        CommandTypes.HideCard => "设置待命牌",
        CommandTypes.RevealCard => "翻开待命牌",
        CommandTypes.MoveUnit => "移动单位",
        CommandTypes.PayCost => "支付费用",
        CommandTypes.ActivateAbility or CommandTypes.LegendAct => "发动能力",
        CommandTypes.TapRune or CommandTypes.RecycleRune => "使用符文",
        CommandTypes.AssembleEquipment => "装配装备",
        CommandTypes.DeclareBattle => "进入战斗",
        CommandTypes.AssignCombatDamage or CommandTypes.AssignEffectDamage => "分配伤害",
        CommandTypes.OrderTriggers => "安排触发顺序",
        CommandTypes.ChooseHandCards or CommandTypes.ChooseCards => "完成选牌",
        CommandTypes.RequestRewind => "发起回退",
        CommandTypes.AcceptRewind => "同意回退",
        CommandTypes.RejectRewind => "拒绝回退",
        CommandTypes.CancelRewind => "撤回请求",
        _ => "执行对局行动"
    };
}
