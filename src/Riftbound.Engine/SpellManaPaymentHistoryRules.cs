using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmedSpellManaPayment(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string PlayerId,
    [property: JsonRequired] string PlayId,
    [property: JsonRequired] int Mana,
    [property: JsonRequired] int TurnNumber,
    [property: JsonRequired] long Tick);

internal static class SpellManaPaymentHistoryRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static IReadOnlyList<string> ValidateOriginalPayments(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events)
    {
        var errors = new List<string>();
        foreach (var play in (state.CardPlays?.Finalized ?? []).Where(e => e?.PaidMana is not null))
        {
            if (play.Source is null)
            { errors.Add("confirmed spell mana history has no source binding"); continue; }
            var births = events.Where(e => e.Tick == play.Tick && e.Event.Kind == "CARD_PLAYED"
                && Text(e.Event, "sourceObjectId") == play.Source.ObjectId
                && Text(e.Event, "cardNo") == play.CardNo && Text(e.Event, "playerId") == play.PlayerId).ToArray();
            var payments = events.Where(e => e.Tick == play.Tick && e.Event.Kind == "COST_PAID"
                && Text(e.Event, "playId") == play.PlayId).ToArray();
            var birth = births.Length == 1 ? births[0] : null;
            var payment = payments.Length == 1 ? payments[0] : null;
            var receipt = birth is null ? null : ReadReceipt(birth.Event);
            RecoveredCommand[] accepted = birth is null ? [] : commands.Where(c => c.Accepted
                && (c.CommandType == CommandTypes.PlayCard || c.CommandType == CommandTypes.RevealCard
                    && Flag(birth.Event, "playedFromStandby")) && c.PlayerId == play.PlayerId
                && c.CompletedTick == play.Tick && c.StartedEventSequence < birth.Sequence
                && c.CompletedEventSequence >= birth.Sequence).ToArray();
            var command = accepted.Length == 1 ? accepted[0] : null;
            if (receipt is null || payment is null || command is null || (play.Types & 4) == 0
                || receipt.Source != play.Source || receipt.CardNo != play.CardNo || receipt.PlayerId != play.PlayerId
                || receipt.PlayId != play.PlayId || receipt.Mana != play.PaidMana || receipt.Tick != play.Tick
                || receipt.TurnNumber != state.CardPlays?.TurnNumber || receipt.TurnNumber != state.TurnNumber
                || play.PlayId != $"STACK-{play.Tick}-{play.Source.ObjectId}"
                || payment.Sequence <= birth!.Sequence || payment.Sequence > command.CompletedEventSequence
                || payment.Sequence <= command.StartedEventSequence
                || Text(payment.Event, "playerId") != play.PlayerId || Text(payment.Event, "sourceObjectId") != play.Source.ObjectId
                || Text(payment.Event, "cardNo") != play.CardNo || Number(payment.Event, "mana") != play.PaidMana
                || Number(payment.Event, "turnNumber") != receipt.TurnNumber)
                errors.Add("confirmed spell mana history differs from its original accepted payment");
        }
        return errors;
    }

    private static ConfirmedSpellManaPayment? ReadReceipt(GameEvent ev)
    {
        if (!ev.Payload.TryGetValue("spellManaPayment", out var value) || value is null) return null;
        try { return value as ConfirmedSpellManaPayment
                ?? JsonSerializer.SerializeToElement(value, Json).Deserialize<ConfirmedSpellManaPayment>(Json); }
        catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        { return null; }
    }

    private static string? Text(GameEvent ev, string key) => ev.Payload.GetValueOrDefault(key) switch
    { string value => value, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
    private static long? Number(GameEvent ev, string key) => ev.Payload.GetValueOrDefault(key) switch
    { int value => value, long value => value,
        JsonElement { ValueKind: JsonValueKind.Number } value when value.TryGetInt64(out var number) => number, _ => null };
    private static bool Flag(GameEvent ev, string key) => ev.Payload.GetValueOrDefault(key) switch
    { true => true, JsonElement { ValueKind: JsonValueKind.True } => true, _ => false };
}
