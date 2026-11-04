using System.Globalization;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record MysteriousWeaponRecallContext(int TurnNumber, string ControllerId,
    ObjectBinding Source, ObjectBinding Target, string CardNo, long CreatedTick);

public sealed partial class CoreRuleEngine
{
    internal const string MysteriousWeaponRecallPrefix = "MYSTERIOUS_WEAPON_RECALL|";

    private static string MysteriousWeaponIdentity(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string? ReadMysteriousWeaponIdentity(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return null; }
    }

    private static string MysteriousWeaponRecallMarker(MysteriousWeaponRecallContext context)
        => string.Join('|', "MYSTERIOUS_WEAPON_RECALL",
            context.TurnNumber.ToString(CultureInfo.InvariantCulture),
            MysteriousWeaponIdentity(context.ControllerId),
            MysteriousWeaponIdentity(context.Source.ObjectId),
            context.Source.Generation.ToString(CultureInfo.InvariantCulture),
            MysteriousWeaponIdentity(context.Target.ObjectId),
            context.Target.Generation.ToString(CultureInfo.InvariantCulture),
            MysteriousWeaponIdentity(context.CardNo),
            context.CreatedTick.ToString(CultureInfo.InvariantCulture));

    internal static string MysteriousWeaponRecallMarker(int turnNumber, string controllerId,
        ObjectBinding source, ObjectBinding target, string cardNo, long createdTick)
        => MysteriousWeaponRecallMarker(new(turnNumber, controllerId, source, target, cardNo, createdTick));

    internal static MysteriousWeaponRecallContext? ReadMysteriousWeaponRecallMarker(string marker)
    {
        if (!marker.StartsWith(MysteriousWeaponRecallPrefix, StringComparison.Ordinal)) return null;
        var fields = marker.Split('|');
        if (fields.Length != 9
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var turn)
            || turn < 1
            || ReadMysteriousWeaponIdentity(fields[2]) is not { Length: > 0 } controller
            || ReadMysteriousWeaponIdentity(fields[3]) is not { Length: > 0 } sourceId
            || !long.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var sourceGeneration)
            || sourceGeneration < 0
            || ReadMysteriousWeaponIdentity(fields[5]) is not { Length: > 0 } targetId
            || !long.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out var targetGeneration)
            || targetGeneration < 0
            || ReadMysteriousWeaponIdentity(fields[7]) is not { Length: > 0 } cardNo
            || !long.TryParse(fields[8], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)
            || tick < 1) return null;
        var context = new MysteriousWeaponRecallContext(turn, controller,
            new(sourceId, sourceGeneration), new(targetId, targetGeneration), cardNo, tick);
        return MysteriousWeaponRecallMarker(context) == marker ? context : null;
    }

    internal static bool ValidMysteriousWeaponRecallMarker(MatchState state, string marker, string targetObjectId)
    {
        var context = ReadMysteriousWeaponRecallMarker(marker);
        if (context is null || context.TurnNumber != state.TurnNumber || context.CreatedTick > state.Tick
            || context.Target.ObjectId != targetObjectId || !state.Seats.ContainsKey(context.ControllerId)
            || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(
                P4ActivatedAbilityCatalog.MysteriousWeaponRecallAbilityId, context.CardNo)
            || !state.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
            || source.ObjectGeneration < context.Source.Generation
            || source.ObjectGeneration == context.Source.Generation && source.CardNo != context.CardNo
            || !state.CardObjects.TryGetValue(targetObjectId, out var target)
            || target.ObjectGeneration != context.Target.Generation
            || !IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(state.PlayerZones, targetObjectId)) return false;
        return true;
    }

    internal static bool ValidCurrentMysteriousWeaponRecalls(MatchState state)
    {
        foreach (var target in state.CardObjects.Values)
        {
            if (target.UntilEndOfTurnEffects is null) return false;
            var markers = target.UntilEndOfTurnEffects
                .Where(effect => effect.StartsWith(MysteriousWeaponRecallPrefix, StringComparison.Ordinal)).ToArray();
            if (markers.Distinct(StringComparer.Ordinal).Count() != markers.Length
                || markers.Any(marker => !ValidMysteriousWeaponRecallMarker(state, marker, target.ObjectId))) return false;
        }
        return true;
    }

    internal static IEnumerable<(string Marker, MysteriousWeaponRecallContext Context)> MysteriousWeaponRecalls(
        MatchState state, CardObjectState target)
        => target.UntilEndOfTurnEffects
            .Where(marker => marker.StartsWith(MysteriousWeaponRecallPrefix, StringComparison.Ordinal))
            .Select(marker => (Marker: marker, Context: ReadMysteriousWeaponRecallMarker(marker)))
            .Where(entry => entry.Context is not null
                && ValidMysteriousWeaponRecallMarker(state, entry.Marker, target.ObjectId))
            .Select(entry => (entry.Marker, entry.Context!));
}
