using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OptionalPlayCostReductionUse(ObjectBinding Source, string CardNo, string InstanceId,
    long FaceRevision, long CapturedTick, int TurnNumber, string ControllerId,
    RuleTextInstanceBinding? Origin = null, string? TokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string OptionalPlayCostReductionUsePrefix = "OPTIONAL_PLAY_COST_REDUCTION_USED|";
    internal const string OptionalPlayCostReductionChoicePrefix = "USE_PLAY_COST_REDUCTION:";
    private const string PlayCostReductionUnitPrefix = "PLAY_COST_REDUCTION_UNIT:";
    private static bool IsOptionalPlayCostReductionChoice(string value)
        => value.StartsWith(OptionalPlayCostReductionChoicePrefix, StringComparison.Ordinal)
            || value.StartsWith(PlayCostReductionUnitPrefix, StringComparison.Ordinal);

    private sealed record OptionalPlayCostReducer(string ChoiceId, string Name,
        OptionalPlayCostReductionSpec Spec, OptionalPlayCostReductionUse Use, IReadOnlyList<string> SupportingUnits);
    private static readonly Lazy<IReadOnlyDictionary<string, OptionalPlayCostReductionSpec>> OptionalPlayCostReductionSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Select(card => (card.CardNo, Spec: OptionalPlayCostReductionParser.Parse(card.CardEffect)))
            .Where(row => row.Spec is not null).ToDictionary(row => row.CardNo, row => row.Spec!, StringComparer.Ordinal));

    private static string PlayCostReductionMarker(OptionalPlayCostReductionUse use)
        => OptionalPlayCostReductionUsePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(use)));

    private static OptionalPlayCostReductionUse? ReadPlayCostReductionUse(string marker)
    {
        if (!marker.StartsWith(OptionalPlayCostReductionUsePrefix, StringComparison.Ordinal)) return null;
        try
        {
            var use = JsonSerializer.Deserialize<OptionalPlayCostReductionUse>(Encoding.UTF8.GetString(
                Convert.FromBase64String(marker[OptionalPlayCostReductionUsePrefix.Length..])));
            return use is { Source.ObjectId.Length: > 0, CardNo.Length: > 0, InstanceId.Length: > 0, ControllerId.Length: > 0 }
                && marker == PlayCostReductionMarker(use) ? use : null;
        }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException) { return null; }
    }

    private static bool SamePlayCostReductionInstance(OptionalPlayCostReductionUse left, OptionalPlayCostReductionUse right)
        => left.Source == right.Source && left.CardNo == right.CardNo && left.InstanceId == right.InstanceId
            && left.FaceRevision == right.FaceRevision;

    private static string[] PlayCostReductionUnits(MatchState state, string player)
        => state.PlayerZones.TryGetValue(player, out var zones)
            ? zones.Base.Concat(zones.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Where(id => IsPlayerControlledFieldUnitObject(state, player, id)
                    && CanExhaustFriendlyUnitAsOptionalCost(state, player, id)).ToArray() : [];

    private static string PlayCostReductionUnitChoice(OptionalPlayCostReducer source, string unit)
        => PlayCostReductionUnitPrefix + source.ChoiceId[OptionalPlayCostReductionChoicePrefix.Length..]
            + ":" + Uri.EscapeDataString(unit);

    private static IReadOnlyList<OptionalPlayCostReducer> OptionalPlayCostReducers(MatchState state, string player,
        CardBehaviorDefinition behavior)
    {
        if (EffectPlayIgnoresAllCosts(state, player) || !state.PlayerZones.TryGetValue(player, out var zones)) return [];
        var category = behavior.PlaysSourceToBaseAsEquipment ? "EQUIPMENT" : behavior.PlaysSourceToBaseAsUnit ? "UNIT" : "SPELL";
        var used = state.UntilEndOfTurnEffects.Select(ReadPlayCostReductionUse).OfType<OptionalPlayCostReductionUse>().ToArray();
        var result = new List<OptionalPlayCostReducer>();
        foreach (var id in zones.Base.Concat(zones.Battlefields).Concat(zones.LegendZone).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source)) continue;
            if (source.IsFaceDown
                || source.ControllerId != player || !state.ObjectLocations.TryGetValue(source.ObjectId, out var at)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (!OptionalPlayCostReductionSpecs.Value.TryGetValue(instance.CardNo, out var spec)
                    || spec.Category != category || at.PlayerId != player
                    || (spec.SourceZone == "PUBLIC"
                        ? !IsPlayerControlledFieldUnitObject(state, player, id) && !(at.Zone == "LEGEND" && zones.LegendZone.Contains(id))
                        : at.Zone != spec.SourceZone || !IsPlayerControlledFieldUnitObject(state, player, id))
                    || spec.ExhaustSource && source.IsExhausted) continue;
                var support = spec.SupportingUnitCount > 0
                    ? PlayCostReductionUnits(state, player).Where(unit => unit != id).ToArray() : [];
                if (support.Length < spec.SupportingUnitCount) continue;
                var use = new OptionalPlayCostReductionUse(new(source.ObjectId, source.ObjectGeneration), instance.CardNo, instance.Id,
                    instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, state.Tick + 1,
                    state.TurnNumber, player, instance.Origin, IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null);
                if (spec.OncePerTurn && used.Any(old => SamePlayCostReductionInstance(old, use))) continue;
                // Opaque choice keys carry no private identity if the source subsequently leaves play.
                var identity = JsonSerializer.Serialize(use with { CapturedTick = 0 });
                var choice = OptionalPlayCostReductionChoicePrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                var name = CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var printed) ? printed.DisplayName
                    : OfficialCardSourceIdentityGroups.DisplayName(instance.CardNo);
                result.Add(new(choice, name, spec, use, support));
            }
        }
        return result;
    }

    internal static IReadOnlyList<ActionPromptChoiceDto> OptionalPlayCostReductionChoices(MatchState state, string player,
        CardBehaviorDefinition behavior)
        => OptionalPlayCostReducers(state, player, behavior).SelectMany(source => new[] { new ActionPromptChoiceDto(source.ChoiceId,
            $"{source.Name}：费用减少 {source.Spec.Mana} 法力和 {source.Spec.Power} 符能",
            source.Spec.ExhaustSource ? $"同时让来源及另外 {source.Spec.SupportingUnitCount} 名友方单位休眠，作为额外费用。"
                : "每个技能文本实例每回合可选择使用一次；同时减免两项费用，最低为零。", [source.Use.Source.ObjectId]) }
            .Concat(source.SupportingUnits.Select(unit => new ActionPromptChoiceDto(PlayCostReductionUnitChoice(source, unit),
                $"为{source.Name}让友方单位休眠：{OfficialCardSourceIdentityGroups.DisplayName(state.CardObjects[unit].CardNo ?? unit)}",
                $"仅在选择该来源减费时提交；须选择 {source.Spec.SupportingUnitCount} 名不同的友方单位。", [unit])))).ToArray();

    internal static IReadOnlyList<object> OptionalPlayCostReductionGroups(MatchState state, string player, CardBehaviorDefinition behavior)
        => OptionalPlayCostReducers(state, player, behavior).Where(source => source.Spec.ExhaustSource)
            .Select(source => (object)new { choiceId = source.ChoiceId, sourceObjectId = source.Use.Source.ObjectId,
                requiredUnitCount = source.Spec.SupportingUnitCount,
                unitChoiceIds = source.SupportingUnits.Select(unit => PlayCostReductionUnitChoice(source, unit)).ToArray() }).ToArray();

    private static bool TrySelectOptionalPlayCostReducers(MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> costs, out IReadOnlyList<OptionalPlayCostReducer> selected, out IReadOnlyList<string> exhausted,
        out IReadOnlyList<IReadOnlyList<string>> exhaustionGroups)
    {
        exhausted = []; exhaustionGroups = [];
        var choices = costs.Where(choice => choice.StartsWith(OptionalPlayCostReductionChoicePrefix, StringComparison.Ordinal)).ToArray();
        var available = OptionalPlayCostReducers(state, player, behavior).ToDictionary(source => source.ChoiceId, StringComparer.Ordinal);
        selected = choices.Where(available.ContainsKey).Select(id => available[id]).ToArray();
        if (costs.Distinct(StringComparer.Ordinal).Count() != costs.Count || selected.Count != choices.Length) return false;
        var support = selected.SelectMany(source => source.SupportingUnits.Select(unit =>
            (Id: PlayCostReductionUnitChoice(source, unit), Source: source.ChoiceId, Unit: unit))).ToDictionary(row => row.Id, StringComparer.Ordinal);
        var selectedSupport = costs.Where(cost => cost.StartsWith(PlayCostReductionUnitPrefix, StringComparison.Ordinal)).ToArray();
        if (selectedSupport.Any(id => !support.ContainsKey(id))) return false;
        var paid = new List<string>(); var groups = new List<IReadOnlyList<string>>();
        foreach (var source in selected)
        {
            var units = selectedSupport.Where(id => support[id].Source == source.ChoiceId).Select(id => support[id].Unit).ToArray();
            if (units.Length != source.Spec.SupportingUnitCount) return false;
            var group = (source.Spec.ExhaustSource ? new[] { source.Use.Source.ObjectId } : [])
                .Concat(units).ToArray();
            paid.AddRange(group);
            if (group.Length > 0) groups.Add(group);
        }
        if (paid.Distinct(StringComparer.Ordinal).Count() != paid.Count) return false;
        exhausted = paid; exhaustionGroups = groups; return true;
    }

    private static int MaximumOptionalPlayReduction(MatchState state, string player, CardBehaviorDefinition behavior, bool mana)
    {
        var sources = OptionalPlayCostReducers(state, player, behavior);
        if (sources.Count == 0) return 0;
        if (sources.All(source => !source.Spec.ExhaustSource && source.Spec.SupportingUnitCount == 0))
            return sources.Sum(source => mana ? source.Spec.Mana : source.Spec.Power);
        var budget = PlayCostReductionUnits(state, player).Length;
        var best = new int[budget + 1];
        // Text copies on one object cannot pay that object's exhaustion twice.
        foreach (var group in sources.GroupBy(source => source.Spec.ExhaustSource
                     ? source.Use.Source.ObjectId : source.ChoiceId, StringComparer.Ordinal))
        {
            var previous = best.ToArray();
            foreach (var source in group)
            {
                var cost = source.Spec.SupportingUnitCount + (source.Spec.ExhaustSource
                    && IsPlayerControlledFieldUnitObject(state, player, source.Use.Source.ObjectId) ? 1 : 0);
                var amount = mana ? source.Spec.Mana : source.Spec.Power;
                for (var remaining = budget; remaining >= cost; remaining--)
                    best[remaining] = Math.Max(best[remaining], previous[remaining - cost] + amount);
            }
        }
        return best.Max();
    }

    internal static int AvailableOptionalPlayManaReduction(MatchState state, string player, CardBehaviorDefinition behavior)
        => MaximumOptionalPlayReduction(state, player, behavior, mana: true);

    internal static int AvailableOptionalPlayPowerReduction(MatchState state, string player, CardBehaviorDefinition behavior)
        => MaximumOptionalPlayReduction(state, player, behavior, mana: false);

    internal static bool ValidOptionalPlayCostReductionUses(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(effect => effect is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(OptionalPlayCostReductionUsePrefix, StringComparison.Ordinal)).ToArray();
        var uses = markers.Select(ReadPlayCostReductionUse).ToArray();
        return uses.All(use => use is { Source.Generation: >= 0, FaceRevision: >= 0, CapturedTick: > 0 }
            && use.CapturedTick <= state.Tick && use.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(use.ControllerId)
            && OptionalPlayCostReductionSpecs.Value.TryGetValue(use.CardNo, out var spec) && spec.OncePerTurn
            && (use.Origin is null ? use.InstanceId == $"PRINTED:{use.Source.ObjectId}:{use.Source.Generation}"
                && PredictBindingMatches(state, use.Source, use.CardNo, use.FaceRevision, use.CapturedTick, use.TokenFactoryCardNo)
                : use.Origin.Equipment is { } gear && use.InstanceId == $"ATTACHED:{gear.ObjectId}:{gear.Generation}:{use.Origin.Revision}"
                    && AttachedRuleTextRules.ValidCaptured(state, use.Origin, use.Source.ObjectId, use.Source.Generation, use.CardNo)))
            && uses.Select(use => (use!.Source, use.CardNo, use.InstanceId, use.FaceRevision)).Distinct().Count() == uses.Length;
    }
}
