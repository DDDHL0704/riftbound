using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BrinhilCardPlayProhibitionTests
{
    private const string Brinhil = "OGN·026/298", Meditation = "OGN·048/298", Apothecary = "UNL-021/219";
    private const string Lock = "CANNOT_PLAY_CARDS_THIS_TURN:P2";

    [Theory]
    [InlineData(Meditation)]
    [InlineData(Apothecary)]
    public async Task ResponsesAreLegalBeforeTheAbilityResolvesAndAllCardTypesAreBlockedAfterwards(string responseCard)
    {
        var game = new Driver(Position(responseCard));
        await game.Act("P1", new PlayCardCommand("UNIT", Brinhil, [], Destination: "BASE"));
        Assert.DoesNotContain(Lock, game.State.UntilEndOfTurnEffects);
        if (game.State.PriorityPlayerId != "P2") await game.Act("P1", new PassPriorityCommand());
        var response = Play("RESPONSE", responseCard);
        Assert.True(Quote(game.State, "P2", response).CanPay);
        await game.Act("P2", response);
        if (game.State.PendingCardChoice is { } optional)
            await game.Act("P2", new ChooseCardsCommand(optional.ChoiceId, optional.ChoiceWindow, []));
        while (game.State.StackItems[^1].SourceObjectId != "UNIT") await game.Top();
        await game.Top();
        Assert.Contains(Lock, game.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain("CANNOT_PLAY_CARDS_THIS_TURN:P1", game.State.UntilEndOfTurnEffects);

        // The controller can still play. The opponent has an ordinary response
        // window and an otherwise legal card, so timing cannot explain rejection.
        await game.Act("P1", new PlayCardCommand("FIRST", Meditation, []));
        if (game.State.PriorityPlayerId != "P2") await game.Act("P1", new PassPriorityCommand());
        var blocked = Play("NEXT", responseCard);
        var quote = Quote(game.State, "P2", blocked);
        Assert.False(quote.CanPay);
        Assert.Contains("不能打出卡牌", quote.Message);
        var prompt = ResolutionResult.BuildPrompts(game.State)["P2"];
        Assert.Contains("不能打出卡牌", prompt.Reason);
        Assert.DoesNotContain(prompt.Candidates ?? [], candidate => candidate.Action == CommandTypes.PlayCard
            && candidate.Enabled && candidate.Sources?.Any(source => source.Id == "NEXT") == true);
        await Reject(game.State, "P2", blocked);
        await game.Top();
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·001/298")]
    [InlineData("OGN·023/298")]
    [InlineData(Meditation)]
    public async Task TheRestrictionExpiresBeforeTheOpponentsTurnAndNormalPlaysResume(string cardNo)
    {
        var initial = Position(cardNo);
        var runes = Enumerable.Range(1, 5).Select(i => "RUNE-" + i).ToArray();
        foreach (var rune in runes)
            initial = RadGearTestPosition.Put(initial, rune, "UNL-R01", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Brinhil, [], Destination: "BASE"));
        await game.Top();
        Assert.Contains(Lock, game.State.UntilEndOfTurnEffects);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.DoesNotContain(Lock, game.State.UntilEndOfTurnEffects);
        await game.Act("P2", new TapRuneCommand(runes[0], runes));
        var command = new PlayCardCommand("NEXT", cardNo, [], Destination: "BASE");
        var quote = Quote(game.State, "P2", command);
        Assert.True(quote.CanPay, quote.Message);
        await game.Act("P2", command);
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("NEXT", cardNo == Meditation ? game.State.PlayerZones["P2"].Graveyard : game.State.PlayerZones["P2"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceDoesNotCancelTheIndependentAbilityOrItsTurnDuration()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Brinhil, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("RETREAT", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(Lock, game.State.UntilEndOfTurnEffects);
        await game.Top();
        Assert.Contains(Lock, game.State.UntilEndOfTurnEffects);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(Lock, game.State.UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAlreadyPendingEnemySpellStillResolvesButCannotUseAnEffectPlayPermission(bool enemyRevival)
    {
        var initial = InGraveyard(Position(), "UNIT", "P1");
        initial = RadGearTestPosition.Put(initial, "ALLY", "OGN·088/298", "P1", "BASE");
        initial = HandDiscardObserverTests.Put(initial, "REVIVE", "UNL-142/219", "P1", "HAND");
        if (enemyRevival)
        {
            initial = InGraveyard(initial, "NEXT", "P2");
            initial = RadGearTestPosition.Put(initial, "NEXT", "OGN·001/298", "P2", "GRAVEYARD");
            initial = RadGearTestPosition.Put(initial, "ENEMY", "OGN·088/298", "P2", "BASE");
            initial = HandDiscardObserverTests.Put(initial, "ENEMY-REVIVE", "UNL-142/219", "P2", "HAND");
        }
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", Meditation, []));
        if (game.State.PriorityPlayerId != "P2") await game.Act("P1", new PassPriorityCommand());
        var enemySource = enemyRevival ? "ENEMY-REVIVE" : "RESPONSE";
        await game.Act("P2", enemyRevival
            ? new PlayCardCommand(enemySource, "UNL-142/219", ["NEXT"], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:ENEMY"])
            : new PlayCardCommand(enemySource, Meditation, []));
        if (game.State.PriorityPlayerId != "P1") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("REVIVE", "UNL-142/219", ["UNIT"], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:ALLY"]));
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("UNIT", Brinhil, [], Destination: "BASE"));
        await game.Top();
        Assert.Contains(Lock, game.State.UntilEndOfTurnEffects);
        Assert.Contains(game.State.StackItems, item => item.SourceObjectId == enemySource);
        var hand = game.State.PlayerZones["P2"].Hand.Count;
        await game.Top();
        if (enemyRevival)
        {
            Assert.Contains("NEXT", game.State.PlayerZones["P2"].Graveyard);
            if (game.State.PendingEffectPlay is { } pending)
            {
                await Reject(game.State, "P2", new PlayCardCommand("NEXT", "OGN·001/298", [], Destination: "BASE"));
                Assert.Contains(ResolutionResult.BuildPrompts(game.State)["P2"].Candidates!,
                    candidate => candidate.Action == CommandTypes.ChooseCards && candidate.Enabled);
                await game.Act("P2", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            }
            Assert.DoesNotContain("NEXT", game.State.PlayerZones["P2"].Base);
        }
        else Assert.Equal(hand + 1, game.State.PlayerZones["P2"].Hand.Count);
        Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == enemySource);
        Assert.Contains(Lock, game.State.UntilEndOfTurnEffects);
        await game.Top();
        await game.Replay();
    }

    [Fact]
    public async Task TheOpponentCanStillTakeAResourceActionDuringAResponseWindow()
    {
        var initial = RadGearTestPosition.Put(Position(), "RUNE", "UNL-R01", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Brinhil, [], Destination: "BASE"));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("FIRST", Meditation, []));
        if (game.State.PriorityPlayerId != "P2") await game.Act("P1", new PassPriorityCommand());
        var mana = game.State.RunePools["P2"].Mana;
        await game.Act("P2", new TapRuneCommand("RUNE"));
        Assert.Equal(mana + 1, game.State.RunePools["P2"].Mana);
        Assert.True(game.State.CardObjects["RUNE"].IsExhausted);
        await game.Top();
        await game.Replay();
    }

    private static MatchState Position(string response = Meditation)
    {
        var initial = VenUnitEntryBasicTests.Position(Brinhil);
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = HandDiscardObserverTests.Put(initial, "FIRST", Meditation, "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "RETREAT", "OGN·104/298", "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "RESPONSE", response, "P2", "HAND");
        return HandDiscardObserverTests.Put(initial, "NEXT", response, "P2", "HAND");
    }

    private static MatchState InGraveyard(MatchState state, string source, string player)
        => state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { Hand = state.PlayerZones[player].Hand.Where(id => id != source).ToArray(),
                    Graveyard = [..state.PlayerZones[player].Graveyard, source] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [source] = new(player, "GRAVEYARD") } };

    private static PlayCardCommand Play(string source, string cardNo)
        => new(source, cardNo, [], Destination: cardNo == Apothecary ? "BATTLEFIELD:OTHER" : "BASE");

    private static PlayCostQuoteDto Quote(MatchState state, string player, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state, command));

    private static async Task Reject(MatchState state, string player, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("brinhil-rejected", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Contains("不能打出卡牌", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }
}
