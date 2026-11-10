using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExemptsControllerIfExperiencePaid { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredUnitDestructionScope { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string EachPlayerDestroyUnitEffect = "EACH_PLAYER_DESTROY_OWN_UNIT";
    internal const string OpponentsDestructionScope = "OPPONENTS";
    internal const string DefendingPlayerHereDestructionScope = "DEFENDING_PLAYER_HERE";

    private static IEnumerable<string> RequiredDestructionPlayerOrder(MatchState state)
        => new[] { state.TurnPlayerId }.Concat(PlayerIdsStartingAfter(state, state.TurnPlayerId)
            .Where(player => player != state.TurnPlayerId));

    private static StackResolutionResult ResolveEachPlayerDestroyUnit(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        var origin = item.UnitEvent!.RequiredUnitDestruction;
        var players = RequiredDestructionPlayerOrder(state).Where(player => spec.RequiredUnitDestructionScope switch {
            OpponentsDestructionScope => player != item.ControllerId,
            DefendingPlayerHereDestructionScope => player == origin!.DefendingPlayerId,
            _ => !(player == item.ControllerId && spec.ExemptsControllerIfExperiencePaid
                && item.UnitEvent.SourceOptionalExperiencePaid) });
        return ResolveRequiredUnitDestructions(state, item, players,
            spec.RequiredUnitDestructionScope == DefendingPlayerHereDestructionScope ? origin!.Battlefield : null);
    }

    private static StackResolutionResult ResolveEachPlayerDestructionSpell(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, bool deferCompletion)
    {
        var result = ResolveRequiredUnitDestructions(state, item, RequiredDestructionPlayerOrder(state));
        var zones = result.PlayerZones.ToDictionary(); var cards = result.CardObjects.ToDictionary();
        var events = result.Events.ToList();
        if (!deferCompletion) CompleteSpellSource(zones, cards, item, behavior, events);
        return result with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(result.ObjectLocations ?? state.ObjectLocations, zones) };
    }

    private static StackResolutionResult ResolveRequiredUnitDestructions(MatchState state, StackItemState item,
        IEnumerable<string> players, ObjectBinding? battlefield = null)
    {
        if (battlefield is not null
            && BattlefieldLocalRules.Battlefield(state, battlefield.ObjectId)?.ObjectGeneration != battlefield.Generation)
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var pools = state.RunePools; var events = new List<GameEvent>(); var triggers = new List<TriggerQueueItemState>();
        var destroyedOwners = new List<string>();
        // CN303.2.a /355.10.e /412.1: each player chooses during resolution,
        // in turn order, and is responsible for their own destruction action.
        foreach (var player in players)
        {
            var current = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
            var legal = FriendlyDestructionChoices(current, player, CardObjectTags.UnitCard)
                .Where(card => battlefield is null || IsObjectLocatedAtBattlefield(zones, current.ObjectLocations,
                    card.ObjectId, battlefield.ObjectId)).ToArray();
            if (legal.Length == 0) continue;
            var id = RuleChoices.Value!.Choose(player, "选择必须摧毁的一名自己控制的单位。",
                legal.Select(card => new RuleChoiceOption(card.ObjectId,
                    card.CardNo is { } no ? OfficialCardSourceIdentityGroups.DisplayName(no) : "单位", [card.ObjectId])).ToArray(),
                kind: EachPlayerDestroyUnitEffect, preview: current);
            var removal = ResolveFieldDestructions(current, zones, cards, item, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.Concat(destroyedOwners).ToHashSet(), pools,
                objectLocations: current.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { id },
                explicitDestroyReason: "SKILL_EFFECT", responsiblePlayers: new Dictionary<string, string> { [id] = player });
            events.AddRange(removal.Events); triggers.AddRange(removal.TriggerQueue);
            pools = removal.RunePools; destroyedOwners.AddRange(removal.DestroyedUnitOwnerIds);
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            TriggerQueue = triggers, DestroyedUnitOwnerIds = destroyedOwners, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
