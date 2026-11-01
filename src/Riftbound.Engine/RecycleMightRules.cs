using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecycleMightActivationContext(ObjectBinding Source, string CardNo, int AbilityIndex,
    ObjectBinding RecycledCard, long AcquiredTick, long FaceRevision, string RuleTextInstanceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceTokenFactoryCardNo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CeasedSelfAbilitySource? CeasedSource { get; init; }
}

internal sealed record RecycleMightAbility(string Id, RuleTextInstance Instance, int Index, ActivatedAbilitySpec Spec)
{
    internal string? InstanceId => Instance.Origin is null ? null : $"{Instance.Id}:{Id}";
}

internal static class RecycleMightRules
{
    internal const string Effect = "RECYCLE_GRAVEYARD_CARD_GRANT_SELF_MIGHT";
    private static readonly Lazy<IReadOnlyDictionary<string, ActivatedAbilitySpec[]>> SpecsByCard = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(card => card.CardNo,
            card => ActivatedAbilityParser.Parse(card.CardEffect).Where(a => a.Kind == ActivatedAbilityKinds.RecycleGraveyardCardGrantSelfMight).ToArray(), StringComparer.Ordinal));
    internal static IReadOnlyList<ActivatedAbilitySpec> Specs(string cardNo) => SpecsByCard.Value.GetValueOrDefault(cardNo) ?? [];
    internal static IEnumerable<RecycleMightAbility> Abilities(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).SelectMany(instance => Specs(instance.CardNo)
            .Select((spec, index) => new RecycleMightAbility($"RECYCLE-MIGHT:{index}", instance, index, spec)));
    internal static bool CanActivate(MatchState state, string player, CardObjectState source)
        => CoreRuleEngine.CanActivateOrdinaryPowerAbility(state, player) && source.ControllerId == player
            && !source.IsFaceDown && source.Tags.Contains(CardObjectTags.UnitCard)
            && state.PlayerZones.TryGetValue(player, out var zones)
            && (zones.Base.Contains(source.ObjectId) || zones.Battlefields.Contains(source.ObjectId));
    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (item.RecycleMightActivation is not { } context) return item.EffectKind != Effect;
        var specs = Specs(context.CardNo);
        if (item.EffectKind != Effect || item.CardNo != context.CardNo || context.AbilityIndex < 0
            || context.AbilityIndex >= specs.Count || specs[context.AbilityIndex].PowerModifierAmount is not > 0
            || context.Source is not { Generation: >= 0 } || context.Source.ObjectId != item.SourceObjectId
            || context.RecycledCard is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(context.RecycledCard.ObjectId)
            || context.AcquiredTick <= 0 || context.AcquiredTick > state.Tick || context.FaceRevision < 0
            || item.StackItemId != $"STACK-{context.AcquiredTick}-RECYCLE-MIGHT"
            || item.TargetObjectIds.Count != 0 || item.OptionalCosts.Count != 0 || item.TargetGenerations is not null
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.DamageAmount != 0
            || !state.Seats.ContainsKey(item.ControllerId)
            || !SelfAbilitySourceRules.Valid(state, context.Source, context.CardNo, context.FaceRevision, context.AcquiredTick,
                context.SourceTokenFactoryCardNo, context.CeasedSource, context.Origin is null)
            || !state.CardObjects.TryGetValue(context.RecycledCard.ObjectId, out var paid) || paid.ObjectGeneration < context.RecycledCard.Generation
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, item.SourceObjectId, context.Source.Generation, context.CardNo)
            || item.EmpowerActivation is not null || item.TargetedPowerAbility is not null || item.PlayCost is not null
            || item.SpellContext is not null || item.FieldContext is not null) return false;
        return context.Origin is null
            ? context.RuleTextInstanceId == $"PRINTED:{context.Source.ObjectId}:{context.Source.Generation}"
            : context.RuleTextInstanceId == $"ATTACHED:{context.Origin.Equipment.ObjectId}:{context.Origin.Equipment.Generation}:{context.Origin.Revision}";
    }
}

