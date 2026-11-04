using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkedGraveyardExileOrigin(string CardNo, ObjectBinding Source, long FaceRevision,
    string StackItemId, string InstanceId, long CreatedTick, IReadOnlyDictionary<string, long> GraveyardObjects);

public sealed partial class CoreRuleEngine
{
    internal const string LinkedGraveyardPlayAbility = "CURSED_SARCOPHAGUS_EXHAUST_PLAY_LINKED_UNIT";

    private static StackResolutionResult CaptureLinkedGraveyardExile(MatchState state, StackItemState item,
        StackResolutionResult result)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            || !behavior.BanishesAllFriendlyGraveyardUnits
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)) return result;
        var ids = result.Events.Where(e => e.Kind == "CARDS_BANISHED"
                && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), item.SourceObjectId))
            .SelectMany(e => e.Payload.GetValueOrDefault("cardIds") as IReadOnlyList<string> ?? [])
            .Where(id => state.PlayerZones[item.ControllerId].Graveyard.Contains(id)
                && result.PlayerZones[item.ControllerId].Banished.Contains(id)
                && result.CardObjects.ContainsKey(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return result;
        var cards = result.CardObjects.ToDictionary();
        var before = ids.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        foreach (var id in ids)
            cards[id] = cards[id] with { ObjectGeneration = Math.Max(cards[id].ObjectGeneration, checked(before[id] + 1)) };
        // The play trigger belongs to the object that entered. A response can
        // already have returned the physical equipment before this resolves.
        var binding = PlayInstructionSource(state, item) ?? item.PermanentPlaySource
            ?? new ObjectBinding(source.ObjectId, source.ObjectGeneration);
        var instance = $"PRINTED:{binding.ObjectId}:{binding.Generation}";
        var group = new LinkedExileGroup(binding.ObjectId, binding.Generation, item.ControllerId,
            ids.ToDictionary(id => id, id => cards[id].ObjectGeneration)) {
            GraveyardOrigin = new(item.CardNo, binding, source.CopyFaceTransitions?.Count ?? 0,
                item.StackItemId, instance, RuleChoices.Value?.CompletionTick ?? state.Tick + 1, before) };
        var groups = (result.LinkedExiles ?? state.LinkedExiles).Where(g => g.SourceObjectId != group.SourceObjectId
            || g.SourceGeneration != group.SourceGeneration || g.GraveyardOrigin?.InstanceId != instance).Append(group).ToArray();
        return result with { CardObjects = cards, LinkedExiles = groups,
            Events = result.Events.Append(new GameEvent("LINKED_EXILE_CREATED", "已记录打出技能的关联放逐", new Dictionary<string, object?> {
                ["link"] = group })).ToArray() };
    }

    internal static bool ValidLinkedGraveyardExile(MatchState state, LinkedExileGroup group)
        => group.UnitEventOrigin is null && group.GraveyardOrigin is { } origin
            && origin.CardNo == "UNL-148/219" && origin.Source == new ObjectBinding(group.SourceObjectId, group.SourceGeneration)
            && origin.FaceRevision >= 0 && origin.CreatedTick > 0 && origin.CreatedTick <= state.Tick
            && !string.IsNullOrWhiteSpace(origin.StackItemId)
            && origin.InstanceId == $"PRINTED:{group.SourceObjectId}:{group.SourceGeneration}"
            && group.Objects.Count > 0 && group.Objects.Count == origin.GraveyardObjects.Count
            && state.Seats.ContainsKey(group.ControllerId)
            && state.CardObjects.TryGetValue(group.SourceObjectId, out var source)
            && source.ObjectGeneration >= group.SourceGeneration
            && (source.ObjectGeneration != group.SourceGeneration
                || CopyFaceTimeline.FaceAt(source, origin.FaceRevision, origin.CreatedTick) == origin.CardNo)
            && group.Objects.All(p => origin.GraveyardObjects.TryGetValue(p.Key, out var prior) && prior >= 0
                && p.Value == prior + 1 && state.CardObjects.TryGetValue(p.Key, out var card)
                && card.ObjectGeneration >= p.Value);

    private static LinkedExileGroup? ActivatedGraveyardExileGroup(MatchState state, StackItemState item)
        => item.SimpleActivation is { } context ? state.LinkedExiles.SingleOrDefault(g => g.GraveyardOrigin is { } origin
            && g.SourceObjectId == context.Source.ObjectId && g.SourceGeneration == context.Source.Generation
            && origin.InstanceId == context.RuleTextInstanceId) : null;

    private static IReadOnlyDictionary<string, long> CurrentLinkedGraveyardUnits(MatchState state, LinkedExileGroup group)
        => group.Objects.Where(p => state.PlayerZones[group.ControllerId].Banished.Contains(p.Key)
            && state.CardObjects.TryGetValue(p.Key, out var card) && card.ObjectGeneration == p.Value
            && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior) && behavior.PlaysSourceToBaseAsUnit)
            .ToDictionary();

    private static StackResolutionResult ResolveLinkedGraveyardExilePlay(MatchState state, StackItemState item)
    {
        var result = NoopStackResolutionResult(state);
        if (item.EffectPlayCompleted || ActivatedGraveyardExileGroup(state, item) is not { } group
            || !ValidLinkedGraveyardExile(state, group)) return result;
        var sources = CurrentLinkedGraveyardUnits(state, group);
        if (sources.Count == 0) return result;
        return result with { PendingEffectPlay = new($"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId,
            item, "BANISHED", sources, false, false, 0, "", false) {
            SourcePlayerId = group.ControllerId == item.ControllerId ? null : group.ControllerId } };
    }

    internal static bool ValidLinkedGraveyardExilePlay(MatchState state, PendingEffectPlayState pending)
    {
        var item = pending.Parent;
        var suffix = ":" + item.StackItemId;
        return item.SimpleActivation?.AbilityId == LinkedGraveyardPlayAbility && ValidSimpleActivation(state, item)
            && !item.EffectPlayCompleted && ActivatedGraveyardExileGroup(state, item) is { } group
            && ValidLinkedGraveyardExile(state, group)
            && pending.ChoiceId.StartsWith("EFFECT-PLAY:", StringComparison.Ordinal)
            && pending.ChoiceId.EndsWith(suffix, StringComparison.Ordinal)
            && pending.ChoiceId.Length > "EFFECT-PLAY:".Length + suffix.Length
            && long.TryParse(pending.ChoiceId.AsSpan("EFFECT-PLAY:".Length,
                pending.ChoiceId.Length - "EFFECT-PLAY:".Length - suffix.Length), out var tick)
            && tick > item.SimpleActivation.AcquiredTick && tick <= state.Tick
            && pending.PlayerId == item.ControllerId
            && pending.SourcePlayerId == (group.ControllerId == item.ControllerId ? null : group.ControllerId)
            && pending.SourceZone == "BANISHED" && !pending.IgnoreAllCosts && !pending.IgnoreBaseMana && !pending.IgnoreBasePower
            && pending.ManaReduction == 0 && pending.DestinationPolicy == "" && !pending.Optional
            && pending.ViewedCardIds is null && pending.RevealedHand is null && pending.SourceReplay is null
            && pending.MaximumUnitPower is null && pending.OriginalLocation is null && pending.OriginalBattlefield is null
            && pending.Sources.Count > 0
            && MatchStateHasher.HashValue(pending.Sources) == MatchStateHasher.HashValue(CurrentLinkedGraveyardUnits(state, group));
    }
}
