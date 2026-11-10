using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static UnitEventObservation[] CaptureEquipmentLastBreathUnitEvents(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState source, string controller)
    {
        // An animated equipment is captured once by the ordinary unit death.
        if (source.IsFaceDown || !source.Tags.Contains(CardObjectTags.EquipmentCard)
            || source.Tags.Contains(CardObjectTags.UnitCard) || !IsObjectOnField(zones, source.ObjectId)
            || CaptureUnitEvents(zones, cards, [source.ObjectId], ArmamentLastBreathRules.Timing).Length == 0) return [];
        var frame = RuleChoices.Value!;
        var deathId = $"death-{frame.Origin.Tick}-{frame.DeathSequence++}";
        var fact = new DestroyedUnitFact(source.ObjectId, source.CardNo!, source.OwnerId ?? controller,
            controller, source.ObjectGeneration, false, false, TokenObjectRules.IsToken(source));
        return CaptureLastBreathUnitEvents(state, zones, cards, source, deathId, fact)
            .Select(context => new UnitEventObservation(source.ObjectId, controller, context, "EQUIPMENT_DESTROYED")).ToArray();
    }
}
