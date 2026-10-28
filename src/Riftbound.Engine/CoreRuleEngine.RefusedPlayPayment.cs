using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult ResolveRefusePlayPayment(MatchState state, PlayerIntent intent,
        PendingEffectPlayState pending, RefusePlayPaymentCommand refusal)
    {
        var play = refusal.AsPlayCard();
        if (IsAmbushPlayMode(play.Mode))
            return RejectWithCorePrompts(state, "再次打出不能选择伏击模式。", ErrorCodes.InvalidTarget);
        // This shared plan validates source/generation, mode, targets, destination,
        // optional costs and reductions before authorizing or committing payment.
        // As with preview, an otherwise valid plan survives insufficient resources.
        TryBuildPlayCardPlan(state, intent, play, out var plan, out var rejection);
        if (plan is null) return rejection;
        if (plan.TotalManaCost == 0 && plan.TotalPowerCost == 0)
            return RejectWithCorePrompts(state, "所选打出的最终资源费用为零，不能拒付。", ErrorCodes.InvalidTarget);
        // CN445.2.a/358.5 withdraw this play before any debit, nonresource cost,
        // source move or play trigger. Prior independent resource actions and the
        // already-paid parent remain in the original history and current state.
        return FinishEffectPlay(state with { Tick = state.Tick + 1 }, intent, pending, []);
    }
}
