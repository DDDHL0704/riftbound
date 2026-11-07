using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RevealedRuneFact(ObjectBinding Rune, string CardNo, long FaceRevision,
    long RevealedTick, IReadOnlyList<string> Traits);

public sealed partial class CoreRuleEngine
{
    internal const string RuneDomainRevealEffect = "REVEAL_RECYCLE_RUNE_DOMAIN";
    internal const string RuneDomainDamageEffect = "DAMAGE_ENEMY_AND_OTHERS_HERE";
    internal const string RuneDomainReflexiveCreatedEvent = "RUNE_DOMAIN_REFLEXIVE_CREATED";

    private static bool RuneDomainModeApplies(UnitEventContext context, UnitEventModeSpec mode)
        => UnitEventSpec(context.CardNo)?.Effect != RuneDomainRevealEffect
            || context.RevealedRune is { } rune && mode.RequiredRevealedRuneTrait is { } trait
                && rune.Traits.Contains(trait, StringComparer.Ordinal);

    private static bool ValidRevealedRuneContext(MatchState state, UnitEventContext context, string? controller)
    {
        if (UnitEventSpec(context.CardNo)?.Effect != RuneDomainRevealEffect) return context.RevealedRune is null;
        if (context.RevealedRune is not { } revealed) return context.Mode is null && context.ReflexiveTick is null;
        if (revealed.Rune is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(revealed.Rune.ObjectId)
            || revealed.FaceRevision < 0 || revealed.RevealedTick < context.CapturedTick || revealed.RevealedTick > state.Tick
            || context.ReflexiveTick != revealed.RevealedTick || revealed.Traits is null
            || !PrintedCardFactory.TryCreate("printed-rune", controller ?? string.Empty, revealed.CardNo, out var printed)
            || !printed.Tags.Contains(CardObjectTags.RuneCard)
            || !revealed.Traits.SequenceEqual(printed.Tags.Where(t => t.StartsWith("COLOR:", StringComparison.Ordinal))
                .Select(t => t[6..]).Order(StringComparer.Ordinal))
            || !PredictBindingMatches(state, revealed.Rune, revealed.CardNo, revealed.FaceRevision, revealed.RevealedTick, null)
            || !state.CardObjects.TryGetValue(revealed.Rune.ObjectId, out var card)
            || controller is not null && card.OwnerId != controller) return false;
        var eligible = (UnitEventSpec(context.CardNo)!.Modes ?? []).Where(mode => RuneDomainModeApplies(context, mode)).ToArray();
        return context.Mode is null ? eligible.Length > 1 : eligible.Any(mode => mode.Id == context.Mode);
    }

    private static StackResolutionResult ResolveRuneDomainReveal(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var player = item.ControllerId;
        var events = new List<GameEvent>();
        BeforeDeckReveal(state, zones, cards, item, player, events, zone: "RUNE_DECK");
        if (zones[player].RuneDeck.FirstOrDefault() is not { } top || !cards.TryGetValue(top, out var rune)
            || !rune.Tags.Contains(CardObjectTags.RuneCard)) return NoopStackResolutionResult(state);
        var source = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        events.Add(new("CARDS_REVEALED", "展示符文牌堆顶部的一张牌", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["cardIds"] = new[] { top },
            ["cardNo"] = rune.CardNo, ["count"] = 1, ["zone"] = "RUNE_DECK" }));
        // Reordering within this deck creates no new incarnation and generates
        // no Power. It still is a recycle instruction for ordinary observers.
        zones[player] = zones[player] with { RuneDeck = [..zones[player].RuneDeck.Skip(1), top] };
        events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", "将展示的符文回收到符文牌堆底部", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["cardIds"] = new[] { top },
            ["count"] = 1, ["sourceZone"] = "RUNE_DECK", ["destinationZone"] = "RUNE_DECK" }), zones, cards));
        var context = item.UnitEvent! with { ReflexiveTick = state.Tick + 1,
            RevealedRune = new(new(top, rune.ObjectGeneration), rune.CardNo!, rune.CopyFaceTransitions?.Count ?? 0,
                state.Tick + 1, rune.Tags.Where(t => t.StartsWith("COLOR:", StringComparison.Ordinal))
                    .Select(t => t[6..]).Order(StringComparer.Ordinal).ToArray()) };
        var modes = UnitEventSpec(context.CardNo)!.Modes!.Where(m => RuneDomainModeApplies(context, m)).ToArray();
        if (modes.Length > 0)
        {
            if (modes.Length == 1) context = context with { Mode = modes[0].Id };
            events.Add(WithUnitEvents(new(RuneDomainReflexiveCreatedEvent, "根据展示符文的特性创建后续技能", new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = source, ["runeObjectId"] = top }),
                [new(item.SourceObjectId, player, context)]));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
    private static StackResolutionResult ResolveRuneDomainDamage(MatchState state, StackItemState item, UnitEventAbilitySpec spec)
    {
        // The other-enemies instruction refers to the primary target (CN359.3.e.14).
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !UnitEventTargets(state, item).Contains(target)) return NoopStackResolutionResult(state);
        return ResolveUnitEventAreaDamage(state, item, spec.OtherEnemyDamageAmount, target, spec.Amount);
    }

}
