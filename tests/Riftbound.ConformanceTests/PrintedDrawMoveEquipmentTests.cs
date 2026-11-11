using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedDrawMoveEquipmentTests
{
    private static async Task<Driver> Play(string no, Func<MatchState, MatchState>? arrange = null)
    {
        var initial = PrintedExhaustAbilityTests.Position(no);
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(),
                Hand = [..initial.PlayerZones["P1"].Hand, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
        initial = arrange?.Invoke(initial) ?? initial;
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        while (game.State.StackItems.Count > 0) await game.Top();
        return game;
    }

    private static MatchState Graveyard(MatchState state)
    {
        string[] ids = ["G0", "G1", "G2"];
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        string[] faces = ["OGN·219/298", "OGN·181/298", "OGN·104/298"];
        for (var i = 0; i < ids.Length; i++) {
            cards[ids[i]] = Printed(ids[i], faces[i], "P1");
            locations[ids[i]] = new("P1", "GRAVEYARD");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Graveyard = [..state.PlayerZones["P1"].Graveyard, ..ids] } } };
    }

    [Fact]
    public async Task WhizRecyclesThreeDifferentGraveyardCardsAndPaysManaBeforeDrawing()
    {
        var game = await Play("OGN·099/298", Graveyard);
        var before = game.State;
        var id = PrintedExhaustAbilityTests.Ability(before);
        string[] recycled = ["RECYCLE_GRAVEYARD_CARD:G0", "RECYCLE_GRAVEYARD_CARD:G1", "RECYCLE_GRAVEYARD_CARD:G2"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], OptionalCosts: recycled));
        Assert.Equal(before.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.RngCursor > before.RngCursor);
        Assert.Empty(game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "G0", "G1", "G2" }, game.State.PlayerZones["P1"].MainDeck.TakeLast(3).Order().ToArray());
        Assert.Equal(before.PlayerZones["P1"].Hand.Count, game.State.PlayerZones["P1"].Hand.Count);
        await game.Top();
        Assert.Equal(before.PlayerZones["P1"].Hand.Count + 1, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task WhizCannotRecycleTheSameCardTwiceOrUseAnOffGraveyardCard()
    {
        var game = await Play("OGN·099/298", Graveyard);
        var id = PrintedExhaustAbilityTests.Ability(game.State);
        foreach (var costs in new[] {
            new[] { "RECYCLE_GRAVEYARD_CARD:G0", "RECYCLE_GRAVEYARD_CARD:G0", "RECYCLE_GRAVEYARD_CARD:G2" },
            new[] { "RECYCLE_GRAVEYARD_CARD:G0", "RECYCLE_GRAVEYARD_CARD:G1", "RECYCLE_GRAVEYARD_CARD:ALLY" } })
            await Reject(game.State, new ActivateAbilityCommand("HOST", id, [], OptionalCosts: costs));
    }

    [Fact]
    public async Task WhizCannotExhaustOrRecycleTheGraveyardWithoutItsManaPayment()
    {
        var game = await Play("OGN·099/298", Graveyard);
        var id = PrintedExhaustAbilityTests.Ability(game.State);
        var noMana = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = new(0, 20) } };
        await Reject(noMana, new ActivateAbilityCommand("HOST", id, [], OptionalCosts:
            ["RECYCLE_GRAVEYARD_CARD:G0", "RECYCLE_GRAVEYARD_CARD:G1", "RECYCLE_GRAVEYARD_CARD:G2"]));
    }

    [Fact]
    public async Task PoroSnaxDrawsOnceWhenPlayedAndPaysDestructionBeforeItsSeparateDrawSkill()
    {
        var game = await Play("SFD·046/221");
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", PrintedExhaustAbilityTests.Ability(before), []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(before.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].PowerByTrait["green"] - 1, game.State.RunePools["P1"].PowerByTrait["green"]);
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PoroSnaxCannotDestroyItselfWithoutItsFullPrintedPayment(bool missingGreen)
    {
        var game = await Play("SFD·046/221");
        var id = PrintedExhaustAbilityTests.Ability(game.State);
        var noCost = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = missingGreen ? new(10, 0, new Dictionary<string, int> { ["blue"] = 20 }) : new(0, 0, new Dictionary<string, int> { ["green"] = 1 }) } };
        await Reject(noCost, new ActivateAbilityCommand("HOST", id, []));
    }

    [Fact]
    public async Task SirenMovesAFriendlyExhaustedBattlefieldUnitToBaseAfterPayingManaAndExhaustingEquipment()
    {
        var game = await Play("OGN·184/298", state => LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "F"));
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", PrintedExhaustAbilityTests.Ability(before), ["ALLY"]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(before.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task SirenDoesNotAllowAnEnemyOrAnAlreadyBasedFriendlyUnit()
    {
        var game = await Play("OGN·184/298", state => LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "F"));
        var id = PrintedExhaustAbilityTests.Ability(game.State);
        await Reject(game.State, new ActivateAbilityCommand("HOST", id, ["ENEMY"]));
        await Reject(LocalDestructionRecallTests.At(game.State, "ALLY", "P1", "BASE"), new ActivateAbilityCommand("HOST", id, ["ALLY"]));
    }

    [Fact]
    public async Task SirenCannotMoveANewHandObjectAfterItsDeclaredTargetReturnsInResponse()
    {
        var game = await Play("OGN·184/298", state => AttackDamageDistributionTests.Spell(
            LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "F"),
            "RESCUE", "OGN·104/298", "P1"));
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("HOST", PrintedExhaustAbilityTests.Ability(game.State), ["ALLY"]));
        if (game.State.PriorityPlayerId == "P2") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RESCUE", "OGN·104/298", ["ALLY"]));
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    private static async Task Reject(MatchState initial, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("bad-draw-move", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }
}
