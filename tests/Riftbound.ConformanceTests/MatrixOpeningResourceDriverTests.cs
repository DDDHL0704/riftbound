using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MatrixOpeningResourceDriverTests
{
    [Fact]
    public async Task OriginalElevenPreconstructedDeckInputsRemainExactAcrossBothFormats()
    {
        using var original = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "preconstructed-original-4fca-inputs.json")));
        Assert.Equal(1, original.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("4fca367eaab1dd198ffde839b23461c058a2b470",
            original.RootElement.GetProperty("sourceCommit").GetString());
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var format in original.RootElement.GetProperty("formats").EnumerateArray())
        {
            var formatName = format.GetProperty("format").GetString()!;
            var decks = PreconstructedDeckCatalog.Build(catalog, Enum.Parse<OfficialDeckFormat>(formatName));
            var expectedDecks = format.GetProperty("decks");
            Assert.Equal(11, expectedDecks.GetArrayLength());
            foreach (var expected in expectedDecks.EnumerateArray())
            {
                var id = expected.GetProperty("id").GetString()!;
                var actual = Assert.Single(decks, deck => deck.Id == id);
                Assert.True(JsonElement.DeepEquals(expected.GetProperty("decklist"),
                        JsonSerializer.SerializeToElement(actual.Decklist, json)),
                    $"Original deck input changed: {formatName}/{id}");
            }
        }
    }

    [Fact]
    public async Task OriginalRumbleArmamentsPairPaysExposedUnitPowerThroughRealRuneActions()
    {
        const string room = "b0-full-game-preconstructed-catalog-replay-room-jhin-lowcurve-vs-rumble-armaments";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var decks = PreconstructedDeckCatalog.Build(catalog);
        var original = typeof(FullGameEndToEndTests);
        var setup = original.GetMethod("BuildSeatedInitialState", BindingFlags.Static | BindingFlags.NonPublic)!;
        var initial = (MatchState)setup.Invoke(null, [room, 424242L])!;
        var drive = original.GetMethod("DriveOfficialLowCurveDecksToEstablishedBattlefieldsAsync", BindingFlags.Static | BindingFlags.NonPublic,
            [typeof(MatchState), typeof(IMatchJournal), typeof(OfficialDecklist), typeof(OfficialDecklist)])!;
        var journal = new Journal();
        var (_, result) = await (ValueTask<(MatchSession, ResolutionResult)>)drive.Invoke(null,
            [initial, journal, decks.Single(deck => deck.Id == "jhin-lowcurve").Decklist,
                decks.Single(deck => deck.Id == "rumble-armaments").Decklist])!;
        Assert.Equal(424242, result.State.Seed);
        Assert.Contains(journal.Entries, entry => entry.PlayerId == "P2" && entry.CommandType == "RECYCLE_RUNE" && entry.Accepted);
        Assert.DoesNotContain(journal.Entries, entry => !entry.Accepted);
        Assert.All(result.State.Seats.Keys, player => Assert.Contains(result.State.BattlefieldStates.Values,
            field => field.ControllerId == player));
        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence,
            entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) { ErrorCode = entry.ErrorCode, TypedCommand = entry.TypedCommand, RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((value, index) =>
            new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, value))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State,
            new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
