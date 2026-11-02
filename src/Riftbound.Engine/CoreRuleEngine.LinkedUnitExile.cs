using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkedUnitExileOrigin(UnitEventContext Ability, string StackItemId, string InstanceId,
    ObjectBinding Target, string TargetCardNo, string TargetOwnerId, long CreatedTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkedExileHeldOrigin(UnitEventOtherObject Source, ObjectBinding Battlefield,
    string ControllerId, long CapturedTick);

public sealed partial class CoreRuleEngine
{
    internal const string LinkedExileHeldKind = "PLAY_LINKED_EXILE";

    private static bool IsLinkedExileEffectPlaySource(MatchState state, string player, string source)
        => state.PendingEffectPlay is { } pending && pending.PlayerId == player
            && pending.Sources.ContainsKey(source) && ValidLinkedExileEffectPlay(state, pending);

    private static string LinkedExileInstance(string source, UnitEventContext context)
        => context.Origin is { } origin
            ? $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : $"PRINTED:{source}:{context.SourceGeneration}";

    private static StackResolutionResult ResolveLinkedUnitExile(MatchState state, StackItemState item)
    {
        if (item.TargetObjectIds.SingleOrDefault() is not { } id
            || !StackObjectRules.ReferencesCurrentObject(state, item, id)
            || !UnitEventTargets(state, item).Contains(id)) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var before = cards[id];
        if (!TryRemoveFieldTargetCore(zones, cards, id, true, out var removal)) return NoopStackResolutionResult(state);
        var events = BuildFieldRemovalEvents("关联放逐技能", item, id, removal).ToArray();
        var groups = state.LinkedExiles.ToList();
        // CN186.1: the exiled token vanishes and cannot become a linked card.
        if (cards.TryGetValue(id, out var exiled) && !TokenObjectRules.IsToken(before))
        {
            var context = item.UnitEvent!;
            var instance = LinkedExileInstance(item.SourceObjectId, context);
            groups.RemoveAll(g => g.SourceObjectId == item.SourceObjectId && g.SourceGeneration == context.SourceGeneration
                && g.UnitEventOrigin?.InstanceId == instance);
            var group = new LinkedExileGroup(item.SourceObjectId, context.SourceGeneration, item.ControllerId,
                new Dictionary<string, long> { [id] = exiled.ObjectGeneration }) {
                UnitEventOrigin = new(context, item.StackItemId, instance, new(id, before.ObjectGeneration),
                    exiled.CardNo!, exiled.OwnerId!, RuleChoices.Value?.CompletionTick ?? state.Tick + 1) };
            groups.Add(group);
            events = events.Append(new GameEvent("LINKED_EXILE_CREATED", "已记录来源与放逐卡牌的关联", new Dictionary<string, object?> {
                ["link"] = group })).ToArray();
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events, LinkedExiles = groups };
    }

    internal static bool ValidLinkedUnitExile(MatchState state, LinkedExileGroup group)
    {
        if (group.UnitEventOrigin is not { } origin)
            return state.CardObjects.TryGetValue(group.SourceObjectId, out var legacySource)
                && SpellTriggerSpec(legacySource.CardNo ?? "", TriggerKinds.LegendHighCostSpellBanishCompletion) is not null;
        var context = origin.Ability;
        var prefix = $"ordered-unit-event-{context.CapturedTick}-";
        var suffix = $"-{group.SourceObjectId}-{context.Timing}";
        return context.SourceGeneration == group.SourceGeneration
            && UnitEventSpec(context.CardNo)?.Effect == "BANISH_LINKED_UNIT_HERE"
            && ValidUnitEvent(state, context, UnitEventEffect, group.SourceObjectId, context.CardNo)
            && origin.InstanceId == LinkedExileInstance(group.SourceObjectId, context)
            && origin.StackItemId.StartsWith(prefix, StringComparison.Ordinal)
            && origin.StackItemId.EndsWith(suffix, StringComparison.Ordinal)
            && origin.StackItemId.Length > prefix.Length + suffix.Length
            && int.TryParse(origin.StackItemId[prefix.Length..^suffix.Length], out var ordinal) && ordinal >= 0
            && origin.CreatedTick >= context.CapturedTick && origin.CreatedTick <= state.Tick
            && origin.Target is { Generation: >= 0 } target && group.Objects.Count == 1
            && group.Objects.GetValueOrDefault(target.ObjectId, -1) == target.Generation + 1
            && state.Seats.ContainsKey(origin.TargetOwnerId)
            && state.CardObjects.TryGetValue(target.ObjectId, out var current)
            && current.ObjectGeneration >= target.Generation + 1 && current.OwnerId == origin.TargetOwnerId
            && !TokenObjectRules.IsToken(current)
            && CardBehaviorRegistry.TryGetByCardNo(origin.TargetCardNo, out var permanent)
            && (permanent.PlaysSourceToBaseAsUnit || permanent.PlaysSourceToBaseAsEquipment);
    }

    private static bool ValidLinkedExileHeldContext(MatchState state, HeldTriggerContext context,
        string effect, string controller, string source, string? no)
        => context.Kind == LinkedExileHeldKind && effect == "HOLD_" + LinkedExileHeldKind
            && no == context.CardNo && context.Amount == 0 && context.Deployment is null
            && context.RequiredExactOtherFriendlyBattlefieldUnits is null
            && CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior) && behavior.HeldPlaysLinkedExile
            && context.LinkedExile is { Source: { } captured, Battlefield: { Generation: >= 0 } field } origin
            && captured.Binding.ObjectId == source && captured.Binding.Generation == context.SourceGeneration
            && origin.ControllerId == controller && state.Seats.ContainsKey(controller)
            && origin.CapturedTick > 0 && origin.CapturedTick <= state.Tick
            && controller == state.TurnPlayerId && state.Phase == MatchPhases.TurnStart && state.TurnStartStep == "CHANNEL"
            && field.ObjectId == context.BattlefieldObjectId
            && BattlefieldLocalRules.Battlefield(state, field.ObjectId) is { } battlefield && battlefield.ObjectGeneration >= field.Generation
            && PredictBindingMatches(state, captured.Binding, captured.CardNo, captured.FaceRevision, origin.CapturedTick, captured.TokenFactoryCardNo)
            && AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo)
            && context.RuleTextInstanceId == (context.Origin is { } attached
                ? $"ATTACHED:{attached.Equipment.ObjectId}:{attached.Equipment.Generation}:{attached.Revision}"
                : $"PRINTED:{source}:{context.SourceGeneration}");

    private static bool ValidLinkedExileHeldStack(MatchState state, StackItemState item)
        => item.HeldContext is { } context
            && ValidLinkedExileHeldContext(state, context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
            && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.Destination.Length == 0;

    private static LinkedExileGroup? HeldLinkedGroup(MatchState state, StackItemState item)
        => state.LinkedExiles.SingleOrDefault(g => g.UnitEventOrigin is { } origin
            && g.SourceObjectId == item.SourceObjectId && g.SourceGeneration == item.HeldContext!.SourceGeneration
            && origin.InstanceId == item.HeldContext.RuleTextInstanceId);

    private static StackResolutionResult ResolveLinkedExileHeld(MatchState state, StackItemState item)
    {
        if (!ValidLinkedExileHeldStack(state, item)) throw new InvalidOperationException("Invalid captured linked exile Hold.");
        var result = NoopStackResolutionResult(state);
        if (item.TriggerCost is null || !ValidTriggerCostReceipt(state, item)) return result;
        var group = HeldLinkedGroup(state, item);
        if (item.EffectPlayCompleted || group is null) return result;
        var origin = group.UnitEventOrigin!;
        var binding = group.Objects.Single();
        if (!ValidLinkedUnitExile(state, group)
            || !state.CardObjects.TryGetValue(binding.Key, out var card) || card.ObjectGeneration != binding.Value
            || !state.PlayerZones[origin.TargetOwnerId].Banished.Contains(binding.Key)
            || !CardBehaviorRegistry.TryGetByCardNo(card.CardNo!, out var behavior)
            || !behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment)
            return result;
        // CN204.1.c: self-destruction was paid at confirmation, before responses.
        // The captured controller plays the card, while its owner stays unchanged.
        var pending = new PendingEffectPlayState($"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId,
            item, "BANISHED", new Dictionary<string, long> { [binding.Key] = binding.Value }, true, true,
            0, "ANY", false, IgnoreAllCosts: true) { SourcePlayerId = origin.TargetOwnerId };
        return result with { PendingEffectPlay = pending };
    }

    internal static bool ValidLinkedExileEffectPlay(MatchState state, PendingEffectPlayState pending)
        => pending.Parent.HeldContext?.Kind == LinkedExileHeldKind && ValidLinkedExileHeldStack(state, pending.Parent)
            && pending.Parent.TriggerCost is not null && ValidTriggerCostReceipt(state, pending.Parent)
            && !pending.Parent.EffectPlayCompleted && HeldLinkedGroup(state, pending.Parent) is { UnitEventOrigin: { } origin } group
            && ValidLinkedUnitExile(state, group)
            && pending.ChoiceId == $"EFFECT-PLAY:{state.Tick}:{pending.Parent.StackItemId}"
            && pending.PlayerId == pending.Parent.ControllerId && pending.SourcePlayerId == origin.TargetOwnerId
            && pending.SourceZone == "BANISHED" && pending.IgnoreAllCosts && pending.IgnoreBaseMana && pending.IgnoreBasePower
            && pending.ManaReduction == 0 && pending.DestinationPolicy == "ANY" && !pending.Optional
            && pending.ViewedCardIds is null && pending.RevealedHand is null && pending.SourceReplay is null
            && pending.MaximumUnitPower is null && pending.OriginalLocation is null && pending.OriginalBattlefield is null
            && pending.Sources.Count == 1 && pending.Sources.All(s => group.Objects.GetValueOrDefault(s.Key, -1) == s.Value
                && state.CardObjects.TryGetValue(s.Key, out var card) && card.ObjectGeneration == s.Value
                && card.CardNo == origin.TargetCardNo && card.OwnerId == origin.TargetOwnerId
                && state.PlayerZones[origin.TargetOwnerId].Banished.Contains(s.Key)
                && state.ObjectLocations.GetValueOrDefault(s.Key) == new ObjectLocationState(origin.TargetOwnerId, "BANISHED"));
}
