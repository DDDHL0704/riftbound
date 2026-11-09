using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArmamentDeathFact(string DeathId, DestroyedUnitFact Unit,
    LastBreathRepetitionContext? Repetition = null);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ArmamentDeathFact? ArmamentDeath { get; init; }
}

internal static class ArmamentLastBreathRules
{
    internal const string Timing = "LAST_BREATH";
    internal const string BanishEffect = "BANISH_ARMAMENT_DEAD_SOURCE";
    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> Specs = new(() =>
        OfficialArmamentCatalog.All.Select(definition => {
            var text = Regex.Replace(definition.EffectText, "（[^）]*）", "").Trim();
            var effect = Regex.IsMatch(text, @"^\{\{绝念\}\}[—\-－\s:：]*抽一张牌[。.]?$") ? "DRAW"
                : Regex.IsMatch(text, @"^\{\{绝念\}\}[—\-－\s:：]*放逐我[。.]?$") ? BanishEffect : null;
            return (definition.CardNo, Spec: effect is null ? null
                : new UnitEventAbilitySpec(Timing, effect, 1) { GrantedByArmament = true });
        }).Where(row => row.Spec is not null).ToDictionary(row => row.CardNo, row => row.Spec!));

    internal static UnitEventAbilitySpec? Spec(string no) => Specs.Value.GetValueOrDefault(no);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArmamentExiledUnit(UnitEventContext Ability, string StackItemId,
    string CardNo, string OwnerId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkedArmamentExileOrigin(string InstanceId, long CreatedTick,
    IReadOnlyDictionary<string, ArmamentExiledUnit> Units);

public sealed partial class CoreRuleEngine
{
    private static UnitEventContext[] CaptureLastBreathUnitEvents(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState before, string deathId, DestroyedUnitFact fact)
    {
        var frame = RuleChoices.Value?.CurrentDestructionDrawFrame ?? CapturePublicMightFrame(state, zones, cards);
        if (!frame.CardObjects.TryGetValue(before.ObjectId, out var source)
            || source.ObjectGeneration != before.ObjectGeneration) return [];
        var repetitions = CaptureLastBreathRepetitions(state, zones, cards, before);
        return CaptureUnitEvents(frame.PlayerZones, frame.CardObjects, [source.ObjectId], ArmamentLastBreathRules.Timing)
            .SelectMany(observation => repetitions.Select(repetition => observation.Context with {
                ArmamentDeath = new(deathId, fact, repetition) })).ToArray();
    }

    private static void QueueLastBreathUnitEvents(MatchState state, CapturedUnitDeath death,
        List<TriggerQueueItemState> queue, List<GameEvent> events)
    {
        foreach (var context in death.LastBreathUnitEvents)
        {
            var ordinal = RuleChoices.Value!.UnitEventOrdinal++;
            var trigger = UnitEventTrigger(new(death.Unit.ObjectId, death.Unit.ControllerId, context),
                RuleChoices.Value?.CompletionTick ?? state.Tick, ordinal, TriggerTimings.UnitDestroyed);
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
    }

    private static bool ValidArmamentDeath(MatchState state, UnitEventContext context, string source, string? controller)
    {
        if (context.Timing != ArmamentLastBreathRules.Timing) return context.ArmamentDeath is null;
        if (context.ArmamentDeath is not { Unit: { Generation: >= 0 } unit } death
            || string.IsNullOrWhiteSpace(death.DeathId) || !death.DeathId.StartsWith("death-", StringComparison.Ordinal)
            || unit.ObjectId != source || unit.Generation != context.SourceGeneration
            || string.IsNullOrWhiteSpace(unit.OwnerId)
            || string.IsNullOrWhiteSpace(unit.ControllerId) || !state.Seats.ContainsKey(unit.OwnerId)
            || !state.Seats.ContainsKey(unit.ControllerId)
            || controller is not null && controller != unit.ControllerId) return false;
        var instance = context.Origin switch {
            { ArmamentGranted: true, Equipment: { Generation: >= 0 } equipment } armament
                when unit.CardNo == armament.ArmamentHostCardNo => $"ARMAMENT:{equipment.ObjectId}:{equipment.Generation}",
            { ArmamentGranted: true } => null,
            { Equipment: { Generation: >= 0 } equipment } copied
                => $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{copied.Revision}",
            null when unit.CardNo == context.CardNo => $"PRINTED:{source}:{context.SourceGeneration}",
            _ => null
        };
        if (instance is null) return false;
        return (unit.WasToken ? context.TokenFactoryCardNo is { } factoryNo
                && P6TokenFactoryCatalog.TryGetByCardNo(factoryNo, out var factory)
                && factory.Tags.Any(tag => tag is CardObjectTags.UnitCard or CardObjectTags.EquipmentCard)
                : context.TokenFactoryCardNo is null)
            && ValidLastBreathRepetition(state, death.Repetition, new(source, context.SourceGeneration),
            unit.ControllerId, context.CardNo, instance);
    }

    private static string LinkedArmamentInstance(ObjectBinding equipment)
        => $"ARMAMENT:{equipment.ObjectId}:{equipment.Generation}";

    private static string LinkedArmamentKey(LinkedExileGroup group)
        => MatchStateHasher.HashValue(new { group.SourceObjectId, group.SourceGeneration,
            group.ArmamentOrigin!.InstanceId });

    private static StackResolutionResult ResolveArmamentDeathBanish(MatchState state, StackItemState item)
    {
        var result = NoopStackResolutionResult(state);
        var context = item.UnitEvent!;
        var death = context.ArmamentDeath!.Unit;
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var card)
            || card.ObjectGeneration != context.SourceGeneration + 1 || card.OwnerId != death.OwnerId
            || !state.PlayerZones[death.OwnerId].Graveyard.Contains(card.ObjectId)
            || TokenObjectRules.IsToken(card)) return result;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var own = zones[death.OwnerId];
        zones[death.OwnerId] = own with { Graveyard = own.Graveyard.Where(id => id != card.ObjectId).ToArray(),
            Banished = own.Banished.Append(card.ObjectId).Distinct(StringComparer.Ordinal).ToArray() };
        cards[card.ObjectId] = card with { ObjectGeneration = checked(card.ObjectGeneration + 1) };
        var equipment = context.Origin!.Equipment;
        var instance = LinkedArmamentInstance(equipment);
        var prior = state.LinkedExiles.SingleOrDefault(group => group.SourceObjectId == equipment.ObjectId
            && group.SourceGeneration == equipment.Generation && group.ArmamentOrigin?.InstanceId == instance);
        var objects = prior?.Objects.ToDictionary() ?? new Dictionary<string, long>();
        var units = prior?.ArmamentOrigin!.Units.ToDictionary() ?? new Dictionary<string, ArmamentExiledUnit>();
        objects[card.ObjectId] = cards[card.ObjectId].ObjectGeneration;
        units[card.ObjectId] = new(context, item.StackItemId, card.CardNo!, death.OwnerId);
        var group = new LinkedExileGroup(equipment.ObjectId, equipment.Generation, item.ControllerId, objects) {
            ArmamentOrigin = new(instance, RuleChoices.Value?.CompletionTick ?? state.Tick + 1, units) };
        var events = new List<GameEvent> {
            CaptureOwnedBanishObservers(new("CARD_BANISHED", "武装授予的绝念放逐被摧毁的单位",
                new Dictionary<string, object?> { ["sourceObjectId"] = card.ObjectId,
                    ["targetObjectId"] = card.ObjectId, ["playerId"] = item.ControllerId,
                    ["fromZone"] = "GRAVEYARD", ["destinationZone"] = "BANISHED" }),
                zones, cards, death.OwnerId, [card.ObjectId]),
            // The granting equipment may now be in a private zone. The original
            // accepted event binds the authoritative ledger without publishing it.
            new("ARMAMENT_LINKED_EXILE_CREATED", "记录武装与放逐单位的关联",
                new Dictionary<string, object?> { ["instanceHash"] = LinkedArmamentKey(group),
                    ["linkHash"] = MatchStateHasher.HashValue(group) }) };
        return result with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events,
            LinkedExiles = state.LinkedExiles.Where(value => !ReferenceEquals(value, prior)).Append(group).ToArray() };
    }

