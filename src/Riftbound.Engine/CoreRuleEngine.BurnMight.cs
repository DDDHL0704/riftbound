using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveBurnMightAbility(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        if (context.BurnedUnit is { } burned) {
            if (item.TargetObjectIds.SingleOrDefault() is { } target && UnitEventTargets(state, item).Contains(target)
                && StackObjectRules.ReferencesCurrentObject(state, item, target)) {
                var behavior = new CardBehaviorDefinition(context.CardNo, "燃烧单位后的战力加成", 0,
                    item.EffectKind, 0, 1, PowerModifierAmount: burned.Power);
                cards[target] = ApplyPowerModifier(state, cards[target], behavior, item, target, burned.Power, out var power);
                events.Add(power);
            }
            return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
        }

        var burn = ApplyBurnToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId, 1, state.RngCursor, item, events);
        var burnedId = events.Where(e => e.Kind == "CARDS_BURNED")
            .SelectMany(e => e.Payload["cardIds"] as IReadOnlyList<string> ?? []).SingleOrDefault();
        if (burn.WinnerPlayerId is null && burnedId is not null && cards.TryGetValue(burnedId, out var card)
            && card.Tags.Contains(CardObjectTags.UnitCard)) {
            var followup = context with { ReflexiveTick = state.Tick + 1,
                BurnedUnit = new(new(card.ObjectId, card.ObjectGeneration), card.CardNo!, card.CopyFaceTransitions?.Count ?? 0, card.Power) };
            events.Add(WithUnitEvents(new("REFLEXIVE_BURN_MIGHT_CREATED", "燃烧了单位牌，后续战力技能等待确认",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["burnedCardObjectId"] = card.ObjectId }), [new(item.SourceObjectId, item.ControllerId, followup)]));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), PlayerScores = burn.PlayerScores,
            WinnerPlayerId = burn.WinnerPlayerId, RngCursor = burn.RngCursor, Events = events };
    }
}
