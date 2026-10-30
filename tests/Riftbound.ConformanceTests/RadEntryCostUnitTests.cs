using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEntryCostUnitTests
{
    public static TheoryData<string, long, int, int, int> Cards => new()
    {
        { "RAD·005/167", 39495, 2, 0, 2 }, { "RAD·026/167", 39519, 2, 0, 2 },
        { "RAD·066/167", 39564, 6, 1, 4 }, { "RAD·072/167", 39571, 2, 0, 2 },
        { "RAD·075/167", 39574, 5, 0, 4 }
    };

    [Theory, MemberData(nameof(Cards))]
    public async Task ExactOfficialPrintingPaysItsPrintedCostsAndEntersExhaustedWithoutOptionalChoices(string no, long id, int mana, int powerCost, int power)
    {
        var official = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(id, official.Id); Assert.Equal(mana, official.Energy); Assert.Equal(powerCost, official.ReturnEnergy ?? 0); Assert.Equal(power, official.Power);
        var driver = new Driver(VenUnitEntryBasicTests.Position(no));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(power, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(20 - mana, driver.State.RunePools["P1"].Mana); Assert.Equal(20 - powerCost, driver.State.RunePools["P1"].TotalPower);
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        if (no == "RAD·075/167") Assert.Contains(CardObjectTags.Spellshield, driver.State.CardObjects["UNIT"].Tags);
        await driver.Replay();
    }

    [Theory]
    [InlineData("RAD·005/167")] [InlineData("RAD·026/167")] [InlineData("RAD·066/167")]
    [InlineData("RAD·072/167")] [InlineData("RAD·075/167")]
    public async Task IllegalEntryDestinationAndUnrecognizedChoiceCannotChangeAnyState(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:OTHER"));
        await Reject(state, "P2", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["INVENTED_COST"]));
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", no, []));
    }

    [Theory]
    [InlineData("BASE", false)] [InlineData("BASE", true)]
    [InlineData("BATTLEFIELD:BF", false)] [InlineData("BATTLEFIELD:BF", true)]
    public async Task OptionalReadyEntryProhibitsEveryPlayerFromMovingItForThisTurnOnly(string destination, bool ready)
    {
        const string no = "RAD·026/167"; var driver = new Driver(VenUnitEntryBasicTests.Position(no));
        Assert.Contains("ENTER_READY_CANNOT_MOVE", JsonSerializer.Serialize(driver.PromptFor("P1")));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination,
            OptionalCosts: ready ? ["ENTER_READY_CANNOT_MOVE"] : []));
        var unit = driver.State.CardObjects["UNIT"]; Assert.Equal(!ready, unit.IsExhausted);
        foreach (var player in new[] { "P1", "P2" }) Assert.Equal(!ready, MovementRestrictionRules.CanMove(unit, player));
        if (ready) await Reject(driver.State, "P1", new MoveUnitCommand("UNIT", destination, destination == "BASE" ? "BATTLEFIELD:BF" : "BASE"));
        await driver.Act("P1", new EndTurnCommand());
        Assert.True(MovementRestrictionRules.CanMove(driver.State.CardObjects["UNIT"], "P1"));
        Assert.True(MovementRestrictionRules.CanMove(driver.State.CardObjects["UNIT"], "P2"));
        await driver.Replay();
    }

    [Theory]
    [InlineData("RAD·066/167", "BASE")] [InlineData("RAD·066/167", "BATTLEFIELD:BF")]
    [InlineData("RAD·075/167", "BASE")] [InlineData("RAD·075/167", "BATTLEFIELD:BF")]
    public async Task ReturningEquipmentPaysBeforeEntryAndTheSpiritSkillWaitsForResponses(string no, string destination)
    {
        var initial = VenUnitEntryBasicTests.Position(no); var gearMana = initial.CardObjects["GEAR"].ManaCost;
        var driver = new Driver(initial);
        Assert.Contains("RETURN_FRIENDLY_EQUIPMENT:GEAR", JsonSerializer.Serialize(driver.PromptFor("P1")));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination, OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        Assert.Contains("GEAR", driver.State.PlayerZones["P1"].Hand); Assert.DoesNotContain("GEAR", driver.State.PlayerZones["P1"].Base);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(no == "RAD·066/167" ? 14 : 17, driver.State.RunePools["P1"].Mana);
        if (no == "RAD·066/167")
        {
            Assert.Equal(4, driver.State.CardObjects["UNIT"].Power); Assert.Single(driver.State.StackItems);
            Assert.Equal(gearMana, driver.State.StackItems[0].ReturnedEquipmentManaCost);
            await driver.Top(); Assert.False(driver.State.CardObjects["UNIT"].IsExhausted);
            Assert.Equal(4 + gearMana, driver.State.CardObjects["UNIT"].Power);
            await driver.Act("P1", new EndTurnCommand()); Assert.Equal(4, driver.State.CardObjects["UNIT"].Power);
        }
        else Assert.Empty(driver.State.StackItems);
        await driver.Replay();
    }

    [Theory]
    [InlineData("RAD·066/167", "unit")] [InlineData("RAD·066/167", "enemy")] [InlineData("RAD·066/167", "hidden")]
    [InlineData("RAD·066/167", "missing")] [InlineData("RAD·066/167", "multiple")]
    [InlineData("RAD·075/167", "unit")] [InlineData("RAD·075/167", "enemy")] [InlineData("RAD·075/167", "hidden")]
    [InlineData("RAD·075/167", "missing")] [InlineData("RAD·075/167", "multiple")]
    public async Task EquipmentReturnRejectsIllegalCostsBeforePayingAnything(string no, string branch)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (branch == "enemy") state = LocalDestructionRecallTests.At(state, "GEAR", "P2", "BASE");
        if (branch == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsFaceDown = true } } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: branch == "multiple"
            ? ["RETURN_FRIENDLY_EQUIPMENT:GEAR", "RETURN_FRIENDLY_EQUIPMENT:ALLY"]
            : ["RETURN_FRIENDLY_EQUIPMENT:" + (branch == "unit" ? "ALLY" : branch == "missing" ? "MISSING" : "GEAR")]));
    }

    [Theory]
    [InlineData("unit", "P1", true)] [InlineData("unit", "P2", false)]
    [InlineData("equipment", "P1", true)] [InlineData("equipment", "P2", false)]
    [InlineData("stolen-unit", "P1", true)] [InlineData("stolen-equipment", "P1", true)]
    public async Task FriendlyDestructionUsesTheControllerAtDestructionForUnitsAndEquipment(string kind, string controller, bool ready)
    {
        const string no = "RAD·005/167"; var state = VenUnitEntryBasicTests.Position(no);
        var equipment = kind.Contains("equipment"); var target = equipment ? "GEAR" : "ALLY";
        state = LocalDestructionRecallTests.At(state, target, controller, "BASE");
        if (kind.StartsWith("stolen")) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [target] = state.CardObjects[target] with { OwnerId = "P2" } } };
        state = Hand(state, "KILL", equipment ? "VEN·003" : "OGN·229/298");
        var driver = new Driver(state); await driver.Act("P1", new PlayCardCommand("KILL", equipment ? "VEN·003" : "OGN·229/298", [target]));
        await driver.Top(); await driver.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(!ready, driver.State.CardObjects["UNIT"].IsExhausted); await driver.Replay();
    }

    [Theory]
    [InlineData("base", 2)] [InlineData("absent", 2)] [InlineData("plain", 2)] [InlineData("enemy", 2)]
    [InlineData("other", 2)] [InlineData("hidden", 2)] [InlineData("deployed", 3)] [InlineData("two", 3)]
    public async Task ConditionalEquipmentPowerRequiresVisibleFriendlyDeploymentAtTheSameBattlefield(string branch, int expected)
    {
        var state = VenUnitEntryBasicTests.Position("RAD·072/167");
        // A granted keyword on equipment participates just like its printed keyword.
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", branch == "base" ? "BASE" : "BATTLEFIELD", "BF");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [] } } };
        var gearController = branch == "enemy" ? "P2" : "P1";
        state = LocalDestructionRecallTests.At(state, "GEAR", gearController, branch == "absent" ? "BASE" : "BATTLEFIELD", branch == "other" ? "OTHER" : "BF");
        var cards = state.CardObjects.ToDictionary(); cards["GEAR"] = cards["GEAR"] with {
            Tags = branch == "plain" ? [CardObjectTags.EquipmentCard] : [CardObjectTags.EquipmentCard, "部署"], IsFaceDown = branch == "hidden" };
        if (branch == "two") { cards["GEAR2"] = cards["GEAR"] with { ObjectId = "GEAR2" }; state = state with { CardObjects = cards };
            state = LocalDestructionRecallTests.At(state, "GEAR2", "P1", "BATTLEFIELD", "BF"); cards = state.CardObjects.ToDictionary(); }
        state = state with { CardObjects = cards };
        Assert.Equal(2, state.CardObjects["UNIT"].Power); Assert.Equal(expected, Power(state));
        var projected = ResolutionResult.BuildSnapshots(state); Assert.NotEmpty(projected);
        state = LocalDestructionRecallTests.At(state, "GEAR", "P2", "BASE");
        if (branch != "two") Assert.Equal(2, Power(state));
    }

    [Fact]
    public async Task DiscountMakesSquadronPlayableWithThreeManaAndCannotWaiveItsOptionalEquipmentReturn()
    {
        const string no = "RAD·075/167"; var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0) } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, []));
        var driver = new Driver(state); Assert.Contains("UNIT", JsonSerializer.Serialize(driver.PromptFor("P1")));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        Assert.Equal(0, driver.State.RunePools["P1"].Mana); await driver.Replay();
    }

    [Fact]
    public async Task SpiritCannotReadyOrModifyItsSourceAfterAResponseReturnsItToHand()
    {
        const string no = "RAD·066/167"; var state = Hand(VenUnitEntryBasicTests.Position(no), "RETURN", "OGN·104/298");
        var driver = new Driver(state);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await driver.Top(); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        await driver.Top(); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, driver.State.CardObjects["UNIT"].Power); Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        await driver.Replay();
    }

    [Theory]
    [InlineData("RAD·066/167")] [InlineData("RAD·075/167")]
    public async Task AControlledEquipmentReturnsToItsOwnerAndDoesNotLeakTheirNewHand(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P2" } } };
        var driver = new Driver(state);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        Assert.Contains("GEAR", driver.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("GEAR", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P1"]));
        if (no == "RAD·066/167") await driver.Top();
        await driver.Replay();
    }

    [Fact]
    public async Task EquipmentPowerRecomputesAfterAnAcceptedDestructionAndRoundTripsRecovery()
    {
        const string no = "RAD·072/167"; var state = Hand(VenUnitEntryBasicTests.Position(no), "KILL", "VEN·003");
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { Tags = [CardObjectTags.EquipmentCard, "部署"], AttachedToObjectId = "ALLY" } } };
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        var driver = new Driver(state); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(3, Power(driver.State));
        await driver.Act("P1", new PlayCardCommand("KILL", "VEN·003", ["GEAR"])); await driver.Top();
        Assert.Equal(2, Power(driver.State)); await driver.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("amount")] [InlineData("declaration")]
    public async Task RecoveryRejectsAChangedOrMissingEquipmentReturnReceipt(string branch)
    {
        const string no = "RAD·066/167"; var driver = new Driver(VenUnitEntryBasicTests.Position(no));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(driver.State));
        var item = Assert.Single(driver.State.StackItems);
        var changed = branch == "declaration" ? item with { OptionalCosts = [] }
            : item with { ReturnedEquipmentManaCost = branch == "missing" ? null : item.ReturnedEquipmentManaCost + 1 };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(driver.State with { StackItems = [changed] }));
    }

    private static int Power(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"],
        new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("players").GetProperty("P1").GetProperty("objects")
        .GetProperty("UNIT").GetProperty("effectivePower").GetInt32();

    private static MatchState Hand(MatchState state, string id, string no) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, id] } } };

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-entry-cost", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
