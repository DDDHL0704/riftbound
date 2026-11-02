using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// An in-process server decision, deliberately not a GameCommand or wire DTO.
public sealed record TournamentGameLossDecision(
    [property: JsonRequired] int GameNumber,
    [property: JsonRequired] string LoserPlayerId)
{
    public const string CommandType = "TOURNAMENT_GAME_LOSS";
}

public interface IMatchTournamentAdjudicator
{
    ValueTask<ResolutionResult> ApplyGameLossAsync(string decisionId, TournamentGameLossDecision decision,
        CancellationToken cancellationToken);
}

public sealed partial class MatchSession : IMatchTournamentAdjudicator
{
    private readonly Dictionary<string, CachedResolution> tournamentDecisions = new(StringComparer.Ordinal);

    public async ValueTask<ResolutionResult> ApplyGameLossAsync(string decisionId, TournamentGameLossDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var playerId = NormalizePlayerId(decision.LoserPlayerId);
        RequirePlayer(playerId);
        var intentId = NormalizeClientIntentId(decisionId);
        var input = TypedCommandInput.CaptureGameLoss(decision);
        var fingerprint = InputFingerprint(null, input);
        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (tournamentDecisions.TryGetValue(intentId, out var cached))
                return IntentConflicts(cached, TournamentGameLossDecision.CommandType, fingerprint)
                    ? ResolutionResult.Rejected(state, ClientIntentConflictMessage, ErrorCodes.ClientIntentConflict)
                    : cached.Result;

            var startedSequence = lastEventSequence;
            ResolutionResult result;
            var series = RoomMatchSeries.Current(state);
            if (options.DeckFormat != OfficialDeckFormat.Sealed || series is null)
                result = ResolutionResult.Rejected(state, "当前房间未开放比赛裁定。", ErrorCodes.PhaseNotAllowed);
            else if (series.Phase == MatchSeriesPhase.Finished)
                result = ResolutionResult.Rejected(state, "比赛已经结束，不能追加单局判负。", ErrorCodes.MatchFinished);
            else if (decision.GameNumber != series.GameNumber)
                result = ResolutionResult.Rejected(state, "单局判负的局号与当前对局不一致。", ErrorCodes.PhaseNotAllowed);
            else
            {
                var completed = series.AwardGameLoss(decision.GameNumber, playerId);
                var winner = completed.State.Games[decision.GameNumber - 1].WinnerPlayerId!;
                var terminal = state with { Tick = state.Tick + 1, Status = MatchStatuses.Finished, WinnerPlayerId = winner };
                var events = new[]
                {
                    new GameEvent("GAME_LOSS_AWARDED", "服务端裁定单局判负", new Dictionary<string, object?>
                    { ["gameNumber"] = decision.GameNumber, ["loserPlayerId"] = playerId,
                        ["skipped"] = completed.State.Games[decision.GameNumber - 1].Skipped }),
                    new GameEvent("MATCH_WON", "对手被判本局负", new Dictionary<string, object?>
                    { ["winnerPlayerId"] = winner, ["reason"] = "GAME_LOSS" })
                };
                result = FinalizeSeriesGame(new ResolutionResult(true, null, terminal, events,
                    new Dictionary<string, SnapshotDto>(), new Dictionary<string, ActionPromptDto>()), series, completed);
            }
            var completedSequence = startedSequence + result.Events.Count;
            await journal.RecordAsync(new MatchJournalEntry(RoomId, playerId, intentId, TournamentGameLossDecision.CommandType,
                null, state.Tick, result.State.Tick, startedSequence, completedSequence, result.Accepted, result.ErrorMessage,
                result.State, result.Events, result.Snapshots, result.Prompts, DateTimeOffset.UtcNow)
                { OpeningSeed = openingSeed, ErrorCode = result.ErrorCode, TypedCommand = input }, cancellationToken).ConfigureAwait(false);
            if (result.Accepted) state = result.State;
            lastEventSequence = completedSequence;
            tournamentDecisions[intentId] = new(TournamentGameLossDecision.CommandType, fingerprint, result);
            return result;
        }
        finally { serialGate.Release(); }
    }
}
