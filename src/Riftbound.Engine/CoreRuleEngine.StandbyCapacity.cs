using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string StandbyCapacityChoiceKind = "STANDBY_CAPACITY_REDUCTION";

    private static ResolutionResult CollectStandbyCapacityCleanup(ResolutionResult result)
    {
        var cleanup = EnforceStandbyCapacity(result with { Events = [] });
        if (ReferenceEquals(cleanup.State, result.State)) return result;
        var collected = CollectResolutionEffects(result.State, cleanup, result.Events.Count);
        return collected with { Events = result.Events.Concat(collected.Events).ToArray() };
    }

    // CN 107.3.b.2: the controller chooses exactly the excess. Collect all
    // private decisions before moving any chosen card into its public graveyard.
    private static ResolutionResult EnforceStandbyCapacity(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var state = result.State;
        var chosen = new List<string>();
        foreach (var player in state.Seats.Keys.OrderBy(p => p == state.TurnPlayerId ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal))
            foreach (var field in BattlefieldLocalRules.ControlledBy(state, player).OrderBy(f => f.ObjectId, StringComparer.Ordinal))
            {
                var remaining = StandbyCapacityRules.Occupants(state, field.ObjectId).ToList();
                var excess = remaining.Count - StandbyCapacityRules.Capacity(state, player, field.ObjectId);
                for (var index = 0; index < excess; index++)
                {
                    var selected = RuleChoices.Value!.Choose(player, "待命容量下降，请选择放进废牌堆的待命卡牌。",
                        remaining.Select((id, slot) => new RuleChoiceOption("STANDBY:" + slot,
                            state.CardObjects[id].CardNo is { } no ? OfficialCardSourceIdentityGroups.DisplayName(no) : "待命卡牌", [id])).ToArray(),
                        kind: StandbyCapacityChoiceKind, preview: state);
                    var slot = int.Parse(selected["STANDBY:".Length..], System.Globalization.CultureInfo.InvariantCulture);
                    chosen.Add(remaining[slot]);
                    remaining.RemoveAt(slot);
                }
            }
        if (chosen.Count == 0) return result;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = result.Events.ToList();
        foreach (var id in chosen)
        {
            var card = cards[id];
            var player = card.OwnerId!;
            var field = locations[id].BattlefieldObjectId;
            foreach (var seat in zones.Keys.ToArray())
                zones[seat] = zones[seat] with { Battlefields = RemoveFromZone(zones[seat].Battlefields, id) };
            zones[player] = AddNonDeckGraveyardEntries(zones, cards, player, zones[player], [id]);
            var destination = zones[player].Banished.Contains(id) ? "BANISHED" : "GRAVEYARD";
            cards[id] = card with { IsFaceDown = false, IsAttacking = false, IsDefending = false,
                IsExhausted = false, Damage = 0, DamageMarkedByPlayerIds = null, ControllerId = player,
                StandbyPlacement = null, LastStandbyPlay = null };
            locations[id] = new(player, destination);
            events.Add(new("STANDBY_CARD_REMOVED", "将超出待命容量的卡牌放进废牌堆", new Dictionary<string, object?> {
                ["playerId"] = player, ["targetObjectId"] = id, ["battlefieldObjectId"] = field,
                ["destinationZone"] = destination, ["reason"] = StandbyCapacityChoiceKind }));
        }
        var next = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        return result with { State = next, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }
}
