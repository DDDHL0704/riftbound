using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// A snapshot grant is independent of later changes to its donor. The ordinary
// field departure and turn-end cleanup remove these per-object effects.
internal static class SnapshotKeywordGrantRules
{
    internal const string Prefix = "SNAPSHOT_KEYWORD:";
    internal static readonly string[] Counted = ["强攻", "坚守", "法盾", "狩猎", "缴械"];
    internal static bool HasGrants(CardObjectState card) => card.UntilEndOfTurnEffects.Any(IsGrant);
    internal static bool IsGrant(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
    internal static bool IsKeyword(string value)
        => KeywordParser.Parse("{{" + value + "}}", "").Any(k => k.RawText == value);
    internal static bool IsValid(string value)
    {
        if (!IsGrant(value)) return true;
        var at = value.LastIndexOf(':');
        return at > Prefix.Length && value.Length - at - 1 == 64
            && value[(at + 1)..].All(char.IsAsciiHexDigit)
            && IsKeyword(Uri.UnescapeDataString(value[Prefix.Length..at]));
    }
    internal static string Keyword(string value) => Uri.UnescapeDataString(value[Prefix.Length..value.LastIndexOf(':')]);
    internal static string Effect(string keyword, string application)
        => Prefix + Uri.EscapeDataString(keyword) + ":" + MatchStateHasher.HashValue(new { application, keyword });
    internal static int Amount(CardObjectState card, string keyword)
        => card.UntilEndOfTurnEffects.Where(e => IsGrant(e) && IsValid(e))
            .Sum(e => CardCombatKeywordRules.KeywordAmount([Keyword(e)], keyword));
    internal static IEnumerable<string> MaterializedKeywords(CardObjectState card)
        => Keywords(card).Where(k => !Counted.Any(n => CardCombatKeywordRules.KeywordAmount([k], n) > 0));
    internal static string[] Keywords(CardObjectState card)
    {
        var raw = card.UntilEndOfTurnEffects.Where(e => IsGrant(e) && IsValid(e)).Select(Keyword).ToArray();
        return raw.Where(k => !Counted.Any(n => CardCombatKeywordRules.KeywordAmount([k], n) > 0))
            .Distinct(StringComparer.Ordinal)
            .Concat(Counted.Select(n => (Name: n, Amount: raw.Sum(k => CardCombatKeywordRules.KeywordAmount([k], n))))
                .Where(k => k.Amount > 0).Select(k => k.Name + (k.Amount == 1 ? "" : k.Amount)))
            .Order(StringComparer.Ordinal).ToArray();
    }
}

public sealed partial class CoreRuleEngine
{
    internal static string SnapshotDisarmCaptureHash(FieldTriggerContext context, string source, string controller)
        => MatchStateHasher.HashValue(new { source, controller, context.CardNo, context.SourceGeneration,
            Origin = context.Disarm! with { Contributions = context.Disarm!.Contributions
                .Select(c => c with { CeasedSource = null }).ToArray() } });

    private static int CurrentHuntAmount(CardObjectState card)
        => CardResourceKeywordRules.HuntAmountFromTags(card.Tags) + SnapshotKeywordGrantRules.Amount(card, "狩猎");

    private static string[] CurrentSnapshotKeywords(MatchState state, CardObjectState source)
    {
        var raw = source.Tags.Concat(AttachedRuleTextRules.Copies(state, source)
                .SelectMany(i => AttachedRuleTextRules.PrintedKeywords(i.CardNo)))
            .Where(SnapshotKeywordGrantRules.IsKeyword)
            .Where(k => !SnapshotKeywordGrantRules.Counted.Any(n => CardCombatKeywordRules.KeywordAmount([k], n) > 0))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var keyword in new[] { CardCombatKeywordNames.Bulwark, CardCombatKeywordNames.BackRow })
            if (ResolveCurrentCountedCombatKeyword(state, source, keyword) > 0) raw.Add(keyword);
        var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source)!;
        if (HasRoamPermission(state, controller, source.ObjectId, source)) raw.Add(CardCombatKeywordNames.Roam);
        foreach (var keyword in SnapshotKeywordGrantRules.Counted)
        {
            var amount = keyword == CardResourceKeywordNames.Spellshield
                ? ResourceKeywordAmountWithoutFriendlyAuras(state, source, keyword)
                    + ResolveFriendlyFilteredUnitsResourceKeywordAmount(state, state.PlayerZones, source.ObjectId, source, keyword)
                : keyword == "狩猎" ? CurrentHuntAmount(source)
                : keyword == "缴械" ? CardCombatKeywordRules.KeywordAmount(source.Tags, keyword)
                : ResolveCurrentCountedCombatKeyword(state, source, keyword);
            if (amount > 0) raw.Add(keyword + (amount == 1 ? "" : amount));
        }
        return raw.Order(StringComparer.Ordinal).ToArray();
    }

    private static StackResolutionResult ResolveMovementSourceKeywordPower(MatchState state, StackItemState item, List<GameEvent> events)
    {
        if (!ValidTargetedMovementStack(state, item)) throw new InvalidOperationException("Invalid captured movement keyword grant.");
        var result = NoopStackResolutionResult(state);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !TargetedMovementTargets(state, item).Contains(target)) return result with { Events = events };
        state = RecomputeRuleDerivedKeywords(state);
        // CN359.3.e.12: an absent incarnation supplies neither current might
        // nor keywords. A captured skill does not freeze those characteristics.
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != item.MovementObserver!.SourceGeneration
            || !IsFaceUpNonStandbyUnit(source) || !IsObjectOnField(state.PlayerZones, source.ObjectId))
            return result with { Events = events };
        var keywords = CurrentSnapshotKeywords(state, source);
        var amount = Math.Max(0, ResolveCurrentFieldUnitPower(state, source));
        var cards = state.CardObjects.ToDictionary();
        var behavior = new CardBehaviorDefinition(item.CardNo, OfficialCardSourceIdentityGroups.DisplayName(item.CardNo),
            0, item.MovementObserver.Kind, 0, 0, PowerModifierAmount: amount);
        if (amount > 0)
        {
            cards[target] = ApplyPowerModifier(state, cards[target], behavior, item, target, amount, out var applied);
            events.Add(applied with { Payload = new Dictionary<string, object?>(applied.Payload) {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) } });
        }
        var application = MatchStateHasher.HashValue(new { item.StackItemId, target, cards[target].ObjectGeneration });
        var effects = keywords.Select(k => SnapshotKeywordGrantRules.Effect(k, application)).ToArray();
        cards[target] = cards[target] with { UntilEndOfTurnEffects = [..cards[target].UntilEndOfTurnEffects, ..effects] };
        events.Add(new("SNAPSHOT_KEYWORDS_GRANTED", "给予本回合的关键词", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target,
            ["targetGeneration"] = cards[target].ObjectGeneration, ["keywords"] = keywords,
            ["effectIds"] = effects, ["turnNumber"] = state.TurnNumber }));
        return result with { CardObjects = cards, Events = events };
    }
}
