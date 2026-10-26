namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool HasHistoricalPublicDamageSource(MatchState state, StackItemState item)
        => item.DamageDistribution is { Source: { } source }
            && !string.IsNullOrWhiteSpace(item.SourceObjectId)
            && state.CardObjects is { } cards && cards.TryGetValue(item.SourceObjectId, out var current)
            && source.Generation < current.ObjectGeneration
            && ValidDistributionStack(state, item);
}
