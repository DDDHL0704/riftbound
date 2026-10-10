using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record ReplacementResourceAction(RuleChoiceOption Choice, ActivateAbilityCommand Command, P4ActivatedAbilityDefinition Ability);

    // CN 429.3 / 444.2.c: reaction resource skills resolve while paying. A swift
    // skill or a spell-only resource does not acquire that permission here.
    private static IReadOnlyList<ReplacementResourceAction> ReplacementResourceActions(MatchState state, string player)
    {
        var actions = new List<ReplacementResourceAction>();
        foreach (var id in state.PlayerZones[player].Base.Concat(state.PlayerZones[player].Battlefields).Order(StringComparer.Ordinal))
        foreach (var ability in P4ActivatedAbilityCatalog.GetAll().Where(a => a.IsResourceSkill && a.ReactionSpeed
            && string.IsNullOrWhiteSpace(a.ResourceRestriction)))
        {
            if ((!IsImmediatePoolResourceAbility(ability.AbilityId) && !P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId))
                || !IsImmediateResourceSource(state, player, id, ability)) continue;
            if (ability.AbilityId is P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId or P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId)
            {
                var stele = ability.AbilityId == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId;
                var pool = state.RunePools.GetValueOrDefault(player) ?? RunePool.Empty;
                var available = stele ? pool.Mana : pool.TotalPower;
                for (var amount = 1; amount <= available; amount++)
                    Add([(stele ? P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix : P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix) + amount],
                        stele ? $"横置：将 {amount} 法力转为符能" : $"横置：将 {amount} 符能转为法力");
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
                var name = CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo ?? "", out var card) ? card.DisplayName
                    : P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(ability.AbilityId) ? "金币" : state.CardObjects[id].CardNo;
                var key = $"RESOURCE:{id}:{ability.AbilityId}:{string.Join(',',costs)}";
                actions.Add(new(new(key, $"{name}：{label}，之后继续选择支付或放弃", [id]), new(id, ability.AbilityId, [], costs), ability));
            }
        }
        return actions;
    }

    private static MatchState ReplacementResourceContext(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player)
        => state with { PlayerZones = zones, CardObjects = cards, RunePools = pools, PendingRuleChoice = null,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            PendingPayment = new("replacement-resource", RuleChoiceWindow, player) };

    private static void ApplyReplacementResourceAction(MatchState context, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player,
        ReplacementResourceAction action, List<GameEvent> events)
    {
        var intent = new PlayerIntent("replacement-resource", player, CommandTypes.ActivateAbility);
        var result = P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(action.Ability.AbilityId)
            ? ResolveGoldTokenResourceSkill(context, intent, action.Command, action.Ability)
            : ResolveImmediateResourceSkill(context, intent, action.Command, action.Ability);
        if (!result.Accepted) throw new InvalidOperationException("Invalid generated resource choice: " + result.ErrorMessage);
        Replace(zones, result.State.PlayerZones); Replace(cards, result.State.CardObjects); Replace(pools, result.State.RunePools);
        events.AddRange(result.Events);
        var frame = RuleChoices.Value!;
        frame.ResourceTriggers.AddRange(result.State.TriggerQueue.Where(t => !context.TriggerQueue.Any(old => old.TriggerId == t.TriggerId)));
        frame.ResourceDestroyedOwners.UnionWith(result.State.DestroyedUnitOwnerIdsThisTurn.Except(context.DestroyedUnitOwnerIdsThisTurn));
        static void Replace<T>(Dictionary<string,T> target, IReadOnlyDictionary<string,T> source)
        {
            if (ReferenceEquals(target, source)) return;
            target.Clear(); foreach (var pair in source) target[pair.Key] = pair.Value;
        }
    }

    private static ResolutionResult ApplyReplacementResourceContinuations(ResolutionResult result)
    {
        var frame = RuleChoices.Value;
        if (!result.Accepted || frame is null || frame.ResourceTriggers.Count + frame.ResourceDestroyedOwners.Count == 0) return result;
        var next = result.State with { TriggerQueue = result.State.TriggerQueue.Concat(result.State.Status == MatchStatuses.InProgress ? frame.ResourceTriggers : []).DistinctBy(t=>t.TriggerId).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = result.State.DestroyedUnitOwnerIdsThisTurn.Concat(frame.ResourceDestroyedOwners).Distinct().ToArray() };
        frame.ResourceTriggers.Clear(); frame.ResourceDestroyedOwners.Clear();
        return result with { State = next };
    }
}
