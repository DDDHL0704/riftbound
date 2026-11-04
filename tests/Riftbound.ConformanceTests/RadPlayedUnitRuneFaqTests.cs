using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadPlayedUnitRuneFaqTests
{
    private const string Wyvern = "RAD·092/167";
    private const string GemDragon = "UNL-104/219";
    private const string Volibear = "OGN·249/298";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeitherTriggerOrderLetsTheGemDragonReadyRunesCalledLater(bool callResolvesFirst)
    {
        var game = new Driver(Position()) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("UNIT", Wyvern, [], Destination: "BASE"));
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED");
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var call = Assert.Single(game.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var ready = Assert.Single(game.State.TriggerQueue, t => t.SourceObjectId == "GEM");
        AssertRejectedCapturedFrames(game.State, call);
        await game.ColdCheckpoint();
        // The command lists the desired resolution order; the engine reverses it for insertion.
        var order = callResolvesFirst ? new[] { call.TriggerId, ready.TriggerId } : new[] { ready.TriggerId, call.TriggerId };
        await Reject(game.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: order));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        while (game.State.PendingCardChoice is { } choice)
        {
            var isGem = choice.SourceObjectId == "GEM";
            if (isGem)
            {
                Assert.Contains("OLD-RUNE", choice.LegalObjectIds);
                Assert.DoesNotContain("NEW-RUNE-0", choice.LegalObjectIds);
                Assert.DoesNotContain("NEW-RUNE-1", choice.LegalObjectIds);
                Assert.Equal(2, choice.MaxCount);
                await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OLD-RUNE", "OTHER-RUNE", "THIRD-RUNE"]));
                await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["NEW-RUNE-0"]));
            }
            await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [isGem ? "OLD-RUNE" : "LEGEND"]));
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [isGem ? "OLD-RUNE" : "LEGEND"]));
        }
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        var gemSkill = Assert.Single(game.State.StackItems, i => i.SourceObjectId == "GEM");
        Assert.Equal(new[] { "OLD-RUNE" }, gemSkill.TargetObjectIds);
        Assert.Equal(game.State.CardObjects["OLD-RUNE"].ObjectGeneration, gemSkill.TargetGenerations!["OLD-RUNE"]);
        Assert.Equal(callResolvesFirst ? "LEGEND" : "GEM", game.State.StackItems[^1].SourceObjectId);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal(callResolvesFirst, game.State.CardObjects["OLD-RUNE"].IsExhausted);
        Assert.Equal(callResolvesFirst ? 1 : 3, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.False(game.State.CardObjects["OLD-RUNE"].IsExhausted);
        Assert.Equal(new[] { "NEW-RUNE-2" }, game.State.PlayerZones["P1"].RuneDeck);
        foreach (var id in new[] { "NEW-RUNE-0", "NEW-RUNE-1" })
        {
            Assert.Contains(id, game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects[id].IsExhausted);
        }
        Assert.Equal(2, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED").Event.Payload["count"]));
        Assert.Single(game.Events(), e => e.Event.Kind == "RUNE_READIED");
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
        await game.Replay();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task TheGemDragonCanChooseZeroOrTwoExistingRunesWhenItPlaysItself(int count)
    {
        var state = Position();
        state = RadUnitEffectTestFixture.Place(state, "GEM", "P1", "GRAVEYARD");
        state = RadUnitEffectTestFixture.Place(state, "LEGEND", "P1", "GRAVEYARD");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", GemDragon, "P1") } };
        var game = new Driver(state) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("UNIT", GemDragon, [], Destination: "BASE"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT", choice.SourceObjectId);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(2, choice.MaxCount);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            count == 0 ? [] : ["OLD-RUNE", "OTHER-RUNE"]));
        Assert.Single(game.State.StackItems);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal(count == 0, game.State.CardObjects["OLD-RUNE"].IsExhausted);
        Assert.Equal(count == 0, game.State.CardObjects["OTHER-RUNE"].IsExhausted);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Wyvern);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var (id, no, zone) in new[] { ("GEM", GemDragon, "BASE"), ("LEGEND", Volibear, "LEGEND"),
            ("OLD-RUNE", "UNL-R06", "BASE"), ("OTHER-RUNE", "UNL-R06", "BASE"), ("THIRD-RUNE", "UNL-R06", "BASE") })
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") with { IsExhausted = zone == "BASE" };
            locations[id] = new("P1", zone);
        }
        var deck = Enumerable.Range(0, 3).Select(i => "NEW-RUNE-" + i).ToArray();
        foreach (var id in deck)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", "P1");
            locations[id] = new("P1", "RUNE_DECK");
        }
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, "GEM", "OLD-RUNE", "OTHER-RUNE", "THIRD-RUNE"],
            LegendZone = ["LEGEND"], RuneDeck = deck };
        return state with { RoomId = "RAD-PLAYED-UNIT-RUNE-FAQ", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static void AssertRejectedCapturedFrames(MatchState state, TriggerQueueItemState trigger)
    {
        var context = trigger.UnitEvent!;
        var observation = context.PlayedUnitRunes!;
        var invalid = new[] {
            context with { PlayedUnitRunes = null },
            context with { PlayedUnitRunes = observation with { ControllerId = "P2" } },
            context with { PlayedUnitRunes = observation with { Unit = observation.Unit with { Generation = observation.Unit.Generation + 1 } } },
            context with { PlayedUnitRunes = observation with { Frame = observation.Frame with {
                PlayerZones = observation.Frame.PlayerZones.ToDictionary(p => p.Key,
                    p => p.Key == "P1" ? p.Value with { Hand = ["PRIVATE"] } : p.Value) } } }
        };
        foreach (var changed in invalid)
        {
            var forged = state with { TriggerQueue = state.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId
                ? t with { UnitEvent = changed } : t).ToArray() };
            Assert.Contains("invalid captured unit event trigger", MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
        }
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rune-faq-rejected", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
