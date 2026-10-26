using System.Globalization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string OptionalCostReductionPrefix = "OPTIONAL_COST_REDUCTION:";

    private sealed record OptionalCostComponent(string Id, int Mana, int GenericPower,
        IReadOnlyDictionary<string, int> TypedPower, string? PrintedCardNo = null, bool IsEcho = false, bool CanReduce = true);

    private sealed record OptionalCostReducer(string ObjectId, long Generation, string Name,
        string InstanceId, RuleTextInstanceBinding? Origin);

    internal sealed record OptionalCostReductionGroup(string SourceObjectId, long SourceGeneration,
        string SourceInstanceId, string ComponentId, IReadOnlyList<string> ChoiceIds,
        IReadOnlyList<string> RequiredOptionalCosts, RuleTextInstanceBinding? RuleTextOrigin);

    private sealed record OptionalCostReductionChoice(string Id, OptionalCostReducer Source,
        string ComponentId, string Mode, string Trait,
        IReadOnlyDictionary<string, int>? PrintedAllocation = null);

    private sealed record OptionalCostReductionResult(int ExtraMana, int ExtraGenericPower,
        IReadOnlyDictionary<string, int> ExtraTypedPower, IReadOnlyList<string> PrintedCosts,
        int EchoManaReduction, int ManaReduction, int PowerReduction,
        IReadOnlyList<string> ChoiceIds, IReadOnlyList<IReadOnlyDictionary<string, object?>> Receipts);

    private sealed record OptionalCostAmounts(int Mana, int GenericPower, IReadOnlyDictionary<string, int> TypedPower)
    {
        public string Key => Mana.ToString(CultureInfo.InvariantCulture) + ":"
            + GenericPower.ToString(CultureInfo.InvariantCulture) + ":" + PrintedPowerCostRules.ChoiceId(TypedPower);
    }

    private static bool IsOptionalCostReductionChoice(string choice)
        => choice.StartsWith(OptionalCostReductionPrefix, StringComparison.Ordinal);

    private static IReadOnlyList<OptionalCostReducer> OptionalCostReducers(
        MatchState state, string player, string? playedObjectId)
    {
        if (!state.PlayerZones.TryGetValue(player, out var zones)) return [];
        var sources = new List<OptionalCostReducer>();
        foreach (var id in zones.Base.Concat(zones.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (id == playedObjectId || !IsPlayerControlledFieldUnitObject(state, player, id)
                || state.CardObjects[id].ControllerId != player || state.CardObjects[id].IsFaceDown
                || !state.ObjectLocations.TryGetValue(id, out var at) || at.PlayerId != player
                || FindFieldObjectLocation(state.PlayerZones, id) is not { } field
                || field.PlayerId != player || at.Zone != field.Zone) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, state.CardObjects[id]))
            {
                if (!CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                    || !behavior.MayReduceOtherCardOptionalAdditionalCostByOneManaOrPower) continue;
                var name = behavior.DisplayName;
                if (instance.Origin is { } origin)
                    name += state.CardObjects.TryGetValue(origin.Equipment.ObjectId, out var equipment)
                        && CardBehaviorRegistry.TryGetByCardNo(equipment.CardNo ?? "", out var gear)
                        ? "（" + gear.DisplayName + "复制）" : "（武装复制）";
                sources.Add(new(id, state.CardObjects[id].ObjectGeneration, name, instance.Id, instance.Origin));
            }
        }
        return sources;
    }

    private static IReadOnlyList<OptionalCostComponent> OptionalCostComponents(
        MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> costs, int mana, int generic, IReadOnlyDictionary<string, int> typed)
    {
        var echoes = EchoCostRules.Selected(state, player, behavior, costs);
        if (echoes.Count == 0)
        {
            // Single-option prompt probes and compound submissions must use
            // the same identities; each fee has its own reduction and floor.
            var hasteCosts = costs.Where(CardPermissionKeywordRules.IsHasteReadyCostChoice).ToArray();
            if (HasReducibleNonHasteOptionalResourceAdditionalCost(behavior)
                && CardPermissionKeywordRules.TryBuildHasteReadyOptionalCost(hasteCosts, behavior,
                    out var hasteMana, out var hastePower, out var hasteTrait))
            {
                IReadOnlyDictionary<string, int> hasteTyped = string.IsNullOrWhiteSpace(hasteTrait)
                    ? new Dictionary<string, int>() : new Dictionary<string, int> { [hasteTrait] = hastePower };
                var hasteGeneric = string.IsNullOrWhiteSpace(hasteTrait) ? hastePower : 0;
                var remainder = typed.Where(p => p.Value > hasteTyped.GetValueOrDefault(p.Key))
                    .ToDictionary(p => p.Key, p => p.Value - hasteTyped.GetValueOrDefault(p.Key), StringComparer.Ordinal);
                var components = new List<OptionalCostComponent> {
                    new(HasteOptionalCostNames.HasteReady, hasteMana, hasteGeneric, hasteTyped) };
                if (mana - hasteMana + generic - hasteGeneric + remainder.Values.Sum() > 0)
                    components.Add(new("ADDITIONAL", mana - hasteMana, generic - hasteGeneric, remainder));
                return components;
            }
            return mana + generic + typed.Values.Sum() > 0
                ? [new("ADDITIONAL", mana, generic, typed, CanReduce: HasReducibleOptionalResourceAdditionalCost(behavior))] : [];
        }
        return echoes.Select(echo => {
            var printed = echo.PrintedCardNo is { } card ? PrintedPowerCostRules.ForCard(card) : null;
            return new OptionalCostComponent(echo.Id, echo.Mana,
                echo.GenericPower + (printed?.Traits.Count == 0 ? printed.Amount : 0), echo.TypedPower,
                printed is { Amount: > 0, Traits.Count: > 0 } ? echo.PrintedCardNo : null, IsEcho: true);
        }).ToArray();
    }

    // CN204.3.a explicitly makes Bullet Time's payment an instruction during
    // resolution. Its legacy prepayment model does not make it an additional
    // play cost. Only these actual optional-additional branches are reducible.
    private static bool HasReducibleOptionalResourceAdditionalCost(CardBehaviorDefinition behavior)
        => behavior.HasteReadyManaCost > 0 || behavior.HasteReadyPowerCost > 0
            || HasReducibleNonHasteOptionalResourceAdditionalCost(behavior);

    private static bool HasReducibleNonHasteOptionalResourceAdditionalCost(CardBehaviorDefinition behavior)
        => behavior.SourceReadyAdditionalPowerCost > 0 || behavior.SourceBoonAdditionalManaCost > 0
            || behavior.SourceDrawAdditionalPowerCost > 0 || behavior.SourceReadyPowerModifierAdditionalPowerCost > 0
            || behavior.SourceStealEnemyEquipmentAdditionalPowerCost > 0 || HasTargetEffectAdditionalCost(behavior);

    private static string OptionalCostReductionId(OptionalCostReducer source, string component, string mode,
        IReadOnlyDictionary<string, int>? allocation = null)
        => OptionalCostReductionPrefix + Uri.EscapeDataString(source.ObjectId) + ":"
            + source.Generation.ToString(CultureInfo.InvariantCulture) + ":" + Uri.EscapeDataString(component) + ":" + mode
            + (allocation is null ? "" : ":" + Uri.EscapeDataString(PrintedPowerCostRules.ChoiceId(allocation)))
            + (source.Origin is null ? "" : ":RULE_TEXT:" + Uri.EscapeDataString(source.InstanceId));

    private static IReadOnlyList<OptionalCostReductionChoice> OptionalCostReductionChoices(
        IReadOnlyList<OptionalCostReducer> sources, IReadOnlyList<OptionalCostComponent> components)
    {
        var choices = new List<OptionalCostReductionChoice>();
        foreach (var source in sources)
        foreach (var component in components.Where(component => component.CanReduce))
        {
            if (component.Mana > 0)
                choices.Add(new(OptionalCostReductionId(source, component.Id, "MANA"), source, component.Id, "MANA", ""));
            if (component.GenericPower > 0)
                choices.Add(new(OptionalCostReductionId(source, component.Id, "POWER-any"), source, component.Id, "POWER", ""));
            var allocations = component.PrintedCardNo is { } card
                ? PrintedPowerCostRules.Allocations(card) : [new Dictionary<string, int>()];
            foreach (var allocation in allocations)
            foreach (var trait in PrintedPowerCostRules.Combine(component.TypedPower, allocation)
                .Where(p => p.Value > 0).Select(p => p.Key).Order(StringComparer.Ordinal))
            {
                var selectedAllocation = component.PrintedCardNo is null ? null : allocation;
                choices.Add(new(OptionalCostReductionId(source, component.Id, "POWER-" + trait, selectedAllocation),
                    source, component.Id, "POWER", trait, selectedAllocation));
            }
        }
        return choices.DistinctBy(choice => choice.Id).ToArray();
    }

    // Affordability explores possible prices, never selects a reducer for the
    // submitted command. Identical source assignments share a resource vector.
    private static IReadOnlyList<OptionalCostAmounts> OptionalCostPriceVariants(
        OptionalCostComponent component, int sources, int echoReduction)
    {
        var allocations = component.PrintedCardNo is { } card
            ? PrintedPowerCostRules.Allocations(card) : [new Dictionary<string, int>()];
        var prices = allocations.Select(allocation => new OptionalCostAmounts(
            component.IsEcho ? Math.Max(0, component.Mana - echoReduction) : component.Mana,
            component.GenericPower, PrintedPowerCostRules.Combine(component.TypedPower, allocation)))
            .DistinctBy(price => price.Key).ToDictionary(price => price.Key, StringComparer.Ordinal);
        var steps = Math.Min(component.CanReduce ? sources : 0,
            prices.Values.Max(price => price.Mana + price.GenericPower + price.TypedPower.Values.Sum()));
        for (var step = 0; step < steps; step++)
        {
            foreach (var price in prices.Values.ToArray())
            {
                if (price.Mana > 0) Add(price with { Mana = price.Mana - 1 });
                if (price.GenericPower > 0) Add(price with { GenericPower = price.GenericPower - 1 });
                foreach (var trait in price.TypedPower.Where(p => p.Value > 0).Select(p => p.Key))
                {
                    var typed = price.TypedPower.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                    if (--typed[trait] == 0) typed.Remove(trait);
                    Add(price with { TypedPower = typed });
                }
            }
        }
        return prices.Values.ToArray();
        void Add(OptionalCostAmounts price) => prices.TryAdd(price.Key, price);
    }

    internal static bool CanOfferOptionalCostWithReductions(
        MatchState state, string player, CardBehaviorDefinition behavior, string? source,
        IReadOnlyList<string> declaredCosts, RunePool availablePool,
        out IReadOnlyList<ActionPromptChoiceDto> reductionChoices,
        ICollection<OptionalCostReductionGroup>? reductionGroups = null)
    {
        reductionChoices = [];
        var reducers = OptionalCostReducers(state, player, source);
        if (reducers.Count == 0) return false;
        // Mandatory nonresource objects are selected elsewhere in the prompt.
        // They are not a monetary component eligible for this reduction.
        var monetaryBehavior = behavior with { RequiresDestroyFriendlyUnitAdditionalCost = false,
            RequiresDestroyFriendlyPowerfulUnitAdditionalCost = false, RequiresDestroyFriendlyTraitUnitAdditionalCost = false,
            RequiresReturnFriendlyEquipmentAdditionalCost = false, MaySacrificeUnitForPrintedCostReduction = false };
        if (!TryBuildOptionalCostPlan(state, player, declaredCosts, monetaryBehavior, out var normalized,
                out var mana, out var generic, out var typed, out var experience, out var optionalReduction,
                out _, out _, out _, out _, out _)) return false;
        if (state.PlayerExperience.GetValueOrDefault(player) < experience) return false;
        var components = OptionalCostComponents(state, player, behavior, normalized, mana, generic, typed);
        if (components.Count == 0) return false;
        var choices = OptionalCostReductionChoices(reducers, components);
        if (choices.Count == 0) return false;
        var automaticEchoReduction = EchoCostRules.Reduction(state, player);
        IReadOnlyList<OptionalCostAmounts> prices = [new(0, 0, new Dictionary<string, int>())];
        foreach (var component in components)
        {
            var variants = OptionalCostPriceVariants(component, reducers.Count, automaticEchoReduction);
            prices = prices.SelectMany(first => variants.Select(second => new OptionalCostAmounts(
                    first.Mana + second.Mana, first.GenericPower + second.GenericPower,
                    PrintedPowerCostRules.Combine(first.TypedPower, second.TypedPower))))
                .DistinctBy(price => price.Key).ToArray();
        }
        var echoReduction = components.Where(c => c.IsEcho).Sum(c => Math.Min(c.Mana, automaticEchoReduction));
        var sacrifices = new string?[] { null }.Concat(behavior.MaySacrificeUnitForPrintedCostReduction
            ? OptionalSacrificeCandidates(state, player) : []);
        foreach (var sacrifice in sacrifices)
        foreach (var price in prices)
        {
            IReadOnlyList<string> costs = sacrifice is null ? normalized : normalized.Append(sacrifice).ToArray();
            var total = CalculatePlayManaCost(state, player, behavior, price.Mana, optionalReduction,
                costs, sourceObjectId: source, componentEchoReduction: echoReduction);
            if (PaymentCostRules.AvailableMana(availablePool, IsSpellPlayBehavior(behavior)
                    ? ManaPaymentPurpose.PlaySpell : ManaPaymentPurpose.Other) < total.Total) continue;
            PrintedPowerCostRules.TrySelectForPlay(behavior, null, availablePool,
                price.GenericPower + SourceReplayPowerCost(state, player), price.TypedPower,
                out var totalGeneric, out var totalTyped);
            totalTyped = ApplySacrificePowerReduction(totalTyped,
                SelectedSacrificeCostReduction(state, player, behavior, costs).Power);
            if (!PaymentCostRules.CanPayPowerCost(availablePool, totalGeneric, totalTyped)) continue;
            reductionChoices = choices.Select(choice => new ActionPromptChoiceDto(choice.Id,
                choice.Source.Name + "：" + (choice.ComponentId.StartsWith("ECHO", StringComparison.Ordinal) ? "回响"
                    : choice.ComponentId == HasteOptionalCostNames.HasteReady ? "急速" : "可选额外费用")
                    + (choice.Mode == "MANA" ? "减少 1 法力" : "减少 1 "
                        + (choice.Trait.Length == 0 ? "任意" : RuneTraitLabel(choice.Trait)) + "符能")
                    + (choice.PrintedAllocation is null ? "" : "（回响原符能："
                        + string.Join("＋", choice.PrintedAllocation.OrderBy(p => p.Key, StringComparer.Ordinal)
                            .Select(p => p.Value + RuneTraitLabel(p.Key))) + "）"),
                "选择后仅减免该额外费用；同一技能实例对每项费用最多选一种，不选则保留原费。",
                choice.Source.Origin is null ? [choice.Source.ObjectId]
                    : [choice.Source.ObjectId, choice.Source.Origin.Equipment.ObjectId])).ToArray();
            if (reductionGroups is not null)
                foreach (var group in choices.GroupBy(choice => (choice.Source.ObjectId, choice.Source.Generation, choice.Source.InstanceId, choice.ComponentId)))
                    reductionGroups.Add(new(group.Key.ObjectId, group.Key.Generation, group.Key.InstanceId, group.Key.ComponentId,
                        group.Select(choice => choice.Id).ToArray(), declaredCosts.ToArray(), group.First().Source.Origin));
            return true;
        }
        return false;
    }

    private static bool TryApplyOptionalCostReductions(
        MatchState state, string player, string? playedObjectId, CardBehaviorDefinition behavior,
        IReadOnlyList<string> behaviorCosts, int mana, int generic, IReadOnlyDictionary<string, int> typed,
        IReadOnlyList<string> choiceIds, out OptionalCostReductionResult result)
    {
        result = new(mana, generic, typed, [], 0, 0, 0, [], []);
        var components = OptionalCostComponents(state, player, behavior, behaviorCosts, mana, generic, typed);
        var available = choiceIds.Count == 0 ? []
            : OptionalCostReductionChoices(OptionalCostReducers(state, player, playedObjectId), components);
        var byId = available.ToDictionary(choice => choice.Id, StringComparer.Ordinal);
        var selected = new List<OptionalCostReductionChoice>();
        var used = new HashSet<(string Source, string Component)>();
        foreach (var id in choiceIds)
        {
            if (!byId.TryGetValue(id, out var choice) || !used.Add((choice.Source.InstanceId, choice.ComponentId))) return false;
            selected.Add(choice);
        }

        var extraMana = 0;
        var extraGeneric = 0;
        IReadOnlyDictionary<string, int> extraTyped = new Dictionary<string, int>();
        var printedCosts = new List<string>();
        var echoReduction = 0;
        var manaReduction = 0;
        var powerReduction = 0;
        var receipts = new List<IReadOnlyDictionary<string, object?>>();
        var automaticEchoReduction = EchoCostRules.Reduction(state, player);
        foreach (var component in components)
        {
            var componentMana = component.IsEcho ? Math.Max(0, component.Mana - automaticEchoReduction) : component.Mana;
            echoReduction += component.Mana - componentMana;
            var componentGeneric = component.GenericPower;
            var componentTyped = component.TypedPower.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var componentChoices = selected.Where(choice => choice.ComponentId == component.Id).ToArray();
            var allocations = componentChoices.Where(choice => choice.PrintedAllocation is not null)
                .Select(choice => choice.PrintedAllocation!).DistinctBy(PrintedPowerCostRules.ChoiceId).ToArray();
            if (allocations.Length > 1) return false;
            if (component.PrintedCardNo is { } printed)
            {
                if (allocations.Length == 0) printedCosts.Add(printed);
                else componentTyped = PrintedPowerCostRules.Combine(componentTyped, allocations[0])
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            }
            foreach (var choice in componentChoices)
            {
                var reducedMana = 0;
                var reducedPower = 0;
                if (choice.Mode == "MANA") { reducedMana = Math.Min(1, componentMana); componentMana -= reducedMana; }
                else if (choice.Trait.Length == 0) { reducedPower = Math.Min(1, componentGeneric); componentGeneric -= reducedPower; }
                else
                {
                    reducedPower = Math.Min(1, componentTyped.GetValueOrDefault(choice.Trait));
                    componentTyped[choice.Trait] = componentTyped.GetValueOrDefault(choice.Trait) - reducedPower;
                }
                manaReduction += reducedMana;
                powerReduction += reducedPower;
                receipts.Add(new Dictionary<string, object?> {
                    ["choiceId"] = choice.Id, ["sourceObjectId"] = choice.Source.ObjectId,
                    ["sourceGeneration"] = choice.Source.Generation, ["sourceInstanceId"] = choice.Source.InstanceId,
                    ["ruleTextOrigin"] = choice.Source.Origin, ["componentId"] = component.Id,
                    ["mode"] = choice.Mode, ["trait"] = choice.Trait,
                    ["manaReduction"] = reducedMana, ["powerReduction"] = reducedPower,
                    ["printedAllocation"] = choice.PrintedAllocation });
            }
            extraMana += componentMana;
            extraGeneric += componentGeneric;
            extraTyped = PrintedPowerCostRules.Combine(extraTyped, componentTyped.Where(p => p.Value > 0)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        }
        result = new(extraMana, extraGeneric, extraTyped, printedCosts, echoReduction,
            manaReduction, powerReduction, choiceIds.ToArray(), receipts);
        return true;
    }
}
