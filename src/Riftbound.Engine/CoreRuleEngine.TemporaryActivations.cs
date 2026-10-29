using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record GrantedActivatedAbilitySpec(string Kind, int GenericPowerCost);
public sealed record TemporaryActivatedAbilityGrant(ObjectBinding Spell, string CardNo, ObjectBinding Unit,
    string CreatingStackItemId, int TurnNumber, int ExecutionIndex);
public sealed record TemporaryActivationContext(TemporaryActivatedAbilityGrant Grant, string CardNo,
    long ActivatedTick, long FaceRevision);

public sealed partial class CoreRuleEngine
{
    internal const string TemporaryActivationPrefix = "GRANTED_ACTIVATION|";
    internal const string TemporaryActivationEffect = "GRANTED_UNIT_ACTIVATION";
    internal const string GrantedReadySelfKind = "READY_SELF";

    private static string TemporaryActivationMarker(TemporaryActivatedAbilityGrant grant)
        => TemporaryActivationPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(grant)));

    internal static TemporaryActivatedAbilityGrant? ReadTemporaryActivation(string marker)
    {
        if (!marker.StartsWith(TemporaryActivationPrefix, StringComparison.Ordinal)) return null;
        try
        {
            var grant = JsonSerializer.Deserialize<TemporaryActivatedAbilityGrant>(
                Encoding.UTF8.GetString(Convert.FromBase64String(marker[TemporaryActivationPrefix.Length..])));
            return grant is not null && marker == TemporaryActivationMarker(grant) ? grant : null;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException) { return null; }
    }

    internal static string TemporaryActivationId(TemporaryActivatedAbilityGrant grant)
        => "GRANTED:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(TemporaryActivationMarker(grant))));

    internal static string PublicTemporaryActivationEffect(string effect)
        => effect.StartsWith(TemporaryActivationPrefix, StringComparison.Ordinal)
            ? ReadTemporaryActivation(effect) is { } grant ? TemporaryActivationId(grant) : "GRANTED:INVALID"
            : effect;

    internal static GrantedActivatedAbilitySpec? TemporaryActivationDefinition(TemporaryActivatedAbilityGrant grant)
        => CardBehaviorRegistry.TryGetByCardNo(grant.CardNo, out var behavior)
            ? behavior.GrantsActivatedAbilityUntilEndOfTurn : null;

    private static bool ValidTemporaryActivationGrant(MatchState state, TemporaryActivatedAbilityGrant grant)
    {
        if (grant.Spell is not { Generation: >= 0 } || grant.Unit is not { Generation: >= 0 }
            || string.IsNullOrWhiteSpace(grant.Spell.ObjectId) || string.IsNullOrWhiteSpace(grant.Unit.ObjectId)
            || grant.Spell.ObjectId == grant.Unit.ObjectId || string.IsNullOrWhiteSpace(grant.CardNo) || grant.TurnNumber != state.TurnNumber
            || grant.TurnNumber <= 0 || grant.ExecutionIndex < 0
            || TemporaryActivationDefinition(grant) is not { Kind: GrantedReadySelfKind, GenericPowerCost: >= 0 }
            || !state.CardObjects.TryGetValue(grant.Spell.ObjectId, out var spell)
            || spell.ObjectGeneration < grant.Spell.Generation
            || spell.ObjectGeneration == grant.Spell.Generation && spell.CardNo != grant.CardNo
            || string.IsNullOrEmpty(grant.CreatingStackItemId)
            || !grant.CreatingStackItemId.StartsWith("STACK-", StringComparison.Ordinal)
            || !grant.CreatingStackItemId.EndsWith("-" + grant.Spell.ObjectId, StringComparison.Ordinal)
            || grant.CreatingStackItemId.Length <= grant.Spell.ObjectId.Length + 7
            || !long.TryParse(grant.CreatingStackItemId[6..^(grant.Spell.ObjectId.Length + 1)],
                NumberStyles.None, CultureInfo.InvariantCulture, out var tick)
            || tick <= 0 || tick > state.Tick
            || grant.CreatingStackItemId != $"STACK-{tick.ToString(CultureInfo.InvariantCulture)}-{grant.Spell.ObjectId}") return false;
        return !state.CardObjects.TryGetValue(grant.Unit.ObjectId, out var unit)
            || unit.ObjectGeneration >= grant.Unit.Generation;
    }

    internal static bool ValidCurrentTemporaryActivations(MatchState state)
    {
        foreach (var unit in state.CardObjects.Values)
        {
            if (unit.UntilEndOfTurnEffects is null) return false;
            var markers = unit.UntilEndOfTurnEffects.Where(e => e?.StartsWith(TemporaryActivationPrefix, StringComparison.Ordinal) == true).ToArray();
            if (markers.Distinct(StringComparer.Ordinal).Count() != markers.Length) return false;
            foreach (var marker in markers)
                if (ReadTemporaryActivation(marker) is not { } grant || !ValidTemporaryActivationGrant(state, grant)
                    || grant.Unit != new ObjectBinding(unit.ObjectId, unit.ObjectGeneration)
                    || !IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(state.PlayerZones, unit.ObjectId)) return false;
        }
        return true;
    }

    internal static IEnumerable<TemporaryActivatedAbilityGrant> TemporaryActivations(MatchState state, string player, CardObjectState unit)
        => unit.ControllerId != player || !IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(state.PlayerZones, unit.ObjectId)
            ? [] : unit.UntilEndOfTurnEffects.Where(e => e.StartsWith(TemporaryActivationPrefix, StringComparison.Ordinal))
                .Select(ReadTemporaryActivation).OfType<TemporaryActivatedAbilityGrant>()
                .Where(grant => grant.Unit == new ObjectBinding(unit.ObjectId, unit.ObjectGeneration)
                    && ValidTemporaryActivationGrant(state, grant));

    private static CardObjectState GrantTemporaryActivation(MatchState state, StackItemState item,
        CardObjectState unit, List<GameEvent> events)
    {
        var source = state.CardObjects[item.SourceObjectId];
        var grant = new TemporaryActivatedAbilityGrant(new(source.ObjectId, source.ObjectGeneration), item.CardNo!,
            new(unit.ObjectId, unit.ObjectGeneration), item.StackItemId, state.TurnNumber, item.CompletedRepeatExecutions);
        var marker = TemporaryActivationMarker(grant);
        events.Add(new("TEMPORARY_ACTIVATED_ABILITY_GRANTED", "单位在本回合内获得启动技能", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = unit.ObjectId,
            ["abilityId"] = TemporaryActivationId(grant), ["duration"] = "UNTIL_END_OF_TURN" }));
        return unit with { UntilEndOfTurnEffects = [..unit.UntilEndOfTurnEffects, marker] };
    }

    internal static bool ValidTemporaryActivation(MatchState state, StackItemState item)
    {
        if (item.TemporaryActivation is not { } context) return item.EffectKind != TemporaryActivationEffect;
        var grant = context.Grant;
        if (grant is null || !ValidTemporaryActivationGrant(state, grant) || item.EffectKind != TemporaryActivationEffect
            || grant.Unit.ObjectId != item.SourceObjectId || context.CardNo != item.CardNo
            || context.ActivatedTick <= 0 || context.ActivatedTick > state.Tick || context.FaceRevision < 0
            || item.StackItemId != $"STACK-{context.ActivatedTick.ToString(CultureInfo.InvariantCulture)}-{item.SourceObjectId}-GRANTED"
            || item.SourceConfirmed || item.TargetObjectIds.Count != 0 || item.DamageAmount != 0
            || item.EffectRepeatCount != 1 || item.OptionalCosts.Count != 0 || item.RepeatExecutions is not null
            || !state.Seats.ContainsKey(item.ControllerId)) return false;
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var unit))
            return P6TokenFactoryCatalog.TryGetByCardNo(context.CardNo, out _);
        return unit.ObjectGeneration > grant.Unit.Generation || unit.ObjectGeneration == grant.Unit.Generation
            && CopyFaceTimeline.FaceAt(unit, context.FaceRevision, context.ActivatedTick) == context.CardNo;
    }

    private static ResolutionResult ResolveTemporaryActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!CanActivateOrdinaryPowerAbility(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "此技能只能在你的开放主阶段激活。", ErrorCodes.PhaseNotAllowed);
        if (command.ServerManaSourceSelection is not null || !string.IsNullOrWhiteSpace(command.AbilityInstanceId)
            || command.TargetObjectIds.Count != 0 || !state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || TemporaryActivations(state, intent.PlayerId, source).FirstOrDefault(g => TemporaryActivationId(g) == command.AbilityId) is not { } grant)
            return RejectWithCorePrompts(state, "来源当前没有所选的本回合启动技能。", ErrorCodes.InvalidTarget);
        var requestedCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRenektonPlayPayment(requestedCosts, out var withoutRenekton, out var renektonResourceAction)
            || !TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, withoutRenekton,
                out var otherCosts, out var resources, out var recycled) || otherCosts.Count != 0)
            return RejectWithCorePrompts(state, "启动技能的支付资源行动无效。", ErrorCodes.InvalidTarget);
        var definition = TemporaryActivationDefinition(grant)!;
        var orderedResources = renektonResourceAction is null ? resources
            : OrderedRenektonPaymentResources(requestedCosts, resources, renektonResourceAction);
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY", intent.PlayerId, source.ObjectId, command.AbilityId);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, "ACTIVATE_ABILITY", intent.PlayerId,
            0, 0, definition.GenericPowerCost, definition.GenericPowerCost,
            paymentResourceActionIds: orderedResources, sourceObjectId: source.ObjectId, abilityId: command.AbilityId,
            reason: TemporaryActivationEffect, powerPurpose: PowerPaymentPurpose.Other,
            manaPurpose: ManaPurposeForActivatedAbility(state, source.ObjectId));
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>();
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (renektonResourceAction is not null)
        {
            if (!TryApplyOrderedRenektonResources(state, intent.PlayerId, orderedResources, zones,
                    ref cards, locations, ref pools, events, "ACTIVATE_ABILITY", paymentId))
                return RejectWithCorePrompts(state, "支付资源行动顺序或来源无效。", ErrorCodes.InvalidTarget);
        }
        else pools = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, locations,
            intent.PlayerId, recycled, events, "ACTIVATE_ABILITY", paymentId);
        var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!payment.Accepted) return RejectWithCorePrompts(state, "符能不足，无法支付启动技能费用。", ErrorCodes.InsufficientCost);
        var cardNo = AttachedRuleTextRules.Face(source)!;
        var context = new TemporaryActivationContext(grant, cardNo, state.Tick + 1, source.CopyFaceTransitions?.Count ?? 0);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-GRANTED", intent.PlayerId,
            source.ObjectId, TemporaryActivationEffect, cardNo, [], 0, 0, []) { TemporaryActivation = context };
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId,
            ["controllerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
            ["abilityId"] = command.AbilityId, ["cardNo"] = cardNo, ["effectKind"] = TemporaryActivationEffect };
        events.Add(new("ABILITY_ACTIVATED", "激活本回合获得的技能", payload));
        events.Add(new("COST_PAID", "支付启动技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools,
            payment.PlayerExperience, new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId })));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = payment.RunePools,
            PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveTemporaryActivationStack(MatchState state, StackItemState item)
    {
        if (!ValidTemporaryActivation(state, item)) throw new InvalidOperationException("Invalid granted activation.");
        var context = item.TemporaryActivation!;
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "本回合获得的启动技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId,
            ["abilityId"] = TemporaryActivationId(context.Grant) }) };
        if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.Grant.Unit.Generation
            && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId))
        {
            var behavior = new CardBehaviorDefinition(context.CardNo, "本回合获得的技能", 0, TemporaryActivationEffect, 0, 0);
            cards[source.ObjectId] = ApplyReadyState(source, behavior, item, source.ObjectId, out var readied, state.PlayerZones, cards);
            if (readied is not null) events.Add(readied);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
