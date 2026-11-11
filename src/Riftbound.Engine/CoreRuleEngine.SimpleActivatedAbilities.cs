using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CeasedSimpleActivationSource(string TokenFactoryCardNo, long DepartedTick)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long SourceFaceRevision { get; init; }
}

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record SimpleActivationContext(string AbilityId, ObjectBinding Source, long AcquiredTick,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? TargetTokenFactories = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    CeasedSimpleActivationSource? CeasedSource = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long SourceFaceRevision { get; init; }
    public string RuleTextInstanceId { get; init; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RuleTextInstanceBinding? Origin { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceTokenFactoryCardNo { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PaidUnitReference? ExhaustedCostUnit { get; init; }
    public string ReturnTiming { get; init; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LinkedAttributeDeclaration? LinkedAttribute { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplayCardNo { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? ConsumedSourceBoon { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ActivationBanishCost? BanishCost { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ObjectBinding>? RefusedEffectPlays { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string SimpleActivationUsePrefix = "PRINTED_ACTIVATION_USED:";

    internal static string SimpleActivationUse(ObjectBinding source, P4ActivatedAbilityDefinition ability, string actor, string? instanceId = null)
        => $"{SimpleActivationUsePrefix}{source.ObjectId}:{source.Generation}:{ability.AbilityId}"
            + (instanceId is null ? "" : ":INSTANCE:" + instanceId)
            + (ability.SimpleEffect?.OncePerTurnPerPlayer == true ? ":PLAYER:" + actor : "");
    internal static bool SimpleActivationInstanceAvailable(MatchState state, CardObjectState source,
        P4ActivatedAbilityDefinition ability, string actor, RuleTextInstance instance)
        => (ability.SimpleEffect?.RequiresEncouraged != true || EncourageActive(state, actor, source.ObjectId))
            && (ability.SimpleEffect?.OncePerTurn != true || !state.UntilEndOfTurnEffects.Contains(
                SimpleActivationUse(new(source.ObjectId, source.ObjectGeneration), ability, actor, instance.Origin is null ? null : instance.Id)));

    private static bool SelectSimpleActivationInstance(MatchState state, CardObjectState source,
        P4ActivatedAbilityDefinition ability, string? id, out RuleTextInstance instance)
    {
        // An omitted id still addresses the host's current text, including a
        // copied face. Additional attached text sets always need their own id.
        var current = AttachmentUnitCopyRules.CurrentText(source);
        instance = AttachedRuleTextRules.ActivatedInstances(state, source, ability).FirstOrDefault(i =>
            string.IsNullOrWhiteSpace(id) ? i.Origin is null || i.Id == current?.Id : i.Id == id)!;
        return instance is not null;
    }

    private static string EnemyUnitTargetMarker(string actor) => "TARGETED_ENEMY_UNIT_THIS_TURN:" + actor;

    // Track confirmed targets for every skill/spell through the shared stack;
    // paying a non-target cost never counts as choosing an enemy unit.
    private static ResolutionResult RecordEnemyUnitTargets(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var markers = result.State.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        foreach (var item in result.State.StackItems) {
            if (item.TargetGenerations is null || item.EmpowerActivation is not null) continue;
            var prior = before.StackItems.FirstOrDefault(i => i.StackItemId == item.StackItemId);
            if (prior?.TargetGenerations is not null && prior.TargetObjectIds.SequenceEqual(item.TargetObjectIds)) continue;
            if (item.TargetObjectIds.Any(id => before.CardObjects.TryGetValue(id, out var target)
                && IsObjectOnField(before.PlayerZones, id) && !target.IsFaceDown
                && target.Tags.Contains(CardObjectTags.UnitCard) && target.ControllerId != item.ControllerId))
                markers.Add(EnemyUnitTargetMarker(item.ControllerId));
        }
        if (markers.Count == result.State.UntilEndOfTurnEffects.Count) return result;
        var next = result.State with { UntilEndOfTurnEffects = markers.Order(StringComparer.Ordinal).ToArray() };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static string[] SimpleActivationDiscardChoices(MatchState state, string actor, bool anyCard = false)
        => state.PlayerZones[actor].Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && (anyCard || card.Tags.Contains(CardObjectTags.EquipmentCard))).Select(id => "DISCARD_HAND_CARD:" + id).ToArray();

    internal static bool SimpleActivationHasAdditionalCost(P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect?.DiscardsEquipmentAsCost == true || ability.SimpleEffect?.DiscardsHandCardAsCost == true
            || ability.SimpleEffect?.ExhaustFriendlyUnitMinimumPowerAsCost is >= 0
            || ability.SimpleEffect?.DestroyFriendlyTagAsCost is not null;

    internal static string[] SimpleActivationAdditionalCostChoices(MatchState state, string actor,
        string sourceId, P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect is { } discard && (discard.DiscardsEquipmentAsCost || discard.DiscardsHandCardAsCost)
            ? SimpleActivationDiscardChoices(state, actor, discard.DiscardsHandCardAsCost)
            : ability.SimpleEffect?.ExhaustFriendlyUnitMinimumPowerAsCost is { } minimum
                ? state.CardObjects.Values.Where(card => (card.ObjectId != sourceId || ability.SimpleEffect.MayExhaustSourceAsAdditionalCost)
                    && CanExhaustUnitForEquipmentCost(state, actor, card.ObjectId)
                    && ResolveCurrentFieldUnitPower(state, card) >= minimum)
                    .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
                    .Select(card => ExhaustFriendlyUnitOptionalCostPrefix + card.ObjectId).ToArray()
            : ability.SimpleEffect?.DestroyFriendlyTagAsCost is { } tag
                ? FriendlyDestructionChoices(state, actor, requiredTag: tag)
                    .Select(card => DestroyFriendlyPermanentAdditionalCostPrefix + card.ObjectId).ToArray() : [];

    internal static bool IsSimpleAbilitySource(MatchState state, string actor, string id, P4ActivatedAbilityDefinition ability)
        => state.CardObjects.TryGetValue(id, out var source) && source.ObjectId == id
            && (source.ControllerId == actor || ability.UsableByAnyPlayerOnOwnTurn && state.TurnPlayerId == actor)
            && !source.IsFaceDown && (!ability.ExhaustsSourceAsCost || !source.IsExhausted)
            && source.Tags.Contains(ability.SourceRequiredTag)
            && AttachedRuleTextRules.ActivatedInstances(state, source, ability)
                .Any(instance => SimpleActivationInstanceAvailable(state, source, ability, actor, instance))
            && FindFieldObjectLocation(state.PlayerZones, id) is { } at && (at.PlayerId == actor || ability.UsableByAnyPlayerOnOwnTurn)
            && state.ObjectLocations.TryGetValue(id, out var precise) && (precise.PlayerId == actor || ability.UsableByAnyPlayerOnOwnTurn) && precise.Zone == at.Zone
            && (!ability.RequiresBattlefieldSource || precise.Zone == "BATTLEFIELD" && state.BattlefieldStates.ContainsKey(id))
            && (ability.SimpleEffect?.RemovesSourceEmpowermentAsCost != true || source.IsEmpowered)
            && (ability.SimpleEffect?.ConsumesSourceBoonAsCost != true || BoonRules.Count(source) > 0)
            && (ability.SimpleEffect?.RequiresUnattachedSource != true || source.AttachedToObjectId is null)
            && (ability.SimpleEffect?.RequiresEnemyUnitTargetThisTurn != true || state.UntilEndOfTurnEffects.Contains(EnemyUnitTargetMarker(actor)))
            && (!SimpleActivationHasAdditionalCost(ability)
                || SimpleActivationAdditionalCostChoices(state, actor, id, ability).Length > 0);

    internal static bool SimpleActivationTargetsOpponent(P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect is { } spec && (spec.CommandsOpponentUnitToSourceBattlefield || spec.OpponentGuessesDeckTopType);

    internal static bool SimpleActivationTargetsPlayer(P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect?.TransfersSourceToChosenPlayer == true || SimpleActivationTargetsOpponent(ability);

    internal static bool IsSimpleAbilityTarget(MatchState state, string actor, string sourceId, string id, P4ActivatedAbilityDefinition ability, string? instanceId = null)
        => ability.SimpleEffect?.RecyclesGraveyardTargets == true ? IsAnyGraveyardCard(state, id)
            : ability.SimpleEffect?.TargetsDeclaredAttribute == true
            ? CurrentLinkedAttribute(state, sourceId, instanceId) is { } declaration && IsLinkedAttributeTarget(state, actor, id, declaration)
            : ability.SimpleEffect is { } friendly && (friendly.MovesToExhaustedCostUnit || friendly.GrantsPaidRecall)
            ? state.CardObjects.TryGetValue(id, out var moved) && IsFaceUpNonStandbyUnit(moved)
                && IsObjectOnField(state.PlayerZones, id) && EffectiveFieldControllerId(state.PlayerZones, id, moved) == actor
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.ReadiesUnitTarget == true
            ? state.CardObjects.TryGetValue(id, out var readyTarget) && IsFaceUpNonStandbyUnit(readyTarget)
                && IsObjectOnField(state.PlayerZones, id) && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.CreatedBattlefieldEquipmentTokenCardNo is not null
            ? BattlefieldLocalRules.Battlefield(state, id) is not null
            : ability.SimpleEffect?.TransfersSourceToChosenPlayer == true ? state.Seats.ContainsKey(id)
            : SimpleActivationTargetsOpponent(ability) ? state.Seats.ContainsKey(id) && id != actor
            : ability.SimpleEffect?.ReturnsFriendlyUnitThenFreePlayUpToPower == true
                ? state.CardObjects.TryGetValue(id, out var returned)
                    && !returned.IsFaceDown && returned.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && SourceObjectControlledByPlayerOrLegacyOwned(returned, actor)
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.GrantsMysteriousWeaponRecall == true
                ? state.CardObjects.TryGetValue(id, out var recallTarget)
                    && !recallTarget.IsFaceDown && recallTarget.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && SourceObjectControlledByPlayerOrLegacyOwned(recallTarget, actor)
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.GrantsBoonOrPower == true
                ? state.CardObjects.TryGetValue(id, out var boonTarget)
                    && !boonTarget.IsFaceDown && boonTarget.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.TargetScope == CardTargetScopes.BattlefieldUnit
                && ability.SimpleEffect is { } battlefield && (battlefield.StunsTarget || battlefield.BattlefieldUnitDamage > 0)
                ? state.CardObjects.TryGetValue(id, out var battlefieldTarget) && IsFaceUpNonStandbyUnit(battlefieldTarget)
                    && IsObjectOnField(state.PlayerZones, id)
                    && state.ObjectLocations.GetValueOrDefault(id)?.Zone == "BATTLEFIELD"
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.StunsTarget == true
                ? state.CardObjects.TryGetValue(id, out var stunTarget)
                    && !stunTarget.IsFaceDown && stunTarget.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && EffectiveFieldControllerId(state.PlayerZones, id, stunTarget) != actor
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : state.CardObjects.TryGetValue(id, out var target) && !target.IsFaceDown
            && IsObjectOnField(state.PlayerZones, id)
            && (ability.SimpleEffect?.EmpowersOtherEquipment == true && id != sourceId && target.Tags.Contains(CardObjectTags.EquipmentCard)
                || ability.SimpleEffect?.BattlefieldUnitDamage > 0 && target.Tags.Contains(CardObjectTags.UnitCard)
                    && state.ObjectLocations.GetValueOrDefault(id)?.Zone == "BATTLEFIELD")
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id);

    private static ResolutionResult ResolveSimpleActivation(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        var spec = ability.SimpleEffect!;
        if (!(ability.SwiftSpeed ? CanActivateSwiftAbility(state, intent.PlayerId) : CanActivateOrdinaryPowerAbility(state, intent.PlayerId)))
            return RejectWithCorePrompts(state, "当前时机不能打出此技能。", ErrorCodes.PhaseNotAllowed);
        var targets = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targets.Count != command.TargetObjectIds.Count || targets.Distinct(StringComparer.Ordinal).Count() != targets.Count
            || !SimpleActivationTargetCountValid(ability, targets.Count)
            || !IsSimpleAbilitySource(state, intent.PlayerId, command.SourceObjectId, ability)
            || !SelectSimpleActivationInstance(state, state.CardObjects[command.SourceObjectId], ability, command.AbilityInstanceId, out var instance)
            || !SimpleActivationInstanceAvailable(state, state.CardObjects[command.SourceObjectId], ability, intent.PlayerId, instance)
            || targets.Any(id => !IsSimpleAbilityTarget(state, intent.PlayerId, command.SourceObjectId, id, ability, instance.Id)))
            return RejectWithCorePrompts(state, "技能来源、横置或解除强化费用、目标不合法。", ErrorCodes.InvalidTarget);
        if (!TryExtractAbilityRunePaymentResourceActions(state, intent.PlayerId, NormalizeOptionalCosts(command.OptionalCosts),
                out var extras, out var resources, out var recycled)
            || extras.Count != (SimpleActivationHasAdditionalCost(ability) ? 1 : 0)
            || extras.Any(cost => !SimpleActivationAdditionalCostChoices(state, intent.PlayerId, command.SourceObjectId, ability).Contains(cost)))
            return RejectWithCorePrompts(state, "技能支付选择不合法。", ErrorCodes.InvalidTarget);
        var tax = ability.AppliesSpellshieldTargetTax ? ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out _) : 0;
        var typed = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        const string window = "ACTIVATE_ABILITY";
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, window, intent.PlayerId, command.SourceObjectId, ability.AbilityId);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, window, intent.PlayerId,
            baseManaCost: ability.ManaCost, totalManaCost: ability.ManaCost,
            genericPowerCost: ability.PowerCost + tax, totalPowerCost: ability.PowerCost + tax + typed.Values.Sum(),
            powerCostByTrait: typed, experienceCost: ability.ExperienceCost, optionalCostIds: extras, paymentResourceActionIds: resources, reason: ability.EffectKind,
            sourceObjectId: command.SourceObjectId, abilityId: ability.AbilityId,
            powerPurpose: PowerPurposeForActivatedAbility(ability), manaPurpose: ManaPurposeForActivatedAbility(state, command.SourceObjectId),
            manaSourceSelection: command.ServerManaSourceSelection);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>();
        var pools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (!TryApplyOrderedPaymentResources(state, intent.PlayerId, resources, zones, ref cards,
                locations, ref pools, events, window, paymentId))
            return RejectWithCorePrompts(state, "支付资源动作顺序或来源无效。", ErrorCodes.InvalidTarget);
        var paid = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!paid.Accepted) return RejectWithCorePrompts(state, "资源不足，未横置或解除来源的强化。", ErrorCodes.InsufficientCost);
        var afterResources = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = paid.RunePools };
        if (!IsSimpleAbilitySource(afterResources, intent.PlayerId, command.SourceObjectId, ability)
            || !SelectSimpleActivationInstance(afterResources, cards[command.SourceObjectId], ability, instance.Id, out var afterInstance)
            || afterInstance != instance)
            return RejectWithCorePrompts(state, "资源动作后无法支付技能来源费用。", ErrorCodes.InvalidTarget);
        var source = cards[command.SourceObjectId];
        ObjectBinding? consumedSourceBoon = null;
        PaidUnitReference? exhaustedCostUnit = null;
        if (spec.ExhaustFriendlyUnitMinimumPowerAsCost is not null)
        {
            // Resource actions settle first. A unit exhausted by one of those
            // actions cannot also pay this cost (CN 415.4).
            var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
            if (!SimpleActivationAdditionalCostChoices(current, intent.PlayerId, source.ObjectId, ability).Contains(extras.Single()))
                return RejectWithCorePrompts(state, "所选友方单位无法支付休眠费用。", ErrorCodes.InvalidTarget);
            var exhausted = extras.Single()[ExhaustFriendlyUnitOptionalCostPrefix.Length..];
            if (spec.MovesToExhaustedCostUnit) {
                if (targets.Contains(exhausted))
                    return RejectWithCorePrompts(state, "移动目标必须是另一名友方单位。", ErrorCodes.InvalidTarget);
                exhaustedCostUnit = new(new(exhausted, cards[exhausted].ObjectGeneration), cards[exhausted].TokenFactoryCardNo);
            }
            cards[exhausted] = ExhaustForInstruction(zones, cards, cards[exhausted]);
            source = cards[source.ObjectId];
            events.Add(new("UNIT_EXHAUSTED", "让友方单位变为休眠状态以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = exhausted,
                ["wasExhausted"] = false, ["isExhausted"] = true, ["reason"] = "ABILITY_COST" }));
        }
        if (spec.DiscardsEquipmentAsCost || spec.DiscardsHandCardAsCost) {
            var discarded = extras.Single()["DISCARD_HAND_CARD:".Length..];
            if (!TryDiscardCardFromHand(zones, cards, intent.PlayerId, discarded))
                return RejectWithCorePrompts(state, "无法弃置所选手牌。", ErrorCodes.InvalidTarget);
            events.Add(new("CARD_DISCARDED", "弃置手牌以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = discarded,
                ["reason"] = "ABILITY_COST", ["destinationZone"] = "GRAVEYARD" }));
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, intent.PlayerId, "CARD_DISCARDED", source.ObjectId, [discarded], events);
        }
        if (spec.ConsumesSourceBoonAsCost)
        {
            consumedSourceBoon = new(source.ObjectId, source.ObjectGeneration);
            using (new MightInstructionScope(afterResources, zones, cards, events))
            {
                source = BoonRules.Consume(source);
                cards[source.ObjectId] = source;
                events.Add(CaptureBoonConsumption(new("BOON_CONSUMED", "消耗来源的一个增益作为主动技能费用", new Dictionary<string, object?> {
                    ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
                    ["sourceGeneration"] = source.ObjectGeneration, ["targetObjectId"] = source.ObjectId,
                    ["targetGeneration"] = source.ObjectGeneration, ["previousPower"] = source.Power + 1,
                    ["power"] = source.Power, ["abilityId"] = ability.AbilityId, ["reason"] = ability.EffectKind }), zones, cards));
            }
        }
        if (spec.RemovesSourceEmpowermentAsCost)
        {
            var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards }, new(source.ObjectId, source.ObjectGeneration), false, intent.PlayerId);
            source = changed.CardObjects[source.ObjectId];
            events.AddRange(changed.Events);
        }
        cards[source.ObjectId] = ability.ExhaustsSourceAsCost ? ExhaustForInstruction(zones, cards, source) : source;
        IReadOnlyList<TriggerQueueItemState> destructionTriggers = [];
        IReadOnlyList<string> destroyedOwners = [];
        var committedPools = paid.RunePools;
        if (consumedSourceBoon is not null)
        {
            // CN 143.2.b / 702.2.b: finish the cost instruction and its lethal
            // damage cleanup before opening the ability's response window.
            var costCause = new StackItemState($"cost-{state.Tick + 1}-{source.ObjectId}", intent.PlayerId,
                source.ObjectId, ability.EffectKind, instance.CardNo, []);
            var cleanup = RunStateBasedCleanupLoop(afterResources, zones, cards, costCause, committedPools,
                objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
            events.AddRange(cleanup.Events);
            committedPools = cleanup.RunePools;
            destructionTriggers = cleanup.TriggerQueue;
            destroyedOwners = cleanup.DestroyedUnitOwnerIds;
        }
        if (spec.DestroysSourceAsCost || spec.DestroyFriendlyTagAsCost is not null)
        {
            // CN357.2 permits either nonstandard-cost order. Exhaust first so
            // this very equipment can pay the destruction without being restored.
            if (!spec.DestroysSourceAsCost
                && !SimpleActivationAdditionalCostChoices(afterResources, intent.PlayerId, source.ObjectId, ability).Contains(extras.Single()))
                return RejectWithCorePrompts(state, "所选物体无法支付摧毁费用。", ErrorCodes.InvalidTarget);
            var destroyedId = spec.DestroysSourceAsCost ? source.ObjectId
                : extras.Single()[DestroyFriendlyPermanentAdditionalCostPrefix.Length..];
            var cause = new StackItemState($"cost-{state.Tick + 1}-{source.ObjectId}", intent.PlayerId,
                source.ObjectId, ability.EffectKind, source.CardNo, []);
            var removal = ResolveFieldDestructions(afterResources, zones, cards, cause, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), committedPools,
                objectLocations: locations, explicitDestroyObjectIds: new HashSet<string> { destroyedId });
            // A replacement still pays the cost (CN357.2.a); it need not leave
            // the originally selected object in a graveyard.
            if (removal.Events.Count == 0)
                return RejectWithCorePrompts(state, "无法支付所选摧毁费用。", ErrorCodes.InvalidTarget);
            events.AddRange(removal.Events);
            committedPools = removal.RunePools;
            destructionTriggers = destructionTriggers.Concat(removal.TriggerQueue).ToArray();
            destroyedOwners = MergeDestroyedUnitOwnerIds(destroyedOwners, removal.DestroyedUnitOwnerIds);
        }
        var tokenTargets = targets.Where(id => state.CardObjects.TryGetValue(id, out var target) && TokenObjectRules.IsToken(target))
            .ToDictionary(id => id, id => state.CardObjects[id].TokenFactoryCardNo ?? state.CardObjects[id].CardNo!, StringComparer.Ordinal);
        var context = new SimpleActivationContext(ability.AbilityId, new(source.ObjectId, source.ObjectGeneration), state.Tick + 1,
            tokenTargets.Count == 0 ? null : tokenTargets) {
                ExhaustedCostUnit = exhaustedCostUnit,
                ConsumedSourceBoon = consumedSourceBoon,
                LinkedAttribute = spec.TargetsDeclaredAttribute ? CurrentLinkedAttribute(state, source.ObjectId, instance.Id) : null,
                SourceFaceRevision = instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
                RuleTextInstanceId = instance.Id, Origin = instance.Origin,
                SourceTokenFactoryCardNo = source.TokenFactoryCardNo,
                BanishCost = spec.BanishesSourceAsCost
                    ? PayActivationSourceBanish(state, zones, cards, source, intent.PlayerId, events) : null,
                ReplayCardNo = spec.BanishesSourceThenReplaysToBase
                    ? source.AttachmentUnitCopies?.Basis.CardNo ?? source.CardNo : null,
                ReturnTiming = ability.SwiftSpeed && state.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen : "" };
        var targetGenerations = targets
            .Where(id => state.CardObjects.ContainsKey(id))
            // Targets were chosen before costs. A resource action or destruction
            // replacement cannot erase a departed token's witness or bind this
            // ability to a newly created object.
            .ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal);
        var item = SimpleActivationStackItem(context, intent.PlayerId, instance.CardNo, ability, targets)
            with { TargetGenerations = targetGenerations.Count == 0 ? null : targetGenerations };
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
            ["abilityId"] = ability.AbilityId, ["effectKind"] = ability.EffectKind, ["targetObjectIds"] = targets.ToArray() };
        if (instance.Origin is not null) payload["abilityInstanceId"] = instance.Id;
        if (spec.BanishesSourceAsCost)
            payload["activationBanishCostHash"] = ActivationBanishCaptureHash(context, intent.PlayerId);
        if (spec.OncePerTurn)
            payload["sourceGeneration"] = source.ObjectGeneration;
        events.Add(new("ABILITY_ACTIVATED", $"打出{ability.DisplayName}的技能", payload));
        if (ability.ExhaustsSourceAsCost)
            events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED", "横置来源以支付技能费用", payload));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, committedPools, paid.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var effects = spec.OncePerTurn
            ? state.UntilEndOfTurnEffects.Append(SimpleActivationUse(new(source.ObjectId, source.ObjectGeneration), ability,
                intent.PlayerId, instance.Origin is null ? null : instance.Id)).ToArray()
            : state.UntilEndOfTurnEffects;
        if (spec.DiscardsEquipmentAsCost || spec.DiscardsHandCardAsCost)
            effects = MarkPlayerDiscardedHandCardsThisTurn(effects, intent.PlayerId, [extras.Single()["DISCARD_HAND_CARD:".Length..]], zones);
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = state.SpellDuelState.IsActive ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed,
            PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = committedPools,
            PlayerExperience = paid.PlayerExperience, StackItems = [..state.StackItems, item],
            TriggerQueue = state.TriggerQueue.Concat(destructionTriggers).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, destroyedOwners),
            UntilEndOfTurnEffects = effects };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackItemState SimpleActivationStackItem(SimpleActivationContext context, string actor, string cardNo,
        P4ActivatedAbilityDefinition ability, IReadOnlyList<string> targets)
        => new($"STACK-{context.AcquiredTick}-{context.Source.ObjectId}-PRINTED", actor, context.Source.ObjectId,
            ability.EffectKind, cardNo, targets, 0, 1, [], timingContext: context.ReturnTiming) { SimpleActivation = context };

    internal static bool ActivatedSkillTargetsPlayer(MatchState state, StackItemState item)
        => item.BorrowedExhaust is { } borrowed
            ? BorrowedExhaustRules.Valid(state, item)
                && BorrowedExhaustRules.Spec(state, borrowed) is { } spec
                && BorrowedExhaustRules.TargetsPlayer(spec)
            : P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
                && SimpleActivationTargetsPlayer(ability) && ValidSimpleActivation(state, item);

    internal static bool ValidSimpleActivation(MatchState state, StackItemState item, bool allowMissingReturnTarget = false)
    {
        var recognized = P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability) && ability.SimpleEffect is not null;
        if (item.SimpleActivation is not { } context) return !recognized;
        if (!recognized || context.AbilityId != ability.AbilityId || context.Source is not { Generation: >= 0 }
            || context.SourceFaceRevision < 0
            || (ability.SwiftSpeed ? context.ReturnTiming is not ("" or TimingStates.SpellDuelOpen) : context.ReturnTiming != "")
            || context.AcquiredTick <= 0 || context.AcquiredTick > state.Tick || !state.Seats.ContainsKey(item.ControllerId)
            || !P4ActivatedAbilityCatalog.IsActivationSourceCardNoForAbility(ability, item.CardNo)
            || !ValidSimpleActivationSource(state, context, ability, item.CardNo!)
            || !ValidActivationBanishCost(state, context, ability.SimpleEffect!)
            || (ability.SimpleEffect!.ConsumesSourceBoonAsCost
                ? context.ConsumedSourceBoon != context.Source
                : context.ConsumedSourceBoon is not null)
            || !ValidActivationAttribute(state, context, ability.SimpleEffect!.TargetsDeclaredAttribute)
            || !ValidExhaustedCostUnit(state, context.ExhaustedCostUnit, ability.SimpleEffect!.MovesToExhaustedCostUnit,
                item.SourceObjectId, item.TargetObjectIds)
            || !SimpleActivationTargetCountValid(ability, item.TargetObjectIds.Count)
            || item.TargetObjectIds.Distinct().Count() != item.TargetObjectIds.Count
            || (item.TargetGenerations?.Count ?? 0) != (SimpleActivationTargetsPlayer(ability) ? 0 : item.TargetObjectIds.Count)
            || context.TargetTokenFactories is not null && context.TargetTokenFactories.Any(p => !item.TargetObjectIds.Contains(p.Key)
                || !P6TokenFactoryCatalog.TryGetByCardNo(p.Value, out var factory)
                || !factory.Tags.Contains(ability.SimpleEffect!.EmpowersOtherEquipment ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard))
            || (SimpleActivationTargetsPlayer(ability)
                ? item.TargetObjectIds.Any(id => !state.Seats.ContainsKey(id)
                    || SimpleActivationTargetsOpponent(ability) && id == item.ControllerId)
                : item.TargetObjectIds.Any(id => item.TargetGenerations!.GetValueOrDefault(id, -1) < 0
                || (state.CardObjects.TryGetValue(id, out var target)
                    ? item.TargetGenerations![id] > target.ObjectGeneration
                        || context.TargetTokenFactories?.GetValueOrDefault(id) != target.TokenFactoryCardNo
                    : !allowMissingReturnTarget && context.TargetTokenFactories?.ContainsKey(id) != true)))) return false;
        var expected = SimpleActivationStackItem(context, item.ControllerId, item.CardNo!, ability, item.TargetObjectIds)
            with { TargetGenerations = item.TargetGenerations, TokenEntryPlan = item.TokenEntryPlan,
                DepartedTokenTargets = item.DepartedTokenTargets,
                EffectPlayCompleted = allowMissingReturnTarget || ability.SimpleEffect!.BanishesSourceThenReplaysToBase
                    || ability.SimpleEffect.PlaysLinkedGraveyardExile || ability.SimpleEffect.PlaysAllLinkedArmamentUnits ? item.EffectPlayCompleted : false };
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(expected)
            && (item.TokenEntryPlan is null || SimpleActivationUnitTokenCount(ability.SimpleEffect!) > 0 && ValidTokenEntryPlan(state, item));
    }

    private static bool ValidSimpleActivationSource(MatchState state, SimpleActivationContext context,
        P4ActivatedAbilityDefinition ability, string cardNo)
    {
        if (context.Origin is { } origin)
        {
            if (origin.Equipment is null
                || context.RuleTextInstanceId != $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
                || context.SourceFaceRevision != origin.FaceRevision || origin.CapturedTick > context.AcquiredTick
                || !AttachedRuleTextRules.ValidCaptured(state, origin, context.Source.ObjectId, context.Source.Generation, cardNo)) return false;
        }
        else if (context.RuleTextInstanceId != $"PRINTED:{context.Source.ObjectId}:{context.Source.Generation}") return false;
        if (state.CardObjects.TryGetValue(context.Source.ObjectId, out var source))
            return context.CeasedSource is null && source.ObjectGeneration >= context.Source.Generation
                && source.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
                && (ability.SimpleEffect?.BanishesSourceThenReplaysToBase == true
                    ? context.ReplayCardNo == (source.AttachmentUnitCopies?.Basis.CardNo ?? source.CardNo)
                    : context.ReplayCardNo is null)
                && (context.Origin is not null || source.ObjectGeneration != context.Source.Generation
                    || CopyFaceTimeline.FaceAt(source, context.SourceFaceRevision, context.AcquiredTick) == cardNo);
        return context.CeasedSource is { } ceased && ceased.DepartedTick >= context.AcquiredTick
            && ceased.DepartedTick <= state.Tick
            && ceased.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
            && ceased.SourceFaceRevision == context.SourceFaceRevision
            && P6TokenFactoryCatalog.TryGetByCardNo(ceased.TokenFactoryCardNo, out var factory)
            && factory.Tags.Contains(ability.SourceRequiredTag);
    }

    internal static bool ValidTranspositionMageEffectPlay(MatchState state, PendingEffectPlayState pending)
    {
        var borrowed = IsBorrowedReturnPlay(pending.Parent);
        var native = P4ActivatedAbilityCatalog.TryGetByEffectKind(pending.Parent.EffectKind, out var ability)
            && ability.SimpleEffect?.ReturnsFriendlyUnitThenFreePlayUpToPower == true;
        if ((!borrowed && !native)
            || pending.RevealedHand is not null || pending.Parent.EffectPlayCompleted
            || pending.SourceZone != "HAND" || pending.PlayerId != pending.Parent.ControllerId
            || pending.DestinationPolicy != "" || pending.SourcePlayerId is not null
            || pending.SourceReplay is not null || pending.OriginalLocation is not null || pending.OriginalBattlefield is not null
            || pending.ViewedCardIds is not null || pending.Sources.Count == 0
            || !pending.Optional || pending.IgnoreAllCosts || !pending.IgnoreBaseMana || !pending.IgnoreBasePower
            || pending.ManaReduction != 0 || pending.MaximumUnitPower is not >= 0
            || !(borrowed ? BorrowedExhaustRules.Valid(state, pending.Parent)
                : ValidSimpleActivation(state, pending.Parent, allowMissingReturnTarget: true))
            || !state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)) return false;

        var targetId = pending.Parent.TargetObjectIds.SingleOrDefault();
        var factories = borrowed ? pending.Parent.BorrowedExhaust!.TargetTokenFactories : pending.Parent.SimpleActivation?.TargetTokenFactories;
        if (targetId is null || !state.PlayerZones.Values.Any(player => player.Hand.Contains(targetId, StringComparer.Ordinal))
            && (state.CardObjects.ContainsKey(targetId) || factories?.ContainsKey(targetId) != true)
            && !ValidReplacedReturnPlayTarget(state, pending, targetId)) return false;

        var eligible = zones.Hand
            .Where(id => state.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior)
                && behavior.PlaysSourceToBaseAsUnit
                && ResolveCurrentFieldUnitPower(state, card) <= pending.MaximumUnitPower.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var valid = pending.Sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(eligible.Order(StringComparer.Ordinal))
            && pending.Sources.All(entry => zones.Hand.Contains(entry.Key, StringComparer.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var card)
                && card.ObjectGeneration == entry.Value);
        return valid;
    }

    private static int SimpleActivationUnitTokenCount(SimpleActivatedEffectSpec spec)
        => spec.CreatedRobotCount + spec.CreatedMinionCount + spec.CreatedUnitTokenCount;

    private static CardBehaviorDefinition SimpleActivationTokenBehavior(P4ActivatedAbilityDefinition ability)
        => new(ability.SourceCardNo, ability.DisplayName, 0, ability.EffectKind, 0, 0,
            CreatedBaseUnitTokenCount: SimpleActivationUnitTokenCount(ability.SimpleEffect!),
            CreatedBaseUnitTokenName: ability.SimpleEffect!.CreatedMinionCount > 0 ? "随从" : "机器人",
            CreatedBaseUnitTokenPower: ability.SimpleEffect.CreatedMinionCount > 0 ? 1 : 3,
            CreatedBaseUnitTokenTags: ability.SimpleEffect.CreatedMinionCount > 0 ? "随从" : "机械");

    private static StackResolutionResult ResolveSimpleActivationEffect(MatchState state, StackItemState item, P4ActivatedAbilityDefinition ability)
    {
        var spec = ability.SimpleEffect!;
        if (!ValidSimpleActivation(state, item,
                allowMissingReturnTarget: spec.ReturnsFriendlyUnitThenFreePlayUpToPower && item.EffectPlayCompleted))
            throw new InvalidOperationException("Invalid captured printed activation.");
        if (spec.MovesToExhaustedCostUnit)
            return ResolveMovementToExhaustedCostUnit(state, item, item.SimpleActivation!.ExhaustedCostUnit!,
                (frame, id) => IsSimpleAbilityTarget(frame, item.ControllerId, item.SourceObjectId, id, ability));
        if (spec.RecyclesGraveyardTargets) return ResolveActivatedGraveyardRecycle(state, item);
        if (spec.GrantsNextUnitReady) return ResolveActivatedNextUnitReady(state, item);
        if (spec.GrantsNextSpellDamage) return ResolveActivatedNextSpellDamage(state, item);
        if (spec.GrantsPaidRecall) return ResolveActivatedPaidRecall(state, item);
        if (spec.PlaysLinkedGraveyardExile) return ResolveLinkedGraveyardExilePlay(state, item);
        if (spec.PlaysAllLinkedArmamentUnits) return ResolveAllLinkedArmamentUnits(state, item);
        if (spec.CreatedUnitTokenCardNo is { } unitFactory)
        {
            if (BeginTokenReplacement(state, item) is { } replacement) return replacement;
            return CreateUnitTokenBatch(state, item, unitFactory, spec.CreatedUnitTokenCount + AdditionalUnitTokens(item),
                entersReady: false, abilityId: ability.AbilityId, instructionAllowsLocationChoice: true);
        }
        if (spec.BanishesSourceThenReplaysToBase)
        {
            if (item.EffectPlayCompleted || !state.CardObjects.TryGetValue(item.SourceObjectId, out var replayed)
                || replayed.ObjectGeneration != item.SimpleActivation!.Source.Generation
                || !IsFaceUpNonStandbyUnit(replayed) || !IsObjectOnField(state.PlayerZones, replayed.ObjectId))
                return NoopStackResolutionResult(state);
            TryGetEffectPlayDefinition(item, out var replayDefinition);
            return BeginEffectPlay(state, item, replayDefinition);
        }
        if (spec.TargetsDeclaredAttribute) return ResolveLinkedAttributeReduction(state, item);
        if (spec.OpponentGuessesDeckTopType) return ResolveGuessedDeckTop(state, item, ability);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", $"{ability.DisplayName}技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = ability.AbilityId }) };
        var result = NoopStackResolutionResult(state);
        if (spec.CommandsOpponentUnitToSourceBattlefield)
            return ResolveCommandOpponentMovement(state, item, item.SimpleActivation!.Source, ability.DisplayName, ability.AbilityId);
        if (spec.GrantsMysteriousWeaponRecall)
        {
            var targetId = item.TargetObjectIds.Single();
            if (!cards.TryGetValue(targetId, out var target)
                || target.ObjectGeneration != item.TargetGenerations!.GetValueOrDefault(targetId, -1)
                || !IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, targetId, ability))
                return result;
            var marker = MysteriousWeaponRecallMarker(state.TurnNumber, item.ControllerId,
                item.SimpleActivation!.Source, new(targetId, target.ObjectGeneration), item.CardNo!, state.Tick + 1);
            cards[targetId] = target with { UntilEndOfTurnEffects = target.UntilEndOfTurnEffects
                .Append(marker).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
            events.Add(new("MYSTERIOUS_WEAPON_RECALL_GRANTED", "来路不明的武器授予本回合红色符能召回替换", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                ["controllerId"] = item.ControllerId, ["duration"] = "UNTIL_END_OF_TURN" }));
            return result with { CardObjects = cards, Events = events };
        }
        if (spec.ReturnsFriendlyUnitThenFreePlayUpToPower && item.EffectPlayCompleted)
            // A child play can reuse the returned card after its modifiers
            // reset. A returned token ceases to exist instead (CN186.1).
            // Neither must remain in a hand when the parent resumes.
            return result;
        if (spec.ReturnsFriendlyUnitThenFreePlayUpToPower)
            return ResolveReturnUnitThenFreePlay(state, item, ability.DisplayName, ability.AbilityId,
                id => IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, id, ability));
        if (spec.GrantsBoonOrPower)
        {
            var targetId = item.TargetObjectIds.SingleOrDefault();
            if (targetId is not null && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == cards.GetValueOrDefault(targetId)?.ObjectGeneration
                && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, targetId, ability))
            {
                var target = cards[targetId];
                if (target.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal))
                {
                    var next = ApplyDirectUntilEndPowerModifier(
                        target, targetId, item.SourceObjectId, item.CardNo, item.EffectKind,
                        "CoreRuleEngine.ResolveSimpleActivationEffect.PrismwingFlock", spec.ExistingBoonPowerBonus,
                        target.Power + spec.ExistingBoonPowerBonus);
                    cards[targetId] = next;
                    events.Add(new("POWER_MODIFIED_UNTIL_END_OF_TURN", "已有增益的单位本回合战力提高",
                        new Dictionary<string, object?> {
                            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                            ["powerDelta"] = spec.ExistingBoonPowerBonus,
                            ["appliedPowerDelta"] = next.Power - target.Power,
                            ["minimumPower"] = 0, ["resultingPower"] = next.Power
                        }));
                }
                else
                    GrantLegendBoon(cards, targetId, item.ControllerId, item.SourceObjectId, item.EffectKind, events, zones);
            }
            else
                events.Add(new("ABILITY_NO_EFFECT", "幻彩飞禽结算时目标已不再合法", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                    ["abilityId"] = ability.AbilityId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
        }
        if (spec.StunsTarget)
        {
            var targetId = item.TargetObjectIds.SingleOrDefault();
            if (targetId is not null && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == cards.GetValueOrDefault(targetId)?.ObjectGeneration
                && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, targetId, ability))
            {
                var target = cards[targetId];
                if (CanExecuteStun(target) && !TryReplaceEmpoweredStun(state, item, cards, targetId, events))
                {
                    cards[targetId] = target with {
                        UntilEndOfTurnEffects = target.UntilEndOfTurnEffects
                            .Concat(["STUNNED"])
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                    var applied = new GameEvent("STATUS_EFFECT_APPLIED", "眩晕目标单位", new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED", ["duration"] = "UNTIL_END_OF_TURN" });
                    events.Add(CaptureStunObservers(applied, zones, cards, item, targetId, locations));
                }
                else if (!CanExecuteStun(target))
                    events.Add(new("ABILITY_NO_EFFECT", "无情潜伏者结算时目标已经眩晕", new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["abilityId"] = ability.AbilityId, ["reason"] = "ALREADY_STUNNED" }));
            }
            else
                events.Add(new("ABILITY_NO_EFFECT", "无情潜伏者结算时目标已不再合法", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                    ["abilityId"] = ability.AbilityId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
        }
        if (spec.TransfersSourceToChosenPlayer)
            return ResolveTransferSourceAndRecall(state, item, item.SimpleActivation!.Source, ability.AbilityId);
        if (spec.GrantsSourceBoon && cards.TryGetValue(item.SourceObjectId, out var boonSource)
            && boonSource.ObjectGeneration == item.SimpleActivation!.Source.Generation
            && IsFaceUpNonStandbyUnit(boonSource) && IsObjectOnField(zones, boonSource.ObjectId))
        {
            var behavior = new CardBehaviorDefinition(item.CardNo!, ability.DisplayName, 0, ability.EffectKind, 0, 0);
            cards[boonSource.ObjectId] = ApplyBoon(boonSource, behavior, item, boonSource.ObjectId,
                out var granted, zones, cards);
            events.AddRange(granted);
        }
        if (spec.GrantsFriendlyHereRoam && cards.TryGetValue(item.SourceObjectId, out var hereSource)
            && hereSource.ObjectGeneration == item.SimpleActivation!.Source.Generation && !hereSource.IsFaceDown
            && IsObjectOnField(zones, hereSource.ObjectId))
            foreach (var unit in cards.Values.Where(card => IsFaceUpNonStandbyUnit(card)
                && IsObjectOnField(zones, card.ObjectId)
                && EffectiveFieldControllerId(zones, card.ObjectId, card) == item.ControllerId
                && IsSameFieldLocation(zones, locations, hereSource.ObjectId, card.ObjectId))
                .OrderBy(card => card.ObjectId, StringComparer.Ordinal).ToArray())
                GrantLegendRoam(cards, unit.ObjectId, item.ControllerId, item.SourceObjectId, ability.AbilityId, events);
        if (spec.ReadiesSource && cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.SimpleActivation!.Source.Generation && IsObjectOnField(zones, source.ObjectId)) {
            var behavior = new CardBehaviorDefinition(item.CardNo!, ability.DisplayName, 0, ability.EffectKind, 0, 0, PowerModifierAmount: spec.SourcePowerBonus);
            source = ApplyReadyState(source, behavior, item, source.ObjectId, out var ready, zones, cards);
            if (ready is not null) events.Add(ready);
            if (spec.SourcePowerBonus != 0)
            {
                source = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, spec.SourcePowerBonus, out var power);
                events.Add(power);
            }
            cards[source.ObjectId] = source;
        }
        if (spec.GrantsSourceRoam && cards.TryGetValue(item.SourceObjectId, out var roaming)
            && roaming.ObjectGeneration == item.SimpleActivation!.Source.Generation && !roaming.IsFaceDown
            && IsObjectOnField(zones, roaming.ObjectId))
            GrantLegendRoam(cards, roaming.ObjectId, item.ControllerId, item.SourceObjectId, ability.AbilityId, events);
        if (spec.BattlefieldUnitDamage > 0) {
            var target = item.TargetObjectIds.Single();
            if (IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, target, ability)
                && item.TargetGenerations![target] == cards[target].ObjectGeneration) {
                var destroyed = new HashSet<string>(StringComparer.Ordinal);
                var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target, spec.BattlefieldUnitDamage, item);
                var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target, amount, destroyed,
                    state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new("DAMAGE_APPLIED", "主动技能造成伤害", BuildDamagePayload(item.SourceObjectId, target, damage)));
                var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, pools, damageTriggeredDestroyTargetObjectIds: destroyed,
                    objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
                events.AddRange(cleanup.Events); pools = cleanup.RunePools.ToDictionary(p => p.Key, p => p.Value);
                result = result with { TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds };
            }
        }
        if (spec.DestroysSourceOnResolution && cards.TryGetValue(item.SourceObjectId, out var destroying)
            && destroying.ObjectGeneration == item.SimpleActivation!.Source.Generation && IsObjectOnField(zones, destroying.ObjectId))
        {
            var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), pools, objectLocations: locations,
                explicitDestroyObjectIds: new HashSet<string> { item.SourceObjectId }, explicitDestroyReason: "SKILL_EFFECT");
            events.AddRange(removal.Events); pools = removal.RunePools.ToDictionary(p => p.Key, p => p.Value);
            result = result with { TriggerQueue = removal.TriggerQueue, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds };
        }
        if (spec.DrawCount > 0)
        {
            var drawn = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId, spec.DrawCount, state.RngCursor, events, deferNormalVictory: true);
            result = result with { PlayerScores = drawn.PlayerScores, WinnerPlayerId = drawn.WinnerPlayerId, RngCursor = drawn.RngCursor };
        }
        if (spec.RuneCallCount is > 0 && result.WinnerPlayerId is null)
        {
            var called = CallRunes(zones, cards, item.ControllerId, spec.RuneCallCount.Value);
            events.Add(new("RUNES_CALLED", $"{item.ControllerId} 召出 {called.CalledRuneObjectIds.Count} 枚休眠符文",
                new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray(),
                    ["reason"] = item.EffectKind }));
        }
        if (SimpleActivationUnitTokenCount(spec) > 0)
            CreateBaseUnitTokens(state, zones, cards, pools, SimpleActivationTokenBehavior(ability), item, events, locations, state.UntilEndOfTurnEffects);
        if (spec.CreatedBattlefieldEquipmentTokenCardNo is { } factoryNo
            && item.TargetObjectIds.SingleOrDefault() is { } battlefield
            && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, battlefield, ability)
            && item.TargetGenerations![battlefield] == cards[battlefield].ObjectGeneration)
        {
            if (!P6TokenFactoryCatalog.TryGetByCardNo(factoryNo, out var factory)
                || !factory.Tags.Contains(CardObjectTags.EquipmentCard))
                throw new InvalidOperationException("Invalid activated equipment token factory.");
            locations = CreateBattlefieldEquipmentTokenBatch(zones, cards, item.ControllerId, item.SourceObjectId,
                item.CardNo!, ability.DisplayName, factory.TokenFamilyName, factory.Tags, 1, false, battlefield,
                events, locations).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }
        if (spec.CreatedGoldCount > 0)
            CreateBaseEquipmentTokens(zones, cards,
                new(ability.SourceCardNo, ability.DisplayName, 0, ability.EffectKind, 0, 0,
                    CreatedBaseEquipmentTokenCount: spec.CreatedGoldCount, CreatedBaseEquipmentTokenName: "金币",
                    CreatedBaseEquipmentTokenTags: CardObjectTags.EquipmentCard, CreatedBaseEquipmentTokenIsExhausted: true), item, events);
        if (spec.EmpowersOtherEquipment)
        {
            var target = item.TargetObjectIds.Single();
            if (IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, target, ability)
                && item.TargetGenerations![target] == cards[target].ObjectGeneration)
            {
                var changed = ApplyEmpowerStatusAction(state, new(target, cards[target].ObjectGeneration), true, item.ControllerId);
                cards[target] = changed.CardObjects[target]; events.AddRange(changed.Events);
            }
        }
        if (spec.ReadiesUnitTarget && item.TargetObjectIds.SingleOrDefault() is { } readyId
            && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, readyId, ability)
            && item.TargetGenerations![readyId] == cards[readyId].ObjectGeneration)
        {
            var behavior = new CardBehaviorDefinition(item.CardNo!, ability.DisplayName, 0, ability.EffectKind, 0, 1, ReadiesTarget: true);
            cards[readyId] = ApplyReadyState(cards[readyId], behavior, item, readyId, out var readied, zones, cards);
            if (readied is not null) events.Add(readied);
        }
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
