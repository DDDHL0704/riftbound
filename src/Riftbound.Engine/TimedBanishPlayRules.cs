using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TimedBanishPlayGrant(string Id, string ControllerId, int CreatedTurnNumber, long CreatedTick,
    string ResolvingStackItemId, MovementObserverContext? Movement, ObjectBinding Card, string CardNo,
    string Kind = "MOVEMENT", int? ExpiringTurnNumber = null);

internal static class TimedBanishPlayRules
{
    internal static string Id(TimedBanishPlayGrant grant)
        => "TIMED-BANISH-PLAY:" + MatchStateHasher.HashValue(new {
            grant.ControllerId, grant.CreatedTurnNumber, grant.CreatedTick, grant.ResolvingStackItemId,
            grant.Movement, grant.Card, grant.CardNo, grant.Kind });

    private static bool CardRemainsBanished(MatchState state, TimedBanishPlayGrant grant)
        => state.PlayerZones.TryGetValue(grant.ControllerId, out var zones) && zones.Banished.Contains(grant.Card.ObjectId)
            && state.CardObjects.TryGetValue(grant.Card.ObjectId, out var card)
            && card.ObjectGeneration == grant.Card.Generation && card.CardNo == grant.CardNo
            && card.OwnerId == grant.ControllerId && !card.IsFaceDown && !TokenObjectRules.IsToken(card)
            && state.ObjectLocations.GetValueOrDefault(grant.Card.ObjectId) == new ObjectLocationState(grant.ControllerId, "BANISHED");

    internal static bool Valid(MatchState state, TimedBanishPlayGrant? grant)
        => grant is { Card: { Generation: > 0 } }
            && state.Seats.ContainsKey(grant.ControllerId) && grant.CreatedTurnNumber > 0
            && grant.CreatedTurnNumber <= state.TurnNumber && grant.CreatedTick > 0 && grant.CreatedTick <= state.Tick
            && !string.IsNullOrWhiteSpace(grant.ResolvingStackItemId) && grant.Id == Id(grant)
            && (grant.Kind == "RAD012"
                ? grant.Movement is null
                : grant.Kind == "MOVEMENT" && grant.Movement is { Kind: TriggerKinds.UnitOrdinalMovementBanishTopPlay } movement
                    && CoreRuleEngine.ValidMovementObserver(state, movement, CoreRuleEngine.MovementObserverEffect,
                        grant.ControllerId, movement.Unit.ObjectId, movement.CardNo)
                    && state.Seats.ContainsKey(movement.Unit.MovingPlayerId)
                    && CoreRuleEngine.ValidMovementInstanceId(movement, grant.ResolvingStackItemId, stack: true))
            && (grant.ExpiringTurnNumber is { } expiry
                ? expiry >= grant.CreatedTurnNumber && expiry == state.TurnNumber && state.TurnPlayerId == grant.ControllerId
                    && state.Phase != MatchPhases.TurnEnd
                : state.TurnPlayerId != grant.ControllerId
                    || state.Phase == MatchPhases.TurnEnd && state.TurnNumber == grant.CreatedTurnNumber)
            && CardRemainsBanished(state, grant);

    internal static bool ValidAll(MatchState state)
        => state.TimedBanishPlayGrants is not { } grants || grants.Count > 0 && grants.All(g => Valid(state, g))
            && grants.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() == grants.Count;

    internal static IReadOnlyList<string> Sources(MatchState state, string player)
        => (state.TimedBanishPlayGrants ?? []).Where(g => g.ControllerId == player && Valid(state, g))
            .Select(g => g.Card.ObjectId).Distinct(StringComparer.Ordinal).ToArray();

    internal static IReadOnlyList<TimedBanishPlayGrant>? Consume(MatchState state, string source)
        => NullIfEmpty((state.TimedBanishPlayGrants ?? []).Where(g => g.Card.ObjectId != source).ToArray());

    internal static MatchState ExpireAtEndStep(MatchState state)
        => state with { TimedBanishPlayGrants = NullIfEmpty((state.TimedBanishPlayGrants ?? [])
            .Where(g => g.ExpiringTurnNumber != state.TurnNumber || g.ControllerId != state.TurnPlayerId).ToArray()) };

    internal static IReadOnlyList<TimedBanishPlayGrant>? BeginNextTurn(MatchState state, string nextPlayer)
        => NullIfEmpty((state.TimedBanishPlayGrants ?? []).Select(g => g.ExpiringTurnNumber is null && g.ControllerId == nextPlayer
            ? g with { ExpiringTurnNumber = state.TurnNumber + 1 } : g).ToArray());

    internal static ResolutionResult Prune(ResolutionResult result)
    {
        if (!result.Accepted || result.State.TimedBanishPlayGrants is not { } grants) return result;
        var remaining = grants.Where(g => CardRemainsBanished(result.State, g)).ToArray();
        if (remaining.Length == grants.Count) return result;
        var next = result.State with { TimedBanishPlayGrants = NullIfEmpty(remaining) };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = ResolutionResult.BuildPrompts(next) };
    }

    private static IReadOnlyList<TimedBanishPlayGrant>? NullIfEmpty(TimedBanishPlayGrant[] grants)
        => grants.Length == 0 ? null : grants;
}

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveMovementTimedBanishPlay(MatchState state, StackItemState item, List<GameEvent> events)
    {
        var zones = NormalizeZonesForSeats(state); var own = zones[item.ControllerId];
        if (own.MainDeck.FirstOrDefault() is not { } id || !state.CardObjects.TryGetValue(id, out var card))
            return NoopStackResolutionResult(state) with { Events = events };
        var cards = state.CardObjects.ToDictionary();
        var restored = PrintedCardFactory.TryRestoreOutsidePlay(card, item.ControllerId, out var printed) ? printed : card;
        cards[id] = restored with { ObjectGeneration = checked(card.ObjectGeneration + 1), IsFaceDown = false };
        zones[item.ControllerId] = own with { MainDeck = own.MainDeck.Skip(1).ToArray(), Banished = own.Banished.Append(id).ToArray() };
        var tick = RuleChoices.Value?.CompletionTick ?? state.Tick + 1;
        // "Until your next Ending Phase" ends on entering that phase. A grant
        // earned during the controller's Main Phase therefore ends this turn;
        // one earned in another player's turn waits for the controller's turn.
        var grant = new TimedBanishPlayGrant("", item.ControllerId, state.TurnNumber, tick, item.StackItemId,
            item.MovementObserver!, new(id, cards[id].ObjectGeneration), cards[id].CardNo!,
            "MOVEMENT", state.TurnPlayerId == item.ControllerId && state.Phase != MatchPhases.TurnEnd ? state.TurnNumber : null);
        grant = grant with { Id = TimedBanishPlayRules.Id(grant) };
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        events[0] = events[0] with { Payload = new Dictionary<string, object?>(events[0].Payload) { ["sourceObjectId"] = source } };
        events.Add(CaptureOwnedBanishObservers(new("CARD_BANISHED", "放逐主牌堆顶牌，直到下一个自己的结束阶段可以正常打出", new Dictionary<string, object?> {
            ["sourceObjectId"] = source, ["targetObjectId"] = id, ["cardNo"] = cards[id].CardNo,
            ["playerId"] = item.ControllerId, ["destinationZone"] = "BANISHED", ["permissionId"] = grant.Id }),
            zones, cards, item.ControllerId, [id]));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events,
            TimedBanishPlayGrants = (state.TimedBanishPlayGrants ?? []).Append(grant).ToArray() };
    }
}
