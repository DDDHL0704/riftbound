using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LedrosMultipleSacrificeTests
{
    private const string Ledros = "OGN·231/298";
    private const string Prefix = "DESTROY_FRIENDLY_UNIT:";

    [Theory]
    [InlineData(0, "BASE")]
    [InlineData(0, "BATTLEFIELD:BF")]
    [InlineData(1, "BASE")]
    [InlineData(1, "BATTLEFIELD:BF")]
    [InlineData(2, "BASE")]
    [InlineData(2, "BATTLEFIELD:BF")]
    [InlineData(4, "BASE")]
    [InlineData(4, "BATTLEFIELD:BF")]
    [InlineData(5, "BASE")]
    [InlineData(5, "BATTLEFIELD:BF")]
    public async Task EachSelectedUnitReducesOneYellowPowerAndEveryChosenUnitIsPaidEvenPastZero(int count, string destination)
    {
        var yellow = Math.Max(0, 4 - count);
        var initial = WithPool(Position(count), new(6, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = yellow }));
        var game = new Driver(initial);
        var selected = Enumerable.Range(1, count).Select(i => Prefix + "VICTIM-" + i).Reverse().ToArray();
        var command = new PlayCardCommand("UNIT", Ledros, [], Destination: destination, OptionalCosts: selected);
        var hash = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(6, quote.Cost!.Mana);
        Assert.Equal(yellow, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        Assert.Equal(hash, MatchStateHasher.Hash(initial));
        await game.Act("P1", command);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(8, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.All(selected, cost => Assert.Contains(cost[Prefix.Length..], game.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(count, game.Events().Count(e => e.Event.Kind == "UNIT_DESTROYED"));
        await game.Replay();
    }

    [Fact]
    public async Task APlayOnlyAffordableWithSeveralSacrificesOffersEveryCandidateAndQuotesTheActualSubset()
    {
        var initial = WithPool(Position(4), new(6, 0));
        var play = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(play.Sources!, s => s.Id == "UNIT");
        for (var i = 1; i <= 4; i++) Assert.Contains(play.OptionalCosts!, c => c.Id == Prefix + "VICTIM-" + i);
        var engine = new CoreRuleEngine();
        var ordinary = new PlayCardCommand("UNIT", Ledros, []);
        Assert.False(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, ordinary)).CanPay);
        var shortPayment = ordinary with { OptionalCosts = [Prefix + "VICTIM-1", Prefix + "VICTIM-2", Prefix + "VICTIM-3"] };
        Assert.False(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, shortPayment)).CanPay);
        var complete = ordinary with { OptionalCosts = Enumerable.Range(1, 4).Select(i => Prefix + "VICTIM-" + i).ToArray() };
        Assert.True(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, complete)).CanPay);
        var game = new Driver(initial); await game.Act("P1", complete); await game.Replay();
    }

    [Fact]
    public async Task DuplicateEnemyEquipmentHandMissingAndExplicitTargetSelectionsRejectAtomically()
    {
        var initial = Position(2);
        foreach (var selection in new[] {
            new[] { Prefix + "VICTIM-1", Prefix + "VICTIM-1" },
            new[] { Prefix + "VICTIM-1", Prefix + "ENEMY" },
            new[] { Prefix + "GEAR" }, new[] { Prefix + "UNIT" }, new[] { Prefix + "MISSING" } })
            await Reject(initial, new("UNIT", Ledros, [], OptionalCosts: selection));
        await Reject(initial, new("UNIT", Ledros, ["ALLY"], OptionalCosts: [Prefix + "VICTIM-1"]));
    }

    [Fact]
    public async Task ExistingObjectIdWhitespaceNormalizationIsPreservedAndAliasesCannotPayTwice()
    {
        var initial = WithPool(Position(1), new(6, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 3 }));
        await Reject(initial, new("UNIT", Ledros, [],
            OptionalCosts: [Prefix + "VICTIM-1", Prefix + "  VICTIM-1  "]));
        // Core parsing preserves the existing trimmed object ID contract. The
        // session wire format separately requires canonical choice strings.
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(initial, new("ledros-trimmed", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", Ledros, [], OptionalCosts: ["  " + Prefix + "  VICTIM-1  "]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("VICTIM-1", result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Ledros, [], OptionalCosts: [Prefix + "VICTIM-1"]));
        Assert.Contains("VICTIM-1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task RedPowerCannotReplaceTheRemainingYellowAndIsPreservedWhenFourUnitsPayAllYellow()
    {
        var initial = WithPool(Position(4), new(6, 0, new Dictionary<string, int> { [RuneTrait.Red] = 4 }));
        await Reject(initial, new("UNIT", Ledros, [], OptionalCosts: [Prefix + "VICTIM-1", Prefix + "VICTIM-2", Prefix + "VICTIM-3"]));
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Ledros, [],
            OptionalCosts: Enumerable.Range(1, 4).Select(i => Prefix + "VICTIM-" + i).ToArray()));
        Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        await game.Replay();
    }

    [Fact]
    public async Task AControlledOpponentOwnedVictimPaysOneAndGoesToItsOwnersGraveyard()
    {
        var initial = Position(1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM-1"] = initial.CardObjects["VICTIM-1"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Ledros, [], OptionalCosts: [Prefix + "VICTIM-1"]));
        Assert.Contains("VICTIM-1", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("VICTIM-1", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task ARealRecallReplacementKeepsBothDeclaredReductionsEvenThoughOnlyOneVictimDies()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(2), "SAVE", "UNL-175/219", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SAVE", "UNL-175/219", ["VICTIM-1"]));
        await game.Top();
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].TotalPower;
        var command = new PlayCardCommand("UNIT", Ledros, [], OptionalCosts: [Prefix + "VICTIM-1", Prefix + "VICTIM-2"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(6, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        await game.Act("P1", command);
        Assert.Contains("VICTIM-1", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["VICTIM-1"].IsExhausted);
        Assert.Contains("VICTIM-2", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(mana - 6, game.State.RunePools["P1"].Mana);
        Assert.Equal(power - 2, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task ALastBreathVictimQueuesItsNormalResponseChainAfterTheUnitAndCostsAreConfirmed()
    {
        var initial = Position(2);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("VICTIM-1", "OGN·096/298", "P1") } };
        var game = new Driver(initial);
        var deck = initial.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P1", new PlayCardCommand("UNIT", Ledros, [], OptionalCosts: [Prefix + "VICTIM-2", Prefix + "VICTIM-1"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Contains("VICTIM-1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.NotEmpty(game.State.StackItems);
        await game.Top();
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        await game.Replay();
    }

    private static MatchState Position(int victims)
    {
        var state = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(Ledros), "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 1; i <= victims; i++) state = HandDiscardObserverTests.Put(state, "VICTIM-" + i, "OGN·219/298", "P1", "BASE");
        return state;
    }
    private static MatchState WithPool(MatchState state, RunePool pool) => state with {
        RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var engine = new CoreRuleEngine();
        Assert.False(engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).CanPay);
        var result = await engine.ResolveAsync(state, new("ledros-invalid", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
