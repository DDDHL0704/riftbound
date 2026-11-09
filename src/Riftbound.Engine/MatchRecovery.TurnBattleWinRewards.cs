namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateTurnBattleWinRewardHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var frames = new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().ToArray();
        if (!frames.Any(frame => frame.UntilEndOfTurnEffects.Any(effect => effect.StartsWith(CoreRuleEngine.TurnBattleWinRewardPrefix, StringComparison.Ordinal))
                || frame.StackItems.Any(item => item.UnitBattleWinDraw?.TurnReward is not null)
                || frame.TriggerQueue.Any(item => item.UnitBattleWinDraw?.TurnReward is not null))
            && !events.Any(entry => entry.Event.Kind == "TURN_BATTLE_WIN_REWARD_CREATED")) return;
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var original = new Dictionary<string, (string Hash, int Turn, string Player, long Tick)>(StringComparer.Ordinal);
        foreach (var entry in events.Where(entry => entry.Sequence > boundary && entry.Event.Kind == "TURN_BATTLE_WIN_REWARD_CREATED"))
        {
            if (!TryReadObjectString(entry.Event.Payload, "rewardId", out var id) || string.IsNullOrWhiteSpace(id)
                || !TryReadObjectString(entry.Event.Payload, "turnBattleWinRewardHash", out var hash) || string.IsNullOrWhiteSpace(hash)
                || !TryReadObjectLong(entry.Event.Payload, "turnNumber", out var turn) || turn is <= 0 or > int.MaxValue
                || !TryReadObjectString(entry.Event.Payload, "turnPlayerId", out var player) || player is null
                || commands.Count(command => command.Accepted && command.CompletedTick == entry.Tick
                    && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence) != 1
                || !original.TryAdd(id, (hash, (int)turn, player, entry.Tick)))
                errors.Add("turn battle-win reward lacks its unique original accepted creation");
        }
        foreach (var frame in frames)
        {
            var rewards = frame.UntilEndOfTurnEffects.Select(CoreRuleEngine.ReadTurnBattleWinReward).OfType<TurnBattleWinReward>().ToArray();
            var expected = original.Where(entry => entry.Value.Turn == frame.TurnNumber
                && entry.Value.Player == frame.TurnPlayerId && entry.Value.Tick <= frame.Tick).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
            if (rewards.Any(reward => !original.TryGetValue(reward.InstanceId, out var birth)
                    || birth.Hash != MatchStateHasher.HashValue(reward) || birth.Tick != reward.CreatedAtTick)
                || frame.Status == MatchStatuses.InProgress && !expected.SetEquals(rewards.Select(reward => reward.InstanceId)))
                errors.Add("turn battle-win rewards differ from their original accepted instruction");
            var retired = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick
                && entry.Event.Kind == "TURN_BATTLE_WIN_REWARD_RETIRED"))
            {
                if (!TryReadObjectString(entry.Event.Payload, "rewardId", out var id) || id is null
                    || !TryReadObjectString(entry.Event.Payload, "turnBattleWinRewardHash", out var hash)
                    || !original.TryGetValue(id, out var birth) || birth.Hash != hash || entry.Tick < birth.Tick
                    || commands.Count(command => command.Accepted && command.CompletedTick == entry.Tick
                        && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence) != 1)
                { errors.Add("inactive battle-win reward lacks its original accepted token departure"); continue; }
                if (birth.Turn != frame.TurnNumber || birth.Player != frame.TurnPlayerId) continue;
                var reward = rewards.FirstOrDefault(reward => reward.InstanceId == id);
                if (!retired.Add(id) || reward?.UnitTokenFactoryCardNo is null
                    || !events.Any(witness => witness.Tick == entry.Tick && witness.Sequence > boundary
                        && (witness.Event.Kind == "TOKEN_CEASED_TO_EXIST" && TryReadObjectString(witness.Event.Payload, "targetObjectId", out var ceased) && ceased == reward.Unit.ObjectId
                            || witness.Event.Kind == "UNIT_TOKEN_CREATED" && TryReadObjectString(witness.Event.Payload, "tokenObjectId", out var created) && created == reward.Unit.ObjectId)))
                    errors.Add("inactive battle-win reward differs from its original token incarnation");
            }
            var currentInactive = frame.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(CoreRuleEngine.InactiveTurnBattleWinRewardPrefix, StringComparison.Ordinal))
                .Select(effect => effect[CoreRuleEngine.InactiveTurnBattleWinRewardPrefix.Length..]);
            if (frame.Status == MatchStatuses.InProgress && !retired.SetEquals(currentInactive))
                errors.Add("inactive battle-win rewards differ from original accepted token departures");
            var births = events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick
                && entry.Event.Kind == "TRIGGER_QUEUED" && TryReadObjectString(entry.Event.Payload, "battleWinContextHash", out _)).ToArray();
            var pending = births.Select(entry => TryReadObjectString(entry.Event.Payload, "triggerId", out var id) ? id : null)
                .OfType<string>().ToHashSet(StringComparer.Ordinal);
            foreach (var entry in events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick
                && entry.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED"))
                if (TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) && id is not null
                    && id.StartsWith("ordered-", StringComparison.Ordinal)) pending.Remove(id[8..]);
            var current = frame.StackItems.Where(item => item.UnitBattleWinDraw?.TurnReward is not null)
                .Select(item => item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal) ? item.StackItemId[8..] : item.StackItemId)
                .Concat(frame.TriggerQueue.Where(item => item.UnitBattleWinDraw?.TurnReward is not null).Select(item => item.TriggerId)).ToArray();
            if (current.Distinct(StringComparer.Ordinal).Count() != current.Length
                || frame.Status == MatchStatuses.InProgress && !pending.SetEquals(current))
                errors.Add("pending delayed battle-win skills differ from their original accepted births");
        }
    }
}
