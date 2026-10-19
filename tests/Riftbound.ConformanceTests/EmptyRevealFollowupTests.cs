using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class EmptyRevealFollowupTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("OGN·103/298", 0, 4)]
    [InlineData("OGN·104/298", 0, 4)]
    [InlineData("OGN·103/298", 1, 4)]
    [InlineData("OGN·104/298", 1, 6)]
    public async Task InitialEmptyRevealCancelsSubsequentInstructionWithoutBurnoutOrPhantomChoice(
        string topNo, int count, int expectedMight)
    {
        // CN432.1.c.1 specifically cancels the remaining instructions when the
        // observation cannot supply the card required by the follow-up actions.
        var initial = PakaaProtectorRevealTests.ControlledJournalPosition(topNo);
        var oldDeck = initial.PlayerZones["P1"].MainDeck;
        var removed = oldDeck.Skip(count).ToArray();
        var locations = initial.ObjectLocations.ToDictionary();
        foreach (var id in removed) locations[id] = new("P1", "GRAVEYARD");
        initial = initial with { ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = oldDeck.Take(count).ToArray(),
                    Graveyard = initial.PlayerZones["P1"].Graveyard.Concat(removed).ToArray() } } };
        Assert.Equal(0, initial.Tick);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await PakaaProtectorRevealTests.Move(driver); await driver.Top();
        var state = driver.State;
        Assert.Equal(expectedMight, ArmamentMightTests.Might(state));
        Assert.Null(state.DeckObservationView); Assert.Null(state.PendingRuleChoice);
        Assert.Null(state.PendingEffectPlay); Assert.Null(state.ObservedDeckPlays);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
        if (count == 0)
        {
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "CARDS_REVEALED" or "CARDS_MILLED" or "CARD_DRAWN" or "POWER_MODIFIED");
            Assert.Empty(state.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(initial.PlayerZones["P1"].MainDeck, state.PlayerZones["P1"].MainDeck);
            Assert.Equal(initial.PlayerZones["P1"].Hand, state.PlayerZones["P1"].Hand);
            Assert.Equal(initial.PlayerZones["P1"].Graveyard, state.PlayerZones["P1"].Graveyard);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        output.WriteLine($"EMPTY_REVEAL_CONTROLLED count={count} top={topNo} expectedMight={expectedMight} originalReplay=true");
    }

    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules)]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724)]
    public async Task OriginalPaidPakaOpeningReachesEmptyDeckThroughLegalTurnsAndPreservesRecovery(
        OfficialDeckFormat format)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "pakaa-protector-openings.json")));
        var recipe = fixture.RootElement.GetProperty("branches").EnumerateArray()
            .Single(row => row.GetProperty("profile").GetString() == "UNIT");
        var run = new CopiedDuelInsightOpeningTests.Opening(36, format);
        foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        var host = recipe.GetProperty("host").GetString()!;
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        Assert.Contains(host, run.State.PlayerZones["P1"].Base);
        var turns = 0;
        while (!(run.State.TurnPlayerId == "P1" && run.State.PlayerZones["P1"].MainDeck.Count == 0))
        {
            Assert.Equal(MatchStatuses.InProgress, run.State.Status);
            Assert.True(turns++ < 90, "The original legal turn sequence must reach the empty main deck.");
            Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
            await run.Submit(run.State.TurnPlayerId!, new EndTurnCommand());
        }
        Assert.Equal(MatchStatuses.InProgress, run.State.Status);
        Assert.False(run.State.CardObjects[host].IsExhausted);
        await run.Cold(); await Save(run, format, "before-move");
        var deck = run.State.PlayerZones["P1"].MainDeck.ToArray();
        var hand = run.State.PlayerZones["P1"].Hand.ToArray();
        var grave = run.State.PlayerZones["P1"].Graveyard.ToArray();
        var firstEvent = run.Frame().Events.Count;
        var destination = run.State.PlayerZones["P1"].Battlefields.First(id => run.State.BattlefieldStates.ContainsKey(id));
        await run.Reject("P2", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination));
        await run.Submit("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination));
        await run.Cold();
        if (run.State.TriggerQueue.Count > 0)
            await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await Save(run, format, "after-resolve");
        var power = CurrentPowerTargetTests.Snapshot(run.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32();
        Assert.Equal(4, power);
        Assert.Equal(deck, run.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(hand, run.State.PlayerZones["P1"].Hand);
        Assert.Equal(grave, run.State.PlayerZones["P1"].Graveyard);
        Assert.Null(run.State.PendingRuleChoice); Assert.Null(run.State.PendingEffectPlay);
        Assert.Null(run.State.DeckObservationView); Assert.Null(run.State.ObservedDeckPlays);
        Assert.DoesNotContain(run.Frame().Events.Skip(firstEvent), entry => entry.Event.Kind is
            "BURNOUT_APPLIED" or "CARDS_REVEALED" or "CARDS_MILLED" or "CARD_DRAWN" or "POWER_MODIFIED");
        await run.Cold();
        var frame = run.Frame();
        Assert.Empty(frame.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        await Save(run, format, "final");
        output.WriteLine("EMPTY_REVEAL_ORDINARY " + JsonSerializer.Serialize(new {
            format = format.ToString(), originalBlankSeed = run.Initial.Seed,
            originalOpeningCommands = recipe.GetProperty("openingIntents").GetArrayLength(), legalAdditionalTurns = turns,
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            originalFullActionEventStateReplay = true, noSourceDeckSeedOrHistoryInjection = true },
            CopiedDuelInsightOpeningTests.Opening.Json));
    }

    private static async Task Save(CopiedDuelInsightOpeningTests.Opening run, OfficialDeckFormat format, string stage)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_EMPTY_REVEAL_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"empty-paka-{format}-{stage}.json.gz"); Assert.False(File.Exists(path));
        await using var file = File.Create(path); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(zip, run.Frame(), CopiedDuelInsightOpeningTests.Opening.JournalJson);
    }
}
