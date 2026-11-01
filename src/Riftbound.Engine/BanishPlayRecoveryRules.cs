using Riftbound.CardCatalog;

namespace Riftbound.Engine;

// Independent of the producer's registry flags: these printed instructions
// grant an owner a single play with base costs waived, never all costs waived.
internal static class BanishPlayRecoveryRules
{
    private sealed record PrintedInstruction(string EffectKind, string Destination, string Text);
    private static readonly IReadOnlyDictionary<string, PrintedInstruction> Instructions =
        new Dictionary<string, PrintedInstruction>(StringComparer.Ordinal) {
            ["VEN·066"] = new("TIME_FISSURE_BANISH_UNIT_PLAY_SAME_LOCATION", "ORIGINAL_LOCATION",
                "放逐一名单位，然后让其拥有者将其打出到同一位置，无视其费用。"),
            ["UNL-184/219"] = new("HUNTING_RHYTHM_BANISH_FRIENDLY_UNIT_PLAY_TO_BATTLEFIELD", "ANY_BATTLEFIELD",
                "放逐一名友方单位，然后让其拥有者将其打出到任意一处战场，无视其费用。"),
            ["OGN·102/298"] = new("PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE", "BASE",
                "放逐一名友方单位，然后让其拥有者将它打出到其所属的基地，无视费用。")
        };
    private static readonly Lazy<IReadOnlySet<string>> PrintedSources = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(c => Instructions.TryGetValue(c.CardNo, out var instruction)
                && c.CardEffect.Contains(instruction.Text, StringComparison.Ordinal))
            .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static bool Applies(PendingEffectPlayState pending)
        => !CoreRuleEngine.IsDockCageEffectPlay(pending)
            && (pending.SourceZone == "BANISHED" || Instructions.ContainsKey(pending.Parent.CardNo));

    internal static bool Valid(MatchState state, PendingEffectPlayState pending)
    {
        if (pending.Parent.LastBreathBattlefieldDamage?.Kind == Riftbound.Contracts.TriggerKinds.UnitLastBreathBanishDeckTopPlay)
            return CoreRuleEngine.ValidLastBreathDeckTopPlay(state, pending);
        var parent = pending.Parent;
        if (!Instructions.TryGetValue(parent.CardNo, out var instruction) || !PrintedSources.Value.Contains(parent.CardNo)
            || parent.EffectKind != instruction.EffectKind || parent.EffectPlayCompleted || parent.SourceConfirmed
            || parent.DeckPlaySequence is not null || parent.ObservedPlayPermission is not null || parent.RecastContext is not null
            || pending.SourceZone != "BANISHED" || pending.DestinationPolicy != instruction.Destination
            || !pending.IgnoreBaseMana || !pending.IgnoreBasePower || pending.IgnoreAllCosts || pending.ManaReduction != 0
            || pending.Optional || pending.ViewedCardIds is not null || pending.RevealedHand is not null || pending.SourceReplay is not null
            || !state.Seats.ContainsKey(parent.ControllerId) || !state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            || pending.Sources.Count != 1 || parent.TargetObjectIds.Count != 1 || parent.TargetGenerations?.Count != 1)
            return false;
        if (instruction.Destination == "ORIGINAL_LOCATION") {
            if (pending.OriginalLocation is not { Zone: "BASE" or "BATTLEFIELD" } at
                || !state.Seats.ContainsKey(at.PlayerId)
                || (at.Zone == "BASE" ? at.BattlefieldObjectId is not null || pending.OriginalBattlefield is not null
                    : pending.OriginalBattlefield is not { } field || field.ObjectId != at.BattlefieldObjectId
                        || field.Generation < 0)) return false;
        } else if (pending.OriginalLocation is not null || pending.OriginalBattlefield is not null) return false;
        var source = pending.Sources.Single();
        return parent.TargetObjectIds[0] == source.Key && source.Value > 0
            && parent.TargetGenerations.GetValueOrDefault(source.Key, -1) == source.Value - 1
            && state.CardObjects.TryGetValue(source.Key, out var card) && card.ObjectGeneration == source.Value
            && !TokenObjectRules.IsToken(card) && card.OwnerId == pending.PlayerId && card.ControllerId == pending.PlayerId
            && card.Tags.Contains(CardObjectTags.UnitCard) && zones.Banished.Contains(source.Key)
            && state.ObjectLocations.GetValueOrDefault(source.Key) is { Zone: "BANISHED" } location
            && location.PlayerId == pending.PlayerId;
    }
}
