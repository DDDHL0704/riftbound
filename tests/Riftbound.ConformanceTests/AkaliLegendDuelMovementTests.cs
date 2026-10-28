using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AkaliLegendDuelMovementTests
{
    private const string Ability = LegendActionAbilityCatalog.AkaliLegendAbilityId;

    [Theory]
    [InlineData("VEN·139", false)]
    [InlineData("VEN·139", true)]
    [InlineData("VEN·189", false)]
    [InlineData("VEN·189", true)]
    [InlineData("VEN·189*", false)]
    [InlineData("VEN·189*", true)]
    public async Task SwiftSkillMovesFriendlyDuelUnitAndReadiesItOnlyWhileEmpowered(string cardNo, bool empowered)
    {
        var state = Position(cardNo, "P1", empowered);
        var duel = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(duel.State.CardObjects["A"].IsExhausted);
        var prompt = ResolutionResult.BuildPrompts(duel.State)["P1"];
        Assert.Contains(prompt.Candidates!, c => c.Action == CommandTypes.LegendAct && c.Sources!.Any(s => s.Id == "LEGEND"));
        var activated = await Act(duel.State, "P1", Command("A"));
        Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal("BATTLEFIELD", activated.State.ObjectLocations["A"].Zone);
        Assert.NotNull(Assert.Single(activated.State.StackItems).LegendDuelMovement);
        Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
        var resolved = await Top(activated.State);
        Assert.Equal("BASE", resolved.State.ObjectLocations["A"].Zone);
        Assert.Equal(!empowered, resolved.State.CardObjects["A"].IsExhausted);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task OpponentsTurnAllowsTheSwiftSkillButNeitherInstructionExecutes()
    {
        var state = Position("VEN·139", "P2", empowered: true);
        var duel = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var focus = await Act(duel.State, "P1", new PassFocusCommand());
        Assert.Equal("P2", focus.State.FocusPlayerId);
        var activated = await Act(focus.State, "P2", Command("D"));
        Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        var resolved = await Top(activated.State);
        Assert.Equal("BATTLEFIELD", resolved.State.ObjectLocations["D"].Zone);
        Assert.True(resolved.State.CardObjects["D"].IsExhausted);
        Restore(resolved.State);
    }

    [Fact]
    public async Task ProhibitedMovementDoesNotStopTheSeparateEmpoweredReadyInstruction()
    {
        var state = Position("VEN·139", "P1", empowered: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { CardNo = "OGN·295/298" } } };
        var duel = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var resolved = await Top((await Act(duel.State, "P1", Command("A"))).State);
        Assert.Equal("BATTLEFIELD", resolved.State.ObjectLocations["A"].Zone);
        Assert.False(resolved.State.CardObjects["A"].IsExhausted);
        Restore(resolved.State);
    }

    [Fact]
    public async Task AResponseMovingTheTargetOutOfTheDuelMakesItIllegalWithoutRefund()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·139", "P1", empowered: true), "FLASH", "OGS·011/024", "P1");
        var duel = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var activated = await Act(duel.State, "P1", Command("A"));
        var response = await Act(activated.State, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["A"]));
        var moved = await Top(response.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["A"].Zone);
        var resolved = await Top(moved.State);
        Assert.True(resolved.State.CardObjects["A"].IsExhausted);
        Assert.True(resolved.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_READIED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "A"));
        Restore(resolved.State);
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData("duplicate")]
    [InlineData("exhausted")]
    [InlineData("wrong-player")]
    [InlineData("wrong-source")]
    [InlineData("outside-duel")]
    public async Task InvalidSkillDoesNotExhaustTheLegendOrMutateState(string branch)
    {
        var state = Position("VEN·139", "P1", empowered: false);
        if (branch != "outside-duel") state = (await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []))).State;
        if (branch == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var command = new LegendActCommand(branch == "wrong-source" ? "A" : "LEGEND", Ability,
            branch == "duplicate" ? ["A", "A"] : branch == "enemy" ? ["D"] : ["A"], []);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-legend-move", branch == "wrong-player" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task PaidEmpowerAndSwiftMovementSurviveRecoveryAndFullReplay()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("VEN·139", "P1", empowered: false));
        await game.Act("P1", new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", []));
        await game.Top();
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", Command("A"));
        await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["A"].Zone);
        Assert.False(game.State.CardObjects["A"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("source")]
    [InlineData("battlefield")]
    [InlineData("target")]
    public async Task RecoveryRejectsForgedCapturedSkill(string branch)
    {
        var duel = await Act(Position("VEN·139", "P1", false), "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var activated = await Act(duel.State, "P1", Command("A"));
        var item = Assert.Single(activated.State.StackItems);
        var context = item.LegendDuelMovement!;
        var forged = branch switch {
            "missing" => item with { LegendDuelMovement = null },
            "source" => item with { LegendDuelMovement = context with { SourceGeneration = -1 } },
            "battlefield" => item with { LegendDuelMovement = context with { Battlefield = new("NO-FIELD", 0) } },
            _ => item with { TargetGenerations = new Dictionary<string, long> { ["A"] = -1 } }
        };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(activated.State with { StackItems = [forged] }),
            error => error.Contains("legend duel movement"));
    }

    [Fact]
    public async Task TargetReturningToHandIsNotDisclosedByTheResolvedSkill()
    {
        var state = Position("VEN·139", "P1", empowered: true);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RUNE"] = new("RUNE", cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { RuneDeck = ["RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "RUNE_DECK") }
        };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var duel = await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var activated = await Act(duel.State, "P1", Command("A"));
        var response = await Act(activated.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["A"]));
        var returned = await Top(response.State);
        Assert.Contains("A", returned.State.PlayerZones["P1"].Hand);
        Assert.Contains(returned.State.StackItems, item => item.LegendDuelMovement is not null);
        var opponent = ResolutionResult.BuildSnapshots(returned.State)["P2"];
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(returned.State.RoomId, returned.State.Tick, 0, [], returned.State);
        Assert.DoesNotContain("\"A\"", JsonSerializer.Serialize(opponent.Stack));
        Assert.DoesNotContain("\"A\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        var resolved = await Top(returned.State);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "STACK_ITEM_RESOLVED"
            && JsonSerializer.Serialize(e.Payload).Contains("\"A\"", StringComparison.Ordinal));
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("VEN·139")]
    [InlineData("VEN·189")]
    [InlineData("VEN·189*")]
    public async Task RuntimeLegendCanUseTheExistingBattlefieldGrantedSkill(string cardNo)
    {
        var state = LegendAttachmentTests.ForgePosition();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { CardNo = cardNo } } };
        var activated = await Act(state, "P1", LegendAttachmentTests.Command(LegendAttachmentTests.Forge));
        Assert.Single(activated.State.StackItems, item => item.LegendAttachment is not null);
        var resolved = await Top(activated.State);
        Assert.Equal("HOST", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    private static LegendActCommand Command(string target) => new("LEGEND", Ability, [target], []);

    private static MatchState Position(string cardNo, string legendOwner, bool empowered)
    {
        var state = DeathAndDuelLifecycleTests.State();
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [legendOwner] = state.PlayerZones[legendOwner] with { LegendZone = ["LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: cardNo, ownerId: legendOwner, controllerId: legendOwner,
                    tags: ["传奇"]) { IsEmpowered = empowered },
                ["D"] = state.CardObjects["D"] with { CardNo = "OGN·001/298", Power = 8, IsExhausted = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new(legendOwner, "LEGEND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20), ["P2"] = new(20, 20) }
        };
    }
}
