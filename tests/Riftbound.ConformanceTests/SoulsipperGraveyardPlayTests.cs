using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SoulsipperGraveyardPlayTests
{
    private const string Soulsipper = "OGN·196/298", Volibear = "OGN·041/298";

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task TheResolvedAbilityOffersARealPlayWithManaWaivedAndPrintedPowerStillPaid(string destination)
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(8, 0, powerByTrait: new Dictionary<string, int> { [RuneTrait.Purple] = 2, [RuneTrait.Red] = 2 }),
            ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Soulsipper, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("CHOSEN", game.State.PlayerZones["P1"].Graveyard);
        await Target(game, "CHOSEN");
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
        var play = new PlayCardCommand("CHOSEN", Volibear, [], Destination: destination);
        var quote = Quote(game.State, play);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", play);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain("CHOSEN", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("CHOSEN", destination == "BASE" ? game.State.PlayerZones["P1"].Base : game.State.PlayerZones["P1"].Battlefields);
        Assert.True(game.State.CardObjects["CHOSEN"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Single(game.State.CardPlays.Resolved, p => p.Source.ObjectId == "CHOSEN");
        await Reject(game.State, "P1", new PlayCardCommand("GRAVE", "OGN·219/298", [], Destination: "BASE"));
        await game.Replay();
    }

    [Fact]
    public async Task MissingPrintedPowerRejectsWithoutConsumingTheOptionalPermission()
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(8, 0, powerByTrait: new Dictionary<string, int> { [RuneTrait.Purple] = 2 }), ["P2"] = new(20, 20) } };
        var game = await Open(initial);
        var play = new PlayCardCommand("CHOSEN", Volibear, [], Destination: "BASE");
        Assert.False(Quote(game.State, play).CanPay);
        await Reject(game.State, "P1", play);
        Assert.NotNull(game.State.PendingEffectPlay);
        await Decline(game);
        Assert.Contains("CHOSEN", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task TheControllerMayDeclineTheTriggerWhenACompletelyFreeUnitIsAvailable()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Soulsipper, [], Destination: "BASE"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("GRAVE", choice.LegalObjectIds);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task AConfirmedFreePlayCannotBeRefusedAsANonzeroPayment()
    {
        var game = await Open(Position(), "GRAVE");
        var pending = game.State.PendingEffectPlay!;
        await Reject(game.State, "P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await game.Act("P1", new PlayCardCommand("GRAVE", "OGN·219/298", [], Destination: "BASE"));
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task NoEligibleGraveyardUnitFinishesTheAbilityWithoutAChoice()
    {
        var initial = Position();
        var grave = initial.PlayerZones["P1"].Graveyard;
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Graveyard = [], Banished = grave } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key,
                p => grave.Contains(p.Key) ? new ObjectLocationState("P1", "BANISHED") : p.Value) };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Soulsipper, [], Destination: "BASE"));
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task TheIndependentAbilityStillOffersThePlayAfterItsSourceIsReturned()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Soulsipper, [], Destination: "BASE"));
        await Target(game, "GRAVE");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("GRAVE", "OGN·219/298", [], Destination: "BASE"));
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Base);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ThePermissionOnlyOffersTheControllersGraveyardUnitsAndRejectsOtherSources()
    {
        var initial = RadGearTestPosition.Put(Position(), "GRAVE-GEAR", "OGN·023/298", "P1", "GRAVEYARD");
        initial = RadGearTestPosition.Put(initial, "GRAVE-SPELL", "OGN·048/298", "P1", "GRAVEYARD");
        initial = RadGearTestPosition.Put(initial, "ENEMY-GRAVE", "OGN·219/298", "P2", "GRAVEYARD");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Soulsipper, [], Destination: "BASE"));
        Assert.Equal(new[] { "CHOSEN", "GRAVE" }, game.State.PendingCardChoice!.LegalObjectIds.Order().ToArray());
        await Target(game, "CHOSEN");
        await game.Top();
        var pending = game.State.PendingEffectPlay!;
        Assert.Equal(new[] { "CHOSEN" }, pending.Sources.Keys.Order().ToArray());
        foreach (var (source, no, player, destination) in new[] {
            ("GRAVE-GEAR", "OGN·023/298", "P1", "BASE"), ("GRAVE-SPELL", "OGN·048/298", "P1", "BASE"),
            ("ENEMY-GRAVE", "OGN·219/298", "P1", "BASE"), ("ALLY", "OGN·219/298", "P1", "BASE"),
            ("CHOSEN", Volibear, "P2", "BASE"), ("CHOSEN", Volibear, "P1", "BATTLEFIELD:OTHER"),
            ("GRAVE", "OGN·219/298", "P1", "BASE") })
            await Reject(game.State, player, new PlayCardCommand(source, no, [], Destination: destination));
        await Decline(game);
        await game.Replay();
    }

    private static MatchState Position()
    {
        var initial = RadGearTestPosition.Put(VenUnitEntryBasicTests.Position(Soulsipper), "CHOSEN", Volibear, "P1", "GRAVEYARD");
        return initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 20, powerByTrait: new Dictionary<string, int> { [RuneTrait.Purple] = 20, [RuneTrait.Red] = 20 }) } };
    }
    private static async Task<Driver> Open(MatchState initial, string source = "CHOSEN")
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Soulsipper, [], Destination: "BASE"));
        await Target(game, source);
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        return game;
    }
    private static async Task Target(Driver game, string source)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [source]));
    }
    private static async Task Decline(Driver game)
    {
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
    }
    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("soulsipper-reject", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }
}
