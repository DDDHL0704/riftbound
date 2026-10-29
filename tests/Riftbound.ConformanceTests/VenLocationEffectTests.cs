using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenLocationEffectTests
{
    [Theory]
    [InlineData("VEN·034", 37986, 2, 1)]
    [InlineData("VEN·066", 37758, 2, 1)]
    [InlineData("VEN·115", 37815, 8, 2)]
    public async Task RuntimeUsesTheCurrentOfficialPrintingAndPrintedPowerCost(string no, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.ReturnEnergy);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)); Assert.Equal(mana, behavior.ManaCost);
        Assert.Equal(power, PrintedPowerCostRules.ForCard(no).Amount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EchoingStrikeMovesFromAnotherLocationThenBuffsUntilTurnEnd(bool fromBattlefield)
    {
        var initial = Position("VEN·034");
        if (fromBattlefield) initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "OTHER") with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·034", ["BF", "ALLY"])); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(6, game.State.CardObjects["ALLY"].Power); Assert.Equal(0, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var effects = game.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(effects, "UNIT_MOVED_TO_BATTLEFIELD") < Array.IndexOf(effects, "POWER_MODIFIED_UNTIL_END_OF_TURN"));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("foreign-field")] [InlineData("same-location")] [InlineData("equipment")]
    [InlineData("enemy-unit")] [InlineData("reversed")] [InlineData("unpayable")]
    public async Task EchoingStrikeRejectsInvalidRelationsAndUnpayableCommandsWithoutChangingAnything(string branch)
    {
        var state = Position("VEN·034"); string[] targets = ["BF", "ALLY"];
        if (branch == "foreign-field") targets = ["OTHER", "ALLY"];
        if (branch == "same-location") state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        if (branch == "equipment") targets = ["BF", "GEAR"];
        if (branch == "enemy-unit") targets = ["BF", "ENEMY"];
        if (branch == "reversed") targets = ["ALLY", "BF"];
        if (branch == "unpayable") state = state with { RunePools = state.RunePools.ToDictionary(p => p.Key, p => RunePool.Empty) };
        await Reject(state, new PlayCardCommand("UNIT", "VEN·034", targets));
    }

    [Fact]
    public async Task MovementProhibitionDoesNotSkipTheIndependentMightInstruction()
    {
        var state = Position("VEN·034");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1")] } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·034", ["BF", "ALLY"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); Assert.Equal(6, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"); await game.Replay();
    }

    [Fact]
    public async Task LostBattlefieldControlInvalidatesOnlyThatTargetAndStillBuffsTheUnit()
    {
        var played = await OfficialGraveyardRecastTests.Act(Position("VEN·034"), "P1",
            new PlayCardCommand("UNIT", "VEN·034", ["BF", "ALLY"]));
        var changed = played.State with { CardObjects = new Dictionary<string, CardObjectState>(played.State.CardObjects) {
            ["BF"] = played.State.CardObjects["BF"] with { ControllerId = null } } };
        var resolved = await OfficialGraveyardRecastTests.Top(changed);
        Assert.Equal("BASE", resolved.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(6, resolved.State.CardObjects["ALLY"].Power);
    }

    [Fact]
    public async Task AReturnedUnitIsNoLongerAMovementOrMightTarget()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·034"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·034", ["BF", "ALLY"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["ALLY"].Zone); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"); await game.Replay();
    }

    [Fact]
    public async Task EchoingStrikeCanBeHiddenAndPlayedWithItsIndependentTwoTargetSelection()
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·034"), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = AddUnit(state, "MOVER", "OGN·219/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new HideCardCommand("UNIT", "VEN·034", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["UNIT"].IsFaceDown);
        await Reject(game.State, new PlayCardCommand("UNIT", "VEN·034", ["BF", "MOVER"]));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·034", ["BF", "MOVER"])); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["MOVER"].BattlefieldObjectId); Assert.Equal(6, game.State.CardObjects["MOVER"].Power);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:OTHER")]
    public async Task OceanDrakePlaysNormallyOrToAnOpenBattlefieldWithARespondableOptionalReturn(string destination)
    {
        var initial = OpenOther(Position("VEN·115")); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·115", ["ENEMY"], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base); Assert.Single(game.State.StackItems);
        Assert.Equal(12, game.State.RunePools["P1"].Mana);
        await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("occupied")] [InlineData("controlled")] [InlineData("forged")]
    public async Task OceanDrakeCannotUseItsOpenBattlefieldPermissionForOtherBattlefields(string branch)
    {
        var state = Position("VEN·115"); var destination = "BATTLEFIELD:OTHER";
        if (branch == "occupied") state = LocalDestructionRecallTests.At(OpenOther(state), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        if (branch == "forged") destination = "BATTLEFIELD:FORGED";
        await Reject(state, new PlayCardCommand("UNIT", "VEN·115", [], Destination: destination));
    }

    [Fact]
    public async Task OceanDrakeOptionalTargetsExcludeDragonsAndEquipmentButAllowDeclining()
    {
        var state = AddUnit(Position("VEN·115"), "DRAGON", "VEN·048", "P1"); var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·115", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.DoesNotContain("UNIT", choice.LegalObjectIds); Assert.DoesNotContain("DRAGON", choice.LegalObjectIds);
        Assert.DoesNotContain("GEAR", choice.LegalObjectIds);
        foreach (var target in new[] { "UNIT", "DRAGON", "GEAR" })
            await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    private static MatchState Position(string no) => VenUnitEntryBasicTests.Position(no);
    private static MatchState OpenOther(MatchState state) => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
        ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = null } } };
    private static MatchState AddUnit(MatchState state, string id, string no, string player)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == player ? p.Value with { Base = [..p.Value.Base, id] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") } };
    private static async Task Reject(MatchState state, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rejected-location", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