    internal static bool ValidLinkedArmamentExile(MatchState state, LinkedExileGroup group)
        => group.UnitEventOrigin is null && group.GraveyardOrigin is null
            && group.ArmamentOrigin is { Units: not null } origin && origin.CreatedTick > 0 && origin.CreatedTick <= state.Tick
            && origin.InstanceId == LinkedArmamentInstance(new(group.SourceObjectId, group.SourceGeneration))
            && group.Objects.Count > 0 && group.Objects.Count == origin.Units.Count
            && origin.Units.All(entry => entry.Value is { OwnerId.Length: > 0,
                    Ability: { CardNo.Length: > 0, Origin: { ArmamentGranted: true, Equipment: not null }, ArmamentDeath: { Unit: not null } death } context }
                && context.Timing == ArmamentLastBreathRules.Timing
                && ArmamentLastBreathRules.Spec(context.CardNo)?.Effect == ArmamentLastBreathRules.BanishEffect
                && context.Origin.Equipment == new ObjectBinding(group.SourceObjectId, group.SourceGeneration)
                && death.Unit.ObjectId == entry.Key && death.Unit.Generation == context.SourceGeneration
                && entry.Value.OwnerId == death.Unit.OwnerId && state.Seats.ContainsKey(entry.Value.OwnerId)
                && context.CapturedTick > 0 && context.CapturedTick <= origin.CreatedTick
                && group.Objects.GetValueOrDefault(entry.Key, -1) == context.SourceGeneration + 2
                && state.CardObjects.TryGetValue(entry.Key, out var card) && card.OwnerId == entry.Value.OwnerId
                && card.ObjectGeneration >= group.Objects[entry.Key]
                && (card.ObjectGeneration > group.Objects[entry.Key] || card.CardNo == entry.Value.CardNo));

    internal static string LinkedArmamentExileKey(LinkedExileGroup group) => LinkedArmamentKey(group);
}
