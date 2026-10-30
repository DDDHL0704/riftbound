using Riftbound.Contracts;
using Riftbound.CardCatalog;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmpowerBattlefieldReduction(ObjectBinding Battlefield, string Choice);

public sealed partial class CoreRuleEngine
{
    // The modifier belongs to the complete payment instruction: choosing any
    // object here suppresses all Ward costs for this spell/skill (CN FAQ, Haisu).
    private static bool TargetsIgnoreSpellshield(MatchState state, IReadOnlyList<string> targets)
        => targets.Any(id => BattlefieldLocalRules.HasAbilityKind(BattlefieldLocalRules.AtObject(state, id),
            StaticAbilityKinds.BattlefieldIgnoreSpellshield));

    internal static int SpellshieldPowerCostForTargets(MatchState state, string player, IReadOnlyList<string> targets)
        => ResolveSpellshieldTargetTaxPower(state, player, targets, out _);

    private static int BattlefieldReactionPowerIncrease(MatchState state, string player, CardBehaviorDefinition behavior)
        => !EffectPlayIgnoresAllCosts(state, player) && behavior.CanPlayDuringPriority && state.SpellDuelState.IsActive
            && BattlefieldLocalRules.HasAbilityKind(BattlefieldLocalRules.Battlefield(state, state.SpellDuelState.BattlefieldObjectId),
                StaticAbilityKinds.BattlefieldReactionPowerIncrease) ? 1 : 0;

    // Each battlefield grants one reduction, regardless of the number of units
    // selected there. Default prompt probes consider possible friendly targets;
    // command/quote evaluation supplies the full target declaration instead.
    private static int BattlefieldFriendlyTargetSpellPowerReduction(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string>? targets)
    {
        if (!IsSpellPlayBehavior(behavior) || EffectPlayIgnoresAllCosts(state, player)) return 0;
        var candidates = targets ?? state.CardObjects.Keys.Where(id => Enumerable.Range(0, behavior.RequiredTargetCount)
            .Any(index => IsTargetObjectInScope(state, player, id, behavior.TargetScope, index, behavior.CanCounterAbilities))
            && TargetProtectionRules.IsLegalPlayCardSpellOrSkillTarget(state, player, behavior, id)).ToArray();
        return candidates.Where(id => IsPlayerControlledFieldObject(state, player, id)
                && BattlefieldLocalRules.HasAbilityKind(BattlefieldLocalRules.AtUnit(state, id),
                    StaticAbilityKinds.BattlefieldFriendlyTargetSpellPowerReduction))
            .Select(id => state.ObjectLocations[id].BattlefieldObjectId).Distinct(StringComparer.Ordinal).Count();
    }

    internal static IEnumerable<EmpowerAbility> BattlefieldEmpowerAbilities(MatchState state, CardObjectState source, EmpowerAbility ability)
    {
        var battlefield = BattlefieldLocalRules.AtUnit(state, source.ObjectId);
        if (!BattlefieldLocalRules.HasAbilityKind(battlefield, StaticAbilityKinds.BattlefieldEmpowerCostReduction)) return [ability];
        var result = new List<EmpowerAbility>(); var cost = ability.Cost;
        var binding = new ObjectBinding(battlefield!.ObjectId, battlefield.ObjectGeneration);
        if (EmpowerRules.Mana(ability.Spec, cost, EmpowerRules.ControlledRunes(state, source.ControllerId!)) > 0)
            result.Add(ability with { Id = ability.Id + ":REDUCE_MANA", Cost = cost with { Mana = cost.Mana - 1 },
                BattlefieldReduction = new(binding, "MANA") });
        foreach (var component in new[] { new KeyValuePair<string, int>("any", cost.Power) }.Concat(cost.TypedPower).Where(c => c.Value > 0))
            result.Add(ability with { Id = ability.Id + ":REDUCE_POWER:" + component.Key,
                Cost = cost with { Power = cost.Power - (component.Key == "any" ? 1 : 0),
                    TypedPower = cost.TypedPower.Where(p => p.Value - (p.Key == component.Key ? 1 : 0) > 0)
                        .ToDictionary(p => p.Key, p => p.Value - (p.Key == component.Key ? 1 : 0)) },
                BattlefieldReduction = new(binding, "POWER:" + component.Key) });
        return result.Count > 0 ? result : [ability];
    }

    internal static bool TryApplyCapturedEmpowerReduction(MatchState state, EmpowerActivationContext context,
        EmpowerCostSpec printed, out EmpowerCostSpec cost)
    {
        cost = printed;
        if (context.BattlefieldReduction is not { } reduction) return true;
        if (string.IsNullOrWhiteSpace(reduction.Choice) || reduction.Battlefield is not { Generation: >= 0 } binding || !state.CardObjects.TryGetValue(binding.ObjectId, out var source)
            || source.ObjectGeneration < binding.Generation || !source.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
            || !BattlefieldLocalRules.HasAbilityKind(source, StaticAbilityKinds.BattlefieldEmpowerCostReduction)) return false;
        if (reduction.Choice == "MANA" && printed.Mana > 0) { cost = printed with { Mana = printed.Mana - 1 }; return true; }
        if (reduction.Choice == "POWER:any" && printed.Power > 0) { cost = printed with { Power = printed.Power - 1 }; return true; }
        if (reduction.Choice.StartsWith("POWER:", StringComparison.Ordinal) && reduction.Choice[6..] is var trait
            && printed.TypedPower.GetValueOrDefault(trait) > 0) {
            cost = printed with { TypedPower = printed.TypedPower.Where(p => p.Key != trait || p.Value > 1)
                .ToDictionary(p => p.Key, p => p.Value - (p.Key == trait ? 1 : 0)) }; return true;
        }
        return false;
    }
}
