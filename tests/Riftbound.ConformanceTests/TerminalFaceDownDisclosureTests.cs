using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TerminalFaceDownDisclosureTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task EitherConcessionShowsTheCurrentStandbyCardWithoutTurningItFaceUpOrOpeningHiddenZones(string player)
    {
        // CN129.4/422.4/425.1.a: face-down field cards are shown at game end;
        // showing a card does not move it or play/turn it face up.
        var initial = StandbySourceRulesTests.Position("OGN·057/298");
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal);
        async Task<ResolutionResult> Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), Json);
            var result = await session.SubmitAsync(actor, "terminal-show-" + player + "-" + journal.Entries.Count,
                command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); return result;
        }
        var hidden = await Act("P1", new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.True(hidden.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.False(Objects(hidden.Snapshots["P2"], "P1").TryGetProperty("SHADOW", out _));
        Assert.False(Objects(ResolutionResult.BuildSpectatorSnapshot(hidden.State), "P1").TryGetProperty("SHADOW", out _));
        var originalCard = MatchStateHasher.HashValue(hidden.State.CardObjects["SHADOW"]);
        var originalLocation = MatchStateHasher.HashValue(hidden.State.ObjectLocations["SHADOW"]);
        var beforeSnapshot = JsonSerializer.Serialize(hidden.Snapshots, Json);
        var beforeCommands = JsonSerializer.Serialize(Commands(journal), Json);
        var beforeEvents = JsonSerializer.Serialize(Events(journal), Json);
        var ended = await Act(player, new SurrenderCommand());
        Assert.Equal(MatchStatuses.Finished, ended.State.Status);
        Assert.Equal(player == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
        Assert.Equal("MATCH_WON", Assert.Single(ended.Events).Kind);
        Assert.Equal(originalCard, MatchStateHasher.HashValue(ended.State.CardObjects["SHADOW"]));
        Assert.Equal(originalLocation, MatchStateHasher.HashValue(ended.State.ObjectLocations["SHADOW"]));
        Assert.Equal(beforeSnapshot, JsonSerializer.Serialize(hidden.Snapshots, Json));
        Assert.Equal(beforeCommands, JsonSerializer.Serialize(Commands(journal).Take(1), Json));
        Assert.Equal(beforeEvents, JsonSerializer.Serialize(Events(journal).Take(hidden.Events.Count), Json));
        AssertPublicFaceDownCardAndHiddenZones(ended.State, ended.Snapshots);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(ended);
        var restoredState = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(ended.State, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(ended.State), MatchStateHasher.Hash(restoredState));
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(restoredState));
        AssertPublicFaceDownCardAndHiddenZones(restoredState, ResolutionResult.BuildSnapshots(restoredState));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(journal), ended.State, engine, default, Events(journal));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var before = MatchStateHasher.Hash(ended.State);
        var rejected = await engine.ResolveAsync(ended.State, new("post-end", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(MatchStateHasher.HashValue(ended.Snapshots), MatchStateHasher.HashValue(rejected.Snapshots));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task TheSharedFinishedProjectionUsesTheSameDisclosureForEitherWinner(string winner)
    {
        // Projection contract only; actual normal-victory execution is covered
        // separately from this explicit midgame fixture.
        var initial = StandbySourceRulesTests.Position("OGN·057/298");
        var hidden = await new CoreRuleEngine().ResolveAsync(initial, new("terminal-projection-hide", "P1", CommandTypes.HideCard),
            new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]), default);
        Assert.True(hidden.Accepted, hidden.ErrorMessage);
        var finished = hidden.State with { Status = MatchStatuses.Finished, WinnerPlayerId = winner };
        AssertPublicFaceDownCardAndHiddenZones(finished, ResolutionResult.BuildSnapshots(finished));
        Assert.Equal(MatchStateHasher.HashValue(hidden.State.CardObjects), MatchStateHasher.HashValue(finished.CardObjects));
        Assert.Equal(MatchStateHasher.HashValue(hidden.State.PlayerZones), MatchStateHasher.HashValue(finished.PlayerZones));
    }

    private static void AssertPublicFaceDownCardAndHiddenZones(MatchState state, IReadOnlyDictionary<string, SnapshotDto> snapshots)
    {
        foreach (var (viewer, snapshot) in snapshots.Append(new("__spectator__", ResolutionResult.BuildSpectatorSnapshot(state))))
        {
            var card = Objects(snapshot, "P1").GetProperty("SHADOW");
            Assert.Equal("OGN·057/298", card.GetProperty("cardNo").GetString());
            Assert.True(card.GetProperty("isFaceDown").GetBoolean());
            foreach (var (owner, zones) in state.PlayerZones)
            {
                var objects = Objects(snapshot, owner);
                foreach (var id in zones.MainDeck.Concat(zones.RuneDeck)) Assert.False(objects.TryGetProperty(id, out _));
                if (viewer != owner)
                    foreach (var id in zones.Hand) Assert.False(objects.TryGetProperty(id, out _));
            }
        }
    }

    private static JsonElement Objects(SnapshotDto snapshot, string player)
        => JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players").GetProperty(player).GetProperty("objects");

    private static RecoveredCommand[] Commands(Journal journal) => journal.Entries.Select(e => new RecoveredCommand(
        e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick,
        e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
        { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();

    private static RecoveredEvent[] Events(Journal journal) => journal.Entries.SelectMany(e => e.Events.Select((value, i) =>
        new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, value))).ToArray();

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
