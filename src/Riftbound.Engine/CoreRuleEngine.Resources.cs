using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult ResolveMalzaharResourceSkill(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        if (MalzaharResourceSkillTimingContext(state, intent.PlayerId) is null)
            return RejectWithCorePrompts(state, "该迅捷资源技能需要开放主阶段或法术对决焦点。", ErrorCodes.PhaseNotAllowed);
        if (command.TargetObjectIds.Count != 1 || (command.OptionalCosts?.Count ?? 0) != 0)
            return RejectWithCorePrompts(state, "请选择一个友方单位或装备作为摧毁费用。", ErrorCodes.InvalidTarget);
        bool IsControlledPermanent(string id) => state.CardObjects.TryGetValue(id, out var card)
            && card.ObjectId == id && card.ControllerId == intent.PlayerId && !card.IsFaceDown
            && !card.Tags.Contains(CardObjectTags.Standby)
            && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))
            && FindFieldObjectLocation(state.PlayerZones, id) is { } field && field.PlayerId == intent.PlayerId
            && state.ObjectLocations.TryGetValue(id, out var position)
            && position.PlayerId == intent.PlayerId && position.Zone == field.Zone;
        var costId = command.TargetObjectIds[0];
        if (!IsControlledPermanent(command.SourceObjectId) || !IsControlledPermanent(costId)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var source) || source.IsExhausted
            || !source.Tags.Contains(CardObjectTags.UnitCard)
            || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, source.CardNo))
            return RejectWithCorePrompts(state, "技能来源或摧毁费用不合法。", ErrorCodes.InvalidTarget);

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        // The card says a friendly unit, not another unit. Exhaust first so the
        // source can itself pay the destruction cost without being resurrected.
        cards[command.SourceObjectId] = source with { IsExhausted = true };
        var cost = new StackItemState($"cost-{state.Tick + 1}-{command.SourceObjectId}", intent.PlayerId,
            command.SourceObjectId, ability.EffectKind, source.CardNo, [costId]);
        var destroyed = ResolveFieldDestructions(state, zones, cards, cost, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
            objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { costId });
        if (destroyed.Events.Count == 0)
            return RejectWithCorePrompts(state, "无法支付摧毁费用。", ErrorCodes.InvalidTarget);
        var pools = destroyed.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var pool = pools.GetValueOrDefault(intent.PlayerId, RunePool.Empty);
        pools[intent.PlayerId] = pool with { Power = pool.Power + 2 };
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RunePools = pools,
            TriggerQueue = state.TriggerQueue.Concat(destroyed.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, destroyed.DestroyedUnitOwnerIds) };
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId,
            ["sourceObjectId"] = command.SourceObjectId, ["abilityId"] = ability.AbilityId,
            ["destroyedCostObjectId"] = costId, ["generatedPower"] = 2, ["resourceLifecycle"] = "rune-pool" };
        var events = new List<GameEvent> { new("ABILITY_ACTIVATED", "激活资源技能", payload),
            new("UNIT_EXHAUSTED", "支付横置费用", payload) };
        events.AddRange(destroyed.Events);
        events.Add(new("POWER_GAINED", "获得 2 点任意特性符能", payload));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static ResolutionResult ResolveImmediateResourceTriggers(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var triggers = result.State.TriggerQueue.Where(IsJhinMovementResourceTrigger).ToArray();
        if (triggers.Length == 0) return result;
        var pools = result.State.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = result.Events.ToList();
        foreach (var trigger in triggers)
        {
            // The trigger has already captured its controller and source. Moving again,
            // leaving play, or changing controller cannot cancel an earned resource.
            var pool = pools.GetValueOrDefault(trigger.ControllerId, RunePool.Empty);
            pools[trigger.ControllerId] = pool with { Mana = pool.Mana + 1, Power = pool.Power + 1 };
            var payload = new Dictionary<string, object?>
            {
                ["playerId"] = trigger.ControllerId, ["sourceObjectId"] = trigger.SourceObjectId,
                ["triggerId"] = trigger.TriggerId, ["generatedMana"] = 1, ["generatedPower"] = 1,
                ["resourceLifecycle"] = "rune-pool", ["resourceSkill"] = true
            };
            events.Add(BuildTriggerResolvedEvent(trigger));
            events.Add(new("MANA_GAINED", "移动触发：获得 1 点法力", payload));
            events.Add(new("POWER_GAINED", "移动触发：获得 1 点任意特性符能", payload));
        }
        var state = result.State with { RunePools = pools,
            TriggerQueue = result.State.TriggerQueue.Where(t => !IsJhinMovementResourceTrigger(t)).ToArray() };
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool IsImmediatePoolResourceAbility(string id)
        => id == P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId
            || P4ActivatedAbilityCatalog.IsHoneyfruitResourceAbility(id)
            || P4ActivatedAbilityCatalog.IsSigilTypedResourceAbility(id)
            || P4ActivatedAbilityCatalog.IsResourceConversionEquipmentAbility(id);

    private static bool IsImmediateResourceSource(MatchState state, string player, string id, P4ActivatedAbilityDefinition ability)
    {
        var location = FindFieldObjectLocation(state.PlayerZones, id);
        return location is not null && location.Value.PlayerId == player
            && state.CardObjects.TryGetValue(id, out var source) && source.ObjectId == id
            && state.ObjectLocations.TryGetValue(id, out var precise) && precise.PlayerId == player && precise.Zone == location.Value.Zone
            && source.ControllerId == player && !source.IsFaceDown && !source.IsExhausted && !source.Tags.Contains(CardObjectTags.Standby)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, source.CardNo)
            && source.Tags.Contains(ability.RequiresBaseEquipmentSource ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard);
    }

    private static ResolutionResult ResolveImmediateResourceSkill(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        if (!CanActivateReactionResourceSkill(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "当前没有使用资源技能的行动权。", ErrorCodes.PhaseNotAllowed);
        if (!IsImmediateResourceSource(state, intent.PlayerId, command.SourceObjectId, ability)
            || command.TargetObjectIds.Count != 0)
            return RejectWithCorePrompts(state, "请选择你控制的、具有此技能的活跃场上物体。", ErrorCodes.InvalidTarget);

        var source = state.CardObjects[command.SourceObjectId];
        var choices = command.OptionalCosts ?? [];
        var pool = state.RunePools.TryGetValue(intent.PlayerId, out var available) ? available : RunePool.Empty;
        var mana = ability.GeneratedMana;
        var power = ability.GeneratedPower;
        var traits = P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(ability);
        if (P4ActivatedAbilityCatalog.IsHoneyfruitResourceAbility(ability.AbilityId))
        {
            if (choices.Count > 1 || choices.Count == 1
                && (choices[0] != P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix + command.SourceObjectId
                    || state.PlayerExperience.GetValueOrDefault(intent.PlayerId) < 6))
                return RejectWithCorePrompts(state, "蜜糖果实的强化技能需要至少 6 点经验及合法的分支选择。", ErrorCodes.InvalidTarget);
            mana = choices.Count == 1 ? 1 : 0;
        }
        else if (P4ActivatedAbilityCatalog.IsResourceConversionEquipmentAbility(ability.AbilityId))
        {
            if (!TryReadResourceConversionAmount(ability.AbilityId, choices, out var amount))
                return RejectWithCorePrompts(state, "请选择合法的资源转换数量。", ErrorCodes.InvalidTarget);
            if (ability.AbilityId == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId)
            {
                if (pool.Mana < amount) return RejectWithCorePrompts(state, "法力不足。", ErrorCodes.InsufficientCost);
                pool = pool with { Mana = pool.Mana - amount };
                power = amount;
            }
            else if (ability.AbilityId == P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId)
            {
                // [A] as a cost accepts power of any trait (CN 135.2.e.5.a).
                if (pool.TotalPower < amount) return RejectWithCorePrompts(state, "符能不足。", ErrorCodes.InsufficientCost);
                var paid = PayPowerCost(pool, amount, new Dictionary<string, int>());
                pool = new(pool.Mana, paid.AnyPower, paid.PowerByTrait);
                mana = amount;
            }
        }
        else if (choices.Count != 0)
            return RejectWithCorePrompts(state, "此资源技能不接受额外选项。", ErrorCodes.InvalidTarget);

        var generatedTraits = pool.PowerByTrait.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        foreach (var (trait, amount) in traits) generatedTraits[trait] = generatedTraits.GetValueOrDefault(trait) + amount;
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        pools[intent.PlayerId] = new(pool.Mana + mana, pool.Power + power, generatedTraits);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        cards[command.SourceObjectId] = source with { IsExhausted = true };
        var next = state with { Tick = state.Tick + 1, CardObjects = cards, RunePools = pools };
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = intent.PlayerId, ["sourceObjectId"] = command.SourceObjectId,
            ["targetObjectId"] = command.SourceObjectId, ["abilityId"] = command.AbilityId,
            ["cardNo"] = source.CardNo, ["resourceSkill"] = true, ["reactionSpeed"] = true,
            ["generatedMana"] = mana, ["generatedPower"] = power, ["generatedPowerByTrait"] = traits,
            ["isExhausted"] = true, ["stackPolicy"] = "no-ordinary-stack-item",
            ["timingContext"] = state.TimingState, ["resourceLifecycle"] = "rune-pool"
        };
        var events = new List<GameEvent>
        {
            new("ABILITY_ACTIVATED", "激活资源技能", payload),
            new(source.Tags.Contains(CardObjectTags.UnitCard) ? "UNIT_EXHAUSTED" : "EQUIPMENT_EXHAUSTED", "横置以支付资源技能费用", payload),
            new("RESOURCE_SKILL_RESOLVED", "资源加入符文池，行动权保持不变", payload)
        };
        if (mana > 0) events.Add(new("MANA_GAINED", $"获得 {mana} 点法力", payload));
        if (power + traits.Values.Sum() > 0) events.Add(new("POWER_GAINED", $"获得 {power + traits.Values.Sum()} 点符能", payload));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    // CN 135.2.e.5.b, 166-168, 429: Gold produces ordinary wildcard power.
    // Its resource ability resolves immediately without passing focus/priority.
    internal static bool RenataGoldBonusActive(MatchState state, string player) =>
        state.PlayerZones.TryGetValue(player, out var zones)
        && zones.LegendZone.Any(id => state.CardObjects.TryGetValue(id, out var legend)
            && !legend.IsFaceDown && legend.ControllerId == player
            && LegendCardHasIdentity(legend.CardNo, RenataLegendIdentityId))
        && PlayerWithinWinningScoreDistance(state.PlayerScores, EffectiveWinningScore(state), player, RenataGoldBonusWinningScoreDistance);

    private static ResolutionResult ResolveGoldTokenResourceSkill(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        if (!CanActivateReactionResourceSkill(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "当前没有使用资源技能的行动权。", ErrorCodes.PhaseNotAllowed);
        if (command.TargetObjectIds.Count != 0 || (command.OptionalCosts?.Count ?? 0) != 0)
            return RejectWithCorePrompts(state, "金币技能不需要目标或额外选项。", ErrorCodes.InvalidTarget);
        if (!IsImmediateResourceSource(state, intent.PlayerId, command.SourceObjectId, ability))
            return RejectWithCorePrompts(state, "请选择你控制的活跃金币。休眠金币不能支付横置费用。", ErrorCodes.InvalidTarget);

        var source = state.CardObjects[command.SourceObjectId];
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        cards[command.SourceObjectId] = source with { IsExhausted = true };
        var cost = new StackItemState($"gold-cost-{state.Tick + 1}-{command.SourceObjectId}", intent.PlayerId,
            command.SourceObjectId, ability.EffectKind, source.CardNo, []);
        var destroyed = ResolveFieldDestructions(state, zones, cards, cost, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
            objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { command.SourceObjectId });
        if (destroyed.Events.Count == 0)
            return RejectWithCorePrompts(state, "无法支付金币的摧毁费用。", ErrorCodes.InvalidTarget);
        var pools = destroyed.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var pool = pools.GetValueOrDefault(intent.PlayerId, RunePool.Empty);
        var bonusMana = RenataGoldBonusActive(state, intent.PlayerId) ? 1 : 0;
        pools[intent.PlayerId] = pool with { Power = pool.Power + 1, Mana = pool.Mana + bonusMana };
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RunePools = pools,
            TriggerQueue = state.TriggerQueue.Concat(destroyed.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, destroyed.DestroyedUnitOwnerIds) };
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = intent.PlayerId, ["sourceObjectId"] = command.SourceObjectId,
            ["targetObjectId"] = command.SourceObjectId, ["abilityId"] = command.AbilityId,
            ["cardNo"] = source.CardNo, ["resourceSkill"] = true, ["reactionSpeed"] = true,
            ["generatedPower"] = 1, ["generatedMana"] = bonusMana,
            ["stackPolicy"] = "no-ordinary-stack-item", ["timingContext"] = state.TimingState
        };
        var events = new List<GameEvent>
        {
            new("ABILITY_ACTIVATED", "横置并摧毁金币以支付技能费用", payload),
            new("EQUIPMENT_EXHAUSTED", "支付金币横置费用", payload)
        };
        events.AddRange(destroyed.Events);
        events.Add(new("RESOURCE_SKILL_RESOLVED", "金币获得任意特性符能", payload));
        events.Add(new("POWER_GAINED", "获得 1 点任意特性符能", payload));
        if (bonusMana > 0) events.Add(new("MANA_GAINED", "金币额外获得法力", payload));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }
}
