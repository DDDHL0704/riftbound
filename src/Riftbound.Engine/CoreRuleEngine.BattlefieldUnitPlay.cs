using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnBattlefieldConquests(int TurnNumber, IReadOnlyList<BattlefieldConquestRecord> Entries);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestRecord(string PlayerId, ObjectBinding Battlefield);

public sealed partial class CoreRuleEngine
{
    internal const string BattlefieldUnitPlayCostPrefix = "BATTLEFIELD_UNIT_PLAY:";

    internal static bool IsBattlefieldUnitPlayCost(string cost)
        => cost.StartsWith(BattlefieldUnitPlayCostPrefix, StringComparison.Ordinal);

    private static IEnumerable<(CardObjectState Field, StaticAbilitySpec Ability)> BattlefieldUnitPlayGrants(
        MatchState state, string player, CardBehaviorDefinition behavior)
    {
        if (!behavior.PlaysSourceToBaseAsUnit) yield break;
        foreach (var field in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
            if (BattlefieldLocalRules.Battlefield(state, field.ObjectId) is not null
                && !BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + field.ObjectId)
                && UnitPlayLocationRestrictionAllows(state, player, behavior, "BATTLEFIELD:" + field.ObjectId)
                && BattlefieldStaticAbilitySpecRules.TryGetAbility(field.CardNo,
                    a => a.Kind == StaticAbilityKinds.BattlefieldFilteredUnitPlayAdditionalPower
                        && a.Amount > 0 && a.TargetFilter is not null
                        && a.TargetFilter.StartsWith(StaticAuraTargetFilters.TagPrefix, StringComparison.Ordinal)
                        && ParseDelimitedValues(behavior.SourceUnitTags).Contains(a.TargetFilter[StaticAuraTargetFilters.TagPrefix.Length..]),
                    out var ability))
                yield return (field, ability);
    }

    internal static IReadOnlyList<ActionPromptChoiceDto> BattlefieldUnitPlayCostChoices(
        MatchState state, string player, CardBehaviorDefinition behavior)
        => BattlefieldUnitPlayGrants(state, player, behavior).Select(grant => new ActionPromptChoiceDto(
            BattlefieldUnitPlayCostPrefix + grant.Field.ObjectId, $"支付 {grant.Ability.Amount} 任意符能：打出到 {grant.Field.ObjectId}",
            "可选额外费用；不能绕过出牌位置限制")).ToArray();

    private static bool TryBattlefieldUnitPlayCost(MatchState state, string player, CardBehaviorDefinition behavior,
        IReadOnlyList<string> costs, out int power)
    {
        power = 0;
        var grants = BattlefieldUnitPlayGrants(state, player, behavior)
            .ToDictionary(g => BattlefieldUnitPlayCostPrefix + g.Field.ObjectId, g => g.Ability.Amount, StringComparer.Ordinal);
        foreach (var cost in costs)
        {
            if (!grants.TryGetValue(cost, out var amount) || power > int.MaxValue - amount) return false;
            power += amount;
        }
        return costs.Count == costs.Distinct(StringComparer.Ordinal).Count();
    }

    private static bool PaidBattlefieldUnitPlayDestination(IReadOnlyList<string>? costs, string destination)
        => destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && (costs?.Contains(BattlefieldUnitPlayCostPrefix + destination[12..], StringComparer.Ordinal) ?? false);

