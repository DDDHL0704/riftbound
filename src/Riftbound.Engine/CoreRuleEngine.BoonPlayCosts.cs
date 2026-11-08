using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record BoonPlayCostPayment(ObjectBinding Source, int Count);

public sealed partial class CoreRuleEngine
{
    internal const string BoonPlayCostPrefix = "BOON_PLAY_COST:";
    private sealed record BoonPlayCostSpec(bool IgnoreBase, int? Maximum, string? PowerTrait = null, int PowerPerBoon = 0);
    private sealed record BoonPlayCostSource(string Key, ObjectBinding Source, int Count);

    private static BoonPlayCostSpec? OptionalBoonPlaySpec(CardBehaviorDefinition behavior)
    {
        if (IsSpellPlayBehavior(behavior) && CardStaticAbilitySpecRules.TryGetStaticAbility(behavior.CardNo,
            spec => spec.Kind == StaticAbilityKinds.SourceConsumeBoonIgnoreBaseCosts && spec.Amount == 1, out _))
            return new(true, 1);
        if (CardStaticAbilitySpecRules.TryGetStaticAbility(behavior.CardNo,
                spec => spec.Kind == StaticAbilityKinds.SourceMayConsumeAnyBoonsAsAdditionalCost, out _)
            && CardStaticAbilitySpecRules.TryGetStaticAbility(behavior.CardNo,
                spec => spec.Kind == StaticAbilityKinds.SourcePowerReductionPerConsumedBoon && spec.Amount > 0
                    && spec.PowerCostTrait is RuneTrait.Red or RuneTrait.Green or RuneTrait.Blue or RuneTrait.Orange or RuneTrait.Purple or RuneTrait.Yellow,
                out var reduction))
            return new(false, null, reduction.PowerCostTrait, reduction.Amount);
        return null;
    }

    internal static bool HasOptionalBoonPlayCost(CardBehaviorDefinition behavior) => OptionalBoonPlaySpec(behavior) is not null;
    internal static bool HasOptionalUnitPaymentSelection(CardBehaviorDefinition behavior)
        => behavior.MaySacrificeUnitForPrintedCostReduction || HasOptionalBoonPlayCost(behavior);
    private static bool IsBoonPlayCost(string cost) => cost.StartsWith(BoonPlayCostPrefix, StringComparison.Ordinal);

