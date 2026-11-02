using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TokenReplacementApplication(ObjectBinding Source, int TokenIndex, string? FactoryCardNo = null);
public sealed record TokenEntryPlan(int OriginalCount, int NextToken, IReadOnlyList<TokenReplacementApplication> Applied);

public sealed partial class CoreRuleEngine
{
    internal const string TokenReplacementWindow = "TOKEN_ENTRY_REPLACEMENT";
    internal const string TokenReplacementUsed = "TOKEN_ENTRY_REPLACEMENT_USED";
    private static readonly Lazy<IReadOnlyList<string>> TokenReplacementSources = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-086/219"])["UNL-086/219"]);

    private static string? TokenReplacementFactory(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source)
            .Select(instance => CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                ? behavior.UnitTokenReplacementFactoryCardNo : null)
            .FirstOrDefault(factory => factory is not null);

    private static List<string?> TokenEntryFactories(TokenEntryPlan plan)
    {
        var factories = Enumerable.Repeat<string?>(null, plan.OriginalCount).ToList();
        foreach (var application in plan.Applied)
        {
            if (application.TokenIndex < 0 || application.TokenIndex >= factories.Count)
                throw new InvalidOperationException("Invalid token replacement index.");
            if (application.FactoryCardNo is null) factories.Add(factories[application.TokenIndex]);
            else factories[application.TokenIndex] = application.FactoryCardNo;
        }
        return factories;
    }

    internal static bool TokenEntryPlanPending(TokenEntryPlan plan)
        => plan.NextToken < plan.OriginalCount + plan.Applied.Count(a => a.FactoryCardNo is null);

    private static string[] EligibleTokenReplacements(MatchState state, string player, TokenEntryPlan plan)
    {
        var factories = TokenEntryFactories(plan);
        if (plan.NextToken >= factories.Count) return [];
        return state.CardObjects.Values.Where(c => c.ControllerId == player && IsFaceUpNonStandbyUnit(c)
            && BattlefieldLocalRules.AtUnit(state, c.ObjectId) is not null
            && (TokenReplacementSources.Value.Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(c.CardNo))
                    && !c.UntilEndOfTurnEffects.Contains(TokenReplacementUsed)
                || TokenReplacementFactory(state, c) is { } factory && factories[plan.NextToken] != factory))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static CardObjectState ApplyUnitTokenFactoryReplacement(StackItemState item, int index, CardObjectState original)
    {
        if (item.TokenEntryPlan is not { } plan || TokenEntryFactories(plan)[index] is not { } cardNo) return original;
        if (!P6TokenFactoryCatalog.TryGetByCardNo(cardNo, out var factory))
            throw new InvalidOperationException("Missing token replacement factory.");
        return factory.CreateObject(original.ObjectId, original.OwnerId!, original.ControllerId!, isExhausted: true);
    }

