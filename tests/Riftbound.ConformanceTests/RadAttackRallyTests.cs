using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;

public sealed class RadAttackRallyTests
{
    private const string Printing = "RAD·133/167";
    private const string Kind = "ATTACK_MOVE_CONTROLLED_UNIT_TO_SOURCE_BATTLEFIELD";

    private static MatchState Position(string no = Printing, bool copied = false, bool exhausted = false)
    {
        var state = AttackDamageDistributionTests.Position(no, copied);
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·219/298", "OTHER", "P1");
        state = At(state, "ALLY", "P1", "BASE");
        state = OfficialHoldSequenceTests.AddUnit(state, "FAR", "OGN·219/298", "OTHER", "P1");
        state = OfficialHoldSequenceTests.AddUnit(state, "HERE", "OGN·219/298", "F", "P1");
        state = At(state, "HERE", "P1", "BASE");
        var hero = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1");
        return state with { RoomId = "RAD-ATTACK-RALLY", CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = hero with { Tags = [.. hero.Tags, "强攻", "壁垒"] },
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = exhausted },
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1" } } };
    }

    private static Task Attack(Driver game) => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [],
        SourceObjectIds: game.State.PlayerZones["P1"].Base.Contains("HERE") ? ["HOST", "HERE"] : ["HOST"]));
    private static Task Choose(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-rally", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory]
    [InlineData("RAD·133/167", 39638)] [InlineData("RAD·133a/167", 39639)]
    public async Task EachOfficialPrintingPaysItsManaAndYellowPowerFromTheChampionZone(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("冕武天骄", card.SubTitle);
        Assert.Equal(7, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(7, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitAttackMoveControlledUnitToSourceBattlefield);
        Assert.True(spec.Optional); Assert.Equal(TriggerTimings.UnitAttack, spec.Timing);
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(7, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(6, 1) } }, "P1", command);
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(7, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } }, "P1", command);
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.Contains("强攻", game.State.CardObjects["UNIT"].Tags); Assert.Contains("壁垒", game.State.CardObjects["UNIT"].Tags);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·133/167", "ALLY", false)] [InlineData("RAD·133a/167", "ALLY", true)]
    [InlineData("RAD·133/167", "FAR", false)]
    public async Task ConfirmedFriendlyMovesOnlyAfterResponsesWithoutPayingAStandardMoveCost(string no, string target, bool exhausted)
    {
        var game = new Driver(Position(no, exhausted: exhausted)); var mana = game.State.RunePools["P1"].Mana;
        await Attack(game);
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(8, ArmamentMightTests.Might(game.State));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount); Assert.Equal(["ALLY", "FAR"], choice.LegalObjectIds);
        var origin = game.State.ObjectLocations[target]; await Choose(game, target);
        Assert.Equal(origin, game.State.ObjectLocations[target]); Assert.Null(game.State.PendingPayment);
        await game.Top();
        Assert.Equal(new("P1", "BATTLEFIELD", "F"), game.State.ObjectLocations[target]);
        Assert.Equal(exhausted, game.State.CardObjects[target].IsExhausted);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), target));
        await game.Replay();
    }

    [Fact]
    public async Task IllegalTargetsCannotChangeStateAndTheOptionalSkillMayBeDeclined()
    {
        var game = new Driver(Position()); await Attack(game);
        var choice = game.State.PendingCardChoice!;
        foreach (var ids in new[] { new[] { "HOST" }, ["HERE"], ["ENEMY1"], ["GEAR"], ["D0"], ["ALLY", "FAR"], ["invented"] })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await Choose(game);
        Assert.DoesNotContain(game.State.StackItems, i => i.FieldContext?.Kind == Kind);
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); await game.Replay();
    }

    [Theory]
    [InlineData("base")] [InlineData("hand")]
    public async Task ASourceThatLosesItsBattlefieldDoesNotMoveTheTargetOrExposeItsPrivateIdentity(string destination)
    {
        var response = destination == "base" ? "OGS·011/024" : "OGN·104/298";
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", response, "P1"));
        await Attack(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", response, ["HOST"])); await game.Top();
        Assert.Equal(destination == "base" ? "BASE" : "HAND", game.State.ObjectLocations["HOST"].Zone);
        if (destination == "hand") {
            Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
            Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        }
        await game.Top(); Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone);
        if (destination == "hand") Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Fact]
    public async Task TheSourceCurrentBattlefieldIsUsedAfterARealMovementResponse()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "MOVE", "VEN·034", "P1"));
        await Attack(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("MOVE", "VEN·034", ["OTHER", "HOST"])); await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await game.Top(); Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task TheTargetCannotFollowTheSourceAfterLeavingTheField()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1"));
        await Attack(game); await Choose(game, "ALLY");
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        await game.Top(); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task CopiedTextOffersTwoIndependentChoicesAndDecliningOneKeepsTheOther()
    {
        var game = new Driver(Position(copied: true)); await Attack(game);
        var ids = game.State.TriggerQueue.Where(t => t.FieldContext?.Kind == Kind).Select(t => t.TriggerId).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await Choose(game); await Choose(game, "ALLY");
        Assert.Single(game.State.StackItems, i => i.FieldContext?.Kind == Kind);
        await game.Top(); Assert.Equal("F", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task DefendingDoesNotCaptureAnAttackSkill()
    {
        var state = AhriCombatMightTests.Board(Printing, defending: true);
        var game = new Driver(state); await Attack(game);
        Assert.True(game.State.CardObjects["ENEMY1"].IsDefending);
        Assert.DoesNotContain(game.State.TriggerQueue, t => t.FieldContext?.Kind == Kind);
        Assert.DoesNotContain(game.State.StackItems, i => i.FieldContext?.Kind == Kind); await game.Replay();
    }

    [Fact]
    public async Task EquipmentFollowsTheMovedUnitAndItsMovementObserverStillTriggers()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·222/298", "P1"),
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(state); await Attack(game); await Choose(game, "ALLY"); await game.Top();
        Assert.Equal(new("P1", "BATTLEFIELD", "F"), game.State.ObjectLocations["GEAR"]);
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotEmpty(game.State.StackItems); Assert.Empty(MovementTokenTests.Tokens(game.State));
        await game.Top(); Assert.Single(MovementTokenTests.Tokens(game.State)); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedSourcesDestinationsAndTargetGenerations()
    {
        var game = new Driver(Position()); await Attack(game);
        var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!;
        foreach (var forged in new[] {
            item with { FieldContext = context with { BattlefieldId = "invented" } },
            item with { FieldContext = context with { CardNo = "OGN·219/298" } },
            item with { FieldContext = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { FieldContext = context with { SourceLocation = new("P1", "BATTLEFIELD", "OTHER") } },
            item with { DamageAmount = 1 }, item with { Destination = "BATTLEFIELD:OTHER" } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await Choose(game, "ALLY"); item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            TargetGenerations = new Dictionary<string, long> { ["ALLY"] = -1 } }] }));
        await game.Top(); await game.Replay();
    }
}
