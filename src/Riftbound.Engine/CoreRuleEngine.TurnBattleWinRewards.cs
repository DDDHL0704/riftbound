using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnBattleWinReward(string InstanceId, string CreatingStackItemId, string CardNo,
    string CreatingEffectKind, string ControllerId, ObjectBinding Source, ObjectBinding Unit,
    int TurnNumber, string TurnPlayerId, int ExecutionIndex, long CreatedAtTick, int ExperienceCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UnitTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string TurnBattleWinRewardPrefix = "TURN_BATTLE_WIN_REWARD:";
    internal const string InactiveTurnBattleWinRewardPrefix = "INACTIVE_TURN_BATTLE_WIN_REWARD:";

    private static string TurnBattleWinRewardId(TurnBattleWinReward reward)
        => "turn-battle-win-" + MatchStateHasher.HashValue(reward with { InstanceId = "" });

    internal static string TurnBattleWinRewardMarker(TurnBattleWinReward reward)
        => TurnBattleWinRewardPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(reward));

    internal static TurnBattleWinReward? ReadTurnBattleWinReward(string marker)
    {
        try
        {
            if (!marker.StartsWith(TurnBattleWinRewardPrefix, StringComparison.Ordinal)) return null;
            var reward = JsonSerializer.Deserialize<TurnBattleWinReward>(Convert.FromBase64String(marker[TurnBattleWinRewardPrefix.Length..]));
            return reward is not null && TurnBattleWinRewardMarker(reward) == marker ? reward : null;
        }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException) { return null; }
    }

    internal static bool HasValidTurnBattleWinRewardShape(TurnBattleWinReward reward)
        => reward.Source is { Generation: >= 0 } && reward.Unit is { Generation: >= 0 }
            && !string.IsNullOrWhiteSpace(reward.Source.ObjectId) && !string.IsNullOrWhiteSpace(reward.Unit.ObjectId)
            && !string.IsNullOrWhiteSpace(reward.ControllerId) && !string.IsNullOrWhiteSpace(reward.TurnPlayerId)
            && !string.IsNullOrWhiteSpace(reward.CreatingStackItemId) && reward.ExecutionIndex >= 0
            && reward.CreatedAtTick > 0 && reward.TurnNumber > 0
            && reward.InstanceId == TurnBattleWinRewardId(reward)
            && CardBehaviorRegistry.TryGetByCardNo(reward.CardNo, out var behavior)
            && behavior.EffectKind == reward.CreatingEffectKind && behavior.ExperienceWhenTargetWinsBattleThisTurn > 0
            && behavior.ExperienceWhenTargetWinsBattleThisTurn == reward.ExperienceCount;

    internal static bool ValidTurnBattleWinReward(MatchState state, TurnBattleWinReward reward)
        => HasValidTurnBattleWinRewardShape(reward) && reward.CreatedAtTick <= state.Tick
            && reward.TurnNumber == state.TurnNumber && reward.TurnPlayerId == state.TurnPlayerId
            && state.Seats.ContainsKey(reward.ControllerId)
            && state.CardObjects.TryGetValue(reward.Source.ObjectId, out var source) && source.ObjectGeneration >= reward.Source.Generation
            && (state.UntilEndOfTurnEffects.Contains(InactiveTurnBattleWinRewardPrefix + reward.InstanceId, StringComparer.Ordinal)
                ? IsTurnBattleWinTokenFactory(reward.UnitTokenFactoryCardNo)
                : state.CardObjects.TryGetValue(reward.Unit.ObjectId, out var unit)
                ? unit.ObjectGeneration >= reward.Unit.Generation && unit.TokenFactoryCardNo == reward.UnitTokenFactoryCardNo
                : IsTurnBattleWinTokenFactory(reward.UnitTokenFactoryCardNo));

    private static bool IsTurnBattleWinTokenFactory(string? cardNo)
        => cardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(cardNo, out var token)
            && (token.Tags.Contains(CardObjectTags.UnitCard) || token.Tags.Contains(CardObjectTags.EquipmentCard));

    internal static bool ValidCurrentTurnBattleWinRewards(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(effect => effect is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(TurnBattleWinRewardPrefix, StringComparison.Ordinal)).ToArray();
        var rewards = markers.Select(ReadTurnBattleWinReward).ToArray();
        var inactive = state.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(InactiveTurnBattleWinRewardPrefix, StringComparison.Ordinal))
            .Select(effect => effect[InactiveTurnBattleWinRewardPrefix.Length..]).ToArray();
        return rewards.All(reward => reward is not null && ValidTurnBattleWinReward(state, reward))
            && rewards.Select(reward => reward!.InstanceId).Distinct(StringComparer.Ordinal).Count() == rewards.Length
            && inactive.Distinct(StringComparer.Ordinal).Count() == inactive.Length
            && inactive.All(id => rewards.Any(reward => reward?.InstanceId == id && reward.UnitTokenFactoryCardNo is not null));
    }

    // CN 391-393: a spell creates an independent delayed ability. Its controller
    // and affected incarnation are fixed now, even if either object's control or
    // zone later changes. The private receipt never becomes a public effect ID.
    private static void ScheduleTurnBattleWinReward(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, CardObjectState unit, int executionIndex,
        List<string> effects, List<GameEvent> events)
    {
        if (behavior.ExperienceWhenTargetWinsBattleThisTurn <= 0) return;
        var reward = new TurnBattleWinReward("", item.StackItemId, behavior.CardNo, behavior.EffectKind,
            item.ControllerId, new(item.SourceObjectId, state.CardObjects[item.SourceObjectId].ObjectGeneration),
            new(unit.ObjectId, unit.ObjectGeneration), state.TurnNumber, state.TurnPlayerId, executionIndex,
            Math.Max(state.Tick + 1, RuleChoices.Value?.CompletionTick ?? state.Tick + 1),
            behavior.ExperienceWhenTargetWinsBattleThisTurn, unit.TokenFactoryCardNo);
        reward = reward with { InstanceId = TurnBattleWinRewardId(reward) };
        effects.Add(TurnBattleWinRewardMarker(reward));
        events.Add(new("TURN_BATTLE_WIN_REWARD_CREATED", "已建立本回合单位获胜时的独立延迟奖励",
            new Dictionary<string, object?> {
                ["rewardId"] = reward.InstanceId, ["turnBattleWinRewardHash"] = MatchStateHasher.HashValue(reward),
                ["turnNumber"] = reward.TurnNumber, ["turnPlayerId"] = reward.TurnPlayerId }));
    }

    private static string PublicTurnBattleWinEffectId(string effect)
        => effect.StartsWith(TurnBattleWinRewardPrefix, StringComparison.Ordinal)
            ? ReadTurnBattleWinReward(effect)?.InstanceId ?? "TURN_BATTLE_WIN_REWARD" : effect;

    private static ResolutionResult RetireTurnBattleWinRewardsForTokenDeparture(MatchState before, ResolutionResult result)
    {
        var rewards = before.UntilEndOfTurnEffects.Select(ReadTurnBattleWinReward).OfType<TurnBattleWinReward>()
            .Where(reward => reward.UnitTokenFactoryCardNo is not null).ToArray();
        if (rewards.Length == 0) return result;
        var effects = result.State.UntilEndOfTurnEffects;
        var events = result.Events.ToList();
        foreach (var reward in rewards)
        {
            var inactive = InactiveTurnBattleWinRewardPrefix + reward.InstanceId;
            if (reward.UnitTokenFactoryCardNo is null || !effects.Contains(TurnBattleWinRewardMarker(reward), StringComparer.Ordinal)
                || effects.Contains(inactive, StringComparer.Ordinal)
                || !before.CardObjects.TryGetValue(reward.Unit.ObjectId, out var old) || old.ObjectGeneration != reward.Unit.Generation
                || result.State.CardObjects.ContainsKey(reward.Unit.ObjectId)
                    && !events.Any(entry => entry.Kind == "UNIT_TOKEN_CREATED"
                        && Equals(entry.Payload.GetValueOrDefault("tokenObjectId"), reward.Unit.ObjectId))) continue;
            // A later token may reuse this public object ID and generation. Its
            // creation cannot reactivate the old delay. Already earned skills
            // still retain the immutable original receipt until they resolve.
            effects = AddUntilEndOfTurnEffect(effects, inactive);
            events.Add(new("TURN_BATTLE_WIN_REWARD_RETIRED", "原单位指示物消失，停止其后续获胜监听",
                new Dictionary<string, object?> { ["rewardId"] = reward.InstanceId,
                    ["turnBattleWinRewardHash"] = MatchStateHasher.HashValue(reward) }));
        }
        if (ReferenceEquals(effects, result.State.UntilEndOfTurnEffects)) return result;
        var state = result.State with { UntilEndOfTurnEffects = effects };
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static IEnumerable<CapturedUnitBattleWinDraw> CaptureTurnBattleWinRewards(MatchState state,
        CardObjectState unit, string battlefield, string role, long tick)
    {
        foreach (var reward in state.UntilEndOfTurnEffects.Select(ReadTurnBattleWinReward).OfType<TurnBattleWinReward>())
        {
            if (!ValidTurnBattleWinReward(state, reward) || reward.Unit.ObjectId != unit.ObjectId
                || reward.Unit.Generation != unit.ObjectGeneration
                || state.UntilEndOfTurnEffects.Contains(InactiveTurnBattleWinRewardPrefix + reward.InstanceId, StringComparer.Ordinal)) continue;
            var context = new UnitBattleWinDrawContext(reward.Source, reward.CardNo, reward.ControllerId,
                battlefield, tick, role, DrawCount: 0) { ExperienceCount = reward.ExperienceCount, TurnReward = reward };
            var trigger = new TriggerQueueItemState($"{reward.InstanceId}:win:{tick}", reward.ControllerId,
                reward.Source.ObjectId, UnitBattleWinDrawEffect, "BATTLE_RESULT_DETERMINED") {
                    SourceCardNo = reward.CardNo, EnqueuedAtTick = tick };
            yield return new(trigger, context);
        }
    }
}
