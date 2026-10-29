using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenForgeBattlefieldTests
{
    [Theory]
    [InlineData("P1", 20)] [InlineData("P2", 19)] [InlineData(null, 19)]
    public async Task TheFirstFriendlyEquipmentActivationGetsADiscountOnlyWhileTheForgeIsControlled(string? controller, int remaining)
    {
        var initial = Book(Position(controller), "BOOK");
        var game = new Driver(initial);
        await Reject(game.State, "P2", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", []));
        await game.Act("P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", []));
        Assert.Equal(remaining, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task ASecondEquipmentPaysNormallyAndFailedActivationsDoNotConsumeTheFirst()
    {
        var initial = Book(Book(Position(), "BOOK"), "SECOND"); var game = new Driver(initial);
        await Reject(game.State, "P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", ["ALLY"]));
        await game.Act("P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(20, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new ActivateAbilityCommand("SECOND", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(19, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Fact]
    public async Task AFreeResourceAbilityConsumesTheFirstAndTheNextTurnResetsIt()
    {
        var initial = Book(Book(HandDiscardObserverTests.Put(Position(), "EGG", "VEN·075", "P1", "BASE"), "BOOK"), "LATER");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        var game = new Driver(initial); await game.Act("P1", new ActivateAbilityCommand("EGG", "EGG_REACTION_GAIN_MANA", []));
        Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new ActivateAbilityCommand("LATER", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Fact]
    public async Task AnEquipmentAbilityPlayedBeforeConqueringTheForgeAlreadyConsumedTheFirst()
    {
        var initial = Book(HandDiscardObserverTests.Put(Position(null), "EGG", "VEN·075", "P1", "BASE"), "BOOK");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        var game = new Driver(initial); await game.Act("P1", new ActivateAbilityCommand("EGG", "EGG_REACTION_GAIN_MANA", []));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal("P1", game.State.CardObjects["BF"].ControllerId);
        await game.Act("P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Fact]
    public async Task UnitAbilitiesNeitherReceiveNorConsumeTheEquipmentDiscount()
    {
        var initial = Book(HandDiscardObserverTests.Put(Position(), "UNIT-ABILITY", "VEN·014", "P1", "BASE"), "BOOK");
        var game = new Driver(initial); await game.Act("P1", new ActivateAbilityCommand("UNIT-ABILITY", "EMPOWER:0:0", [])); await game.Top();
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(18, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Fact]
    public async Task EquipmentEmpowermentUsesTheDiscountAndKeepsItsPaidReceipt()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "EGG", "VEN·075", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new ActivateAbilityCommand("EGG", "EMPOWER:0:0", []));
        Assert.Equal(20, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["EGG"].IsExhausted);
        var item = Assert.Single(game.State.StackItems); Assert.Equal(1, item.EmpowerActivation!.EquipmentManaReduction!.Amount);
        Assert.Equal(0, item.EmpowerActivation.PaidMana); await game.Top(); Assert.True(game.State.CardObjects["EGG"].IsEmpowered); await game.Replay();
    }

    [Fact]
    public async Task AssembleIsAnActivatedAbilityAndPaysOnlyItsUnreducedPower()
    {
        var initial = Book(HandDiscardObserverTests.Put(Position(), "AXE", "UNL-019/219", "P1", "BASE"), "BOOK");
        var game = new Driver(initial); await game.Act("P1", new AssembleEquipmentCommand("AXE", "ALLY", ["ASSEMBLE_RED"]));
        Assert.Equal(20, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.Equal("ALLY", game.State.CardObjects["AXE"].AttachedToObjectId);
        await game.Act("P1", new ActivateAbilityCommand("BOOK", "BOOK_REMOVE_EMPOWER_DRAW", [])); await game.Top();
        Assert.Equal(19, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    private static MatchState Position(string? controller = "P1")
    {
        var initial = VenUnitEntryBasicTests.Position("OGN·004/298");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        cards.Remove("UNIT"); locations.Remove("UNIT");
        cards["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "VEN·161", "P1") with { ControllerId = controller };
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } } };
        return controller is null ? initial : LocalDestructionRecallTests.At(initial,
            controller == "P1" ? "ALLY" : "ENEMY", controller, "BATTLEFIELD", "BF");
    }
    private static MatchState Book(MatchState state, string id)
    {
        state = HandDiscardObserverTests.Put(state, id, "VEN·054", "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = state.CardObjects[id] with { IsEmpowered = true } } };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-forge", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
