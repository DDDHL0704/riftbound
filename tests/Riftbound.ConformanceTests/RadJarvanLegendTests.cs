using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadJarvanLegendTests
{
    private const string Standard = "RAD·156/167";
    private static MatchState Position(string legend = "RAD·155/167", string equipment = Standard)
    {
        var state = VenUnitEntryBasicTests.Position(equipment);
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", legend, "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND")
            }
        };
    }
    private static async Task Choose(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }
    private static async Task Pay(Driver game, string option = "PAY")
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [option]));
    }
    private static async Task Enter(Driver game, string field = "BF")
        => await game.Act("P1", new PlayCardCommand("UNIT", Standard, [], Destination: "BATTLEFIELD:" + field));

    [Theory]
    [InlineData("RAD·155/167", 39663L, "BF")]
    [InlineData("RAD·155/167", 39663L, "OTHER")]
    [InlineData("RAD·176*/167", 39692L, "BF")]
    [InlineData("RAD·176*/167", 39692L, "OTHER")]
    [InlineData("RAD·176/167", 39693L, "BF")]
    [InlineData("RAD·176/167", 39693L, "OTHER")]
    public async Task AllPrintingsCaptureDeploymentAndPayBothCostsBeforeResponseThenMove(string no, long id, string field)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("德玛西亚皇子", card.CardName);
        Assert.Contains("休眠状态并支付{{A}}", card.CardEffect);
        var game = new Driver(Position(no));
        await Enter(game, field);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", field), game.State.ObjectLocations["UNIT"]);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone);
        var captured = Assert.Single(game.State.StackItems).UnitEvent!;
        Assert.Equal(field, captured.DeploymentBattlefield!.ObjectId);
        Assert.Contains("ALLY", game.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("UNIT", game.State.PendingCardChoice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY", game.State.PendingCardChoice.LegalObjectIds);
        await Choose(game, "ALLY");
        Assert.Equal(1, game.State.PendingPayment!.PowerCost);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await Pay(game);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(1, Assert.Single(game.State.StackItems).TriggerCost!.Power);
        await game.Top();
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", field), game.State.ObjectLocations["ALLY"]);
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DecliningTargetOrPaymentKeepsLegendAndExtraPowerUnspent(bool payment)
    {
        var game = new Driver(Position()); await Enter(game);
        if (payment) { await Choose(game, "ALLY"); await Pay(game, "DECLINE"); }
        else await Choose(game);
        Assert.Empty(game.State.StackItems); Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); await game.Replay();
    }

    [Fact]
    public async Task InsufficientExtraPowerRejectsAtomicallyAndCanStillDecline()
    {
        var state = Position();
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } };
        var game = new Driver(state); await Enter(game); await Choose(game, "ALLY");
        var payment = game.State.PendingPayment!;
        await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted); await Pay(game, "DECLINE"); await game.Replay();
    }

    [Fact]
    public async Task AnExhaustedLegendCannotPayButTheEquipmentRemainsPlayed()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var game = new Driver(state); await Enter(game); await Choose(game, "ALLY");
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingPayment);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Theory]
    [InlineData("enemy")] [InlineData("equipment")] [InlineData("duplicate")] [InlineData("wrong-player")]
    public async Task IllegalTargetSelectionPreservesTheWholeState(string scenario)
    {
        var game = new Driver(Position()); await Enter(game);
        var choice = game.State.PendingCardChoice!;
        string[] targets = scenario switch { "enemy" => ["ENEMY"], "equipment" => ["UNIT"], "duplicate" => ["ALLY", "ALLY"], _ => ["ALLY"] };
        await Reject(game.State, scenario == "wrong-player" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        await Choose(game); await game.Replay();
    }

    [Fact]
    public async Task AResponseReturningTheChosenUnitLeavesBothCostsPaidWithoutMovingTheNewHandObject()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(Position(), "RETURN", "OGN·104/298"));
        await Enter(game); await Choose(game, "ALLY"); await Pay(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·224/298")]
    [InlineData("SFD·135/221")]
    public async Task AResponseRemovingTheEquipmentDoesNotChangeTheCapturedBattlefield(string no)
    {
        var state = LocalDestructionRecallTests.At(Position(equipment: "RAD·094/167"), "UNIT", "P1", "BATTLEFIELD", "BF");
        state = OfficialCopyIdentityTests.AddSpell(state, "REMOVE", no);
        state = state with {
            TurnPlayerId = "P2", ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = ["REMOVE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = true }
            }
        };
        var game = new Driver(state);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        for (var i = 0; game.State.FocusPlayerId is not null; i++) {
            Assert.InRange(i, 0, 3); await game.Act(game.State.FocusPlayerId, new PassFocusCommand());
        }
        await game.Top(); // Defending Bomb Monkey plays three deployment equipment tokens.
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; game.State.PendingCardChoice is not null || game.State.PendingPayment is not null; i++) {
            Assert.InRange(i, 0, 6);
            if (game.State.PendingCardChoice is not null)
                await Choose(game, game.State.CardObjects["LEGEND"].IsExhausted ? [] : ["ALLY"]);
            else await Pay(game);
        }
        var equipment = Assert.Single(game.State.StackItems).UnitEvent!.Other!.Binding.ObjectId;
        Assert.True(game.State.SpellDuelState.IsActive);
        if (game.State.PriorityPlayerId is { } priority && priority != "P1")
            await game.Act(priority, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("REMOVE", no, [equipment]));
        await game.Top(); Assert.False(game.State.CardObjects.ContainsKey(equipment));
        await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task ARealBombTokenPlayUsesTheSameDeploymentObserver()
    {
        var state = LocalDestructionRecallTests.At(Position(equipment: "RAD·112/167"), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [] } } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "RAD_112_EXHAUST_PLAY_BOMB", ["OTHER"])); await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.Equal(token.ObjectId, Assert.Single(game.State.StackItems).UnitEvent!.Other!.Binding.ObjectId);
        await Choose(game, "ALLY"); await Pay(game); await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task EquipmentWithoutDeployDoesNotTriggerTheLegend()
    {
        var game = new Driver(Position(equipment: "OGN·040/298"));
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·040/298", [], Destination: "BASE"));
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitEvent?.DeploymentBattlefield is not null);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task ADeploymentEquipmentCannotBePlayedToBaseOrTriggerTheLegend()
    {
        var game = new Driver(Position());
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", Standard, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task NoFriendlyUnitOutsideThePlayedBattlefieldSkipsTheOptionalSkill()
    {
        var state = LocalDestructionRecallTests.At(Position(), "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Enter(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("equipment-generation")] [InlineData("field-generation")]
    [InlineData("missing-field")] [InlineData("equipment-card")]
    public async Task RecoveryRejectsForgedEquipmentPlayOrigins(string scenario)
    {
        var game = new Driver(Position()); await Enter(game);
        var item = Assert.Single(game.State.StackItems); var c = item.UnitEvent!;
        var forged = scenario switch {
            "source" => c with { SourceGeneration = 99 },
            "equipment-generation" => c with { Other = c.Other! with { Binding = new("UNIT", 99) } },
            "field-generation" => c with { DeploymentBattlefield = new("BF", 99) },
            "missing-field" => c with { DeploymentBattlefield = null },
            _ => c with { Other = c.Other! with { CardNo = "SFD·022/221" } }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { UnitEvent = forged }] }));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("jarvan-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
