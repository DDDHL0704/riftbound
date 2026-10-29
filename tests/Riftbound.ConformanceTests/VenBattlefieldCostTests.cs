using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenBattlefieldCostTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task HaisuIgnoresWardForTheWholePaymentOnlyWhenAnObjectHereIsSelected(bool here)
    {
        var initial = Field("VEN·158", "OGN·248/298");
        if (here) initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [..initial.CardObjects["ENEMY"].Tags, "法盾2"] } } };
        var command = new PlayCardCommand("UNIT", "OGN·248/298", ["ALLY", "ENEMY", "ENEMY", "ENEMY", "ENEMY", "ENEMY"]);
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(here ? 3 : 13, quote.Cost!.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.Equal(here ? 17 : 7, game.State.RunePools["P1"].TotalPower);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID");
        Assert.Equal(here ? 0 : 10, paid.Event.Payload["spellshieldTaxPower"]);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MysticVortexAddsPowerForReactionCardsDuringItsDuelButNotForSwiftCards(bool reaction)
    {
        var initial = Field("VEN·160", reaction ? "OGN·048/298" : "OGN·004/298");
        initial = EnemyField(initial);
        var game = new Driver(initial); await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.SpellDuelState.IsActive); Assert.Equal("BF", game.State.SpellDuelState.BattlefieldObjectId);
        var command = new PlayCardCommand("UNIT", reaction ? "OGN·048/298" : "OGN·004/298", reaction ? [] : ["ALLY"]);
        var quote = Quote(game.State, command); Assert.True(quote.CanPay, quote.Message); Assert.Equal(reaction ? 1 : 0, quote.Cost!.GenericPower);
        if (reaction) await Reject(game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = new(20, 0) } }, "P1", command);
        await game.Act("P1", command); Assert.Equal(reaction ? 19 : 20, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AReactionOutsideTheVortexDuelPaysItsNormalPrice()
    {
        var initial = Field("VEN·160", "OGN·048/298"); var quote = Quote(initial, new("UNIT", "OGN·048/298", []));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.GenericPower);
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "OGN·048/298", [])); await game.Top();
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task AStandbyCardHasReactionAndStillPaysTheVortexIncreaseAfterItsBaseCostIsWaived()
    {
        var initial = Put(Field("VEN·160", "OGN·057/298"), "RUNE", "UNL-R01", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P1", new HideCardCommand("UNIT", "OGN·057/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P2", new PassFocusCommand());
        var command = new PlayCardCommand("UNIT", "OGN·057/298", ["ALLY"]);
        var quote = Quote(game.State, command); Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower); Assert.False(quote.CanPay);
        await game.Act("P1", new RecycleRuneCommand("RUNE"));
        await game.Act("P1", command); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.NotNull(Assert.Single(game.State.StackItems).PlayedFromStandby); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("friendly-here", 2)] [InlineData("friendly-away", 3)] [InlineData("enemy-here", 3)]
    public async Task SandsweptTombReducesTheWholeSpellOnceForAFriendlyTargetHere(string branch, int power)
    {
        var initial = Field("VEN·164", "OGN·248/298");
        var target = branch == "enemy-here" ? "ENEMY" : "ALLY";
        if (branch == "friendly-here") initial = LocalDestructionRecallTests.At(initial, target, "P1", "BATTLEFIELD", "BF");
        if (branch == "enemy-here") initial = EnemyField(initial);
        var command = new PlayCardCommand("UNIT", "OGN·248/298", Enumerable.Repeat(target, 6).ToArray());
        var quote = Quote(initial, command); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(power, quote.Cost!.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(initial); await game.Act("P1", command); Assert.Equal(20 - power, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("MANA", 19, 19)] [InlineData("POWER:red", 18, 20)]
    public async Task AscensionAltarOffersEitherResourceReductionAndKeepsThePaidCostAfterMovement(string reduction, int mana, int power)
    {
        var initial = Field("VEN·163", "VEN·014");
        initial = LocalDestructionRecallTests.At(initial, "UNIT", "P1", "BATTLEFIELD", "BF");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } } };
        initial = HandDiscardObserverTests.Put(initial, "BOUNCE", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        var id = reduction == "MANA" ? "EMPOWER:0:0:REDUCE_MANA" : "EMPOWER:0:0:REDUCE_POWER:red";
        Assert.Contains(id, JsonSerializer.Serialize(game.PromptFor("P1")));
        await Reject(game.State, "P1", new ActivateAbilityCommand("ALLY", id, []));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", id, []));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.NotNull(Assert.Single(game.State.StackItems).EmpowerActivation!.BattlefieldReduction);
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
    }

    [Fact]
    public async Task AscensionAltarDoesNotReduceUnitsInTheBase()
    {
        var initial = Field("VEN·163", "VEN·014"); initial = LocalDestructionRecallTests.At(initial, "UNIT", "P1", "BASE");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } } };
        await Reject(initial, "P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0:REDUCE_MANA", []));
        var game = new Driver(initial); await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
    }

    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
    private static MatchState Field(string no, string source)
    {
        var initial = VenUnitEntryBasicTests.Position(source);
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", no, "P1") with { ControllerId = "P1" } } };
    }
    private static MatchState EnemyField(MatchState initial)
    {
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "BF");
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
    }
    private static MatchState Put(MatchState state, string id, string no, string player) => HandDiscardObserverTests.Put(state, id, no, player, "BASE");
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-battlefield-cost", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
