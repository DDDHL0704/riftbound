using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record CardBehaviorDefinition
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TargetedSpellManaOrPowerReduction { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private const string TargetedSpellReductionPrefix = "TARGETED_SPELL_REDUCTION:";
    private sealed record TargetedSpellReducer(string Key, ObjectBinding Source, string InstanceId,
        string DisplayName, int Amount);
    private sealed record TargetedSpellReduction(int Mana, int Power, IReadOnlyList<string> ChoiceIds);
    private sealed record TargetedSpellPrice(int Mana, SpellPowerReductionOption Power, TargetedSpellReduction Reduction);

    private static bool IsTargetedSpellReductionChoice(string choice)
        => choice.StartsWith(TargetedSpellReductionPrefix, StringComparison.Ordinal);

    // The continuous text applies to the actual friendly source instance being
    // chosen. Exhaustion is irrelevant. Repeatedly choosing one object with the
    // same spell does not create additional instances of that source's rule.
    private static IReadOnlyList<TargetedSpellReducer> TargetedSpellReducers(MatchState state,
        string player, CardBehaviorDefinition spell, IReadOnlyList<string>? targets)
    {
        if (!IsSpellPlayBehavior(spell) || EffectPlayIgnoresAllCosts(state, player)) return [];
        var ids = targets ?? PublicStaticAuraSourceObjectIds(state.PlayerZones).ToArray();
        var result = new List<TargetedSpellReducer>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)
                || !IsFieldUnitObject(state, id)
                || EffectiveFieldControllerId(state.PlayerZones, id, source) != player) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (!CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                    || behavior.TargetedSpellManaOrPowerReduction <= 0
                    || targets is null && !MayTargetSpellCostSource(state, player, spell, id)) continue;
                var binding = new ObjectBinding(id, source.ObjectGeneration);
                var key = TargetedSpellReductionPrefix + MatchStateHasher.HashValue(new {
                    Source = binding, instance.Id, instance.CardNo, Controller = player,
                    FaceRevision = instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0 });
                result.Add(new(key, binding, instance.Id, behavior.DisplayName,
                    behavior.TargetedSpellManaOrPowerReduction));
            }
        }
        return result;
    }

    private static bool MayTargetSpellCostSource(MatchState state, string player, CardBehaviorDefinition spell, string id)
        => spell.SpellModes is { } modes ? modes.Modes.Any(mode => MayTargetSpellCostSource(state, player, mode, id))
            : Enumerable.Range(0, MaxTargetCount(state, player, spell)).Any(index =>
                IsTargetObjectInScope(state, player, id, PlayCardTargetScopeForBehavior(spell), index, spell.CanCounterAbilities)
                && IsTargetTagAllowed(state, id, spell) && IsTargetRequiredTagAllowed(state, id, spell)
                && IsTargetPowerAllowed(state, id, spell) && IsTargetManaCostAllowed(state, player, id, spell));

    internal static int AvailableTargetedSpellReduction(MatchState state, string player, CardBehaviorDefinition spell)
        => TargetedSpellReducers(state, player, spell, null).Sum(source => source.Amount);

    // A single source offers a choice between its two resource dimensions.
    // Keeping one canonical representative for equal totals is safe: these
    // continuous reductions neither consume a source nor change future effects.
    private static bool TryTargetedSpellReductions(MatchState state, string player,
        CardBehaviorDefinition spell, IReadOnlyList<string>? targets, IReadOnlyList<string> declared,
        out IReadOnlyList<TargetedSpellReduction> options)
    {
        options = [];
        var sources = TargetedSpellReducers(state, player, spell, targets);
        var legal = sources.SelectMany(source => new[] { source.Key + ":MANA", source.Key + ":POWER" })
            .ToHashSet(StringComparer.Ordinal);
        if (declared.Distinct(StringComparer.Ordinal).Count() != declared.Count
            || declared.Any(choice => !legal.Contains(choice))) return false;
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var choice in declared)
        {
            var split = choice.LastIndexOf(':');
            if (!selected.TryAdd(choice[..split], choice[(split + 1)..])) return false;
        }
        var allocations = new Dictionary<(int Mana, int Power), TargetedSpellReduction> {
            [(0, 0)] = new(0, 0, []) };
        foreach (var source in sources)
        {
            var next = new Dictionary<(int Mana, int Power), TargetedSpellReduction>();
            var modes = selected.TryGetValue(source.Key, out var chosen) ? new[] { chosen } : ["MANA", "POWER"];
            foreach (var allocation in allocations.Values)
            foreach (var mode in modes)
            {
                var mana = (int)Math.Min(int.MaxValue, (long)allocation.Mana + (mode == "MANA" ? source.Amount : 0));
                var power = (int)Math.Min(int.MaxValue, (long)allocation.Power + (mode == "POWER" ? source.Amount : 0));
                next.TryAdd((mana, power), new(mana, power,
                    allocation.ChoiceIds.Concat([source.Key + ":" + mode]).ToArray()));
            }
            allocations = next;
        }
        options = allocations.Values.ToArray();
        return true;
    }

    internal static IReadOnlyList<ActionPromptChoiceDto> TargetedSpellReductionChoices(MatchState state,
        string player, CardBehaviorDefinition spell)
        => TargetedSpellReducers(state, player, spell, null).SelectMany(source => new[] {
            new ActionPromptChoiceDto(source.Key + ":MANA", $"{source.DisplayName}：减少 {source.Amount} 法力",
                "仅在法术选择此单位为目标时适用；同一份技能选择一种减费。", [source.Source.ObjectId]),
            new ActionPromptChoiceDto(source.Key + ":POWER", $"{source.DisplayName}：减少 {source.Amount} 符能",
                "仅在法术选择此单位为目标时适用；同一份技能选择一种减费。", [source.Source.ObjectId])
        }).ToArray();
}
