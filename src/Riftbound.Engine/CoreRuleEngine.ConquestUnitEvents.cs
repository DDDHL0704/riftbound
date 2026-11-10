using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore] public bool FirstConquestOfTurn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinimumAssignedOverkill { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FirstConquestTurn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AssignedOverkill { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private const string ConquestEquipmentBoon = "DESTROY_EQUIPMENT_BOON_SELF";
    private const string ConquestCallRune = "CALL_RUNE";
    internal const string FirstConquestUsePrefix = "FIRST_CONQUEST_SKILL:";

    private static readonly IReadOnlySet<string> CapturedConquestInstructionKinds = new HashSet<string>(StringComparer.Ordinal) {
        TriggerKinds.UnitConquestDrawOneOrCallRune, TriggerKinds.UnitConquestReadySelfOncePerTurn,
        TriggerKinds.UnitConquestGrantFriendlyBoon, TriggerKinds.UnitConquestFriendlyPowerUntilEndOfTurn,
        TriggerKinds.UnitConquestDestroyEquipmentGrantSelfBoon };

    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> ConquestInstructionSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.Select(card => (
            card.CardNo, Trigger: UnitConquestTriggerSpecRules.TriggersForCard(card.CardNo)
                .FirstOrDefault(t => CapturedConquestInstructionKinds.Contains(t.Kind)
                    && UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger(t))))
            .Where(row => row.Trigger is not null).ToDictionary(row => row.CardNo, row => row.Trigger!.Kind switch {
                TriggerKinds.UnitConquestDrawOneOrCallRune => new("CONQUEST", "MODE", Modes: [
                    new("DRAW", "抽一张牌", "DRAW", row.Trigger.DrawCount ?? 1),
                    new("CALL_RUNE", "召出一枚休眠符文", ConquestCallRune, row.Trigger.RuneCallCount ?? 1)]),
                TriggerKinds.UnitConquestReadySelfOncePerTurn => new UnitEventAbilitySpec("CONQUEST", "READY_SELF") {
                    FirstConquestOfTurn = row.Trigger.OncePerTurn == true },
                TriggerKinds.UnitConquestGrantFriendlyBoon => new("CONQUEST", "BOON_FRIENDLY"),
                TriggerKinds.UnitConquestFriendlyPowerUntilEndOfTurn => new("CONQUEST", "MIGHT_FRIENDLY", row.Trigger.PowerDelta ?? 0),
                _ => new("CONQUEST", ConquestEquipmentBoon, Optional: true)
            }, StringComparer.Ordinal));

    private static UnitEventAbilitySpec? ConquestInstructionSpec(string no)
        => ConquestInstructionSpecs.Value.GetValueOrDefault(no);

    // CN383.1: the ordinal belongs to the unit's event. Copies of its printed
    // skill share that event; acquiring another copy later cannot reset it.
    internal static string FirstConquestUse(string source, UnitEventContext context)
        => FirstConquestUsePrefix + MatchStateHasher.HashValue(new ObjectBinding(source, context.SourceGeneration));

    private static IEnumerable<UnitEventObservation> CaptureConquestEventObservations(MatchState state, IEnumerable<string> units, int assignedOverkill = 0)
        => CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, "CONQUEST")
            .Where(o => UnitEventSpecForContext(o.Context)?.MinimumAssignedOverkill is not { } minimum
                || assignedOverkill >= minimum)
            .Where(o => UnitEventSpec(o.Context.CardNo)?.FirstConquestOfTurn != true
                || !state.UntilEndOfTurnEffects.Contains(FirstConquestUse(o.Source, o.Context), StringComparer.Ordinal))
            .Select(o => o with { Context = o.Context with {
                FirstConquestTurn = UnitEventSpec(o.Context.CardNo)?.FirstConquestOfTurn == true ? state.TurnNumber : null,
                AssignedOverkill = UnitEventSpecForContext(o.Context)?.MinimumAssignedOverkill is not null ? assignedOverkill : null } });

    private static ResolutionResult RecordFirstConquestUses(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var uses = result.Events.Where(e => e.Kind == "TRIGGER_QUEUED")
            .Select(e => e.Payload.GetValueOrDefault("firstConquestUse") as string).OfType<string>()
            .Distinct(StringComparer.Ordinal).Where(use => !result.State.UntilEndOfTurnEffects.Contains(use, StringComparer.Ordinal)).ToArray();
        if (uses.Length == 0) return result;
        var state = result.State with { UntilEndOfTurnEffects = result.State.UntilEndOfTurnEffects.Concat(uses).ToArray() };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    // New instruction families use the same target confirmation and protection
    // pipeline. Existing families retain their shared target predicates.
    private static string[] UnitEventInstructionTargets(MatchState state, StackItemState item)
        => SelectedUnitEventSpec(item.UnitEvent!)?.Effect == ConquestEquipmentBoon
            ? state.CardObjects.Values.Where(c => !c.IsFaceDown && c.Tags.Contains(CardObjectTags.EquipmentCard)
                && IsObjectOnField(state.PlayerZones, c.ObjectId)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
                .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray()
            : UnitEventTargets(state, item);

    private static StackResolutionResult? ResolveConquestUnitEventInstruction(MatchState state, StackItemState item)
    {
        var spec = SelectedUnitEventSpec(item.UnitEvent!)!;
        if (spec.Effect is not (ConquestCallRune or ConquestEquipmentBoon)) return null;
        if (!ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId)
            || spec.Modes is not null) throw new InvalidOperationException("Invalid confirmed conquest instruction.");
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        if (spec.Effect == ConquestCallRune) {
            var call = CallRunes(zones, cards, item.ControllerId, spec.Amount);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                Events = [new("RUNES_CALLED", "征服技能召出休眠符文", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = source,
                    ["count"] = call.CalledRuneObjectIds.Count, ["runeObjectIds"] = call.CalledRuneObjectIds.ToArray(),
                    ["reason"] = item.EffectKind })] };
        }
        if (item.TargetObjectIds.SingleOrDefault() is not { } gear
            || !StackObjectRules.ReferencesCurrentObject(state, item, gear)
            || !UnitEventInstructionTargets(state, item).Contains(gear)) return NoopStackResolutionResult(state);
        var locations = state.ObjectLocations.ToDictionary();
        var destruction = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
            explicitDestroyObjectIds: new HashSet<string> { gear }, explicitDestroyReason: "SKILL_EFFECT",
            objectLocations: locations);
        var events = destruction.Events.ToList();
        if (destruction.DestroyedObjectIds.Contains(gear)
            && cards.TryGetValue(item.SourceObjectId, out var host) && host.ObjectGeneration == item.UnitEvent!.SourceGeneration
            && IsFaceUpNonStandbyUnit(host) && IsObjectOnField(zones, host.ObjectId))
            GrantLegendBoon(cards, host.ObjectId, item.ControllerId, host.ObjectId, item.EffectKind, events, zones);
        // Complete the dependent boon instruction before state-based cleanup:
        // losing an armament's might must not kill a host saved by this boon.
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, destruction.RunePools,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn:
                state.DestroyedUnitOwnerIdsThisTurn.Concat(destruction.DestroyedUnitOwnerIds));
        events.AddRange(cleanup.Events);
        for (var i = 0; i < events.Count; i++)
            if (Equals(events[i].Payload.GetValueOrDefault("sourceObjectId"), item.SourceObjectId))
                events[i] = events[i] with { Payload = new Dictionary<string, object?>(events[i].Payload) { ["sourceObjectId"] = source } };
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = cleanup.RunePools,
            TriggerQueue = destruction.TriggerQueue.Concat(cleanup.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIds = destruction.DestroyedUnitOwnerIds.Concat(cleanup.DestroyedUnitOwnerIds).Distinct().ToArray() };
    }
}