public sealed partial class CoreRuleEngine
{
    internal static string[] RecycleMightCostChoices(MatchState state, string player)
        => !state.PlayerZones.TryGetValue(player, out var zones) ? [] : zones.Graveyard
            .Where(id => CanRecycleGraveyardCardAsAdditionalCost(state, player, id)
                && state.CardObjects.TryGetValue(id, out var card) && !TokenObjectRules.IsToken(card)
                && !card.Tags.Contains(CardObjectTags.RuneCard)
                && card.Tags.Any(tag => tag is CardObjectTags.UnitCard or CardObjectTags.SpellCard or CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static ResolutionResult ResolveRecycleMightActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (command.ServerManaSourceSelection is not null || !state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || !RecycleMightRules.CanActivate(state, intent.PlayerId, source))
            return RejectWithCorePrompts(state, "请在你的开放主阶段选择受控的公开单位。", ErrorCodes.InvalidTarget);
        var ability = RecycleMightRules.Abilities(state, source).FirstOrDefault(a => a.Id == command.AbilityId
            && (a.InstanceId is null ? string.IsNullOrEmpty(command.AbilityInstanceId) : a.InstanceId == command.AbilityInstanceId));
        var costs = command.OptionalCosts ?? [];
        if (ability is null || command.TargetObjectIds.Count != 0 || costs.Count != 1
            || !TryParseRecycleGraveyardCardAdditionalCost(costs[0], out var recycledId)
            || costs[0] != RecycleGraveyardCardAdditionalCostPrefix + recycledId
            || !RecycleMightCostChoices(state, intent.PlayerId).Contains(recycledId, StringComparer.Ordinal))
            return RejectWithCorePrompts(state, "请恰好回收一张你的废牌以支付技能费用。", ErrorCodes.InvalidTarget);

        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var paid = new ObjectBinding(recycledId, cards[recycledId].ObjectGeneration);
        var recycled = RecycleGraveyardCards(state, zones, cards, intent.PlayerId, source.ObjectId, [recycledId], "ACTIVATED_ABILITY_COST");
        var context = new RecycleMightActivationContext(new(source.ObjectId, source.ObjectGeneration), ability.Instance.CardNo,
            ability.Index, paid, state.Tick + 1, source.CopyFaceTransitions?.Count ?? 0, ability.Instance.Id, ability.Instance.Origin) {
                SourceTokenFactoryCardNo = source.TokenFactoryCardNo };
        var item = new StackItemState($"STACK-{state.Tick + 1}-RECYCLE-MIGHT", intent.PlayerId,
            source.ObjectId, RecycleMightRules.Effect, ability.Instance.CardNo, [], 0, 1, []) { RecycleMightActivation = context };
        var events = recycled.Events.Concat(new[] {
            new GameEvent("COST_PAID", "回收一张废牌以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id,
                ["recycledAdditionalCostTargetObjectIds"] = new[] { recycledId } }),
            new GameEvent("ABILITY_ACTIVATED", "激活回收费用技能", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id,
                ["abilityInstanceId"] = command.AbilityInstanceId, ["effectKind"] = RecycleMightRules.Effect }),
            new GameEvent("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?> {
                ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = source.ObjectId, ["effectKind"] = item.EffectKind }) }).ToArray();
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RngCursor = recycled.RngCursor,
            StackItems = [..state.StackItems, item], TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = intent.PlayerId, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveRecycleMightStack(MatchState state, StackItemState item)
    {
        if (!RecycleMightRules.Valid(state, item)) throw new InvalidOperationException("Invalid recycle-cost ability.");
        var context = item.RecycleMightActivation!;
        if (!state.CardObjects.TryGetValue(context.Source.ObjectId, out var source) || source.ObjectGeneration != context.Source.Generation
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state);
        var amount = RecycleMightRules.Specs(context.CardNo)[context.AbilityIndex].PowerModifierAmount!.Value;
        var behavior = new CardBehaviorDefinition(context.CardNo, "回收技能", 0, RecycleMightRules.Effect, 0, 0,
            PowerModifierAmount: amount);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, amount, out var modified);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [modified] };
    }
}
