using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActivationBanishCost(string OwnerId, long BanishedGeneration);

public sealed partial class CoreRuleEngine
{
    private static ActivationBanishCost PayActivationSourceBanish(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        CardObjectState source, string actor, List<GameEvent> events)
    {
        var owner = NonFieldDestinationOwner(zones, source, actor);
        CaptureFieldDeparture(zones, cards, source.ObjectId);
        DetachEquipmentFromRemovedHost(cards, source.ObjectId);
        foreach (var player in zones.Keys.ToArray())
            zones[player] = zones[player] with { Base = RemoveFromZone(zones[player].Base, source.ObjectId),
                Battlefields = RemoveFromZone(zones[player].Battlefields, source.ObjectId) };
        zones[owner] = zones[owner] with { Banished = zones[owner].Banished.Append(source.ObjectId).ToArray() };
        ResetCardOutsidePlay(zones, cards, source.ObjectId, source, owner);
        events.Add(CaptureOwnedBanishObservers(new("CARD_BANISHED", "放逐来源以支付主动技能费用",
            new Dictionary<string, object?> { ["playerId"] = actor, ["sourceObjectId"] = source.ObjectId,
                ["targetObjectId"] = source.ObjectId, ["sourceGeneration"] = source.ObjectGeneration,
                ["ownerPlayerId"] = owner, ["destinationZone"] = "BANISHED", ["reason"] = "ABILITY_COST" }),
            zones, cards, actor, [source.ObjectId]));
        return new(owner, checked(source.ObjectGeneration + 1));
    }

    private static bool ValidActivationBanishCost(MatchState state, SimpleActivationContext context,
        SimpleActivatedEffectSpec spec)
        => (spec.BanishesSourceAsCost ? context.BanishCost is { } cost
                && state.Seats.ContainsKey(cost.OwnerId) && cost.BanishedGeneration == context.Source.Generation + 1
            : context.BanishCost is null)
            && (context.RefusedEffectPlays is null || spec.PlaysAllLinkedArmamentUnits
                && context.RefusedEffectPlays.Count > 0 && context.RefusedEffectPlays.Distinct().Count() == context.RefusedEffectPlays.Count
                && context.RefusedEffectPlays.All(binding => binding.Generation >= 0
                    && state.CardObjects.TryGetValue(binding.ObjectId, out var card) && card.ObjectGeneration >= binding.Generation));

    internal static string ActivationBanishCaptureHash(SimpleActivationContext context, string actor)
        => MatchStateHasher.HashValue(new { Context = context with { CeasedSource = null, RefusedEffectPlays = null }, Actor = actor });

    internal static bool PlaysAllLinkedArmamentUnits(StackItemState item)
        => P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && ability.SimpleEffect?.PlaysAllLinkedArmamentUnits == true;

    private static LinkedExileGroup? ActivatedArmamentExileGroup(MatchState state, StackItemState item)
        => item.SimpleActivation is { } context ? state.LinkedExiles.SingleOrDefault(group => group.ArmamentOrigin is not null
            && group.SourceObjectId == context.Source.ObjectId && group.SourceGeneration == context.Source.Generation) : null;

    private static IReadOnlyDictionary<string, long> CurrentArmamentReplayUnits(MatchState state, StackItemState item)
    {
        var group = ActivatedArmamentExileGroup(state, item);
        if (group is null || !ValidLinkedArmamentExile(state, group)) return new Dictionary<string, long>();
        var refused = item.SimpleActivation!.RefusedEffectPlays ?? [];
        return group.Objects.Where(entry => !refused.Contains(new(entry.Key, entry.Value))
                && group.ArmamentOrigin!.Units.TryGetValue(entry.Key, out var origin)
                && state.PlayerZones[origin.OwnerId].Banished.Contains(entry.Key)
                && state.CardObjects.TryGetValue(entry.Key, out var card) && card.ObjectGeneration == entry.Value
                && !TokenObjectRules.IsToken(card) && card.OwnerId == origin.OwnerId
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior) && behavior.PlaysSourceToBaseAsUnit)
            .ToDictionary();
    }

    private static StackResolutionResult ResolveAllLinkedArmamentUnits(MatchState state, StackItemState item)
    {
        var result = NoopStackResolutionResult(state);
        if (item.EffectPlayCompleted) return result;
        var sources = CurrentArmamentReplayUnits(state, item);
        if (sources.Count == 0) return result;
        return result with { PendingEffectPlay = new($"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId,
            item, "BANISHED", sources, true, true, 0, "", false) {
            SourceOwners = sources.Keys.ToDictionary(id => id, id => state.CardObjects[id].OwnerId!) } };
    }

    internal static bool ValidAllLinkedArmamentUnitPlay(MatchState state, PendingEffectPlayState pending)
    {
        var item = pending.Parent;
        if (!PlaysAllLinkedArmamentUnits(item) || !ValidSimpleActivation(state, item) || item.EffectPlayCompleted
            || pending.PlayerId != item.ControllerId || pending.SourceZone != "BANISHED"
            || !pending.IgnoreBaseMana || !pending.IgnoreBasePower || pending.IgnoreAllCosts || pending.Optional
            || pending.ManaReduction != 0 || pending.DestinationPolicy != "" || pending.SourcePlayerId is not null
            || pending.SourceOwners is null || pending.ViewedCardIds is not null || pending.RevealedHand is not null
            || pending.SourceReplay is not null || pending.MaximumUnitPower is not null
            || pending.OriginalLocation is not null || pending.OriginalBattlefield is not null
            || pending.Sources.Count == 0 || pending.SourceOwners.Count != pending.Sources.Count) return false;
        const string prefix = "EFFECT-PLAY:";
        var suffix = ":" + item.StackItemId;
        if (!pending.ChoiceId.StartsWith(prefix, StringComparison.Ordinal)
            || !pending.ChoiceId.EndsWith(suffix, StringComparison.Ordinal)
            || pending.ChoiceId.Length <= prefix.Length + suffix.Length
            || !long.TryParse(pending.ChoiceId.AsSpan(prefix.Length,
                pending.ChoiceId.Length - prefix.Length - suffix.Length), out var tick)
            || tick <= item.SimpleActivation!.AcquiredTick || tick > state.Tick) return false;
        var expected = CurrentArmamentReplayUnits(state, item);
        return MatchStateHasher.HashValue(pending.Sources) == MatchStateHasher.HashValue(expected)
            && expected.Keys.All(id => pending.SourceOwners.GetValueOrDefault(id) == state.CardObjects[id].OwnerId);
    }

    internal static bool IsForeignLinkedArmamentPlay(MatchState state, string player, string source)
        => state.PendingEffectPlay is { SourceOwners: not null } pending && pending.PlayerId == player
            && pending.Sources.ContainsKey(source) && ValidAllLinkedArmamentUnitPlay(state, pending);
}
