using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveNextSpellEchoGrant(MatchState state, StackItemState item)
        => NoopStackResolutionResult(state) with {
            UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(state.UntilEndOfTurnEffects, item.ControllerId),
            Events = [new("NEXT_SPELL_ECHO_GRANTED", "下一个法术获得等同基础费用的回响", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) })] };
}
