using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceBoonMightContext(ObjectBinding Source, string CardNo, int AbilityIndex,
    long AcquiredTick, long FaceRevision, string RuleTextInstanceId, string? TokenFactoryCardNo = null,
    RuleTextInstanceBinding? Origin = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceBoonMightCost? BoonCost { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CeasedSourceBoonMight? CeasedSource { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceBoonMightCost(ObjectBinding Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? TokenCeasedAtTick = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CeasedSourceBoonMight(ObjectBinding Source, string CardNo, long FaceRevision,
    long AcquiredTick, string TokenFactoryCardNo, long DepartedTick);

internal sealed record SourceBoonMightAbility(string Id, RuleTextInstance Instance, int Index, ActivatedAbilitySpec Spec)
{
    internal string? InstanceId => Instance.Origin is null ? null : $"{Instance.Id}:{Id}";
}

internal static class SourceBoonMightRules
{
    internal const string Effect = "CONSUME_SOURCE_BOON_GRANT_SELF_MIGHT";
    private static readonly Lazy<IReadOnlyDictionary<string, ActivatedAbilitySpec[]>> SpecsByCard = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(card => card.CardNo,
            card => ActivatedAbilityParser.Parse(card.CardEffect)
                .Where(a => a.Kind == ActivatedAbilityKinds.ConsumeSourceBoonGrantSelfMight).ToArray(), StringComparer.Ordinal));
    internal static IReadOnlyList<ActivatedAbilitySpec> Specs(string cardNo) => SpecsByCard.Value.GetValueOrDefault(cardNo) ?? [];
    internal static IEnumerable<SourceBoonMightAbility> Abilities(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).SelectMany(instance => Specs(instance.CardNo)
            .Select((spec, index) => new SourceBoonMightAbility($"SOURCE-BOON-MIGHT:{index}", instance, index, spec)));
    internal static bool CanActivate(MatchState state, string player, CardObjectState source)
        => CoreRuleEngine.CanActivateOrdinaryPowerAbility(state, player) && source.ControllerId == player
            && !source.IsFaceDown && source.Tags.Contains(CardObjectTags.UnitCard) && source.Tags.Contains(CardObjectTags.Boon)
            && state.PlayerZones.TryGetValue(player, out var zones)
            && (zones.Base.Contains(source.ObjectId) || zones.Battlefields.Contains(source.ObjectId));
    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (item.SourceBoonMight is not { } c) return item.EffectKind != Effect;
        if (string.IsNullOrWhiteSpace(c.CardNo)) return false;
        var specs = Specs(c.CardNo);
        if (item.EffectKind != Effect || item.CardNo != c.CardNo || c.AbilityIndex < 0 || c.AbilityIndex >= specs.Count
            || c.Source is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(c.Source.ObjectId) || c.Source.ObjectId != item.SourceObjectId
            || c.AcquiredTick <= 0 || c.AcquiredTick > state.Tick || c.FaceRevision < 0
            || item.StackItemId != $"STACK-{c.AcquiredTick}-SOURCE-BOON-MIGHT"
            || item.TargetObjectIds.Count != 0 || item.OptionalCosts.Count != 0 || item.TargetGenerations is not null
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.DamageAmount != 0
            || string.IsNullOrWhiteSpace(item.ControllerId) || !state.Seats.ContainsKey(item.ControllerId)
            || item.EmpowerActivation is not null || item.TargetedPowerAbility is not null || item.PlayCost is not null
            || item.SpellContext is not null || item.FieldContext is not null) return false;
        return CoreRuleEngine.ValidSourceBoonMightBinding(state, item);
    }
}

public sealed partial class CoreRuleEngine
{
    internal static bool ValidSourceBoonMightBinding(MatchState state, StackItemState item)
    {
        var c = item.SourceBoonMight!;
        if (c.BoonCost is not { } cost || cost.Source != c.Source
            || cost.TokenCeasedAtTick is { } costDeparture
                && (costDeparture != c.AcquiredTick || c.CeasedSource?.DepartedTick != costDeparture)) return false;
        if (state.CardObjects.TryGetValue(c.Source.ObjectId, out var current))
        {
            if (c.CeasedSource is not null || current.ObjectGeneration < c.Source.Generation
                || current.TokenFactoryCardNo != c.TokenFactoryCardNo) return false;
        }
        else if (!P6TokenFactoryCatalog.IsUnitTokenFactory(c.TokenFactoryCardNo)
            || c.CeasedSource is not { } ceased || ceased.Source != c.Source || ceased.CardNo != c.CardNo
            || ceased.FaceRevision != c.FaceRevision || ceased.AcquiredTick != c.AcquiredTick
            || ceased.TokenFactoryCardNo != c.TokenFactoryCardNo || ceased.DepartedTick > state.Tick
            || ceased.DepartedTick < c.AcquiredTick
            || ceased.DepartedTick == c.AcquiredTick && cost.TokenCeasedAtTick != c.AcquiredTick) return false;
        return c.Origin is null
            ? c.RuleTextInstanceId == $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}"
                && PredictBindingMatches(state, c.Source, c.CardNo, c.FaceRevision, c.AcquiredTick, c.TokenFactoryCardNo)
            : c.Origin.Equipment is { } equipment
                && c.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{c.Origin.Revision}"
                && c.FaceRevision == c.Origin.FaceRevision && c.Origin.CapturedTick <= c.AcquiredTick
                && AttachedRuleTextRules.ValidCaptured(state, c.Origin, item.SourceObjectId, c.Source.Generation, c.CardNo);
    }

    private static MatchState RecordCeasedSourceBoonMight(MatchState before, MatchState state, IReadOnlySet<string> departed)
    {
        var stack = state.StackItems.Select(item => {
            if (item.SourceBoonMight is not { CeasedSource: null } context
                || !departed.Contains(context.Source.ObjectId)
                || !P6TokenFactoryCatalog.IsUnitTokenFactory(context.TokenFactoryCardNo)
                || !before.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
                || source.ObjectGeneration != context.Source.Generation
                || source.TokenFactoryCardNo != context.TokenFactoryCardNo
                || !SourceBoonMightRules.Valid(before, item)) return item;
            // CN 186.1, 355.9.c and 377.3: the token disappears while its
            // already paid skill remains independent on the stack.
            return item with { SourceBoonMight = context with { CeasedSource = new(context.Source,
                context.CardNo, context.FaceRevision, context.AcquiredTick, context.TokenFactoryCardNo!, state.Tick) } };
        }).ToArray();
        return state with { StackItems = stack };
    }

    private static ResolutionResult ResolveSourceBoonMightActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (command.ServerManaSourceSelection is not null || !state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || !SourceBoonMightRules.CanActivate(state, intent.PlayerId, source))
            return RejectWithCorePrompts(state, "请在你的开放主阶段选择一名有增益的受控单位。", ErrorCodes.InvalidTarget);
        var ability = SourceBoonMightRules.Abilities(state, source).FirstOrDefault(a => a.Id == command.AbilityId
            && (a.InstanceId is null ? string.IsNullOrEmpty(command.AbilityInstanceId) : a.InstanceId == command.AbilityInstanceId));
        if (ability is null || command.TargetObjectIds.Count != 0 || (command.OptionalCosts?.Count ?? 0) != 0)
            return RejectWithCorePrompts(state, "此技能只消耗来源自身的增益，不指定目标。", ErrorCodes.InvalidTarget);

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var context = new SourceBoonMightContext(new(source.ObjectId, source.ObjectGeneration), ability.Instance.CardNo,
            ability.Index, state.Tick + 1, ability.Instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
            ability.Instance.Id, source.TokenFactoryCardNo, ability.Instance.Origin) {
                BoonCost = new(new(source.ObjectId, source.ObjectGeneration)) };
        var item = new StackItemState($"STACK-{state.Tick + 1}-SOURCE-BOON-MIGHT", intent.PlayerId,
            source.ObjectId, SourceBoonMightRules.Effect, ability.Instance.CardNo, [], 0, 1, []) { SourceBoonMight = context };
        var events = new List<GameEvent> {
            new("BOON_CONSUMED", "消耗来源增益以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
                ["previousPower"] = source.Power, ["power"] = source.Power - 1, ["abilityId"] = ability.Id }),
            new("COST_PAID", "支付消耗自身增益费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id,
                ["consumedBoonObjectIds"] = new[] { source.ObjectId } }),
            new("ABILITY_ACTIVATED", "激活消耗增益技能", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id,
                ["abilityInstanceId"] = command.AbilityInstanceId, ["effectKind"] = item.EffectKind }),
            new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?> {
                ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = source.ObjectId, ["effectKind"] = item.EffectKind }) };
        using (new MightInstructionScope(state, zones, cards, events, locations))
            cards[source.ObjectId] = source with { Power = source.Power - 1,
                Tags = source.Tags.Where(t => t != CardObjectTags.Boon).ToArray() };
        // Paying the boon can make existing damage lethal before anyone may
        // respond. The earned ability remains even when its source dies.
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        if (P6TokenFactoryCatalog.IsUnitTokenFactory(source.TokenFactoryCardNo) && !cards.ContainsKey(source.ObjectId))
        {
            context = context with {
                BoonCost = context.BoonCost! with { TokenCeasedAtTick = state.Tick + 1 },
                CeasedSource = new(context.Source, context.CardNo, context.FaceRevision,
                    context.AcquiredTick, source.TokenFactoryCardNo!, state.Tick + 1) };
            item = item with { SourceBoonMight = context };
        }
        var next = state with { Tick = state.Tick + 1, CardObjects = cards, PlayerZones = zones,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = cleanup.RunePools,
            TriggerQueue = state.TriggerQueue.Concat(cleanup.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, cleanup.DestroyedUnitOwnerIds),
            StackItems = [..state.StackItems, item], TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = intent.PlayerId, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveSourceBoonMightStack(MatchState state, StackItemState item)
    {
        if (!SourceBoonMightRules.Valid(state, item)) throw new InvalidOperationException("Invalid source boon ability.");
        var c = item.SourceBoonMight!;
        var visible = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "消耗增益技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = visible, ["effectKind"] = item.EffectKind }) };
        if (!state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) || source.ObjectGeneration != c.Source.Generation
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state) with { Events = events };
        var amount = SourceBoonMightRules.Specs(c.CardNo)[c.AbilityIndex].PowerModifierAmount!.Value;
        var behavior = new CardBehaviorDefinition(c.CardNo, "消耗增益技能", 0, SourceBoonMightRules.Effect, 0, 0, PowerModifierAmount: amount);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, amount, out var modified);
        events.Add(modified);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
