using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool MatchesNamedConquestActivationEvent(GameEvent ev, NamedConquestActivationOrigin origin, string field)
        => origin.Source is not null && ev.Kind == "TRIGGER_RESOLVED"
            && TryReadObjectString(ev.Payload, "triggerId", out var id) && id == origin.ParentStackItemId
            && TryReadObjectString(ev.Payload, "controllerId", out var controller) && controller == origin.ControllerId
            && TryReadObjectString(ev.Payload, "sourceObjectId", out var source) && source == origin.Source.ObjectId
            && TryReadObjectLong(ev.Payload, "sourceGeneration", out var gen) && gen == origin.Source.Generation
            && TryReadObjectString(ev.Payload, "sourceCardNo", out var card) && card == origin.CardNo
            && TryReadObjectString(ev.Payload, "effectKind", out var effect) && effect == "HOLD_ACTIVATE_CONQUEST"
            && TryReadObjectString(ev.Payload, "battlefieldObjectId", out var at) && at == field;

    private static bool MatchesNamedConquestActivationPayload(object? payload, NamedConquestActivationOrigin origin)
        => origin.Source is not null
            && TryReadObjectString(payload, "activationStackItemId", out var id) && id == origin.ParentStackItemId
            && TryReadObjectString(payload, "activationSourceObjectId", out var source) && source == origin.Source.ObjectId
            && TryReadObjectLong(payload, "activationSourceGeneration", out var gen) && gen == origin.Source.Generation
            && TryReadObjectString(payload, "activationSourceCardNo", out var card) && card == origin.CardNo
            && TryReadObjectString(payload, "activationControllerId", out var controller) && controller == origin.ControllerId;
}
