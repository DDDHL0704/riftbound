using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial record MatchState
{

    private static IReadOnlyDictionary<string, int> PublicFieldSourceOrders(MatchState state)
    {
        var sourceOrders = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextSourceOrder = 1;
        foreach (var playerId in state.PlayerZones.Keys.OrderBy(playerId => playerId, StringComparer.Ordinal))
        {
            var zones = state.PlayerZones[playerId];
            foreach (var objectId in zones.Base
                .Where(objectId => IsPublicFieldObjectLocationCompatible(state, objectId, "BASE"))
                .Concat(zones.Battlefields
                    .Where(objectId => IsPublicFieldObjectLocationCompatible(state, objectId, "BATTLEFIELD")))
                .Concat(zones.LegendZone))
            {
                if (sourceOrders.ContainsKey(objectId)
                    || !state.CardObjects.TryGetValue(objectId, out var cardObject)
                    || cardObject.IsFaceDown)
                {
                    continue;
                }

                sourceOrders[objectId] = nextSourceOrder++;
            }
        }

        return sourceOrders;
    }

    private static bool StaticAuraControllerRequirementsSatisfied(
        StaticAuraSpec aura,
        MatchState state,
        string controllerId)
    {
        if (aura.RequiredPlayerExperience.HasValue
            && (!state.PlayerExperience.TryGetValue(controllerId, out var experience)
                || experience < aura.RequiredPlayerExperience.Value))
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<string> PublicStaticAuraSourceObjectIds(MatchState state)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base
                .Concat(entry.Value.Battlefields)
                .Concat(entry.Value.LegendZone))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string EffectivePublicStaticAuraSourceControllerId(
        MatchState state,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return TryFindPublicStaticAuraSourceLocation(state.PlayerZones, objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static bool TryFindPublicStaticAuraSourceLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BASE");
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BATTLEFIELD");
                return true;
            }

            if (zones.LegendZone.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "LEGEND");
                return true;
            }
        }

        location = default;
        return false;
    }

    private static bool IsObjectLocationCompatibleWithBattlefield(
        MatchState state,
        string objectId,
        string battlefieldObjectId)
    {
        if (!state.ObjectLocations.TryGetValue(objectId, out var location))
        {
            return true;
        }

        return string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(location.BattlefieldObjectId)
                || string.Equals(location.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal));
    }

    private static bool IsPublicFieldObjectLocationCompatible(
        MatchState state,
        string objectId,
        string zone)
    {
        return !state.ObjectLocations.TryGetValue(objectId, out var location)
            || string.Equals(location.Zone, zone, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ControlledPublicFieldEquipmentObjectIds(MatchState state, string controllerId)
    {
        return state.PlayerZones
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                && !candidate.IsFaceDown
                && string.Equals(EffectiveFieldControllerId(state, objectId, candidate), controllerId, StringComparison.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int ResolveBasePower(CardObjectState cardObject)
    {
        return cardObject.Power - cardObject.UntilEndOfTurnPowerModifier;
    }

    private static IReadOnlyList<string> LayerEngineFoundationResiduals()
    {
        return
        [
            "timestamp ordering",
            "dependency ordering",
            "source ordering",
            "keyword gain/loss layering",
            "multiple equipment/static aura interactions",
            "minimum-power layering",
            "full official LayerEngine coverage"
        ];
    }

    private static bool IsBattlefieldCardStateObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId)
    {
        return cardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal);
    }

    private static bool TryFindFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out (string PlayerId, string Zone) location)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BASE");
                return true;
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                location = (playerId, "BATTLEFIELD");
                return true;
            }
        }

        location = default;
        return false;
    }

    private static IReadOnlyDictionary<string, int> NormalizePlayerScores(
        IReadOnlyDictionary<string, int>? playerScores)
    {
        return (playerScores ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> NormalizePlayerExperience(
        IReadOnlyDictionary<string, int>? playerExperience)
    {
        return (playerExperience ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> NormalizePlayerCardsPlayedThisTurn(
        IReadOnlyDictionary<string, int>? playerCardsPlayedThisTurn)
    {
        return (playerCardsPlayedThisTurn ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, OfficialDecklist> NormalizeOfficialDecklists(
        IReadOnlyDictionary<string, OfficialDecklist>? decklists)
    {
        return (decklists ?? new Dictionary<string, OfficialDecklist>(StringComparer.Ordinal))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value is not null)
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => OfficialDeckValidator.Normalize(entry.Value),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<BattleResolutionState> NormalizeBattleResolutions(
        IReadOnlyList<BattleResolutionState>? battleResolutions)
    {
        return (battleResolutions ?? [])
            .Where(resolution => !string.IsNullOrWhiteSpace(resolution.ResolutionId))
            .Select(resolution => new BattleResolutionState(
                resolution.ResolutionId.Trim(),
                resolution.Tick,
                string.IsNullOrWhiteSpace(resolution.Kind) ? string.Empty : resolution.Kind.Trim(),
                string.IsNullOrWhiteSpace(resolution.Reason) ? string.Empty : resolution.Reason.Trim(),
                string.IsNullOrWhiteSpace(resolution.BattlefieldId) ? string.Empty : resolution.BattlefieldId.Trim(),
                NormalizeOptionalText(resolution.AttackingPlayerId),
                NormalizeOptionalText(resolution.DefendingPlayerId),
                NormalizeOptionalText(resolution.WinnerPlayerId),
                NormalizeOrderedTextList(resolution.AttackerObjectIds),
                NormalizeOrderedTextList(resolution.DefenderObjectIds),
                NormalizeOrderedTextList(resolution.SurvivingAttackerObjectIds),
                NormalizeOrderedTextList(resolution.SurvivingDefenderObjectIds),
                NormalizeOrderedTextList(resolution.DestroyedObjectIds),
                NormalizeOrderedTextList(resolution.RelatedEventKinds)) { PublicObjectBindings = resolution.PublicObjectBindings })
            .Take(12)
            .ToArray();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Resolution histories retain command/event order across save and recovery.
    private static IReadOnlyList<string> NormalizeOrderedTextList(IReadOnlyList<string>? values) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<string> NormalizeTextList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static PlayerZones NormalizeZones(PlayerZones zones)
    {
        return new PlayerZones(
            NormalizeZone(zones.MainDeck),
            NormalizeZone(zones.RuneDeck),
            NormalizeZone(zones.Hand),
            NormalizeZone(zones.Base),
            NormalizeZone(zones.Battlefields),
            NormalizeZone(zones.Graveyard),
            NormalizeZone(zones.Banished),
            NormalizeZone(zones.LegendZone),
            NormalizeZone(zones.ChampionZone));
    }

    private static IReadOnlyList<string> NormalizeZone(IReadOnlyList<string>? zone)
    {
        return (zone ?? [])
            .Where(cardId => !string.IsNullOrWhiteSpace(cardId))
            .Select(cardId => cardId.Trim())
            .ToArray();
    }
}
