using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadRivenReformedTests
{
    private const string Ability = "RAD_114_EXHAUST_FRIENDLY_READY_UNIT";
    private static MatchState Position(string no = "RAD·114/167", bool exhausted = true)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        return s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Hand = [], Base = [..s.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BASE") },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["UNIT"] = s.CardObjects["UNIT"] with { IsExhausted = exhausted } } };
    }
    private static ActivateAbilityCommand Activate(string cost = "ALLY", string target = "UNIT")
        => new("UNIT", Ability, [target], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:" + cost]);
    private static async Task ResolveAll(Driver game)
    {
        for (var i = 0; game.State.StackItems.Count > 0; i++) { Assert.True(i < 4); await game.Top(); }
    }

    [Theory]
    [InlineData("RAD·114/167", 39618L)] [InlineData("RAD·114a/167", 39619L)]
    public async Task BothOfficialPrintingsPayTheirPrintedCostsWithoutObservingExhaustedEntry(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("弃暗投明", card.SubTitle);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·114/167")] [InlineData("RAD·114a/167")]
    public async Task AnotherFriendlyUnitPaysBeforeTheReadyAndEarnsAnIndependentTurnBonus(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, i => i.UnitEvent?.Timing == "ANOTHER_FRIENDLY_UNIT_EXHAUSTED");
        await ResolveAll(game);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        await Reject(game.State, Activate("UNIT", "ALLY"));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNIT", "UNIT", false)] [InlineData("UNIT", "ALLY", true)] [InlineData("ALLY", "ALLY", false)]
    public async Task TheSourceMayPayAndTheCostUnitMayAlsoBeTheReadyTarget(string cost, string target, bool sourceStaysExhausted)
    {
        var game = new Driver(Position(exhausted: false));
        await game.Act("P1", Activate(cost, target));
        Assert.True(game.State.CardObjects[cost].IsExhausted); await ResolveAll(game);
        Assert.False(game.State.CardObjects[target].IsExhausted);
        Assert.Equal(sourceStaysExhausted, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(cost == "UNIT" ? 5 : 6, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("enemy")] [InlineData("equipment")] [InlineData("exhausted")] [InlineData("target")] [InlineData("tax")]
    public async Task InvalidCostsTargetsAndUnpayableTaxCannotExhaustAnything(string branch)
    {
        var s = Position();
        if (branch == "exhausted") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ALLY"] = s.CardObjects["ALLY"] with { IsExhausted = true } } };
        if (branch == "tax") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["ENEMY"] = s.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var cost = branch switch { "enemy" => "ENEMY", "equipment" => "GEAR", "missing" => "missing", _ => "ALLY" };
        await Reject(s, Activate(cost, branch == "target" ? "GEAR" : branch == "tax" ? "ENEMY" : "UNIT"));
    }

    [Fact]
    public async Task AnEnemyUnitCanBeReadiedAfterItsSpellshieldTaxIsPaid()
    {
        var s = Position(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY"] = s.CardObjects["ENEMY"] with { IsExhausted = true, Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var game = new Driver(s); await game.Act("P1", Activate(target: "ENEMY")); await ResolveAll(game);
        Assert.False(game.State.CardObjects["ENEMY"].IsExhausted); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Fact]
    public async Task ExhaustingOnlyEquipmentDoesNotCountAsExhaustingAnotherUnit()
    {
        var s = Position(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "VEN·054", "P1") with { IsEmpowered = true } } };
        var game = new Driver(s); await game.Act("P1", new ActivateAbilityCommand("GEAR", "BOOK_REMOVE_EMPOWER_DRAW", []));
        Assert.Single(game.State.StackItems); Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null);
        await ResolveAll(game); Assert.Equal(5, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-riven", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
