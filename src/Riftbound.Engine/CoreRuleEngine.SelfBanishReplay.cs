using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN FAQ: the player executing the skill also executes the following play,
    // even though banishment puts a stolen card in its owner's non-field zone.
    internal static bool ValidSelfBanishReplayEffectPlay(MatchState state, PendingEffectPlayState p)
    {
        var parent = p.Parent;
        if (!P4ActivatedAbilityCatalog.TryGetByEffectKind(parent.EffectKind, out var ability)
            || ability.SimpleEffect?.BanishesSourceThenReplaysToBase != true
            || parent.SimpleActivation is not { } context || !ValidSimpleActivation(state, parent)
            || parent.EffectPlayCompleted || p.PlayerId != parent.ControllerId || !state.Seats.ContainsKey(p.PlayerId)
            || p.SourceZone != "BANISHED" || p.DestinationPolicy != "BASE" || p.Optional
            || !p.IgnoreBaseMana || !p.IgnoreBasePower || p.IgnoreAllCosts || p.ManaReduction != 0
            || p.MaximumUnitPower is not null || p.ViewedCardIds is not null || p.RevealedHand is not null
            || p.SourceReplay is not null || p.OriginalLocation is not null || p.OriginalBattlefield is not null
            || p.Sources.Count != 1 || !p.Sources.TryGetValue(context.Source.ObjectId, out var generation)
            || generation != context.Source.Generation + 1
            || !state.CardObjects.TryGetValue(context.Source.ObjectId, out var card) || card.ObjectGeneration != generation
            || card.IsFaceDown || TokenObjectRules.IsToken(card) || card.CardNo != context.ReplayCardNo
            || (!card.Tags.Contains(CardObjectTags.UnitCard) && !card.Tags.Contains(CardObjectTags.EquipmentCard))
            || card.OwnerId is not { } owner
            || card.ControllerId != owner || p.SourcePlayerId != (owner == p.PlayerId ? null : owner)
            || !state.PlayerZones.TryGetValue(owner, out var zones) || !zones.Banished.Contains(card.ObjectId)
            || state.ObjectLocations.GetValueOrDefault(card.ObjectId) is not { Zone: "BANISHED" } at
            || at.PlayerId != owner) return false;
        return true;
    }

    internal static bool IsForeignSelfBanishReplay(MatchState state, string player, string source)
        => state.PendingEffectPlay is { SourcePlayerId: not null } p && p.PlayerId == player
            && p.Sources.ContainsKey(source) && ValidSelfBanishReplayEffectPlay(state, p);
}
