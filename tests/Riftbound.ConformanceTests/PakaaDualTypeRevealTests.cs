using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class PakaaDualTypeRevealTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    public async Task DualTypeTopIsDrawnOnceWithoutBeingPlayedOrTakingTheNonunitBranch(int count, int equipment)
    {
        // CN178: a unit/gear retains both types. The old TopCard fixture helper
        // selects only one type, so the controlled fixture must retain both.
        var initial = PakaaProtectorRevealTests.ControlledJournalPosition("VEN·058");
        var originalDeck = initial.PlayerZones["P1"].MainDeck.ToArray();
        var top = originalDeck[0];
        var cards = initial.CardObjects.ToDictionary();
        cards[top] = cards[top] with { Tags = [CardObjectTags.EquipmentCard, CardObjectTags.UnitCard] };
        var locations = initial.ObjectLocations.ToDictionary();
        var removed = originalDeck.Skip(count).ToArray();
        foreach (var id in removed) locations[id] = new("P1", "GRAVEYARD");
        var gears = Enumerable.Range(0, equipment).Select(i => "OTHER-GEAR-" + i).ToArray();
        foreach (var id in gears)
        {
            cards[id] = new(id, cardNo: "OGN·072/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard]);
            locations[id] = new("P1", "BASE");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            CardPlays = initial.CardPlays with { TurnNumber = initial.TurnNumber },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = originalDeck.Take(count).ToArray(),
                    Base = initial.PlayerZones["P1"].Base.Concat(gears).ToArray(),
                    Graveyard = initial.PlayerZones["P1"].Graveyard.Concat(removed).ToArray() } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await PakaaProtectorRevealTests.Move(driver); await driver.Top();
        var state = driver.State;
        Assert.Equal(initial.PlayerZones["P1"].Hand.Append(top), state.PlayerZones["P1"].Hand);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck.Skip(1), state.PlayerZones["P1"].MainDeck);
        Assert.Equal(initial.PlayerZones["P1"].Graveyard, state.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, ArmamentMightTests.Might(state));
        Assert.Empty(state.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Contains(CardObjectTags.UnitCard, state.CardObjects[top].Tags);
        Assert.Contains(CardObjectTags.EquipmentCard, state.CardObjects[top].Tags);
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue);
        Assert.Null(state.PendingEffectPlay); Assert.Null(state.ObservedDeckPlays);
        var events = driver.Events().Select(entry => entry.Event).ToArray();
        Assert.Single(events, e => e.Kind == "CARD_DRAWN");
        var reveal = Assert.Single(events, e => e.Kind == "CARDS_REVEALED");
        Assert.Equal(new[] { top }, JsonSerializer.SerializeToElement(reveal.Payload)
            .GetProperty("cardIds").EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain(events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED" or "UNIT_PLAYED_TO_BASE");
        Assert.DoesNotContain(events, e => JsonSerializer.Serialize(e).Contains("sourcePlayDrawObservation", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(initial.CardPlays), JsonSerializer.Serialize(state.CardPlays));
        Assert.DoesNotContain('"' + top + '"', JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"]));
        if (count > 1) Assert.DoesNotContain('"' + originalDeck[1] + '"', JsonSerializer.Serialize(events));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        output.WriteLine($"PAKA_DUAL_CONTROLLED count={count} otherEquipment={equipment} drawn=1 might=4 originalReplay=true");
    }

    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules)]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724)]
    public async Task NewLegalDeckFromBlankOpeningKeepsBothPrintedTypesAndReplaysItsOriginalCommands(OfficialDeckFormat format)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "pakaa-dual-type-openings.json")));
        var recipe = fixture.RootElement;
        var run = new CopiedDuelInsightOpeningTests.Opening(37, format);
        foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        var host = recipe.GetProperty("host").GetString()!;
        var top = run.State.PlayerZones["P1"].MainDeck[0];
        var original = run.State.CardObjects[top];
        Assert.Equal("VEN·058", original.CardNo);
        Assert.Contains(CardObjectTags.UnitCard, original.Tags);
        Assert.Contains(CardObjectTags.EquipmentCard, original.Tags);
        Assert.Equal(2, original.ManaCost); Assert.Equal(2, original.Power);
        var deck = run.State.PlayerZones["P1"].MainDeck.ToArray();
        var hand = run.State.PlayerZones["P1"].Hand.ToArray();
        var grave = run.State.PlayerZones["P1"].Graveyard.ToArray();
        var plays = JsonSerializer.Serialize(run.State.CardPlays);
        await run.Cold(); await Save(run, format, "before-move");
        var from = run.Frame().Events.Count;
        var field = run.State.PlayerZones["P1"].Battlefields.First(id => run.State.BattlefieldStates.ContainsKey(id));
        await run.Reject("P2", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field));
        await run.Submit("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field));
        await run.Cold();
        if (run.State.TriggerQueue.Count > 0)
            await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(hand.Append(top), run.State.PlayerZones["P1"].Hand);
        Assert.Equal(deck.Skip(1), run.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(grave, run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(plays, JsonSerializer.Serialize(run.State.CardPlays));
        Assert.Equal(4, CurrentPowerTargetTests.Snapshot(run.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32());
        Assert.Contains(CardObjectTags.UnitCard, run.State.CardObjects[top].Tags);
        Assert.Contains(CardObjectTags.EquipmentCard, run.State.CardObjects[top].Tags);
        var events = run.Frame().Events.Skip(from).Select(entry => entry.Event).ToArray();
        Assert.Single(events, e => e.Kind == "CARD_DRAWN");
        var reveal = Assert.Single(events, e => e.Kind == "CARDS_REVEALED");
        Assert.Equal(new[] { top }, JsonSerializer.SerializeToElement(reveal.Payload)
            .GetProperty("cardIds").EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain(events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED" or "UNIT_PLAYED_TO_BASE");
        Assert.DoesNotContain('"' + deck[1] + '"', JsonSerializer.Serialize(events));
        Assert.DoesNotContain('"' + top + '"', JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)["P2"]));
        await run.Cold();
        for (var i = 0; i < 12 && (run.State.StackItems.Count > 0 || run.State.TriggerQueue.Count > 0 || run.State.FocusPlayerId is not null); i++)
        {
            if (run.State.TriggerQueue.Count > 0)
                await run.Submit(run.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (run.State.PriorityPlayerId is not null) await run.Submit(run.State.PriorityPlayerId, new PassPriorityCommand());
            else await run.Submit(run.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        await run.Submit("P1", new EndTurnCommand()); await run.Cold(); await Save(run, format, "complete");
        var frame = run.Frame();
        Assert.All(frame.Commands, command => Assert.Equal(run.State.RuleInputs, command.RuleInputs));
        output.WriteLine("PAKA_DUAL_OPENING " + JsonSerializer.Serialize(new { format = format.ToString(),
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            originalSeed = run.Initial.Seed, originalFullReplayMatched = true, sourceCardNo = "VEN·033", topCardNo = "VEN·058" }));
    }

    private static async Task Save(CopiedDuelInsightOpeningTests.Opening run, OfficialDeckFormat format, string step)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_PAKA_DUAL_TYPE_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); var path = Path.Combine(root, $"{format}-{step}.json.gz"); Assert.False(File.Exists(path));
        await using var file = File.Create(path);
        await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(zip, run.Frame(), CopiedDuelInsightOpeningTests.Opening.JournalJson);
    }
}
