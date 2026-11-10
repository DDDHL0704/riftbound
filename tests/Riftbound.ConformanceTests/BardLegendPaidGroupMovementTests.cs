using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BardLegendPaidGroupMovementTests
{
    private const string Bard = "SFD·079/221";
    private const string Cost = "EXHAUST_LEGEND:LEGEND";

    private static MatchState Position(string no = Bard)
    {
        var state = OfficialHoldSequenceTests.AddLegend(RadGearTestPosition.Create("OGN·280/298"), "VEN·141");
        state = RadGearTestPosition.Put(state, "BARD", no, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "THEIR-MOVE", "OGN·043/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "OTHER", "OGN·280/298", "P2", "BATTLEFIELD", "OTHER");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1"),
            ["BF"] = state.CardObjects["BF"] with { ControllerId = null },
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = null } } };
    }

    private static Task Play(Driver game, string no = Bard, bool pay = true)
        => game.Act("P1", new PlayCardCommand("BARD", no, [], OptionalCosts: pay ? [Cost] : []));

    private static Task ChooseUnits(Driver game, params string[] ids)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId,
            game.State.PendingCardChoice.ChoiceWindow, ids));

    private static Task Destination(Driver game, string field = "BF")
    {
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("MOVEMENT_DESTINATION", request.Kind);
        return game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:" + field]));
    }

    [Theory, InlineData(Bard), InlineData("SFD·228/221"), InlineData("SFD·228*/221")]
    public async Task AnActualPaidPlayEntersBeforeAnIndependentGroupAndDestinationAreConfirmed(string no)
    {
        var game = new Driver(Position(no));
        Assert.Contains(Cost, JsonSerializer.Serialize(game.PromptFor("P1")));
        await Play(game, no);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Base);
        Assert.Equal(26, game.State.RunePools["P1"].Mana);
        Assert.Equal(29, game.State.RunePools["P1"].TotalPower);
        var skill = Assert.Single(game.State.StackItems);
        Assert.NotNull(skill.UnitEvent!.PaidLegendPlayCost);
        Assert.Null(game.State.PriorityPlayerId);
        Assert.Equal(0, game.State.PendingCardChoice!.RequiredCount);
        await ChooseUnits(game, "BARD", "ALLY");
        Assert.Null(game.State.PriorityPlayerId);
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Base);
        await Destination(game);
        Assert.Equal("P1", game.State.PriorityPlayerId);
        Assert.Equal("BATTLEFIELD:BF", Assert.Single(game.State.StackItems).Destination);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["BARD"].BattlefieldObjectId);
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["BARD"].IsExhausted);
        Assert.Single(game.Events(), record => record.Event.Kind == "LEGEND_PLAY_ADDITIONAL_COST_PAID");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DecliningTheOptionalCostDoesNotProduceAMovementSkillOrExhaustTheLegend()
    {
        var game = new Driver(Position()); await Play(game, pay: false);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingRuleChoice); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ARiftHeraldHandPlayStillChargesPowerAndMayPayTheLegendFee(bool payLegend)
    {
        var state = RadGearTestPosition.Put(Position(), "HERALD", RiftHeraldMovementLookTests.Herald, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["HERALD"])); await game.Top();
        Assert.Equal("LAST_BREATH", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top();
        var permission = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.True(permission.IgnoreBaseMana); Assert.False(permission.IgnoreBasePower);
        Assert.False(permission.IgnoreAllCosts); Assert.Contains("BARD", permission.Sources.Keys);
        Assert.Contains(Cost, JsonSerializer.Serialize(game.PromptFor("P1")));
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("BARD", Bard, [],
            OptionalCosts: payLegend ? [Cost] : [], Destination: "BASE"));
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Base);
        Assert.Equal(before.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(before.PaidPowerThisTurn + 1, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(payLegend, game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Null(game.State.PendingEffectPlay);
        if (payLegend)
        {
            Assert.NotNull(Assert.Single(game.State.StackItems).UnitEvent!.PaidLegendPlayCost);
            await ChooseUnits(game, "ALLY"); await Destination(game); await game.Top();
            Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        }
        else
        {
            Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        }
        Assert.Equal(payLegend ? 1 : 0, game.Events().Count(record => record.Event.Kind == "LEGEND_PLAY_ADDITIONAL_COST_PAID"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ChoosingNoUnitsDeclinesWithoutOpeningAPhantomDestination()
    {
        var game = new Driver(Position()); await Play(game); await ChooseUnits(game);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("exhausted"), InlineData("opponent"), InlineData("unit"), InlineData("missing"),
        InlineData("duplicate"), InlineData("wrong-type")]
    public async Task InvalidLegendFeesRejectWithoutPayingAnyResourcesOrMovingTheHandCard(string invalid)
    {
        var state = Position();
        if (invalid is "exhausted" or "opponent")
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = state.CardObjects["LEGEND"] with {
                    IsExhausted = invalid == "exhausted", ControllerId = invalid == "opponent" ? "P2" : "P1" } } };
        if (invalid == "wrong-type")
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "OGN·219/298", "P1") } };
        var costs = invalid switch {
            "unit" => new[] { "EXHAUST_LEGEND:ALLY" }, "missing" => ["EXHAUST_LEGEND:MISSING"],
            "duplicate" => [Cost, Cost], _ => [Cost] };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("BARD", Bard, [], OptionalCosts: costs));
    }

    [Fact]
    public async Task AResourceActionCannotAlsoSupplyTheSameLegendAsAnAdditionalFee()
    {
        var state = Position();
        var cost = "LEGEND_RESOURCE:LEGEND:LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA:POWER_SPEND:A:2";
        const string mana = "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:2";
        var ordinary = new Driver(state);
        await ordinary.Act("P1", new PlayCardCommand("BARD", Bard, [], OptionalCosts: [cost, mana]));
        Assert.True(ordinary.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Empty(ordinary.State.StackItems); Assert.Null(ordinary.State.PendingCardChoice);
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("BARD", Bard, [], OptionalCosts: [cost, mana, Cost]));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task OnlyCurrentlyEmptyAndUncontrolledFieldsAreOfferedAfterTheEntryCleanup(bool occupied)
    {
        var state = Position();
        if (occupied) state = RadGearTestPosition.Put(state, "OCCUPANT", "OGN·219/298", "P2", "BATTLEFIELD", "BF");
        else state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P1" } } };
        Assert.False(CoreRuleEngine.IsOpenBattlefield(state, "BF"));
        var game = new Driver(state); await Play(game); await ChooseUnits(game, "ALLY");
        var request = game.State.PendingRuleChoice!.Request;
        if (occupied)
        {
            Assert.DoesNotContain(request.Options, option => option.Id == "BATTLEFIELD:BF");
            await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:BF"]));
        }
        else
        {
            // CN323.5: the initial empty controlled field loses control in the
            // opening cleanup, before this independent declaration is made.
            Assert.True(CoreRuleEngine.IsOpenBattlefield(game.State, "BF"));
            Assert.Contains(request.Options, option => option.Id == "BATTLEFIELD:BF");
        }
        await Destination(game, "OTHER"); await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningThePaidSourceCannotCancelMovementOfOtherConfirmedUnits()
    {
        var game = new Driver(Position()); await Play(game); await ChooseUnits(game, "BARD", "ALLY");
        await Destination(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BARD"])); await game.Top();
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"BARD\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top();
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ARegularMovementSpellCannotInterruptTheConfirmedSkill()
    {
        var game = new Driver(Position()); await Play(game); await ChooseUnits(game, "ALLY"); await Destination(game);
        await game.Act("P1", new PassPriorityCommand());
        // Charm has neither Reaction nor another permission to interrupt a chain.
        await RadGearTestPosition.Reject(game.State, "P2",
            new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["BARD"], Destination: "BATTLEFIELD:BF"));
        Assert.Contains("THEIR-MOVE", game.State.PlayerZones["P2"].Hand);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task PayingTheLegendFeeDoesNotGrantAnOpenBattlefieldEntryPermissionToTheUnitItself()
        => await RadGearTestPosition.Reject(Position(), "P1",
            new PlayCardCommand("BARD", Bard, [], OptionalCosts: [Cost], Destination: "BATTLEFIELD:BF"));

    [Theory, InlineData("erase-fee"), InlineData("legend"), InlineData("payment-tick"),
        InlineData("destination"), InlineData("destination-generation"), InlineData("targets"), InlineData("erase-skill")]
    public async Task OriginalAcceptedPaymentTargetsAndDestinationCannotBeRewrittenTogether(string mutation)
    {
        var game = new Driver(Position()); await Play(game); await ChooseUnits(game, "ALLY"); await Destination(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var receipt = context.PaidLegendPlayCost!;
        var changed = item with {
            UnitEvent = context with { PaidLegendPlayCost = mutation switch {
                "erase-fee" => null, "legend" => receipt with { Legend = new("ALLY", 0) },
                "payment-tick" => receipt with { PaidTick = receipt.PaidTick + 1 }, _ => receipt } },
            Destination = mutation == "destination" ? "BATTLEFIELD:OTHER" : item.Destination,
            UnitEventMovementBattlefield = mutation switch {
                "destination" => new("OTHER", 0),
                "destination-generation" => item.UnitEventMovementBattlefield! with { Generation = 1 },
                _ => item.UnitEventMovementBattlefield },
            TargetObjectIds = mutation == "targets" ? ["BARD"] : item.TargetObjectIds,
            TargetGenerations = mutation == "targets" ? new Dictionary<string, long> { ["BARD"] = game.State.CardObjects["BARD"].ObjectGeneration }
                : item.TargetGenerations };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = mutation == "erase-skill" ? [] : [changed] }));
        await game.Top(); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TheGroupUsesOneSharedImposedMovementPaymentAndCanDeclineIt(bool pay)
    {
        var state = RadGearTestPosition.Put(Position(), "TAX", "RAD·062/167", "P2", "BATTLEFIELD", "BF");
        // PrintedCardFactory creates the card face. A field equipment fixture
        // also needs the Deploy keyword applied by the real equipment-entry flow.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TAX"] = state.CardObjects["TAX"] with {
                Tags = state.CardObjects["TAX"].Tags.Append(CardEquipmentKeywordNames.Deploy).Distinct().ToArray() } } };
        var game = new Driver(state); await Play(game); await ChooseUnits(game, "BARD", "ALLY");
        await Destination(game); await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", request.Kind);
        Assert.Equal(1, request.ManaCost); Assert.Equal(1, request.PowerCost);
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Base);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(pay ? "BF" : null, game.State.ObjectLocations["BARD"].BattlefieldObjectId);
        Assert.Equal(pay ? "BF" : null, game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(pay ? 25 : 26, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 28 : 29, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheSharedGroupMoveCarriesASelectedUnitsAttachedEquipment()
    {
        var state = RadGearTestPosition.Put(Position(), "GEAR", "SFD·022/221", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(state); await Play(game); await ChooseUnits(game, "ALLY");
        await Destination(game); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal("BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task WithNoOpenBattlefieldThePaidEntryHasNoTargetOrDestinationPrompt()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "OCCUPANT", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" },
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P2" } } };
        var game = new Driver(state); await Play(game);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Contains("BARD", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingRuleChoice); await game.ColdCheckpoint();
    }
}
