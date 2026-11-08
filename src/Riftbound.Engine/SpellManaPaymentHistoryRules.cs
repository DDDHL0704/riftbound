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
            if (play.Source is null || string.IsNullOrEmpty(play.PlayId))
            { errors.Add("confirmed spell mana history has no source binding or play identity"); continue; }
            var births = events.Where(e => e.Event.Kind == "CARD_PLAYED"
                && Text(e.Event, "sourceObjectId") == play.Source.ObjectId
                && Text(e.Event, "cardNo") == play.CardNo && Text(e.Event, "playerId") == play.PlayerId
                && ReadReceipt(e.Event)?.PlayId == play.PlayId).ToArray();
            var birth = births.Length == 1 ? births[0] : null;
            var payments = events.Where(e => e.Tick == birth?.Tick && e.Event.Kind == "COST_PAID"
                && Text(e.Event, "playId") == play.PlayId).ToArray();
            var payment = payments.Length == 1 ? payments[0] : null;
            var receipt = birth is null ? null : ReadReceipt(birth.Event);
            RecoveredCommand[] accepted = birth is null ? [] : commands.Where(c => c.Accepted
                && c.CompletedTick == birth.Tick && c.StartedEventSequence < birth.Sequence
                && c.CompletedEventSequence >= birth.Sequence).ToArray();
            var command = accepted.Length == 1 ? accepted[0] : null;
            if (receipt is null || payment is null || command is null || (play.Types & 4) == 0
                || receipt.Source != play.Source || receipt.CardNo != play.CardNo || receipt.PlayerId != play.PlayerId
                || receipt.PlayId != play.PlayId || receipt.Mana != play.PaidMana || receipt.Tick != play.Tick
                || receipt.TurnNumber != state.CardPlays?.TurnNumber || receipt.TurnNumber != state.TurnNumber
                || !ValidConfirmationOrigin(play, birth!, command, commands, events)
                || payment.Sequence <= birth!.Sequence || payment.Sequence > command.CompletedEventSequence
                || payment.Sequence <= command.StartedEventSequence
                || Text(payment.Event, "playerId") != play.PlayerId || Text(payment.Event, "sourceObjectId") != play.Source.ObjectId
                || Text(payment.Event, "cardNo") != play.CardNo || Number(payment.Event, "mana") != play.PaidMana
                || Number(payment.Event, "turnNumber") != receipt.TurnNumber)
                errors.Add("confirmed spell mana history differs from its original accepted payment");
        }
        return errors;
    }

    private static bool ValidConfirmationOrigin(CardPlayHistoryEntry play, RecoveredEvent birth,
        RecoveredCommand completed, IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events)
    {
        // Development preparation executes real engine commands inside one
        // accepted seed action. Its public events have the outer journal tick;
        // the typed receipt retains the nested confirmation's actual tick.
        if (IsScenarioPreparation(completed, events))
        {
            var suffix = "-" + play.Source.ObjectId;
            return play.Tick > completed.StartedTick && play.Tick <= completed.CompletedTick
                && play.PlayId.StartsWith("STACK-", StringComparison.Ordinal)
                && play.PlayId.EndsWith(suffix, StringComparison.Ordinal)
                && play.PlayId.Length > "STACK-".Length + suffix.Length
                && long.TryParse(play.PlayId.AsSpan("STACK-".Length,
                    play.PlayId.Length - "STACK-".Length - suffix.Length),
                    System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var started)
                && started > completed.StartedTick && started <= play.Tick;
        }
        if (birth.Tick != play.Tick) return false;
        if (IsOriginalPlay(completed, play, birth)) return true;
        if (completed.CommandType != CommandTypes.PayCost) return false;
        var origins = commands.Where(command => command.Accepted && command.CompletedTick < completed.CompletedTick
            && IsOriginalPlay(command, play, birth)).ToArray();
        if (origins.Length != 1) return false;
        var previous = origins[0];
        var continuations = commands.Where(command => command.Accepted
            && command.StartedEventSequence >= previous.CompletedEventSequence
            && command.CompletedEventSequence <= completed.CompletedEventSequence)
            .OrderBy(command => command.StartedEventSequence).ToArray();
        foreach (var next in continuations)
        {
            if (next.StartedTick != previous.CompletedTick || next.CompletedTick != next.StartedTick + 1
                || next.StartedEventSequence != previous.CompletedEventSequence
                || next.CommandType != CommandTypes.PayCost
                || ReadCommand(next) is not PayCostCommand { PaymentWindow: CoreRuleEngine.RuleChoiceWindow,
                    PaymentChoiceIds.Count: 1 } answer
                || answer.PaymentId is not { } paymentId
                || !paymentId.StartsWith($"REPLACEMENT:{origins[0].StartedTick}:", StringComparison.Ordinal)) return false;
            var requests = events.Where(e => e.Sequence > previous.StartedEventSequence
                && e.Sequence <= previous.CompletedEventSequence && e.Event.Kind == "RULE_CHOICE_REQUESTED").ToArray();
            if (requests.Length != 1 || Text(requests[0].Event, "choiceId") != answer.PaymentId
                || Text(requests[0].Event, "playerId") != next.PlayerId) return false;
            previous = next;
        }
        return continuations.Length > 0 && previous == completed;
    }

    private static bool IsOriginalPlay(RecoveredCommand command, CardPlayHistoryEntry play, RecoveredEvent birth)
        => command.PlayerId == play.PlayerId && command.CompletedTick == command.StartedTick + 1
            && play.PlayId == $"STACK-{command.CompletedTick}-{play.Source.ObjectId}"
            && (ReadCommand(command) switch {
                PlayCardCommand original => command.CommandType == CommandTypes.PlayCard
                    && original.SourceObjectId == play.Source.ObjectId && original.CardNo == play.CardNo,
                RevealCardCommand original => command.CommandType == CommandTypes.RevealCard
                    && Flag(birth.Event, "playedFromStandby")
                    && original.SourceObjectId == play.Source.ObjectId && original.CardNo == play.CardNo,
                _ => false });

    private static bool IsScenarioPreparation(RecoveredCommand command, IReadOnlyList<RecoveredEvent> events)
    {
        const string prefix = "DEV_SEED_SCENARIO:";
        if (command.CommandType is not { } commandType || !commandType.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var scenario = commandType[prefix.Length..];
        var inputMatches = command.TypedCommand?.TryScenario(out var typed) == true ? typed == scenario
            : command.RawCommand is { ValueKind: JsonValueKind.Object } raw
                && raw.TryGetProperty("scenarioId", out var value) && value.ValueKind == JsonValueKind.String
                && value.GetString() == scenario;
        var seeded = events.Where(e => e.Sequence > command.StartedEventSequence
            && e.Sequence <= command.CompletedEventSequence && e.Event.Kind == "DEV_SCENARIO_SEEDED").ToArray();
        return inputMatches && seeded.Length == 1 && Text(seeded[0].Event, "scenarioId") == scenario
            && Text(seeded[0].Event, "playerId") == command.PlayerId;
    }

    private static GameCommand? ReadCommand(RecoveredCommand command)
    {
        try { return command.RawCommand is { ValueKind: JsonValueKind.Object } raw ? GameCommandJsonMapper.Map(raw)
                : command.TypedCommand?.TryRestore(out var typed) == true ? typed : null; }
        catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        { return null; }
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
