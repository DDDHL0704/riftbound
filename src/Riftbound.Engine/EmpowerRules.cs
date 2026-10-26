using Riftbound.CardCatalog;
using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record EmpowerActivationContext(ObjectBinding Source, string CardNo, int Keyword, int Alternative,
    int PaidMana, int ControlledRunes, long AcquiredTick, long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ObjectBinding? DiscardedHandCard = null);
internal sealed record EmpowerAbility(string Id, RuleTextInstance Instance, EmpowerSpec Spec, EmpowerCostSpec Cost, int Alternative)
{
    internal string? InstanceId => Instance.Origin is null ? null : $"{Instance.Id}:{Id}";
}

internal static class EmpowerRules
{
    internal const string Effect = "EMPOWER_ACTIVATION";
    internal static bool RequiresHandDiscard(EmpowerCostSpec cost)
        => !cost.SupportedResources && cost.AdditionalCost == "弃置一张手牌" && !cost.Exhaust
            && cost.Mana == 0 && cost.Power == 0 && cost.TypedPower.Count == 0;
    internal static bool SupportedCost(EmpowerCostSpec cost) => cost.SupportedResources || RequiresHandDiscard(cost);
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo));
    private static readonly Lazy<IReadOnlyDictionary<string, (IReadOnlyList<EmpowerSpec> Specs, EmpoweredSelfSpec Self, IReadOnlyList<EmpoweredSpellSpec> Spells)>> Profiles = new(() =>
        Cards.Value.ToDictionary(p => p.Key, p => (EmpowerParser.Parse(p.Value.CardEffect), EmpowerParser.Self(p.Value.CardEffect), EmpowerParser.SpellEffects(p.Value.CardEffect))));
    internal static IReadOnlyList<EmpowerSpec> Specs(string cardNo) => Profiles.Value.TryGetValue(cardNo, out var p) ? p.Specs : [];
    internal static EmpoweredSelfSpec Self(string cardNo) => Profiles.Value.TryGetValue(cardNo, out var p) ? p.Self : new(0, []);
    internal static IReadOnlyList<EmpoweredSpellSpec> SpellEffects(string cardNo) => Profiles.Value.TryGetValue(cardNo, out var p) ? p.Spells : [];
    internal static int Might(MatchState state, CardObjectState source) => source.IsEmpowered && !source.IsFaceDown
        && state.PlayerZones.Values.Any(z => z.Base.Contains(source.ObjectId) || z.Battlefields.Contains(source.ObjectId))
        ? AttachedRuleTextRules.Instances(state, source).Sum(i => Self(i.CardNo).Might) : 0;
    internal static string[] Keywords(MatchState state, CardObjectState source) => !source.IsEmpowered || source.IsFaceDown
        ? [] : AttachedRuleTextRules.Instances(state, source).SelectMany(i => Self(i.CardNo).Keywords).SelectMany(keyword => {
            foreach (var counted in new[] { CardObjectTags.Spellshield, CardCombatKeywordNames.Assault, CardCombatKeywordNames.Steadfast })
                if (keyword.StartsWith(counted, StringComparison.Ordinal)) {
                    var suffix = keyword[counted.Length..]; var count = suffix.Length == 0 ? 1 : int.Parse(suffix);
                    return Enumerable.Repeat(counted, count);
                }
            return [keyword];
        }).ToArray();
    internal static IEnumerable<EmpowerAbility> Abilities(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).SelectMany(instance => Specs(instance.CardNo)
            .SelectMany(spec => spec.Alternatives.Select((cost, index) => new EmpowerAbility(
                $"EMPOWER:{spec.Index}:{index}", instance, spec, cost, index)).Where(a => SupportedCost(a.Cost))));
    internal static int ControlledRunes(MatchState state, string actor) => state.PlayerZones.Values
        .SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct().Count(id => state.CardObjects.TryGetValue(id, out var c)
            && c.Tags.Contains(CardObjectTags.RuneCard) && !c.IsFaceDown && c.ControllerId == actor);
    internal static int Mana(EmpowerSpec spec, EmpowerCostSpec cost, int runes)
        => Math.Max(0, cost.Mana - (spec.RuneLimit is { } limit && runes <= limit ? spec.ManaReductionAtMostRunes : 0)
            - (spec.ReduceManaPerRune ? runes : 0));
    internal static bool CanActivate(MatchState state, string actor, CardObjectState source)
        => CoreRuleEngine.CanActivateOrdinaryPowerAbility(state, actor) && !source.IsEmpowered && !source.IsFaceDown && source.ControllerId == actor
            && source.Tags.Contains(CardObjectTags.UnitCard)
            && state.PlayerZones.TryGetValue(actor, out var z) && (z.Base.Contains(source.ObjectId) || z.Battlefields.Contains(source.ObjectId));
    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (item.EmpowerActivation is not { } c) return item.EffectKind != Effect;
        var specs = Specs(c.CardNo);
        if (item.EffectKind != Effect || item.CardNo != c.CardNo || c.Keyword < 0 || c.Keyword >= specs.Count
            || c.Alternative < 0 || c.Alternative >= specs[c.Keyword].Alternatives.Count
            || c.Source is null || c.Source.ObjectId != item.SourceObjectId || c.Source.Generation < 0
            || c.ControlledRunes < 0 || c.AcquiredTick < 0 || c.AcquiredTick > state.Tick || c.FaceRevision < 0
            || item.StackItemId != $"STACK-{c.AcquiredTick}-{c.Source.ObjectId}-EMPOWER"
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.TargetObjectIds.Count != 0 || item.OptionalCosts.Count != 0
            || !state.Seats.ContainsKey(item.ControllerId)
            || state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) && c.Source.Generation > source.ObjectGeneration
            || !AttachedRuleTextRules.ValidCaptured(state, c.Origin, item.SourceObjectId, c.Source.Generation, c.CardNo)
            || c.Origin is null && state.CardObjects.TryGetValue(c.Source.ObjectId, out var live) && live.ObjectGeneration == c.Source.Generation
                && CopyFaceTimeline.FaceAt(live, c.FaceRevision, c.AcquiredTick) != c.CardNo) return false;
        var spec = specs[c.Keyword]; var cost = spec.Alternatives[c.Alternative];
        if (!SupportedCost(cost) || c.PaidMana != Mana(spec, cost, c.ControlledRunes)) return false;
        if (!RequiresHandDiscard(cost)) return c.DiscardedHandCard is null;
        return c.DiscardedHandCard is { Generation: >= 0 } discarded
            && !string.IsNullOrWhiteSpace(discarded.ObjectId) && discarded.ObjectId != c.Source.ObjectId
            && state.CardObjects.TryGetValue(discarded.ObjectId, out var paidCard)
            && discarded.Generation <= paidCard.ObjectGeneration;
    }
}

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> EmpowerHandDiscardCostChoices(MatchState state, string actor, string source)
        => !state.PlayerZones.TryGetValue(actor, out var zones) ? [] : zones.Hand
            .Where(id => id != source && IsFriendlyHandCard(state, actor, id)
                && state.CardObjects.TryGetValue(id, out var card) && !card.IsFaceDown && !IsTokenObject(card)
                && !card.Tags.Contains(CardObjectTags.RuneCard)
                && card.Tags.Any(tag => tag is CardObjectTags.UnitCard or CardObjectTags.SpellCard or CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal).ToArray();

    internal static bool TrySelectEmpowerHandDiscardCost(MatchState state, string actor, string source,
        IReadOnlyList<string> choices, out ObjectBinding? discarded)
    {
        discarded = null;
        if (choices.Count != 1 || !TryParseDiscardHandCardOptionalCost(choices[0], out var id)
            || choices[0] != DiscardHandCardOptionalCostPrefix + id
            || !EmpowerHandDiscardCostChoices(state, actor, source).Contains(id, StringComparer.Ordinal)) return false;
        discarded = new(id, state.CardObjects[id].ObjectGeneration); return true;
    }

    private static ResolutionResult ResolveEmpowerActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var source) || !EmpowerRules.CanActivate(state, intent.PlayerId, source))
            return RejectWithCorePrompts(state, "请在你的开放主阶段选择一名尚未强化的公开单位。", ErrorCodes.InvalidTarget);
        var ability = EmpowerRules.Abilities(state, source).FirstOrDefault(a => a.Id == command.AbilityId
            && (a.InstanceId is null ? string.IsNullOrEmpty(command.AbilityInstanceId) : a.InstanceId == command.AbilityInstanceId));
        if (ability is null || command.TargetObjectIds.Count != 0 || ability.Cost.Exhaust && source.IsExhausted)
            return RejectWithCorePrompts(state, "强化来源不是目标，请选择当前存在的技能与费用。", ErrorCodes.InvalidTarget);
        var requestedCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, requestedCosts,
            out var extra, out var resources, out var recycled))
            return RejectWithCorePrompts(state, "强化费用选择无效。", ErrorCodes.InvalidTarget);
        ObjectBinding? discarded = null;
        if (EmpowerRules.RequiresHandDiscard(ability.Cost))
        {
            if (requestedCosts.Count != (command.OptionalCosts?.Count ?? 0)
                || !TrySelectEmpowerHandDiscardCost(state, intent.PlayerId, source.ObjectId, extra, out discarded))
                return RejectWithCorePrompts(state, "请恰好选择一张合法手牌支付强化费用。", ErrorCodes.InvalidTarget);
        }
        else if (extra.Count != 0)
            return RejectWithCorePrompts(state, "强化费用选择无效。", ErrorCodes.InvalidTarget);
        var runes = EmpowerRules.ControlledRunes(state, intent.PlayerId); var mana = EmpowerRules.Mana(ability.Spec, ability.Cost, runes);
        var cost = ability.Cost; var totalPower = cost.Power + cost.TypedPower.Values.Sum();
        var plan = new PaymentCostRules.PaymentPlan(PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY", intent.PlayerId,
            source.ObjectId, ability.Id), "ACTIVATE_ABILITY", intent.PlayerId, cost.Mana, mana, cost.Power, totalPower, cost.TypedPower,
            paymentResourceActionIds: resources, reason: EmpowerRules.Effect, sourceObjectId: source.ObjectId, abilityId: ability.Id);
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>(); var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var pools = ApplyRecycleRunePaymentResourceActions(state.RunePools, zones, cards, locations, intent.PlayerId, recycled, events,
            "ACTIVATE_ABILITY", plan.PaymentId); var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience);
        if (!payment.Accepted) return RejectWithCorePrompts(state, "资源不足，无法支付强化费用。", ErrorCodes.InsufficientCost);
        var effects = state.UntilEndOfTurnEffects;
        if (discarded is not null)
        {
            if (!TryDiscardCardFromHand(zones, cards, intent.PlayerId, discarded.ObjectId))
                return RejectWithCorePrompts(state, "无法支付所选弃牌费用。", ErrorCodes.InvalidTarget);
            events.Add(new("CARD_DISCARDED", "弃置所选手牌作为强化费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
                ["targetObjectId"] = discarded.ObjectId, ["destinationZone"] = "GRAVEYARD", ["reason"] = "REQUIRED_COST" }));
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, intent.PlayerId, "CARD_DISCARDED",
                source.ObjectId, [discarded.ObjectId], events);
            effects = MarkPlayerDiscardedHandCardsThisTurn(effects, intent.PlayerId, [discarded.ObjectId]);
            locations = ReconcileObjectLocations(locations, zones);
        }
        if (cost.Exhaust) cards[source.ObjectId] = cards[source.ObjectId] with { IsExhausted = true };
        var context = new EmpowerActivationContext(new(source.ObjectId, source.ObjectGeneration), ability.Instance.CardNo,
            ability.Spec.Index, ability.Alternative, mana, runes, state.Tick + 1, source.CopyFaceTransitions?.Count ?? 0,
            ability.Instance.Origin, discarded);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-EMPOWER", intent.PlayerId, source.ObjectId,
            EmpowerRules.Effect, ability.Instance.CardNo, [], 0, 0, []) { EmpowerActivation = context };
        var paidDetails = new Dictionary<string, object?> { ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id };
        if (discarded is not null) paidDetails["discardedHandCardObjectIds"] = new[] { discarded.ObjectId };
        events.Add(new("COST_PAID", "支付强化费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, paidDetails)));
        events.Add(new("ABILITY_ACTIVATED", "激活强化技能", new Dictionary<string, object?> { ["sourceObjectId"] = source.ObjectId,
            ["abilityId"] = ability.Id, ["abilityInstanceId"] = command.AbilityInstanceId, ["effectKind"] = EmpowerRules.Effect,
            ["targetObjectIds"] = Array.Empty<string>() }));
        events.Add(new("STACK_ITEM_ADDED", "强化技能进入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = source.ObjectId, ["effectKind"] = EmpowerRules.Effect }));
        var next = state with { Tick = state.Tick + 1, CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            RunePools = payment.RunePools, PlayerExperience = payment.PlayerExperience, UntilEndOfTurnEffects = effects,
            StackItems = [..state.StackItems, item],
            TimingState = TimingStates.NeutralClosed, ActivePlayerId = intent.PlayerId, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveEmpowerStack(MatchState state, StackItemState item)
    {
        if (!EmpowerRules.Valid(state, item)) throw new InvalidOperationException("Invalid captured empower activation.");
        return ApplyEmpowerAction(state, item.EmpowerActivation!.Source);
    }

    private static StackResolutionResult ApplyEmpowerAction(MatchState state, ObjectBinding binding)
    {
        if (!state.CardObjects.TryGetValue(binding.ObjectId, out var source) || source.ObjectGeneration != binding.Generation
            || !IsObjectOnField(state.PlayerZones, source.ObjectId) || source.IsEmpowered) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value); cards[source.ObjectId] = source with { IsEmpowered = true };
        var payload = new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["objectGeneration"] = source.ObjectGeneration };
        var insights = CaptureRuleTextInsights(state.PlayerZones, cards, cards[source.ObjectId],
            EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source), "EMPOWERED");
        if (insights.Count > 0) payload["insightTriggerContexts"] = insights;
        return NoopStackResolutionResult(state) with { CardObjects = cards,
            Events = [new("OBJECT_EMPOWERED", source.Tags.Contains(CardObjectTags.UnitCard) ? "单位变为已强化状态" : "物体变为已强化状态", payload)] };
    }
}
