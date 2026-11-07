using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FriendlyUnitActionOrigin(string Actor, string Action, int? RoleUnitCount = null,
    bool? WasExhausted = null, int? PreviousBoonCount = null, int? ResultingBoonCount = null);

public sealed partial class CoreRuleEngine
{
    private const string FriendlySoloBattleRole = "FRIENDLY_SOLO_BATTLE_ROLE";
    private const string FriendlyUnitReadied = "FRIENDLY_UNIT_READIED";
    private const string FriendlyUnitBoonGranted = "FRIENDLY_UNIT_BOON_GRANTED";
    private const string MightObservedUnit = "MIGHT_OBSERVED_UNIT";
    private const string ReadyObservedUnit = "READY_OBSERVED_UNIT";

    private static string? FriendlyUnitActionTiming(string action) => action switch {
        "ATTACK" or "DEFEND" => FriendlySoloBattleRole,
        "READIED" => FriendlyUnitReadied,
        "BOON_GRANTED" => FriendlyUnitBoonGranted,
        _ => null };

    private static bool IsFriendlyObservedUnitSpec(UnitEventAbilitySpec spec)
        => spec.Timing is FriendlySoloBattleRole or FriendlyUnitReadied or FriendlyUnitBoonGranted;

    private static UnitEventObservation[] CaptureFriendlyUnitActionObservers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState unit, FriendlyUnitActionOrigin origin)
    {
        if (FriendlyUnitActionTiming(origin.Action) is not { } timing || !IsFaceUpNonStandbyUnit(unit)
            || !IsObjectOnField(zones, unit.ObjectId)
            || EffectiveFieldControllerId(zones, unit.ObjectId, unit) != origin.Actor) return [];
        var sources = PublicStaticAuraSourceObjectIds(zones).Where(id => cards.TryGetValue(id, out var source)
            && EffectiveFieldControllerId(zones, id, source) == origin.Actor);
        return CaptureUnitEvents(zones, cards, sources, timing).Select(observation => observation with {
            Context = observation.Context with {
                Other = new(new(unit.ObjectId, unit.ObjectGeneration), unit.CardNo!,
                    unit.CopyFaceTransitions?.Count ?? 0, unit.TokenFactoryCardNo),
                FriendlyUnitAction = origin } }).ToArray();
    }

    private static UnitEventObservation[] CaptureSoloRoleObservers(MatchState state, IEnumerable<string> ids, string role)
    {
        if (role is not ("ATTACK" or "DEFEND")) return [];
        var observations = new List<UnitEventObservation>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal)) {
            if (!state.CardObjects.TryGetValue(id, out var unit) || !IsFaceUpNonStandbyUnit(unit)
                || !state.ObjectLocations.TryGetValue(id, out var at) || at.Zone != "BATTLEFIELD" || at.BattlefieldObjectId is not { Length: > 0 }
                || (role == "ATTACK" ? !unit.IsAttacking : !unit.IsDefending)) continue;
            // Count the entire current side, including earlier participants,
            // after this instruction has assigned every new battle role.
            var count = state.CardObjects.Values.Count(other => IsFaceUpNonStandbyUnit(other)
                && IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, other.ObjectId, at.BattlefieldObjectId!)
                && (role == "ATTACK" ? other.IsAttacking : other.IsDefending));
            if (count != 1) continue;
            var player = EffectiveFieldControllerId(state.PlayerZones, id, unit);
            observations.AddRange(CaptureFriendlyUnitActionObservers(state.PlayerZones, state.CardObjects, unit,
                new(player, role, RoleUnitCount: count)));
        }
        return observations.ToArray();
    }

    private static bool TryGrantBoonForInstruction(CardObjectState before,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string actor, out CardObjectState after)
    {
        if (!BoonRules.TryGrant(before, zones, cards, out after)) return false;
        if (RuleChoices.Value is { } frame)
            frame.InstructionUnitEvents.AddRange(CaptureFriendlyUnitActionObservers(zones, cards, before,
                new(actor, "BOON_GRANTED", PreviousBoonCount: BoonRules.Count(before), ResultingBoonCount: BoonRules.Count(after)))
                .Select(observation => observation with { InstructionEventKind = "BOON_GRANTED" }));
        return true;
    }

    private static bool ValidFriendlyUnitActionContext(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string? controller)
    {
        if (!IsFriendlyObservedUnitSpec(spec)) return context.FriendlyUnitAction is null;
        if (context.FriendlyUnitAction is not { } action || string.IsNullOrWhiteSpace(action.Actor) || !state.Seats.ContainsKey(action.Actor)
            || controller is not null && controller != action.Actor
            || FriendlyUnitActionTiming(action.Action) != spec.Timing
            || context.Other is not { Binding.Generation: >= 0, FaceRevision: >= 0 } other
            || string.IsNullOrWhiteSpace(other.Binding.ObjectId) || string.IsNullOrWhiteSpace(other.CardNo)
            || !PredictBindingMatches(state, other.Binding, other.CardNo, other.FaceRevision,
                context.CapturedTick, other.TokenFactoryCardNo)) return false;
        return spec.Timing switch {
            FriendlySoloBattleRole => spec.Effect == MightObservedUnit && action.RoleUnitCount == 1
                && action.WasExhausted is null && action.PreviousBoonCount is null && action.ResultingBoonCount is null,
            FriendlyUnitReadied => spec.Effect == MightObservedUnit && action.WasExhausted == true
                && action.RoleUnitCount is null && action.PreviousBoonCount is null && action.ResultingBoonCount is null,
            FriendlyUnitBoonGranted => spec.Effect == ReadyObservedUnit && action.PreviousBoonCount is >= 0
                && action.ResultingBoonCount is >= 1 && (long)action.ResultingBoonCount == (long)action.PreviousBoonCount + 1
                && action.RoleUnitCount is null && action.WasExhausted is null,
            _ => false };
    }

    private static StackResolutionResult ResolveObservedFriendlyUnit(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        if (LeadingTriggerCost(item) is not null && item.TriggerCost is null) return NoopStackResolutionResult(state);
        var context = item.UnitEvent!; var binding = context.Other!.Binding;
        if (!state.CardObjects.TryGetValue(binding.ObjectId, out var unit) || unit.ObjectGeneration != binding.Generation
            || !IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(state.PlayerZones, unit.ObjectId))
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(); var events = new List<GameEvent>();
        var behavior = new CardBehaviorDefinition(context.CardNo, "友方单位事件触发", 0, item.EffectKind, 0, 0,
            PowerModifierAmount: spec.Amount, ReadiesTarget: spec.Effect == ReadyObservedUnit);
        // Friendship belongs to the captured event. This instruction neither
        // chooses a target nor rechecks the recipient's current controller.
        if (spec.Effect == MightObservedUnit) {
            using var instruction = new MightInstructionScope(state, zones, cards, events);
            cards[unit.ObjectId] = ApplyPowerModifier(state, unit, behavior, item, unit.ObjectId, spec.Amount, out var changed);
            events.Add(changed);
        }
        else if (spec.Effect == ReadyObservedUnit) {
            cards[unit.ObjectId] = ApplyReadyState(unit, behavior, item, unit.ObjectId, out var ready, zones, cards);
            if (ready is not null) events.Add(ready);
        }
        else throw new InvalidOperationException("Invalid observed-unit instruction.");
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
