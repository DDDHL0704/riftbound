using System.Globalization;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record BoonAmplifier(string Marker, int TurnNumber, string ControllerId, ObjectBinding Source,
    string CardNo, string StackItemId, int ExecutionIndex, int Amount);

public sealed partial class CoreRuleEngine
{
    internal const string BoonAmplifierPrefix = "FRIENDLY_BOON_MIGHT|";
    internal const string BoonAmplifierEffect = "TEMPORARY_FRIENDLY_BOON_MIGHT";
    private static string EncodeBoonIdentity(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    private static string? DecodeBoonIdentity(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch (FormatException) { return null; }
    }

    private static string BoonMarker(int turn, string controller, string source, long generation, string card,
        string stack, int execution, int amount) => string.Join('|', BoonAmplifierPrefix.TrimEnd('|'),
        turn.ToString(CultureInfo.InvariantCulture), EncodeBoonIdentity(controller), EncodeBoonIdentity(source),
        generation.ToString(CultureInfo.InvariantCulture), EncodeBoonIdentity(card), EncodeBoonIdentity(stack),
        execution.ToString(CultureInfo.InvariantCulture), amount.ToString(CultureInfo.InvariantCulture));

    internal static BoonAmplifier? ReadBoonAmplifier(string marker)
    {
        var f = marker.Split('|');
        if (f.Length != 9 || f[0] != BoonAmplifierPrefix.TrimEnd('|')
            || !int.TryParse(f[1], out var turn) || turn <= 0
            || DecodeBoonIdentity(f[2]) is not { Length: > 0 } player
            || DecodeBoonIdentity(f[3]) is not { Length: > 0 } source
            || !long.TryParse(f[4], out var generation) || generation < 0
            || DecodeBoonIdentity(f[5]) is not { Length: > 0 } card
            || DecodeBoonIdentity(f[6]) is not { Length: > 0 } stack
            || !int.TryParse(f[7], out var execution) || execution < 0
            || !int.TryParse(f[8], out var amount) || amount <= 0
            || marker != BoonMarker(turn, player, source, generation, card, stack, execution, amount)) return null;
        return new(marker, turn, player, new(source, generation), card, stack, execution, amount);
    }

    internal static bool ValidBoonAmplifier(MatchState state, BoonAmplifier c)
        => c.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(c.ControllerId)
            && CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var b) && b.FriendlyBoonBonusUntilEndOfTurn == c.Amount
            && state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) && source.ObjectGeneration >= c.Source.Generation
            && (source.ObjectGeneration != c.Source.Generation || source.CardNo == c.CardNo)
            && c.StackItemId.StartsWith("STACK-", StringComparison.Ordinal) && c.StackItemId.EndsWith("-" + c.Source.ObjectId, StringComparison.Ordinal)
            && c.StackItemId.Length > c.Source.ObjectId.Length + 7
            && long.TryParse(c.StackItemId[6..^(c.Source.ObjectId.Length + 1)], out var tick) && tick > 0 && tick <= state.Tick
            && c.StackItemId == "STACK-" + tick.ToString(CultureInfo.InvariantCulture) + "-" + c.Source.ObjectId;

    internal static bool ValidCurrentBoonAmplifiers(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(e => e is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(BoonAmplifierPrefix, StringComparison.Ordinal)).ToArray();
        return markers.Length == markers.Distinct(StringComparer.Ordinal).Count()
            && markers.All(e => ReadBoonAmplifier(e) is { } c && ValidBoonAmplifier(state, c));
    }

    private static void AddBoonAmplifiers(MatchState state, StackItemState item, CardBehaviorDefinition b,
        List<string> effects, List<GameEvent> events)
    {
        if (b.FriendlyBoonBonusUntilEndOfTurn <= 0) return;
        var source = state.CardObjects[item.SourceObjectId];
        for (var i = 0; i < item.EffectRepeatCount; i++)
        {
            var marker = BoonMarker(state.TurnNumber, item.ControllerId, source.ObjectId, source.ObjectGeneration,
                item.CardNo!, item.StackItemId, item.CompletedRepeatExecutions + i, b.FriendlyBoonBonusUntilEndOfTurn);
            effects.Add(marker);
            events.Add(new("BOON_MIGHT_AMPLIFIER_CREATED", "本回合所有友方增益额外提高战力", new Dictionary<string, object?> {
                ["sourceObjectId"] = source.ObjectId, ["effectId"] = marker, ["controllerId"] = item.ControllerId,
                ["powerDeltaPerBoon"] = b.FriendlyBoonBonusUntilEndOfTurn, ["duration"] = "UNTIL_END_OF_TURN" }));
        }
    }

    internal static IReadOnlyList<BoonAmplifier> BoonAmplifiersFor(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState unit)
    {
        if (unit.IsFaceDown || unit.Tags.Contains(CardObjectTags.Standby) || !unit.Tags.Contains(CardObjectTags.Boon)
            || !IsFieldUnitObject(state with { PlayerZones = zones }, unit.ObjectId)) return [];
        var controller = EffectiveFieldControllerId(zones, unit.ObjectId, unit);
        return state.UntilEndOfTurnEffects.Where(e => e.StartsWith(BoonAmplifierPrefix, StringComparison.Ordinal))
            .Select(ReadBoonAmplifier).OfType<BoonAmplifier>()
            .Where(c => c.TurnNumber == state.TurnNumber && c.ControllerId == controller).ToArray();
    }
}
