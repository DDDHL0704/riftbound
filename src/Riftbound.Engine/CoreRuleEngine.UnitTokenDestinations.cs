using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string UnitTokenDestinationChoiceKind = "TOKEN_PLAY_LOCATION";
    private static readonly Lazy<IReadOnlyDictionary<string, string>> UnitTokenInstructionTexts = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(
            card => card.CardNo, card => card.CardEffect, StringComparer.Ordinal));

    // An instruction's explicit "here" or "to your base" remains a restriction
    // even when another effect grants a wider set of ordinary play locations.
    private static bool UnitTokenInstructionAllowsLocationChoice(string? cardNo, string family)
        => cardNo is not null && UnitTokenInstructionTexts.Value.TryGetValue(cardNo, out var text)
            && UnitTokenLocationParser.AllowsOrdinaryLocationChoice(text, family);

    private static bool UnitTokenLocationChoiceGranted(IReadOnlyList<string>? effects, string player)
        => ActiveUnitPlayGrants(effects, player, "PLAY_BOON").Any(GrantsAnyUnitPlayBattlefield);

    private static bool ShouldOfferUnitTokenLocationChoice(MatchState state, string player, string? cardNo,
        string family, Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, CardBehaviorDefinition? behavior = null)
    {
        if (!UnitTokenInstructionAllowsLocationChoice(cardNo, family)) return false;
        // Entry-trigger tokens and Rengar's unit-play window consume the token
        // immediately; their follow-up choice must stay ahead of a destination
        // menu. Ordinary token plays retain the normal base/battlefield choice.
        if (behavior is { CreatedTokenReadyCount: > 0 } || behavior?.CreatedBaseUnitTokenPerEquipment == true
            || ControllerHasRengarLegend(state, player)) return false;
        // An outside-hand haste grant has its own pre-entry payment window. Do
        // not interleave an ordinary destination choice before that payment,
        // including when the grant source is hidden, controlled by the other
        // player, or has just left the field while the token play is resolving.
        if (cards.Values.Any(card => (card.OwnerId == player || card.ControllerId == player)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var grant)
                && grant.GrantsHasteToFriendlyUnitsPlayedOutsideHand)) return false;
        return true;
    }

    private static string? ChooseUnitTokenBattlefield(MatchState state, string player, string family,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, ObjectLocationState> locations, IReadOnlyDictionary<string, RunePool> pools,
        IReadOnlyList<string> effects)
    {
        var context = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations, zones), UntilEndOfTurnEffects = effects };
        var anywhere = UnitTokenLocationChoiceGranted(effects, player);
        var options = new List<RuleChoiceOption> { new("BASE", "基地") };
        foreach (var field in cards.Values.Where(card => BattlefieldLocalRules.Battlefield(context, card.ObjectId) is not null)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal))
        {
            var destination = "BATTLEFIELD:" + field.ObjectId;
            if (!BattlefieldLocalRules.PreventsUnitPlay(context, destination)
                && (anywhere || IsPlayCardUnitBattlefieldDestinationAllowed(context, player, destination)))
                options.Add(new(destination, OfficialCardSourceIdentityGroups.DisplayName(field.CardNo ?? field.ObjectId), [field.ObjectId]));
        }
        if (options.Count == 1) return null;
        var selected = RuleChoices.Value!.Choose(player, $"选择{family}的打出位置。", options,
            kind: UnitTokenDestinationChoiceKind, preview: context);
        return selected == "BASE" ? null : selected[12..];
    }
}
