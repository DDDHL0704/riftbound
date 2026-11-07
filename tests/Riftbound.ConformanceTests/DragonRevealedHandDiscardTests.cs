using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DragonRevealedHandDiscardTests
{
    private const string Dragon = "OGN·192/298";
    private static MatchState Position(bool empty = false)
    {
        var state = VenUnitEntryBasicTests.Position(Dragon);
        if (!empty) {
            state = RadGearTestPosition.Put(state, "OP-A", "OGN·219/298", "P2", "HAND");
            state = RadGearTestPosition.Put(state, "OP-B", "OGN·048/298", "P2", "HAND");
        }
        return state;
    }
    private static string View(MatchState state) => JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P1"]);
    private static async Task Select(Driver game, params string[] cards)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, cards));
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ThePlayedUnitRevealsTheHandOnlyOnResolutionThenDiscardsExactlyOneWithoutDrawing(string destination)
    {
        var game = new Driver(Position());
        Assert.DoesNotContain("OP-A", View(game.State)); Assert.DoesNotContain("OP-B", View(game.State));
        await game.Act("P1", new PlayCardCommand("UNIT", Dragon, [], Destination: destination));
        Assert.Equal(13, game.State.RunePools["P1"].Mana); Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain("OP-A", View(game.State));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("REVEALED_HAND_EFFECT", choice.ChoiceWindow); Assert.Equal("P1", choice.PlayerId);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "OP-A", "OP-B" }, choice.LegalObjectIds);
        Assert.Contains("OP-A", View(game.State)); Assert.Contains("OP-B", View(game.State));
        await Select(game, "OP-B");
        Assert.Equal(new[] { "OP-A" }, game.State.PlayerZones["P2"].Hand);
        Assert.Contains("OP-B", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.DoesNotContain("OP-A", View(game.State));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task ARealOpponentResponseChangesTheHandBeforeItIsRevealedAndChosen()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "OP-REACTION", "UNL-103/219", "P2", "HAND"));
        await game.Act("P1", new PlayCardCommand("UNIT", Dragon, []));
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("OP-REACTION", "UNL-103/219", [], Mode: "DRAW_1"));
        await game.Top();
        Assert.Contains("P2-DRAW-1", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("P2-DRAW-1", View(game.State));
        await game.Top();
        var choice = game.State.PendingCardChoice!;
        Assert.Contains("P2-DRAW-1", choice.LegalObjectIds); Assert.DoesNotContain("OP-REACTION", choice.LegalObjectIds);
        Assert.DoesNotContain("P2-DRAW-2", View(game.State));
        await Select(game, "P2-DRAW-1");
        Assert.Contains("P2-DRAW-1", game.State.PlayerZones["P2"].Graveyard);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task AnEmptyOpponentHandStillRevealsAndCompletesWithoutInventingADiscardOrDraw()
    {
        var game = new Driver(Position(empty: true));
        await game.Act("P1", new PlayCardCommand("UNIT", Dragon, []));
        await game.Top();
        Assert.Empty(game.State.PendingCardChoice!.LegalObjectIds);
        Assert.Equal(0, game.State.PendingCardChoice.RequiredCount);
        await Select(game);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "HAND_REVEALED");
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData("wrong-player")] [InlineData("decline")] [InlineData("two")]
    [InlineData("friendly")] [InlineData("deck")]
    public async Task TheControllersMandatoryChoiceRejectsForeignPlayersAndInvalidSelectionsWithoutMutation(string invalid)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Dragon, [])); await game.Top();
        var choice = game.State.PendingCardChoice!;
        var selected = invalid switch { "decline" => Array.Empty<string>(), "two" => ["OP-A", "OP-B"],
            "friendly" => ["ALLY"], "deck" => ["P2-DRAW-1"], _ => ["OP-A"] };
        await RadGearTestPosition.Reject(game.State, invalid == "wrong-player" ? "P2" : "P1",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        await Select(game, "OP-A");
        await game.Replay();
    }

    [Theory]
    [InlineData("OP-A")] [InlineData("OP-B")]
    public async Task TheCasterCannotPreselectAnUnrevealedHandCardAsAPlayTarget(string target)
    {
        var state = Position();
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("UNIT", Dragon, [target]));
        Assert.DoesNotContain(target, View(state));
    }

    [Fact]
    public async Task ReturningTheDragonDuringResponsesDoesNotCancelItsIndependentRevealedHandEffect()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("UNIT", Dragon, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Top(); await Select(game, "OP-B");
        Assert.Contains("OP-B", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(JsonSerializer.Serialize("UNIT"), JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Replay();
    }

    [Fact]
    public async Task DiscardedScrapHeapEarnsItsOwnDrawAfterTheDragonFinishesWithoutGrantingAnExtraDraw()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OP-B"] = OfficialGarenConquestBattlefieldCountTests.Printed("OP-B", "OGN·182/298", "P2") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Dragon, [])); await game.Top(); await Select(game, "OP-B");
        var trigger = Assert.Single(game.State.StackItems);
        Assert.Equal("OP-B", trigger.SourceObjectId); Assert.Equal("P2", trigger.ControllerId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Top();
        Assert.Equal(new[] { "OP-A", "P2-DRAW-1" }, game.State.PlayerZones["P2"].Hand);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRejectsForgedRevealedHandOwnerAndGeneration(bool generation)
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("UNIT", Dragon, [])); await game.Top();
        var choice = game.State.PendingCardChoice!; var context = choice.HandContext!;
        context = generation ? context with { Cards = new Dictionary<string, long>(context.Cards) { ["OP-A"] = 999 } }
            : context with { OwnerId = "P1" };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingCardChoice = choice with { HandContext = context } }));
    }
}
