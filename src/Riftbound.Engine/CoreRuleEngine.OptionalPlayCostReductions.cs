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
    private static bool IsOptionalPlayCostReductionChoice(string value)
        => value.StartsWith(OptionalPlayCostReductionChoicePrefix, StringComparison.Ordinal);

    private sealed record OptionalPlayCostReducer(string ChoiceId, string Name,
        OptionalPlayCostReductionSpec Spec, OptionalPlayCostReductionUse Use);
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

    private static IReadOnlyList<OptionalPlayCostReducer> OptionalPlayCostReducers(MatchState state, string player,
        CardBehaviorDefinition behavior)
    {
        if (EffectPlayIgnoresAllCosts(state, player) || !state.PlayerZones.TryGetValue(player, out var zones)) return [];
        var category = behavior.PlaysSourceToBaseAsEquipment ? "EQUIPMENT" : behavior.PlaysSourceToBaseAsUnit ? "UNIT" : "SPELL";
        var used = state.UntilEndOfTurnEffects.Select(ReadPlayCostReductionUse).OfType<OptionalPlayCostReductionUse>().ToArray();
        var result = new List<OptionalPlayCostReducer>();
        foreach (var id in zones.Base.Concat(zones.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source)) continue;
            if (!IsPlayerControlledFieldUnitObject(state, player, source.ObjectId) || source.IsFaceDown
                || source.ControllerId != player || !state.ObjectLocations.TryGetValue(source.ObjectId, out var at)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (!OptionalPlayCostReductionSpecs.Value.TryGetValue(instance.CardNo, out var spec)
                    || spec.Category != category || at.Zone != spec.SourceZone) continue;
                var use = new OptionalPlayCostReductionUse(new(source.ObjectId, source.ObjectGeneration), instance.CardNo, instance.Id,
                    instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, state.Tick + 1,
                    state.TurnNumber, player, instance.Origin, IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null);
                if (used.Any(old => SamePlayCostReductionInstance(old, use))) continue;
                // Opaque choice keys carry no private identity if the source subsequently leaves play.
                var identity = JsonSerializer.Serialize(use with { CapturedTick = 0 });
                var choice = OptionalPlayCostReductionChoicePrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                result.Add(new(choice, CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var printed) ? printed.DisplayName : "技能",
                    spec, use));
            }
        }
        return result;
    }

    internal static IReadOnlyList<ActionPromptChoiceDto> OptionalPlayCostReductionChoices(MatchState state, string player,
        CardBehaviorDefinition behavior)
        => OptionalPlayCostReducers(state, player, behavior).Select(source => new ActionPromptChoiceDto(source.ChoiceId,
            $"{source.Name}：费用减少 {source.Spec.Mana} 法力和 {source.Spec.Power} 符能",
            "每个技能文本实例每回合可选择使用一次；同时减免两项费用，最低为零。", [source.Use.Source.ObjectId])).ToArray();

    private static bool TrySelectOptionalPlayCostReducers(MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> costs, out IReadOnlyList<OptionalPlayCostReducer> selected)
    {
        var choices = costs.Where(choice => choice.StartsWith(OptionalPlayCostReductionChoicePrefix, StringComparison.Ordinal)).ToArray();
        var available = OptionalPlayCostReducers(state, player, behavior).ToDictionary(source => source.ChoiceId, StringComparer.Ordinal);
        selected = choices.Where(available.ContainsKey).Select(id => available[id]).ToArray();
        return choices.Distinct(StringComparer.Ordinal).Count() == choices.Length && selected.Count == choices.Length;
    }

    internal static int AvailableOptionalPlayManaReduction(MatchState state, string player, CardBehaviorDefinition behavior)
        => OptionalPlayCostReducers(state, player, behavior).Sum(source => source.Spec.Mana);

    internal static int AvailableOptionalPlayPowerReduction(MatchState state, string player, CardBehaviorDefinition behavior)
        => OptionalPlayCostReducers(state, player, behavior).Sum(source => source.Spec.Power);

    internal static bool ValidOptionalPlayCostReductionUses(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(effect => effect is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(OptionalPlayCostReductionUsePrefix, StringComparison.Ordinal)).ToArray();
        var uses = markers.Select(ReadPlayCostReductionUse).ToArray();
        return uses.All(use => use is { Source.Generation: >= 0, FaceRevision: >= 0, CapturedTick: > 0 }
            && use.CapturedTick <= state.Tick && use.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(use.ControllerId)
            && OptionalPlayCostReductionSpecs.Value.ContainsKey(use.CardNo)
            && (use.Origin is null ? use.InstanceId == $"PRINTED:{use.Source.ObjectId}:{use.Source.Generation}"
                && PredictBindingMatches(state, use.Source, use.CardNo, use.FaceRevision, use.CapturedTick, use.TokenFactoryCardNo)
                : use.Origin.Equipment is { } gear && use.InstanceId == $"ATTACHED:{gear.ObjectId}:{gear.Generation}:{use.Origin.Revision}"
                    && AttachedRuleTextRules.ValidCaptured(state, use.Origin, use.Source.ObjectId, use.Source.Generation, use.CardNo)))
            && uses.Select(use => (use!.Source, use.CardNo, use.InstanceId, use.FaceRevision)).Distinct().Count() == uses.Length;
    }
}
