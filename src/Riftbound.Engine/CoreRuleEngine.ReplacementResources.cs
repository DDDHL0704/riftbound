using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record ReplacementResourceAction(RuleChoiceOption Choice, GameCommand Command, P4ActivatedAbilityDefinition? Ability = null);

    // CN 430.3: reaction resource skills resolve while paying, including a
    // restricted gain that cannot itself pay the instruction being resolved.
    private static IReadOnlyList<ReplacementResourceAction> ReplacementResourceActions(MatchState state, string player)
    {
        var actions = new List<ReplacementResourceAction>();
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        if (pool.HasSupportedAmounts)
        foreach (var id in state.PlayerZones[player].LegendZone.Order(StringComparer.Ordinal))
        foreach (var abilityId in new[] { KaisaLegendAbilityId, OrnnLegendAbilityId, DariusLegendAbilityId, DianaLegendAbilityId, RenektonLegendAbilityId })
        {
            if (!TryGetLegendAbility(abilityId, out var legendAbility)
                || !IsLegendAbilityTimingAllowed(state, player, legendAbility)
                || legendAbility.RequiresPlayedAnotherCardThisTurn && !ControllerPlayedAnotherCardThisTurn(state, player)
                || (long)pool.TotalStoredMana + legendAbility.ManaGainAmount > int.MaxValue
                || (long)pool.TotalStoredPower + legendAbility.PowerGainAmount > int.MaxValue
                || !PaymentCostRules.CanPayPowerCost(pool, legendAbility.PowerCost, new Dictionary<string, int>())
                || !state.CardObjects.TryGetValue(id, out var source) || source.ObjectId != id
                || source.IsFaceDown || source.IsExhausted || !SourceObjectControlledByPlayerOrLegacyOwned(source, player)
                || !LegendAbilitySourceHasAbility(state, player, source.CardNo, legendAbility)) continue;
            if (abilityId == RenektonLegendAbilityId)
            {
                var options = RenektonPowerSpendChoices(pool);
                foreach (var option in options)
                {
                    var baseId = $"LEGEND_RESOURCE:{id}:{abilityId}";
                    var choiceId = options.Count == 1 ? baseId : baseId + ":" + option;
                    var spent = string.Join("、", option[PlayPowerSpendSelectionRules.ChoicePrefix.Length..].Split(',')
                        .Select(part => part.Split(':')).Select(pair => pair[1] + " 点" + (pair[0] == "A" ? "任意" : RuneTraitLabel(pair[0])) + "符能"));
                    actions.Add(new(new(choiceId, $"支付{spent}并横置传奇：获得 2 点单位专用法力", [id]),
                        new LegendActCommand(id, abilityId, [], [option])));
                }
                continue;
            }
            var restriction = abilityId == KaisaLegendAbilityId ? "打出法术" : "打出装备或使用装备技能";
            var gain = abilityId == DianaLegendAbilityId ? "1 点仅可在法术对决期间消耗的法力" : legendAbility.ManaGainAmount > 0 ? "1 点法力" : $"1 点仅可用于{restriction}的符能";
            actions.Add(new(new($"LEGEND_RESOURCE:{id}:{abilityId}",
                $"横置传奇：获得 {gain}，之后继续选择", [id]),
                new LegendActCommand(id, abilityId, [], [])));
        }
        foreach (var id in state.PlayerZones[player].Base.Concat(state.PlayerZones[player].Battlefields).Order(StringComparer.Ordinal))
        foreach (var ability in P4ActivatedAbilityCatalog.GetAll().Where(a => a.IsResourceSkill && a.ReactionSpeed
            && (string.IsNullOrWhiteSpace(a.ResourceRestriction) || P4ActivatedAbilityCatalog.IsLuxResourceAbility(a.AbilityId))))
        {
            if (P4ActivatedAbilityCatalog.IsLuxResourceAbility(ability.AbilityId))
            {
                if (CanUseLuxSpellOnlyResourceSource(state, player, id)
                    && CanGainSpellOnlyMana(state.RunePools.GetValueOrDefault(player, RunePool.Empty), P4ActivatedAbilityCatalog.LuxGeneratedMana))
                    Add([], "横置：获得 2 点只能用于打出法术的法力");
                continue;
            }
            if ((!IsImmediatePoolResourceAbility(ability.AbilityId) && !P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId))
                || !IsImmediateResourceSource(state, player, id, ability)) continue;
            if (ability.AbilityId is P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId or P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId)
            {
                var stele = ability.AbilityId == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId;
                if (!ResourceQuantityRules.TryConversionMaximum(pool, ability.AbilityId, out var maximum, state)) continue;
                var prefix = ResourceQuantityRules.ConversionPrefix(ability.AbilityId)!;
                if ((long)maximum + 1 > ResourceQuantityRules.DirectChoiceLimit)
                    Add([prefix + ResourceQuantityRules.ChooseAmountSuffix], $"横置：选择 0–{maximum} 的转换数量");
                else
                    for (var amount = 0; amount <= maximum; amount++)
                        Add([prefix + amount], stele ? $"横置：将 {amount} 法力转为符能" : $"横置：将 {amount} 符能转为法力");
            }
            else
            {
                var traits = P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(ability);
                var gain = traits.Count > 0 ? string.Join("、",traits.Select(t=>$"{t.Value} 点{RuneTraitLabel(t.Key)}符能"))
                    : ability.GeneratedPower > 0 ? $"{ability.GeneratedPower} 点任意符能" : $"{ability.GeneratedMana} 点法力";
                if (P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId) && RenataGoldBonusActive(state,player)) gain += "和 1 点法力";
                Add([], (P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId) ? "摧毁并横置" : "横置") + "：获得 " + gain);
                if (P4ActivatedAbilityCatalog.IsHoneyfruitResourceAbility(ability.AbilityId) && state.PlayerExperience.GetValueOrDefault(player) >= 6)
                    Add([P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix + id], "横置：获得 1 点法力和 1 点任意符能");
            }
            void Add(string[] costs, string label)
            {
                if (!ResourceAbilityAmountsSupported(state, player, ability, costs)) return;
                var name = CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo ?? "", out var card) ? card.DisplayName
                    : P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId) ? "金币" : state.CardObjects[id].CardNo;
                var key = $"RESOURCE:{id}:{ability.AbilityId}:{string.Join(',',costs)}";
                actions.Add(new(new(key, $"{name}：{label}，之后继续选择支付或放弃", [id]), new ActivateAbilityCommand(id, ability.AbilityId, [], costs), ability));
            }
        }
        return actions;
    }

    private static IReadOnlyList<string> RenektonPowerSpendChoices(RunePool pool)
    {
        var wallet = PaymentCostRules.PowerPoolForPurpose(pool, PowerPaymentPurpose.Other);
        var keys = new[] { "A", "blue", "green", "orange", "purple", "red", "yellow" };
        int Available(string key) => key == "A" ? wallet.Power : wallet.PowerByTrait.GetValueOrDefault(key);
        var result = new List<string>();
        for (var first = 0; first < keys.Length; first++)
        for (var second = first; second < keys.Length; second++)
        {
            if (Available(keys[first]) < (first == second ? 2 : 1) || Available(keys[second]) < 1) continue;
            result.Add(first == second ? $"POWER_SPEND:{keys[first]}:2"
                : $"POWER_SPEND:{keys[first]}:1,{keys[second]}:1");
        }
        return result;
    }

    internal static bool ResourceAbilityAmountsSupported(MatchState state, string player,
        P4ActivatedAbilityDefinition ability, string[] costs)
    {
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        if (!pool.HasSupportedAmounts) return false;
        if (P4ActivatedAbilityCatalog.IsLuxResourceAbility(ability.AbilityId))
            return CanGainSpellOnlyMana(pool, P4ActivatedAbilityCatalog.LuxGeneratedMana);
        long mana = (long)pool.Mana + pool.DuelOnlyMana, power = pool.TotalStoredPower;
        if (ability.AbilityId is P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId or P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId)
        {
            var prefix = ResourceQuantityRules.ConversionPrefix(ability.AbilityId)!;
            if (costs.SequenceEqual([prefix + ResourceQuantityRules.ChooseAmountSuffix]))
                return ResourceQuantityRules.TryConversionMaximum(pool, ability.AbilityId, out _, state);
            if (!TryReadResourceConversionAmount(ability.AbilityId, costs, out var amount)) return false;
            if (ability.AbilityId == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId)
            {
                if (amount > PaymentCostRules.AvailableMana(pool, ManaPaymentPurpose.Other, state)) return false;
                mana -= amount; power += amount;
            }
            else
            {
                if (amount > PaymentCostRules.PowerPoolForPurpose(pool, PowerPaymentPurpose.Equipment).TotalPower) return false;
                power -= amount; mana += amount;
            }
        }
        else if (P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId))
        { power++; if (RenataGoldBonusActive(state, player)) mana++; }
        else
        {
            mana += P4ActivatedAbilityCatalog.IsHoneyfruitResourceAbility(ability.AbilityId)
                ? costs.Length == 1 ? 1 : 0 : ability.GeneratedMana;
            power += ability.GeneratedPower + P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(ability).Values.Sum(v => (long)v);
        }
        return mana >= 0 && mana + pool.SpellOnlyMana + pool.UnitPlayOrUnitAbilityOnlyMana <= int.MaxValue
            && power is >= 0 and <= int.MaxValue;
    }

    private static MatchState ReplacementResourceContext(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player)
        => state with { PlayerZones = zones, CardObjects = cards, RunePools = pools, PendingRuleChoice = null,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            PendingPayment = new("replacement-resource", RuleChoiceWindow, player) };

    private static ResolutionResult ApplyReplacementResourceAction(MatchState context, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player,
        ReplacementResourceAction action, List<GameEvent> events)
    {
        context = RuleChoiceResourceContext(context);
        var capture = BeginRuleChoiceResourceStep(context, player, action.Choice.Id);
        var intent = new PlayerIntent("replacement-resource", player, action.Command.CmdType);
        var result = action.Command switch
        {
            LegendActCommand legend => ResolveLegendAct(context, intent, legend),
            ActivateAbilityCommand activated when action.Ability is { } ability
                => P4ActivatedAbilityCatalog.IsLuxResourceAbility(ability.AbilityId)
                    ? ResolveLuxRestrictedMana(context, intent, activated)
                    : P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId)
                        ? ResolveGoldTokenResourceSkill(context, intent, activated, ability)
                        : ResolveImmediateResourceSkill(context, intent, activated, ability),
            _ => throw new InvalidOperationException("Invalid generated resource command.")
        };
        if (!result.Accepted) throw new InvalidOperationException("Invalid generated resource choice: " + result.ErrorMessage);
        result = FinalizeTokenDepartures(context, result);
        result = CompleteRuleChoiceResourceEffects(context, result);
        Replace(zones, result.State.PlayerZones); Replace(cards, result.State.CardObjects); Replace(pools, result.State.RunePools);
        events.AddRange(result.Events);
        var frame = RuleChoices.Value!;
        frame.NestedTriggers.AddRange(result.State.TriggerQueue.Where(t => !context.TriggerQueue.Any(old => old.TriggerId == t.TriggerId)));
        frame.NestedDestroyedOwners.UnionWith(result.State.DestroyedUnitOwnerIdsThisTurn.Except(context.DestroyedUnitOwnerIdsThisTurn));
        CompleteRuleChoiceResourceStep(capture, result.State, result.Events);
        return result;
        static void Replace<T>(Dictionary<string,T> target, IReadOnlyDictionary<string,T> source)
        {
            if (ReferenceEquals(target, source)) return;
            target.Clear(); foreach (var pair in source) target[pair.Key] = pair.Value;
        }
    }

    private static ResolutionResult ApplyNestedDestructionContinuations(ResolutionResult result)
    {
        var frame = RuleChoices.Value;
        if (!result.Accepted || frame is null || frame.NestedTriggers.Count + frame.NestedDestroyedOwners.Count == 0) return result;
        var next = result.State with { TriggerQueue = result.State.TriggerQueue.Concat(result.State.Status == MatchStatuses.InProgress ? frame.NestedTriggers : []).DistinctBy(t=>t.TriggerId).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = result.State.DestroyedUnitOwnerIdsThisTurn.Concat(frame.NestedDestroyedOwners).Distinct().ToArray() };
        frame.NestedTriggers.Clear(); frame.NestedDestroyedOwners.Clear();
        return result with { State = next };
    }
}
