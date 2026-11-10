using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RiftHeraldMovementLookTests
{
    internal const string Herald = "UNL-179/219";
    internal const string Alternate = "UNL-179a/219";

    private static MatchState Position(string no = Herald, bool inField = false, int deckCount = 6, bool hasUnit = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "HERALD", no,
            "P1", inField ? "BATTLEFIELD" : "BASE", inField ? "BF" : null);
        state = RadGearTestPosition.Put(state, "FOOTHOLD", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        string[] prints = ["OGN·275/298", hasUnit ? "OGN·219/298" : "SFD·022/221", "SFD·022/221",
            "OGN·219/298", "OGN·219/298", "OGN·219/298"];
        for (var i = 0; i < 6; i++) state = RadGearTestPosition.Put(state, "P1-D" + i, prints[i], "P1",
            i < deckCount ? "MAIN_DECK" : "GRAVEYARD");
        return state;
    }

    private static Task Move(Driver game, bool toBase = false)
        => game.Act("P1", new MoveUnitCommand("HERALD", toBase ? "BATTLEFIELD" : "BASE",
            toBase ? "BASE" : "BATTLEFIELD:BF"));

    private static async Task Choose(Driver game, bool select = true)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("DECK_EFFECT", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, select ? ["P1-D1"] : []));
    }

    [Theory]
    [InlineData(Herald)] [InlineData(Alternate)]
    public async Task ArrivalPrivatelyLooksAtThreeThenRevealsDrawsTheUnitAndRecyclesOnlyTheRest(string no)
    {
        var game = new Driver(Position(no)); await Move(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("MOVE", skill.UnitEvent!.Timing); Assert.Null(game.State.PendingCardChoice);
        await game.Top(); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(["P1-D0", "P1-D1", "P1-D2"], choice.ContextObjectIds);
        Assert.Equal(["P1-D1"], choice.LegalObjectIds); Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.DoesNotContain("P1-D0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.DoesNotContain("P1-D1", JsonSerializer.Serialize(game.PromptFor("P2")));
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-D0"]));
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-D3"]));
        await Choose(game); Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-D3", "P1-D4", "P1-D5"], game.State.PlayerZones["P1"].MainDeck.Take(3));
        Assert.Equal(new[] { "P1-D0", "P1-D2" }, game.State.PlayerZones["P1"].MainDeck.Skip(3).Order(StringComparer.Ordinal));
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_REVEALED" && Equals(e.Event.Payload.GetValueOrDefault("objectId"), "P1-D1"));
        Assert.Contains(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(6, true)] [InlineData(2, true)] [InlineData(2, false)] [InlineData(0, true)]
    public async Task DecliningOrFindingNoUnitStillRecyclesAllAvailableLookedCards(int count, bool hasUnit)
    {
        var game = new Driver(Position(deckCount: count, hasUnit: hasUnit)); await Move(game); await game.Top();
        if (count > 0)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal(Math.Min(3, count), choice.ContextObjectIds.Count);
            if (!hasUnit) Assert.Empty(choice.LegalObjectIds);
            await Choose(game, false);
        }
        Assert.Equal(count, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_REVEALED");
        Assert.DoesNotContain("P1-D1", game.State.PlayerZones["P1"].Hand);
        if (count == 6) Assert.Equal("P1-D3", game.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task MovingBackToBaseDoesNotTriggerEitherIndependentSkill()
    {
        var game = new Driver(Position(inField: true)); await Move(game, true);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsForcedMoveAwardsTheLookToTheHeraldsController()
    {
        var state = RadGearTestPosition.Put(Position(), "THEIR-MOVE", "OGN·043/298", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["HERALD"], Destination: "BATTLEFIELD:BF"));
        await game.Top(); Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top(); await Choose(game); Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task AnEarnedMovementLookStillResolvesAfterARealSourceReturn()
    {
        var game = new Driver(Position()); await Move(game);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HERALD"])); await game.Top();
        Assert.Contains("HERALD", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("HERALD", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); await Choose(game); Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("timing")] [InlineData("destination")] [InlineData("erase")] [InlineData("duplicate")]
    public async Task RecoveryBindsTheEarnedMovementToItsOriginalSkillAndArrival(string mutation)
    {
        var game = new Driver(Position()); await Move(game); var skill = Assert.Single(game.State.StackItems);
        var changed = skill with { UnitEvent = mutation == "timing" ? skill.UnitEvent! with { Timing = "LAST_BREATH" }
            : skill.UnitEvent! with { Here = new("P1", "BATTLEFIELD", "OTHER") } };
        var forged = game.State with { StackItems = mutation switch { "erase" => [], "duplicate" => [skill, skill], _ => [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); await Choose(game); await game.Replay();
    }
}
