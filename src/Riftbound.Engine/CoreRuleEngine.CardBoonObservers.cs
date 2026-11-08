using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Every caller captures immediately after removing the boon and before
    // the instruction's might cleanup. An observer losing its own final boon
    // still earns the skill even if the subsequent cleanup destroys it.
    private static GameEvent CaptureBoonConsumption(GameEvent ev,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards)
    {
        if (ev.Payload.GetValueOrDefault("playerId") is not string player) return ev;
        return WithUnitEvents(ev, CaptureUnitEvents(zones, cards,
            cards.Values.Where(card => EffectiveFieldControllerId(zones, card.ObjectId, card) == player)
                .Select(card => card.ObjectId), UnitEventTimings.BoonConsumed));
    }

    internal static bool RequiresObservedUnitEventHistory(UnitEventContext? context)
        => context?.Timing is UnitEventTimings.PrintedPowerCardPlayed or UnitEventTimings.BoonConsumed;

    internal static string ObservedUnitEventCaptureHash(UnitEventContext context, string source, string controller)
        => MatchStateHasher.HashValue(new { source, controller, context.CardNo, context.Timing,
            context.SourceGeneration, context.FaceRevision, context.CapturedTick,
            context.TokenFactoryCardNo, context.Origin });
}
