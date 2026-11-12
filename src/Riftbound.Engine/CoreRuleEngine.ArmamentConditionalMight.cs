using Riftbound.Contracts;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal sealed record ArmamentConditionalMightContribution(string SourceId, string SourceCardNo,
    int Amount, string Condition, IReadOnlyList<string> Participants);

public sealed partial class CoreRuleEngine
{
    internal const string ArmamentAttachedThisTurn = "ARMAMENT_ATTACHED_THIS_TURN";
    internal const string ArmamentConditionalMightEffect = "ATTACHED_ARMAMENT_CONDITIONAL_MIGHT";

    private const string PrivateArmamentAttachmentTurn = "INTERNAL_ARMAMENT_ATTACHMENT_TURN";
    internal const string ArmamentAttachmentTurnObjectKey = "armamentAttachmentTurnObject";

    internal static string ArmamentAttachmentTurnObject(CardObjectState card)
        => MatchStateHasher.HashValue(new ObjectBinding(card.ObjectId, card.ObjectGeneration));

    // Publish only an opaque binding for the just accepted instruction. The
    // raw incarnation stays internal, including entry normalization shared by
    // attachment observers. Existing accepted history is never rewritten.
    private static ResolutionResult RecordArmamentAttachmentTurnHistory(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Payload.ContainsKey(PrivateArmamentAttachmentTurn))) return result;
        var events = result.Events.Select(ev =>
        {
            if (!ev.Payload.TryGetValue(PrivateArmamentAttachmentTurn, out var raw)) return ev;
            if (raw is not ObjectBinding binding) throw new InvalidOperationException("Invalid private attachment-turn capture.");
            var generation = before.CardObjects.TryGetValue(binding.ObjectId, out var prior)
                && !IsObjectOnField(before.PlayerZones, binding.ObjectId)
                && IsObjectOnField(result.State.PlayerZones, binding.ObjectId)
                    ? Math.Max(binding.Generation, checked(prior.ObjectGeneration + 1)) : binding.Generation;
            var payload = new Dictionary<string, object?>(ev.Payload);
            payload.Remove(PrivateArmamentAttachmentTurn);
            payload[ArmamentAttachmentTurnObjectKey] = MatchStateHasher.HashValue(binding with { Generation = generation });
            return ev with { Payload = payload };
        }).ToArray();
        return result with { Events = events };
    }

    internal static IReadOnlyList<ArmamentConditionalMightContribution> ArmamentConditionalMightContributions(
        MatchState state, IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState host)
    {
        if (host.IsFaceDown || !IsFaceUpNonStandbyUnit(host) || !IsObjectOnField(zones, host.ObjectId)) return [];
        var controller = EffectiveFieldControllerId(zones, host.ObjectId, host);
        var here = state.ObjectLocations.GetValueOrDefault(host.ObjectId);
        var others = here is { Zone: "BATTLEFIELD", BattlefieldObjectId: { Length: > 0 } } ? state.CardObjects.Values.Where(other =>
                other.ObjectId != host.ObjectId && IsFaceUpNonStandbyUnit(other) && IsObjectOnField(zones, other.ObjectId)
                && EffectiveFieldControllerId(zones, other.ObjectId, other) == controller
                && state.ObjectLocations.TryGetValue(other.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
                && at.BattlefieldObjectId == here.BattlefieldObjectId)
            .Select(other => other.ObjectId).Order(StringComparer.Ordinal).ToArray() : [];
        var contributions = new List<ArmamentConditionalMightContribution>();
        foreach (var gear in state.CardObjects.Values.Where(gear => gear.ObjectId != host.ObjectId && gear.AttachedToObjectId == host.ObjectId
            && !gear.IsFaceDown && gear.Tags.Contains(CardObjectTags.EquipmentCard) && IsObjectOnField(zones, gear.ObjectId))
            .OrderBy(gear => gear.ObjectId, StringComparer.Ordinal))
        {
            if (OfficialArmamentCatalog.Find(gear.CardNo)?.ConditionalMight is not { } bonus) continue;
            var applies = bonus.Condition switch {
                "ATTACHED_THIS_TURN" => gear.UntilEndOfTurnEffects.Contains(ArmamentAttachedThisTurn),
                "CONTROLLER_EXPERIENCE_AT_LEAST" => state.PlayerExperience.GetValueOrDefault(controller) >= bonus.Threshold,
                "EXACT_OTHER_FRIENDLY_UNITS_HERE" => here is { Zone: "BATTLEFIELD" } && others.Length == bonus.Threshold,
                _ => false };
            if (applies) contributions.Add(new(gear.ObjectId, gear.CardNo!, bonus.Amount, bonus.Condition,
                bonus.Condition == "EXACT_OTHER_FRIENDLY_UNITS_HERE" ? [host.ObjectId, ..others] : [host.ObjectId]));
        }
        return contributions;
    }
}
