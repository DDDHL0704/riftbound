using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IEnumerable<TriggerQueueItemState> CaptureIvernTriggers(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string field, long tick) => zones[player].LegendZone
        .Where(id => cards.TryGetValue(id, out var card) && card.ControllerId == player && !card.IsFaceDown
            && LegendCardHasIdentity(card.CardNo, IvernLegendIdentityId))
        .Select(id => new TriggerQueueItemState($"conquest-brush-{tick}-{field}-{id}", player, id,
            "HOLD_IVERN", "BATTLEFIELD_CONQUERED") {
            HeldContext = new(cards[id].CardNo!, field, "IVERN", 1, cards[id].ObjectGeneration) });

    private static ResolutionResult QueueBattlefieldReturnTriggers(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Kind != "SCORE_GAINED" || ev.Payload.GetValueOrDefault("battlefieldReturnContext") is not HeldTriggerContext context) continue;
            var trigger = new TriggerQueueItemState($"brush-return-{result.State.Tick}-{index}-{context.BattlefieldObjectId}",
                (string)ev.Payload["playerId"]!, context.BattlefieldObjectId, "HOLD_BRUSH_RETURN", "BATTLEFIELD_SCORED") { HeldContext = context };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static CardObjectState? ReplacedBattlefieldCard(MatchState state, CardObjectState current)
        => current.ReplacedBattlefieldCard is { } origin
            && state.CardObjects.TryGetValue(origin.ObjectId, out var card) && card.ObjectGeneration == origin.Generation
            && card.ReplacedAtBattlefieldId == current.ObjectId && card.TokenFactoryCardNo is null
            && IsBattlefieldCardObject(card) && state.PlayerZones.Values.Any(z => z.Banished.Contains(origin.ObjectId)) ? card : null;

    private static bool CanReturnBattlefield(MatchState state, StackItemState item)
        => BattlefieldLocalRules.Battlefield(state, item.HeldContext!.BattlefieldObjectId) is { } field
            && field.ObjectGeneration == item.HeldContext.SourceGeneration
            && P6TokenFactoryCatalog.IsBrushBattlefieldToken(field.CardNo) && ReplacedBattlefieldCard(state, field) is not null;

    private static StackResolutionResult ResolveBattlefieldReplacement(MatchState state, StackItemState item)
    {
        if (item.TriggerCost is null || BattlefieldLocalRules.Battlefield(state, item.HeldContext!.BattlefieldObjectId) is not { } field)
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (item.HeldContext.Kind == "BRUSH_RETURN")
        {
            if (!CanReturnBattlefield(state, item)) return NoopStackResolutionResult(state);
            var original = ReplacedBattlefieldCard(state, field)!;
            // Restore the printed face and owner, while inheriting the current
            // control, score markers, damage, attachments and location references.
            cards[field.ObjectId] = ReplaceBattlefieldFace(field, original) with { ReplacedBattlefieldCard = null };
            foreach (var owner in zones.Keys.ToArray()) zones[owner] = zones[owner] with { Banished = RemoveFromZone(zones[owner].Banished, original.ObjectId) };
            cards.Remove(original.ObjectId); locations.Remove(original.ObjectId);
            events.Add(new("BATTLEFIELD_RESTORED", "草丛换回原战场，保留当前状态", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["battlefieldObjectId"] = field.ObjectId,
                ["cardNo"] = original.CardNo, ["replacedCardObjectId"] = original.ObjectId }));
        }
        else
        {
            if (!P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo, out var factory))
                throw new InvalidOperationException("Brush token definition is required.");
            var origin = field.ReplacedBattlefieldCard;
            if (field.TokenFactoryCardNo is null)
            {
                var originalId = NextTokenObjectId(zones, cards, field.ObjectId + "-REPLACED", 1);
                var owner = field.OwnerId!;
                var original = new CardObjectState(originalId, cardNo: field.CardNo, tags: field.Tags,
                    power: field.Power, manaCost: field.ManaCost, ownerId: owner, controllerId: owner)
                    { ReplacedAtBattlefieldId = field.ObjectId };
                cards[originalId] = original;
                zones[owner] = zones[owner] with { Banished = zones[owner].Banished.Append(originalId).ToArray() };
                locations[originalId] = new(owner, "BANISHED");
                origin = new(originalId, original.ObjectGeneration);
                events.Add(new("BATTLEFIELD_CARD_REPLACED", "原战场牌因替换置于放逐区域（不是放逐行动）", new Dictionary<string, object?> {
                    ["battlefieldObjectId"] = field.ObjectId, ["replacedCardObjectId"] = originalId, ["ownerId"] = owner }));
            }
            var face = factory.CreateObject(field.ObjectId, item.ControllerId, field.ControllerId ?? item.ControllerId);
            cards[field.ObjectId] = ReplaceBattlefieldFace(field, face) with { ReplacedBattlefieldCard = origin };
            events.Add(new("BATTLEFIELD_REPLACED", "战场替换为草丛，保留位置与状态", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["battlefieldObjectId"] = field.ObjectId, ["replacementTokenObjectId"] = field.ObjectId,
                ["replacementTokenCardNo"] = factory.CardNo, ["replacedCardObjectId"] = origin?.ObjectId }));
        }
        // Replacement is not a play or zone transition of the live game object:
        // do not capture entry/conquest events or reset its generation (CN 438.1.a).
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }

    private static CardObjectState ReplaceBattlefieldFace(CardObjectState current, CardObjectState face)
        => current with { CardNo = face.CardNo, Tags = face.Tags, Power = face.Power, ManaCost = face.ManaCost,
            TokenFactoryCardNo = face.TokenFactoryCardNo, OwnerId = face.OwnerId, ReplacedAtBattlefieldId = null };

    internal static void ValidateBattlefieldReplacements(MatchState state, List<string> errors)
    {
        foreach (var field in state.CardObjects.Values.Where(c => c.ReplacedBattlefieldCard is not null))
        {
            if (!P6TokenFactoryCatalog.IsBrushBattlefieldToken(field.TokenFactoryCardNo)
                || BattlefieldLocalRules.Battlefield(state, field.ObjectId) is null || ReplacedBattlefieldCard(state, field) is null)
                errors.Add("invalid replaced battlefield origin");
        }
        foreach (var original in state.CardObjects.Values.Where(c => c.ReplacedAtBattlefieldId is not null))
        {
            if (original.ReplacedAtBattlefieldId == original.ObjectId || original.TokenFactoryCardNo is not null
                || !IsBattlefieldCardObject(original) || !state.PlayerZones.Values.Any(z => z.Banished.Contains(original.ObjectId))
                || !state.CardObjects.TryGetValue(original.ReplacedAtBattlefieldId!, out var current)
                || current.ReplacedBattlefieldCard != new ObjectBinding(original.ObjectId, original.ObjectGeneration))
                errors.Add("invalid replaced battlefield card in exile");
        }
    }
}
