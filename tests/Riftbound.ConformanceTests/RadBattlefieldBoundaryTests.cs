using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadBattlefieldBoundaryTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task EachBlackMarketOffersItsOwnUseToTheTurnPlayer(string actor)
    {
        var initial = Put(Create("RAD·158/167"), "OTHER", "RAD·158/167", "P2", "BATTLEFIELD", "OTHER")
            with { TurnPlayerId = actor, ActivePlayerId = actor };
        var game = new Driver(initial);
        foreach (var field in new[] { "BF", "OTHER" })
        {
            Assert.Contains(game.PromptFor(actor).Candidates ?? [], c => c.Action == "ACTIVATE_ABILITY" && c.Enabled
                && (c.Sources ?? []).Any(source => source.Id == field));
            var command = new ActivateAbilityCommand(field, P4ActivatedAbilityCatalog.BlackMarketAbilityId, []);
            await game.Act(actor, command);
            await game.Top();
            await Reject(game.State, actor, command);
        }
        Assert.Equal(26, game.State.RunePools[actor].TotalPower);
        Assert.Equal(2, game.State.PlayerZones[actor].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuinedMonasteryWithAnEmptyHandNeverCallsARune(bool hasRune)
    {
        var initial = ConquestLifecycleRegressionTests.State("RAD·166/167", 0);
        if (hasRune) initial = Put(initial, "P1-MONASTERY-RUNE", "OGN·007/298", "P1", "RUNE_DECK");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        await game.Top();
        Assert.Null(game.State.PendingHandChoice);
        Assert.Equal(hasRune ? 1 : 0, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED");
        await game.Replay();
    }

    [Fact]
    public async Task RuinedMonasteryCanDiscardWithAnEmptyRuneDeckWithoutDrawing()
    {
        var game = new Driver(ConquestLifecycleRegressionTests.State("RAD·166/167", 1));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        await game.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H1"]));
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("H1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·166/167", 2)]
    [InlineData("OGN·298/298", 0)]
    public async Task ConquestDiscardRecoveryRejectsForgedRequiredCounts(string no, int required)
    {
        var game = new Driver(ConquestLifecycleRegressionTests.State(no, 3));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        await game.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        var forged = game.State with { PendingHandChoice = choice with { RequiredCount = required } };
        var commands = game.Commands();
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, commands[^1].CompletedEventSequence,
            commands, game.Events(), new Dictionary<string, RecoveredPlayerView>(), forged, forged.Tick);
        Assert.Contains(errors, e => e.Contains("invalid battlefield conquest discard hand continuation", StringComparison.Ordinal));
        await Reject(forged, "P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
    }

    [Fact]
    public async Task CosmicVistaDoesNotTriggerWhenTheLastRuneIsChanneledAfterHold()
    {
        var initial = OfficialHoldSequenceTests.State("RAD·160/167");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { RuneDeck = ["R1"] } },
            CardObjects = initial.CardObjects.Where(p => p.Key is not ("R2" or "R3")).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key is not ("R2" or "R3")).ToDictionary() };
        var game = new Driver(initial);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        await game.Replay();
    }
}