    // This describes the next token-producing instruction. It does not create
    // objects, spend a replacement's once-per-turn use, or finish the parent.
    private static int TokenEntryCount(MatchState state, StackItemState parent)
    {
        if (parent.CompletedRepeatExecutions < 0 || parent.RepeatExecutions is { } repeats
            && parent.CompletedRepeatExecutions >= repeats.Count) return 0;
        var item = parent.RepeatExecutions is { } executions && parent.CompletedRepeatExecutions < executions.Count
            ? parent with { EffectKind = executions[parent.CompletedRepeatExecutions].EffectKind, EffectRepeatCount = 1, RepeatExecutions = null }
            : parent;
        if (TryMovementTokenInstruction(state, item, out var movementToken)) return movementToken.Count;
        if (item.SourceUnitTokens is { } sourceToken)
            return TrySourceUnitTokenDestination(state, item, out _)
                ? SourceUnitTokenSpecs.Value[sourceToken.CardNo].CreatedTokenCount.GetValueOrDefault() : 0;
        if (P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var activated)
            && activated.SimpleEffect is { } simple && SimpleActivationUnitTokenCount(simple) > 0)
            return SimpleActivationUnitTokenCount(simple);
        if (TryGetLegendUnitToken(item.EffectKind, out _)) return 1;
        if (item.DeathObserver?.Kind is TriggerKinds.UnitDestroyedNonMinionCreateMinion
            or TriggerKinds.BattlefieldFirstNonTokenDestroyedCreateMinion) return 1;
        if (item.HeldContext is { Kind: "LEBLANC_DISCARD" } image && item.TriggerCost is not null)
            return BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + image.BattlefieldObjectId) ? 0 : 1;
        if (item.HeldContext is { Kind: "MINION" or "ROBOT" } held) return held.Amount;
        if (UnitDestroyedTriggerSpecRules.TryGetTrigger(item.CardNo,
                t => UnitDestroyedTriggerSpecRules.IsLastBreathCreateBaseUnitTrigger(t) && t.Kind == item.EffectKind, out var death))
            return death.CreatedTokenCount.GetValueOrDefault() * item.EffectRepeatCount;
        if (CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && (!behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment || item.SourceConfirmed)
            && ShouldCreateBaseUnitTokens(behavior, item))
            return behavior.CreatedBaseUnitTokensCopySource && SelfCopyTokenLocation(state, item) is null
                ? 0 : BaseUnitTokenInstructionCount(state, item, behavior);
        return 0;
    }

    private static PendingCardChoiceState TokenReplacementChoice(MatchState state, StackItemState parent, TokenEntryPlan plan)
    {
        var sources = EligibleTokenReplacements(state, parent.ControllerId, plan);
        return new($"token-replacement:{parent.StackItemId}:{parent.CompletedRepeatExecutions}:{plan.NextToken}:{plan.Applied.Count}", TokenReplacementWindow,
            parent.ControllerId, 0, 1, sources, sources,
            $"第 {plan.NextToken + 1}/{TokenEntryFactories(plan).Count} 名单位指示物：选择基兰增加复制体，或选择黑默丁格将它替换为机器人；不选则保留该指示物及未使用的替换。",
            parent.SourceObjectId, parent.EffectKind) { ResolvingStackItemId = parent.StackItemId };
    }

    private static StackResolutionResult? BeginTokenReplacement(MatchState state, StackItemState item)
    {
        var count = TokenEntryCount(state, item);
        if (count <= 0 || item.TokenEntryPlan is { } existing && !TokenEntryPlanPending(existing)) return null;
        var plan = item.TokenEntryPlan ?? new TokenEntryPlan(count, 0, []);
        if (EligibleTokenReplacements(state, item.ControllerId, plan).Length == 0) return null;
        var parent = item with { TokenEntryPlan = plan };
        return NoopStackResolutionResult(state) with {
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(parent).ToArray(),
            PendingCardChoice = TokenReplacementChoice(state, parent, plan)
        };
    }

    internal static bool ValidTokenEntryPlan(MatchState state, StackItemState item)
    {
        if (item.TokenEntryPlan is not { } p) return true;
        if (p.OriginalCount <= 0 || p.OriginalCount != TokenEntryCount(state, item) || p.Applied is null || p.Applied.Any(a => a is null)
            || p.NextToken < 0 || p.NextToken > p.OriginalCount + p.Applied.Count(a => a.FactoryCardNo is null)) return false;
        var count = p.OriginalCount; var lastIndex = 0;
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var factories = Enumerable.Repeat<string?>(null, count).ToList();
        foreach (var a in p.Applied)
        {
            if (a is null || a.Source is null || a.TokenIndex < lastIndex || a.TokenIndex >= count || a.TokenIndex > p.NextToken
                || !state.CardObjects.TryGetValue(a.Source.ObjectId, out var source) || source.ObjectGeneration != a.Source.Generation
                || source.ControllerId != item.ControllerId || !IsFaceUpNonStandbyUnit(source)
                || BattlefieldLocalRules.AtUnit(state, source.ObjectId) is null) return false;
            if (a.FactoryCardNo is null)
            {
                if (!source.UntilEndOfTurnEffects.Contains(TokenReplacementUsed) || !duplicates.Add(source.ObjectId)
                    || !TokenReplacementSources.Value.Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(source.CardNo))) return false;
                factories.Add(factories[a.TokenIndex]); count++;
            }
            else
            {
                if (a.FactoryCardNo != TokenReplacementFactory(state, source) || factories[a.TokenIndex] == a.FactoryCardNo
                    || !P6TokenFactoryCatalog.IsUnitTokenFactory(a.FactoryCardNo)) return false;
                factories[a.TokenIndex] = a.FactoryCardNo;
            }
            lastIndex = a.TokenIndex;
        }
        return true;
    }

    internal static bool ValidTokenReplacementChoice(MatchState state, PendingCardChoiceState choice)
    {
        var parent = state.StackItems.LastOrDefault();
        if (parent?.TokenEntryPlan is not { } p || !ValidTokenEntryPlan(state, parent) || !TokenEntryPlanPending(p)) return false;
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
            var factory = TokenReplacementFactory(state, source);
            if (factory is null)
                cards[source.ObjectId] = source with { UntilEndOfTurnEffects = source.UntilEndOfTurnEffects.Append(TokenReplacementUsed).Order(StringComparer.Ordinal).ToArray() };
            plan = plan with { Applied = plan.Applied.Append(new TokenReplacementApplication(new(source.ObjectId, source.ObjectGeneration), plan.NextToken, factory)).ToArray() };
        }
        var next = state with { CardObjects = cards, PendingCardChoice = null };
        while (TokenEntryPlanPending(plan) && EligibleTokenReplacements(next, parent.ControllerId, plan).Length == 0)
            plan = plan with { NextToken = plan.NextToken + 1 };
        parent = parent with { TokenEntryPlan = plan };
        next = next with { StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(parent).ToArray() };
        var events = new[] { new GameEvent("TOKEN_REPLACEMENT_CHOSEN", selected.Count == 0 ? "保留未使用的替换次数" : "应用所选指示物替换",
            new Dictionary<string,object?> { ["playerId"] = choice.PlayerId, ["sourceObjectId"] = selected.FirstOrDefault(),
                ["parentSourceObjectId"] = parent.SourceObjectId, ["tokenIndex"] = state.StackItems[^1].TokenEntryPlan!.NextToken,
                ["applied"] = selected.Count != 0, ["extraCount"] = plan.Applied.Count(a => a.FactoryCardNo is null) }) };
        if (TokenEntryPlanPending(plan))
        {
            next = next with { Tick = state.Tick + 1, PendingCardChoice = TokenReplacementChoice(next, parent, plan) };
            return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
        }
        var resumed = ResolvePassPriority(next, new("token-entry-continuation", parent.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = events.Concat(resumed.Events).ToArray() };
    }

    private static int AdditionalUnitTokens(StackItemState item) => item.TokenEntryPlan?.Applied.Count(a => a.FactoryCardNo is null) ?? 0;
}
