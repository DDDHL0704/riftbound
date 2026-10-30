using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentAbilityManaReductionReceipt(IReadOnlyList<ObjectBinding> Sources, int Amount);

public sealed partial class CoreRuleEngine
{
    private static string EquipmentActivationMarker(MatchState state, string player)
        => $"EQUIPMENT_ABILITY_PLAYED:{state.TurnNumber}:{player}";

    private static CardObjectState[] EquipmentAbilityReducers(MatchState state, string player)
        => state.UntilEndOfTurnEffects.Contains(EquipmentActivationMarker(state, player)) ? []
            : state.CardObjects.Values.Where(c => c.ControllerId == player && !c.IsFaceDown
                && IsObjectOnField(state.PlayerZones, c.ObjectId) && BattlefieldLocalRules.HasAbilityKind(c,
                    StaticAbilityKinds.BattlefieldFirstEquipmentAbilityManaReduction)).OrderBy(c => c.ObjectId, StringComparer.Ordinal).ToArray();

    internal static int EquipmentAbilityManaReduction(MatchState state, string player, string? sourceId = null)
        => sourceId is not null && (!state.CardObjects.TryGetValue(sourceId, out var source)
            || !source.Tags.Contains(CardObjectTags.EquipmentCard)) ? 0 : EquipmentAbilityReducers(state, player).Length;

    internal static P4ActivatedAbilityDefinition BattlefieldAdjustedAbility(MatchState state, string player,
        string source, P4ActivatedAbilityDefinition ability)
        => ability with { ManaCost = Math.Max(0, ability.ManaCost - EquipmentAbilityManaReduction(state, player, source)) };

    internal static EquipmentAbilityManaReductionReceipt? CaptureEquipmentEmpowerReduction(MatchState state,
        string player, string source, int mana)
    {
        var amount = Math.Min(mana, EquipmentAbilityManaReduction(state, player, source));
        return amount <= 0 ? null : new(EquipmentAbilityReducers(state, player)
            .Select(c => new ObjectBinding(c.ObjectId, c.ObjectGeneration)).ToArray(), amount);
    }

    internal static bool ValidEquipmentEmpowerReduction(MatchState state, EmpowerActivationContext context, int mana)
        => context.EquipmentManaReduction is not { } receipt || receipt.Amount > 0 && receipt.Amount <= mana
            && receipt.Sources.Count > 0 && receipt.Sources.Distinct().Count() == receipt.Sources.Count
            && receipt.Amount == Math.Min(mana, receipt.Sources.Count)
            && state.CardObjects.TryGetValue(context.Source.ObjectId, out var unit) && unit.Tags.Contains(CardObjectTags.EquipmentCard)
            && receipt.Sources.All(b => b.Generation >= 0 && state.CardObjects.TryGetValue(b.ObjectId, out var source)
                && source.ObjectGeneration >= b.Generation && BattlefieldLocalRules.HasAbilityKind(source,
                    StaticAbilityKinds.BattlefieldFirstEquipmentAbilityManaReduction));

    private static ResolutionResult RecordEquipmentAbilityPlays(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var markers = result.State.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        foreach (var activated in result.Events.Where(e => e.Kind == "ABILITY_ACTIVATED")) {
            if (activated.Payload.GetValueOrDefault("sourceObjectId") is not string id) continue;
            var source = before.CardObjects.GetValueOrDefault(id) ?? result.State.CardObjects.GetValueOrDefault(id);
            if (source is null || !source.Tags.Contains(CardObjectTags.EquipmentCard)) continue;
            var player = activated.Payload.GetValueOrDefault("playerId") as string ?? source.ControllerId;
            if (player is not null && result.State.Seats.ContainsKey(player)) markers.Add(EquipmentActivationMarker(result.State, player));
        }
        if (markers.Count == result.State.UntilEndOfTurnEffects.Count) return result;
        var next = result.State with { UntilEndOfTurnEffects = markers.Order(StringComparer.Ordinal).ToArray() };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }
}
