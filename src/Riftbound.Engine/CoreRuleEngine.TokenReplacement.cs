using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TokenReplacementApplication(ObjectBinding Source, int TokenIndex);
public sealed record TokenEntryPlan(int OriginalCount, int NextToken, IReadOnlyList<TokenReplacementApplication> Applied);

public sealed partial class CoreRuleEngine
{
    internal const string TokenReplacementWindow = "TOKEN_ENTRY_REPLACEMENT";
    internal const string TokenReplacementUsed = "TOKEN_ENTRY_REPLACEMENT_USED";
    private static readonly Lazy<IReadOnlyList<string>> TokenReplacementSources = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-086/219"])["UNL-086/219"]);

    private static string[] EligibleTokenReplacements(MatchState state, string player) => state.CardObjects.Values
        .Where(c => c.ControllerId == player && IsFaceUpNonStandbyUnit(c)
            && TokenReplacementSources.Value.Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(c.CardNo))
            && BattlefieldLocalRules.AtUnit(state, c.ObjectId) is not null
            && !c.UntilEndOfTurnEffects.Contains(TokenReplacementUsed))
        .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    // This describes the next token-producing instruction. It does not create
    // objects, spend a replacement's once-per-turn use, or finish the parent.
    private static int TokenEntryCount(MatchState state, StackItemState parent)
    {
        if (parent.CompletedRepeatExecutions < 0 || parent.RepeatExecutions is { } repeats
            && parent.CompletedRepeatExecutions >= repeats.Count) return 0;
        var item = parent.RepeatExecutions is { } executions && parent.CompletedRepeatExecutions < executions.Count
            ? parent with { EffectKind = executions[parent.CompletedRepeatExecutions].EffectKind, EffectRepeatCount = 1, RepeatExecutions = null }
            : parent;
        if (TryGetLegendUnitToken(item.EffectKind, out _)) return 1;
        if (item.DeathObserver?.Kind == TriggerKinds.UnitDestroyedNonMinionCreateMinion) return 1;
        if (item.HeldContext is { Kind: "LEBLANC_DISCARD" } image && item.TriggerCost is not null)
            return BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + image.BattlefieldObjectId) ? 0 : 1;
        if (item.HeldContext is { Kind: "MINION" or "ROBOT" } held) return held.Amount;
        if (UnitDestroyedTriggerSpecRules.TryGetTrigger(item.CardNo,
                t => UnitDestroyedTriggerSpecRules.IsLastBreathCreateBaseUnitTrigger(t) && t.Kind == item.EffectKind, out var death))
            return death.CreatedTokenCount.GetValueOrDefault() * item.EffectRepeatCount;
        if (CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && (!behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment || item.SourceConfirmed)
            && ShouldCreateBaseUnitTokens(behavior, item))
            return behavior.CreatedBaseUnitTokenCount * item.EffectRepeatCount;
        return 0;
    }

    private static PendingCardChoiceState TokenReplacementChoice(MatchState state, StackItemState parent, TokenEntryPlan plan)
    {
        var sources = EligibleTokenReplacements(state, parent.ControllerId);
        return new($"token-replacement:{parent.StackItemId}:{parent.CompletedRepeatExecutions}:{plan.NextToken}:{plan.Applied.Count}", TokenReplacementWindow,
            parent.ControllerId, 0, 1, sources, sources,
            $"第 {plan.NextToken + 1}/{plan.OriginalCount} 名单位指示物：选择一名基兰，多打出一个复制体；不选则跳过此指示物的剩余替换，保留未使用的回合次数。已增加 {plan.Applied.Count} 名。",
            parent.SourceObjectId, parent.EffectKind) { ResolvingStackItemId = parent.StackItemId };
    }

    private static StackResolutionResult? BeginTokenReplacement(MatchState state, StackItemState item)
    {
        var count = TokenEntryCount(state, item);
        if (count <= 0 || item.TokenEntryPlan is { } existing && existing.NextToken >= existing.OriginalCount
            || EligibleTokenReplacements(state, item.ControllerId).Length == 0) return null;
        var plan = item.TokenEntryPlan ?? new TokenEntryPlan(count, 0, []);
        var parent = item with { TokenEntryPlan = plan };
        return NoopStackResolutionResult(state) with {
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(parent).ToArray(),
            PendingCardChoice = TokenReplacementChoice(state, parent, plan)
        };
    }

    internal static bool ValidTokenEntryPlan(MatchState state, StackItemState item)
    {
        if (item.TokenEntryPlan is not { } p) return true;
        return p.OriginalCount > 0 && p.OriginalCount == TokenEntryCount(state, item) && p.NextToken >= 0 && p.NextToken <= p.OriginalCount
            && p.Applied is not null && p.Applied.All(a => a is not null && a.Source is not null
                && a.TokenIndex >= 0 && a.TokenIndex < p.OriginalCount && a.TokenIndex <= p.NextToken
                && state.CardObjects.TryGetValue(a.Source.ObjectId, out var source) && source.ObjectGeneration == a.Source.Generation
                && source.ControllerId == item.ControllerId && source.UntilEndOfTurnEffects.Contains(TokenReplacementUsed)
                && TokenReplacementSources.Value.Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(source.CardNo)))
            && p.Applied.Select(a => a.Source.ObjectId).Distinct().Count() == p.Applied.Count;
    }

    internal static bool ValidTokenReplacementChoice(MatchState state, PendingCardChoiceState choice)
    {
        var parent = state.StackItems.LastOrDefault();
        if (parent?.TokenEntryPlan is not { } p || p.NextToken >= p.OriginalCount || !ValidTokenEntryPlan(state, parent)) return false;
        var expected = TokenReplacementChoice(state, parent, p);
        return choice.ChoiceWindow == TokenReplacementWindow && choice.ChoiceId == expected.ChoiceId
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.ResolvingStackItemId == parent.StackItemId
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.Count > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveTokenReplacementChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidTokenReplacementChoice(state, choice))
            return RejectWithCorePrompts(state, "指示物替换选择已失效。", ErrorCodes.InvalidTarget);
        var parent = state.StackItems[^1];
        var plan = parent.TokenEntryPlan!;
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        if (selected.Count == 0) plan = plan with { NextToken = plan.NextToken + 1 };
        else
        {
            var source = cards[selected[0]];
            cards[source.ObjectId] = source with { UntilEndOfTurnEffects = source.UntilEndOfTurnEffects.Append(TokenReplacementUsed).Order(StringComparer.Ordinal).ToArray() };
            plan = plan with { Applied = plan.Applied.Append(new TokenReplacementApplication(new(source.ObjectId, source.ObjectGeneration), plan.NextToken)).ToArray() };
        }
        var next = state with { CardObjects = cards, PendingCardChoice = null };
        if (EligibleTokenReplacements(next, parent.ControllerId).Length == 0) plan = plan with { NextToken = plan.OriginalCount };
        parent = parent with { TokenEntryPlan = plan };
        next = next with { StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(parent).ToArray() };
        var events = new[] { new GameEvent("TOKEN_REPLACEMENT_CHOSEN", selected.Count == 0 ? "保留未使用的替换次数" : "本次指示物进场增加一个复制体",
            new Dictionary<string,object?> { ["playerId"] = choice.PlayerId, ["sourceObjectId"] = selected.FirstOrDefault(),
                ["parentSourceObjectId"] = parent.SourceObjectId, ["tokenIndex"] = state.StackItems[^1].TokenEntryPlan!.NextToken,
                ["applied"] = selected.Count != 0, ["extraCount"] = plan.Applied.Count }) };
        if (plan.NextToken < plan.OriginalCount)
        {
            next = next with { Tick = state.Tick + 1, PendingCardChoice = TokenReplacementChoice(next, parent, plan) };
            return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
        }
        var resumed = ResolvePassPriority(next, new("token-entry-continuation", parent.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }

    private static int AdditionalUnitTokens(StackItemState item) => item.TokenEntryPlan?.Applied.Count ?? 0;
}
