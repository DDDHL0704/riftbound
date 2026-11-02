namespace Riftbound.Engine;

// Trusted application entry point. The per-session adjudicator is the state
// transition used by replay; this service also completes public result storage.
public sealed class MatchTournamentAdjudicationService(IMatchResultStore results)
{
    public async ValueTask<ResolutionResult> ApplyGameLossAsync(
        IMatchTournamentAdjudicator match, string decisionId, TournamentGameLossDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);
        var result = await match.ApplyGameLossAsync(decisionId, decision, cancellationToken).ConfigureAwait(false);
        await MatchResultRecords.RecordFinishedSeriesAsync(results, result, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
