using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record UnitTokenReplacementSource(string ObjectId, string InstanceId, bool Robot)
    {
        public string Id => (Robot ? "ROBOT:" : "COPY:") + InstanceId;
    }

    private static UnitTokenReplacementSource[] RobotTokenReplacementSources(MatchState state, string player)
        => state.CardObjects.Values.Where(card => card.ControllerId == player && IsFaceUpNonStandbyUnit(card)
                && BattlefieldLocalRules.AtUnit(state, card.ObjectId) is not null)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
            .SelectMany(card => AttachedRuleTextRules.Instances(state, card)
                .Where(instance => CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo,
                    ability => ability.Kind == StaticAbilityKinds.BattlefieldUnitTokenRobotReplacement, out _))
                .Select(instance => new UnitTokenReplacementSource(card.ObjectId, instance.Id, true))).ToArray();

    private static UnitTokenReplacementSource[] UnitTokenReplacementOrder(MatchState state, StackItemState item)
    {
        var remaining = RobotTokenReplacementSources(state, item.ControllerId).ToList();
        // The existing card-choice continuation has already applied or declined
        // the copy replacement for every original event in this instruction.
        if (item.TokenEntryPlan is null)
            remaining.AddRange(EligibleTokenReplacements(state, item.ControllerId)
                .Select(id => new UnitTokenReplacementSource(id,
                    $"PRINTED:{id}:{state.CardObjects[id].ObjectGeneration}", false)));
        var ordered = new List<UnitTokenReplacementSource>();
        // CN 372 / 373.2: choose one order for all simultaneous original events.
        // Whether to use each optional replacement is decided for each event.
        while (remaining.Count > 1)
        {
            var options = remaining.Select(source => new RuleChoiceOption(source.Id,
                source.Robot ? "改为机器人" : "增加一名复制体", [source.ObjectId])).ToArray();
            var selected = RuleChoices.Value!.Choose(item.ControllerId, "选择下一项指示物替换的生效顺序。",
                options, kind: "TOKEN_REPLACEMENT_ORDER", preview: state);
            var source = remaining.Single(candidate => candidate.Id == selected);
            ordered.Add(source); remaining.Remove(source);
        }
        return ordered.Concat(remaining).ToArray();
    }

    private static IReadOnlyList<bool> ChooseUnitTokenReplacementForms(MatchState state, StackItemState item,
        IReadOnlyList<UnitTokenReplacementSource> order, int originalIndex,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, ObjectLocationState> locations, IReadOnlyDictionary<string, RunePool> pools,
        List<GameEvent> events)
    {
        var forms = new List<bool> { false };
        foreach (var source in order)
        {
            var context = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
                ObjectLocations = ReconcileObjectLocations(locations, zones) };
            if (source.Robot ? !RobotTokenReplacementSources(context, item.ControllerId).Contains(source)
                : !EligibleTokenReplacements(context, item.ControllerId).Contains(source.ObjectId)) continue;
            // A replacement sees all results of earlier replacements, and never
            // revisits its own results (CN 370.2). A later replacement can see
            // the extra event created by the copy replacement.
            for (var index = 0; index < forms.Count; index++)
            {
                var options = new[] { new RuleChoiceOption("DECLINE", "保留当前指示物"),
                    new RuleChoiceOption("APPLY", source.Robot ? "改为机器人" : "增加一名复制体", [source.ObjectId]) };
                var selected = RuleChoices.Value!.Choose(item.ControllerId,
                    $"第 {originalIndex + 1} 名指示物的第 {index + 1} 个结果：是否应用所选替换？", options,
                    kind: "UNIT_TOKEN_REPLACEMENT", preview: context);
                if (selected == "DECLINE") continue;
                if (source.Robot) forms[index] = true;
                else
                {
                    var card = cards[source.ObjectId];
                    cards[source.ObjectId] = card with { UntilEndOfTurnEffects = card.UntilEndOfTurnEffects
                        .Append(TokenReplacementUsed).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                    forms.Insert(index + 1, forms[index]);
                }
                events.Add(new("TOKEN_REPLACEMENT_CHOSEN", source.Robot ? "本次指示物改为机器人" : "本次指示物增加一名复制体",
                    new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId,
                        ["ruleTextInstanceId"] = source.InstanceId,
                        ["parentSourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(context, item.SourceObjectId, "__spectator__"),
                        ["tokenIndex"] = originalIndex, ["applied"] = true,
                        ["replacementKind"] = source.Robot ? "ROBOT" : "COPY" }));
                if (!source.Robot) break; // Its once-per-turn use was consumed.
            }
        }
        return forms;
    }

    // Grant only instruction keywords to a replacement's new physical form.
    // Family names and printed traits of the original token are not modifiers.
    private static string[] UnitTokenInstructionKeywords(IEnumerable<string> tags)
        => tags.Where(tag => KeywordParser.Parse(tag, "").Any(keyword => keyword.RawText == tag))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
