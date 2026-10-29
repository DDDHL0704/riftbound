using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenActivatedEquipmentTests
{
    public static TheoryData<string, int, bool> Cards => new() {
        { "VEN·054", 3, false }, { "VEN·062", 2, true }, { "VEN·075", 3, true }, { "VEN·077", 4, false }, { "VEN·087", 4, false }
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task EachEquipmentPaysItsPrintedCostAndUsesItsPrintedEntryReadiness(string no, int mana, bool exhausted)
    {
        var initial = Position(no);
        var command = new PlayCardCommand("UNIT", no, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(no == "VEN·087" ? 1 : 0, quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Contains(CardObjectTags.EquipmentCard, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(exhausted, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·054")] [InlineData("VEN·062")] [InlineData("VEN·075")] [InlineData("VEN·077")] [InlineData("VEN·087")]
    public async Task UnpayableEquipmentAndInventedPlayTargetsAreRejectedWithoutMutation(string no)
    {
        await Reject(Position(no), new PlayCardCommand("UNIT", no, ["ALLY"]));
        var state = Position(no);
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } }, new PlayCardCommand("UNIT", no, []));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task ImperialToolReadsCurrentOrLastKnownEmpowermentAndItsBonusExpires(bool empowered, bool destroyed)
    {
        var state = Position("VEN·077");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = empowered } } };
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BASE");
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [] } : p.Value) };
        var game = new Driver(state); var power = state.CardObjects["ALLY"].Power;
        foreach (var illegal in new[] { "UNIT", "GEAR", "GRAVE", "P1-DRAW-1" })
            await Reject(game.State, new ActivateAbilityCommand("UNIT", "IMPERIAL_TOOL_POWER", [illegal]));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "IMPERIAL_TOOL_POWER", ["ALLY"]));
        Assert.Equal(power, game.State.CardObjects["ALLY"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(empowered, Assert.Single(game.State.StackItems).TargetedPowerAbility!.LastKnownSourceEmpowered);
        if (destroyed) {
            var departed = game.State with {
                CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                    ["UNIT"] = game.State.CardObjects["UNIT"] with { ObjectGeneration = 1, IsEmpowered = false } },
                PlayerZones = game.State.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with {
                    Base = p.Value.Base.Where(id => id != "UNIT").ToArray(), Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
                ObjectLocations = new Dictionary<string, ObjectLocationState>(game.State.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") }
            };
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(departed));
            var done = await OfficialGraveyardRecastTests.Top(departed);
            Assert.Equal(power + (empowered ? 4 : 2), done.State.CardObjects["ALLY"].Power);
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(done.State));
            return;
        }
        await game.Top(); Assert.Equal(power + (empowered ? 4 : 2), game.State.CardObjects["ALLY"].Power);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(power, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ImperialToolTracksAnEmpowermentChangeBeforeItsResolution(bool initiallyEmpowered)
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·077"), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [] } : p.Value),
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = initiallyEmpowered } } };
        var engine = new CoreRuleEngine();
        var started = await engine.ResolveAsync(state, new("activate", "P1", CommandTypes.ActivateAbility), new ActivateAbilityCommand("UNIT", "IMPERIAL_TOOL_POWER", ["ALLY"]), default);
        Assert.True(started.Accepted);
        var changed = started.State with { CardObjects = new Dictionary<string, CardObjectState>(started.State.CardObjects) {
            ["UNIT"] = started.State.CardObjects["UNIT"] with { IsEmpowered = !initiallyEmpowered } } };
        var captured = await engine.ResolveAsync(changed, new("pass1", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(captured.Accepted);
        Assert.Equal(!initiallyEmpowered, Assert.Single(captured.State.StackItems).TargetedPowerAbility!.LastKnownSourceEmpowered);
        Assert.Contains(captured.Events, e => e.Kind == "ABILITY_SOURCE_STATUS_CAPTURED");
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(captured.State));
        var done = await engine.ResolveAsync(captured.State, new("pass2", "P2", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(done.Accepted); Assert.Equal(state.CardObjects["ALLY"].Power + (initiallyEmpowered ? 2 : 4), done.State.CardObjects["ALLY"].Power);
    }

    [Fact]
    public async Task ImperialToolRecoveryRejectsAFalseOrMissingCurrentSourceStatus()
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·077"), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [] } : p.Value) };
        var game = new Driver(state); await game.Act("P1", new ActivateAbilityCommand("UNIT", "IMPERIAL_TOOL_POWER", ["ALLY"]));
        var item = Assert.Single(game.State.StackItems);
        foreach (bool? status in new bool?[] { null, true }) {
            var forged = game.State with { StackItems = [item with { TargetedPowerAbility = item.TargetedPowerAbility! with { LastKnownSourceEmpowered = status } }] };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("targeted power ability"));
        }
    }

    [Theory]
    [InlineData("VEN·054", "BOOK_REMOVE_EMPOWER_DRAW")]
    [InlineData("VEN·087", "HEXTECH_DISC_CREATE_ROBOT")]
    public async Task EmpoweredEquipmentPaysRemovalManaAndExhaustBeforeItsDrawOrRobotResolves(string no, string ability)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        await Reject(game.State, new ActivateAbilityCommand("UNIT", ability, []));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        await NextOwnTurn(game);
        await Reject(game.State, new ActivateAbilityCommand("UNIT", ability, ["ALLY"]));
        await Reject(game.State, new ActivateAbilityCommand("UNIT", ability, []));
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        await game.Act("P1", new TapRuneCommand("PAY-RUNE"));
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        var robots = game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo == "SFD·T01");
        await game.Act("P1", new ActivateAbilityCommand("UNIT", ability, []));
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(robots, game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo == "SFD·T01"));
        await game.Top();
        if (no == "VEN·054") Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        else {
            var robot = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T01");
            Assert.Equal(3, robot.Power); Assert.True(robot.IsExhausted);
            Assert.Contains(robot.ObjectId, game.State.PlayerZones["P1"].Base);
            Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        }
        await Reject(game.State, new ActivateAbilityCommand("UNIT", ability, []));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnEggIsAReactionResourceActionAndUsesItsCurrentEmpowerment(bool empowered)
    {
        var game = new Driver(Position("VEN·075"));
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·075", []));
        await Reject(game.State, new ActivateAbilityCommand("UNIT", "EGG_REACTION_GAIN_MANA", []));
        await NextOwnTurn(game);
        if (empowered) {
            await game.Act("P1", new TapRuneCommand("PAY-RUNE"));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
            await game.Top();
            await NextOwnTurn(game);
        }
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EGG_REACTION_GAIN_MANA", []));
        Assert.Equal(mana + (empowered ? 2 : 1), game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await Reject(game.State, new ActivateAbilityCommand("UNIT", "EGG_REACTION_GAIN_MANA", []));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EquationCanEmpowerAnotherEquipmentIncludingOneAlreadyEmpowered(bool alreadyEmpowered)
    {
        var state = Position("VEN·062");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsEmpowered = alreadyEmpowered } } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·062", []));
        await Reject(game.State, new ActivateAbilityCommand("UNIT", "HEXTECH_EQUATION_EMPOWER_EQUIPMENT", ["GEAR"]));
        await NextOwnTurn(game);
        foreach (var illegal in new[] { "UNIT", "ALLY", "GRAVE" })
            await Reject(game.State, new ActivateAbilityCommand("UNIT", "HEXTECH_EQUATION_EMPOWER_EQUIPMENT", [illegal]));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "HEXTECH_EQUATION_EMPOWER_EQUIPMENT", ["GEAR"]));
        Assert.Equal(alreadyEmpowered, game.State.CardObjects["GEAR"].IsEmpowered);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Top();
        Assert.True(game.State.CardObjects["GEAR"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task ASpentTargetTokenDoesNotInvalidateThePendingEquationSkillOrRecoverAsANewTarget()
    {
        var state = Position("VEN·062");
        P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GOLD"] = factory.CreateObject("GOLD", "P2", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, "GOLD"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GOLD"] = new("P2", "BASE") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·062", []));
        await NextOwnTurn(game);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "HEXTECH_EQUATION_EMPOWER_EQUIPMENT", ["GOLD"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new ActivateAbilityCommand("GOLD", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        Assert.DoesNotContain("GOLD", game.State.CardObjects.Keys);
        await game.Act("P2", new PassPriorityCommand());
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("ability")] [InlineData("tick")]
    public async Task RecoveryRejectsForgedPrintedActivationBindings(string mutation)
    {
        var state = Position("VEN·054");
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [] } } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "BOOK_REMOVE_EMPOWER_DRAW", []));
        var item = Assert.Single(game.State.StackItems);
        var context = item.SimpleActivation!;
        var forged = mutation switch {
            "source" => context with { Source = new("GEAR", context.Source.Generation) },
            "ability" => context with { AbilityId = "HEXTECH_DISC_CREATE_ROBOT" },
            _ => context with { AcquiredTick = context.AcquiredTick + 1 }
        };
        var corrupt = game.State with { StackItems = [item with { SimpleActivation = forged }] };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(corrupt, new Dictionary<string, RecoveredPlayerView>(), null));
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PAY-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("PAY-RUNE", "OGN·007/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "PAY-RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["PAY-RUNE"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 2 }) } };
    }

    private static async Task NextOwnTurn(Driver game)
    { await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand()); }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-equipment", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