    private static IReadOnlyList<BoonPlayCostSource> BoonPlayCostSources(MatchState state, string player)
        => state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
                && IsPlayerControlledFieldUnitObject(state, player, card.ObjectId) && BoonRules.Count(card) > 0)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal).Select(card => {
                var source = new ObjectBinding(card.ObjectId, card.ObjectGeneration);
                // Opaque keys retain no link to a payer's later private incarnation.
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { player, source }))));
                return new BoonPlayCostSource(key, source, BoonRules.Count(card));
            }).ToArray();

    private static string BoonPlayChoice(BoonPlayCostSource source, int count)
        => BoonPlayCostPrefix + source.Key + ":" + count.ToString(CultureInfo.InvariantCulture);

    internal static IReadOnlyList<ActionPromptChoiceDto> BoonPlayCostChoices(MatchState state, string player, CardBehaviorDefinition behavior)
        => OptionalBoonPlaySpec(behavior) is not { } spec || EffectPlayIgnoresAllCosts(state, player) ? []
            : BoonPlayCostSources(state, player).Select(source => new ActionPromptChoiceDto(BoonPlayChoice(source, 1),
                "消耗增益：" + OfficialCardSourceIdentityGroups.DisplayName(state.CardObjects[source.Source.ObjectId].CardNo ?? source.Source.ObjectId),
                spec.IgnoreBase ? "消耗一枚受你控制单位的增益，作为额外费用，无视本法术的基础费用。"
                    : "每消耗一枚受你控制单位的增益，总费用减少一枚对应符能；可选择任意数量。",
                [source.Source.ObjectId])).ToArray();

    internal static IReadOnlyList<object> BoonPlayCostGroups(MatchState state, string player, CardBehaviorDefinition behavior)
        => OptionalBoonPlaySpec(behavior) is not { } spec || EffectPlayIgnoresAllCosts(state, player) ? []
            : BoonPlayCostSources(state, player).Select(source => (object)new {
                choiceId = BoonPlayChoice(source, 1), choicePrefix = BoonPlayCostPrefix + source.Key + ":",
                sourceObjectId = source.Source.ObjectId, maximumCount = Math.Min(source.Count, spec.Maximum ?? source.Count),
                maximumTotalCount = spec.Maximum, consumesBoon = true }).ToArray();

    private static IReadOnlyList<IReadOnlyList<string>> OptionalBoonPlaySelections(MatchState state, string player, CardBehaviorDefinition behavior)
    {
        var selections = new List<IReadOnlyList<string>> { Array.Empty<string>() };
        if (OptionalBoonPlaySpec(behavior) is not { } spec || EffectPlayIgnoresAllCosts(state, player)) return selections;
        var sources = BoonPlayCostSources(state, player);
        if (spec.IgnoreBase) selections.AddRange(sources.Select(source => (IReadOnlyList<string>)new[] { BoonPlayChoice(source, 1) }));
        else if (sources.Count > 0) selections.Add(sources.Select(source => BoonPlayChoice(source, source.Count)).ToArray());
        return selections;
    }

    private static bool TrySelectBoonPlayCosts(MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> costs, out IReadOnlyList<BoonPlayCostPayment> payments)
    {
        payments = [];
        var choices = costs.Where(IsBoonPlayCost).ToArray();
        if (choices.Length == 0) return true;
        if (OptionalBoonPlaySpec(behavior) is not { } spec || EffectPlayIgnoresAllCosts(state, player)
            || choices.Distinct(StringComparer.Ordinal).Count() != choices.Length) return false;
        var available = BoonPlayCostSources(state, player).ToDictionary(source => source.Key, StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal); var selected = new List<BoonPlayCostPayment>();
        foreach (var choice in choices) {
            var parts = choice[BoonPlayCostPrefix.Length..].Split(':');
            if (parts.Length != 2 || !available.TryGetValue(parts[0], out var source) || !used.Add(parts[0])
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0
                || count > source.Count || choice != BoonPlayChoice(source, count)) return false;
            selected.Add(new(source.Source, count));
        }
        if (spec.Maximum is { } maximum && selected.Sum(payment => (long)payment.Count) > maximum) return false;
        payments = selected; return true;
    }

    private static bool TryApplyBoonPlayCosts(MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> costs, out CardBehaviorDefinition priced)
    {
        priced = behavior;
        if (!TrySelectBoonPlayCosts(state, player, behavior, costs, out var payments)) return false;
        if (payments.Count == 0) return true;
        var spec = OptionalBoonPlaySpec(behavior)!;
        IReadOnlyDictionary<string, int>? reduction = spec.PowerTrait is { } trait ? new Dictionary<string, int> {
            [trait] = (int)Math.Min(int.MaxValue, payments.Sum(payment => (long)payment.Count) * spec.PowerPerBoon) } : null;
        priced = behavior with { IgnoreBasePlayCostFromBoons = spec.IgnoreBase,
            BoonPlayCostPayments = payments, BoonPlayPowerReductions = reduction };
        return true;
    }

    private static IReadOnlyDictionary<string, int> ApplyBoonPlayPowerReduction(CardBehaviorDefinition behavior,
        IReadOnlyDictionary<string, int> typed)
        => behavior.BoonPlayPowerReductions is null ? typed : typed
            .Select(pair => (pair.Key, Amount: Math.Max(0, pair.Value - behavior.BoonPlayPowerReductions.GetValueOrDefault(pair.Key))))
            .Where(pair => pair.Amount > 0).ToDictionary(pair => pair.Key, pair => pair.Amount, StringComparer.Ordinal);

    private static bool TryCommitBoonPlayCosts(MatchState state, string player, string playedSource, CardBehaviorDefinition behavior,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var payments = behavior.BoonPlayCostPayments ?? [];
        if (payments.Count == 0) return true;
        var current = state with { PlayerZones = zones, CardObjects = cards };
        if (payments.Any(payment => !cards.TryGetValue(payment.Source.ObjectId, out var card)
            || card.ObjectGeneration != payment.Source.Generation || !IsFaceUpNonStandbyUnit(card)
            || !IsPlayerControlledFieldUnitObject(current, player, card.ObjectId) || BoonRules.Count(card) < payment.Count)) return false;
        using var instruction = new MightInstructionScope(state, zones, cards, events);
        foreach (var payment in payments)
            for (var i = 0; i < payment.Count; i++) {
                var before = cards[payment.Source.ObjectId]; var after = BoonRules.Consume(before);
                cards[before.ObjectId] = after;
                events.Add(CaptureBoonConsumption(new("BOON_CONSUMED", "支付打出卡牌的增益额外费用", new Dictionary<string, object?> {
                    ["playerId"] = player, ["sourceObjectId"] = playedSource, ["targetObjectId"] = before.ObjectId,
                    ["count"] = 1, ["remainingBoonCount"] = BoonRules.Count(after), ["previousPower"] = before.Power,
                    ["power"] = after.Power, ["paymentWindow"] = "PLAY_CARD" }), zones, cards));
            }
        return true;
    }
}
