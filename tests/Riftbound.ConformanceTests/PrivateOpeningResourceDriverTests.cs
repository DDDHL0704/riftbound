using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PrivateOpeningResourceDriverTests
{
    [Fact]
    public async Task OriginalUnfundableFirstUnitDoesNotBlockTheNextLegallyPayableUnit()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "private-opening-unfundable-first-unit.json")));
        var fixture = document.RootElement; var original = typeof(FullGameEndToEndTests);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        // This is fresh setup from the captured QA seed before any action.
        // It does not deserialize or migrate the archived implementation state.
        var initial = (MatchState)original.GetMethod("BuildSeatedInitialState", flags)!.Invoke(null,
            [fixture.GetProperty("roomId").GetString()!, fixture.GetProperty("seed").GetInt64()])!;
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        ResolutionResult current = null!;
        foreach (var entry in fixture.GetProperty("acceptedCommands").EnumerateArray())
        {
            var actor = entry.GetProperty("playerId").GetString()!; var intent = entry.GetProperty("clientIntentId").GetString()!;
            var raw = entry.GetProperty("rawCommand"); var command = GameCommandJsonMapper.Map(raw);
            current = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                _ => await session.SubmitAsync(actor, intent, command, raw, default) };
            Assert.True(current.Accepted, current.ErrorMessage);
        }
        Assert.Equal(8, journal.Entries.Count); Assert.Equal("P1", current.State.ActivePlayerId);
        Assert.Equal(2, current.State.RunePools["P1"].Mana); Assert.Equal(0, current.State.RunePools["P1"].TotalPower);
        var runes = current.State.PlayerZones["P1"].Base.Where(id => current.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        Assert.Equal(2, runes.Length); Assert.All(runes, id => Assert.Contains("COLOR:blue", current.State.CardObjects[id].Tags));
        var play = current.Prompts["P1"].Candidates!.Single(c => c.Action == CommandTypes.PlayCard && c.Enabled);
        var first = play.Sources![0].Id; Assert.Equal("ARC-001/006", current.State.CardObjects[first].CardNo);
        var before = MatchStateHasher.Hash(current.State);
        var rejected = await engine.ResolveAsync(current.State, new("red-cost-negative", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(first, "ARC-001/006", [], Destination: "BASE"), default);
        Assert.False(rejected.Accepted); Assert.Equal(before, MatchStateHasher.Hash(rejected.State)); Assert.Empty(rejected.Events);

        var drive = original.GetMethod("TryPlayFirstUnitAsync", flags)!;
        var result = await (ValueTask<ResolutionResult>)drive.Invoke(null,
            [session, "P1", current, "original-private-opening-regression", false, null])!;
        var played = Assert.Single(journal.Entries.Skip(8), e => e.CommandType == CommandTypes.PlayCard && e.Accepted);
        Assert.Equal("OGN·103/298", played.RawCommand!.Value.GetProperty("cardNo").GetString());
        Assert.DoesNotContain(journal.Entries.Skip(8), e => e.CommandType == CommandTypes.RecycleRune);
        Assert.All(journal.Entries, e => Assert.True(e.Accepted));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join(';', replay.Errors));
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken ct) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
