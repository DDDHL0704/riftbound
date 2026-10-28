using System.Text.Json.Serialization;

namespace Riftbound.Contracts;

// Declare the same play choices, then refuse its server-calculated resource fee.
// No quoted price, client waiver or debit is accepted in this intent.
public sealed record RefusePlayPaymentCommand(
    string SourceObjectId,
    string CardNo,
    IReadOnlyList<string> TargetObjectIds,
    string Mode = "",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? OptionalCosts = null,
    string Destination = "",
    IReadOnlyList<SpellRepeatChoice>? RepeatChoices = null,
    IReadOnlyList<string>? MovementDestinations = null) : GameCommand(CommandTypes.RefusePlayPayment)
{
    public PlayCardCommand AsPlayCard() => new(SourceObjectId, CardNo, TargetObjectIds,
        Mode, OptionalCosts, Destination, RepeatChoices, MovementDestinations);
}