    private static bool ValidBattlefieldUnitPlayDeclaration(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination, IReadOnlyList<string>? costs)
    {
        var selected = (costs ?? []).Where(IsBattlefieldUnitPlayCost).ToArray();
        if (!TryBattlefieldUnitPlayCost(state, player, behavior, selected, out _)) return false;
        if (selected.Length == 0 || PaidBattlefieldUnitPlayDestination(selected, destination)) return true;
        if (state.PendingEffectPlay is not { } pending || !(pending.RevealedHand is not null
                || pending.Parent.PlayedFromStandby is not null && player == pending.Parent.ControllerId
                || pending.DestinationPolicy is "ORIGINAL_LOCATION" or "BASE" or "CONTROLLED_BATTLEFIELD" or "ANY_BATTLEFIELD")) return false;
        // CN355.17.a: combine compatible positive instructions. Only genuinely
        // conflicting sets permit choosing the original instruction instead.
        var instructed = OrdinaryEffectPlayDestinations(state, player);
        return instructed.Contains(destination) && !selected.Any(cost =>
            instructed.Contains("BATTLEFIELD:" + cost[BattlefieldUnitPlayCostPrefix.Length..]));
    }

    private static bool EffectPlayDeclaredDestinationAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination, IReadOnlyList<string>? costs)
        => EffectPlayDestinations(state, player, behavior).Contains(destination)
            || PaidBattlefieldUnitPlayDestination(costs, destination)
                && ValidBattlefieldUnitPlayDeclaration(state, player, behavior, destination, costs);

    internal static bool UnitPlayLocationRestrictionAllows(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination)
        => (!IsDeployedEquipment(behavior) || DeployedEquipmentDestinationAllowed(state, behavior, destination))
            && (!behavior.PlaysSourceToBaseAsUnit || !CardStaticAbilitySpecRules.TryGetStaticAbility(behavior.CardNo,
                ability => ability.Kind == StaticAbilityKinds.UnitPlayOnlyToConqueredBattlefieldThisTurn, out _)
            || destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
                && state.BattlefieldConquests is { } history && history.TurnNumber == state.TurnNumber
                && BattlefieldLocalRules.Battlefield(state, destination[12..]) is { } field
                && history.Entries.Contains(new(player, new(field.ObjectId, field.ObjectGeneration))));

    private static ResolutionResult RecordBattlefieldConquests(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        var entries = state.BattlefieldConquests?.TurnNumber == state.TurnNumber
            ? state.BattlefieldConquests.Entries.ToHashSet() : [];
        var events = result.Events.ToArray();
        for (var index = 0; index < events.Length; index++)
        {
            var e = events[index];
            // CN348.2.a.1 / 471.1: establishing control of an already-scored
            // battlefield is not a conquest. The scoring flow emits this event
            // even when the point itself is prevented or replaced.
            if (e.Kind != "BATTLEFIELD_CONQUERED") continue;
            if (TryGetPayloadInt(e, "conquestTurnNumber", out var capturedTurn) && capturedTurn != state.TurnNumber) continue;
            var player = EventPayloadString(e, "playerId");
            var field = EventPayloadString(e, "battlefieldObjectId") ?? EventPayloadString(e, "battlefieldId");
            if (player is not null && state.Seats.ContainsKey(player) && BattlefieldLocalRules.Battlefield(state, field) is { } card)
            {
                entries.Add(new(player, new(card.ObjectId, card.ObjectGeneration)));
                events[index] = e with { Payload = new Dictionary<string, object?>(e.Payload) {
                    ["conquestTurnNumber"] = state.TurnNumber, ["conqueredBattlefieldGeneration"] = card.ObjectGeneration } };
            }
        }
        if (state.BattlefieldConquests is null && entries.Count == 0
            || state.BattlefieldConquests is { } previous && previous.TurnNumber == state.TurnNumber
                && entries.SetEquals(previous.Entries))
            return result with { Events = events };
        var updated = state with { BattlefieldConquests = entries.Count == 0 ? null : new(state.TurnNumber,
            entries.OrderBy(e => e.PlayerId, StringComparer.Ordinal).ThenBy(e => e.Battlefield.ObjectId, StringComparer.Ordinal)
                .ThenBy(e => e.Battlefield.Generation).ToArray()) };
        return result with { State = updated, Events = events, Snapshots = ResolutionResult.BuildSnapshots(updated), Prompts = BuildCorePrompts(updated) };
    }
}
