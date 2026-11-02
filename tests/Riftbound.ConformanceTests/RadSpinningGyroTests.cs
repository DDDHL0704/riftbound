using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadSpinningGyroTests
{
    private const string Gyro = "RAD·022/167";
    private const string GoldSkill = "RAD_022_EXHAUST_CREATE_GOLD";
    private const string MinionSkill = "RAD_022_DESTROY_EQUIPMENT_EXHAUST_CREATE_MINION";
    private const string RobotSkill = "RAD_022_DESTROY_UNIT_EXHAUST_CREATE_ROBOT";
    private const string Cost = "DESTROY_FRIENDLY_PERMANENT:";

    [Fact]
    public async Task ThePrintedEquipmentPaysFourManaAndOneRedBeforeOfferingThreeSkills()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Gyro);
        Assert.Equal(39514, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        var initial = Put(Create(), "GEAR", Gyro, "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Gyro, []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.StackItems);
        var prompt = JsonSerializer.Serialize(game.PromptFor("P1"));
        foreach (var skill in new[] { GoldSkill, MinionSkill, RobotSkill }) Assert.Contains(skill, prompt);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task DormantGoldBelongsToTheCurrentControllerAndWaitsForResponses(string owner)
    {
        var game = new Driver(Position(owner));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", GoldSkill, []));
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted); Assert.Empty(Tokens(game.State, "金币"));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        var gold = Assert.Single(Tokens(game.State, "金币"));
        Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.OwnerId); Assert.Equal("P1", gold.ControllerId);
        Assert.Contains(CardObjectTags.EquipmentCard, gold.Tags); Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);
        var resource = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(a.AbilityId)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(a, gold.CardNo));
        await Reject(game.State, "P1", new ActivateAbilityCommand(gold.ObjectId, resource.AbilityId, []));
        await game.Replay();
    }

    [Theory]
    [InlineData("GEAR")] [InlineData("COST")]
    public async Task AnEquipmentCostCanDestroyTheSourceWithoutResurrectingIt(string id)
    {
        var game = new Driver(Put(Position(), "COST", "OGN·040/298", "P1", "BASE"));
        var generation = game.State.CardObjects["GEAR"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("GEAR", MinionSkill, [], OptionalCosts: [Cost + id]));
        Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(generation, skill.SimpleActivation!.Source.Generation);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations); Assert.Empty(Tokens(game.State, "随从"));
        if (id == "GEAR") Assert.DoesNotContain(id, game.State.PlayerZones["P1"].Base);
        else Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top(); var minion = Assert.Single(Tokens(game.State, "随从"));
        Assert.Equal(1, minion.Power); Assert.True(minion.IsExhausted);
        Assert.Contains(minion.ObjectId, game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AControlledUnitPaysBeforeTheRobotAndReturnsToItsOwner(string owner)
    {
        var state = Put(Position(), "UNIT-COST", "OGN·219/298", owner, "BASE");
        state = RadUnitEffectTestFixture.Place(state, "UNIT-COST", "P1", "BASE");
        var game = new Driver(state);
        Assert.Contains(Cost + "UNIT-COST", JsonSerializer.Serialize(game.PromptFor("P1")));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", RobotSkill, [], OptionalCosts: [Cost + "UNIT-COST"]));
        Assert.Contains("UNIT-COST", game.State.PlayerZones[owner].Graveyard);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted); Assert.Empty(Tokens(game.State, "机器人"));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top(); var robot = Assert.Single(Tokens(game.State, "机器人"));
        Assert.Equal(3, robot.Power); Assert.True(robot.IsExhausted); Assert.Contains("机械", robot.Tags);
        Assert.Equal("P1", robot.OwnerId); Assert.Equal("P1", robot.ControllerId); await game.Replay();
    }

    [Theory]
    [InlineData("金币", MinionSkill, "随从")] [InlineData("随从", RobotSkill, "机器人")]
    public async Task ARealFactoryTokenCanPayAndCeasesToExist(string family, string skill, string created)
    {
        var factory = P6TokenFactoryCatalog.GetAll().First(f => f.TokenFamilyName == family);
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TOKEN-COST"] = factory.CreateObject("TOKEN-COST", "P1", "P1") } };
        state = RadUnitEffectTestFixture.Place(state, "TOKEN-COST", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", skill, [], OptionalCosts: [Cost + "TOKEN-COST"]));
        Assert.False(game.State.CardObjects.ContainsKey("TOKEN-COST"));
        Assert.DoesNotContain("TOKEN-COST", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); Assert.Single(Tokens(game.State, created)); await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("enemy")] [InlineData("wrong-type")]
    [InlineData("hand")] [InlineData("duplicate")] [InlineData("target")]
    [InlineData("graveyard")] [InlineData("unknown")]
    public Task IllegalNonTargetCostsCannotDestroyOrExhaustAnything(string fault)
    {
        var state = Put(Position(), "COST", "OGN·040/298", "P1", "BASE");
        state = Put(state, "ENEMY-GEAR", "OGN·040/298", "P2", "BASE");
        if (fault is "hand" or "graveyard") state = RadUnitEffectTestFixture.Place(state, "COST", "P1", fault == "hand" ? "HAND" : "GRAVEYARD");
        var id = fault switch { "enemy" => "ENEMY-GEAR", "wrong-type" => "ALLY", "unknown" => "missing-id", _ => "COST" };
        var costs = fault == "missing" ? Array.Empty<string>() : fault == "duplicate" ? new[] { Cost + id, Cost + id } : new[] { Cost + id };
        return Reject(state, "P1", new ActivateAbilityCommand("GEAR", MinionSkill,
            fault == "target" ? ["COST"] : [], OptionalCosts: costs));
    }

    [Fact]
    public async Task TheThreeSkillsShareTheSameSourceExhaustionCost()
    {
        var game = new Driver(Position());
        await Reject(game.State, "P2", new ActivateAbilityCommand("GEAR", GoldSkill, []));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", GoldSkill, [])); await game.Top();
        await Reject(game.State, "P1", new ActivateAbilityCommand("GEAR", MinionSkill, [], OptionalCosts: [Cost + "GEAR"]));
        Assert.DoesNotContain(GoldSkill, JsonSerializer.Serialize(game.PromptFor("P1"))); await game.Replay();
    }

    [Theory]
    [InlineData(MinionSkill, "随从", "COST")] [InlineData(RobotSkill, "机器人", "ALLY")]
    public async Task TheSharedUnitTokenReplacementRunsBeforeEntry(string skill, string family, string cost)
    {
        var state = Put(Put(Position(), "COST", "OGN·040/298", "P1", "BASE"), "Z", "UNL-086/219", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", skill, [], OptionalCosts: [Cost + cost])); await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow); Assert.Empty(Tokens(game.State, family));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["Z"]));
        Assert.Equal(2, Tokens(game.State, family).Length); await game.Replay();
    }

    [Fact]
    public async Task AReplacementStillPaysTheDestructionCostAndTheSurvivingUnitIsNotATarget()
    {
        var game = new Driver(Put(Put(Position(), "ALLY", "OGN·096/298", "P1", "BASE"), "GLASS", "OGN·077/298", "P1", "BASE"));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", RobotSkill, [], OptionalCosts: [Cost + "ALLY"]));
        if (game.State.PendingRuleChoice is { } pending)
        {
            var replacement = Assert.Single(pending.Request.Options, option => option.Id.StartsWith("GEAR:", StringComparison.Ordinal)
                && option.ObjectIds?.Contains("GLASS") == true);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [replacement.Id]));
        }
        Assert.Contains("GLASS", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top(); Assert.Single(Tokens(game.State, "机器人")); Assert.Empty(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task AnActualUnitDestructionCostPreservesItsLastBreathAndTheEarnedRobot()
    {
        var game = new Driver(Put(Position(), "ALLY", "OGN·096/298", "P1", "BASE"));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", RobotSkill, [], OptionalCosts: [Cost + "ALLY"]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(game.State.StackItems, item => item.SimpleActivation?.AbilityId == RobotSkill);
        Assert.Equal(1, game.State.StackItems.Count(item => item.SourceObjectId == "ALLY")
            + game.State.TriggerQueue.Count(item => item.SourceObjectId == "ALLY"));
        for (var i = 0; game.State.StackItems.Count > 0; i++)
        {
            Assert.InRange(i, 0, 2); await game.Top();
        }
        Assert.Single(game.State.PlayerZones["P1"].Hand); Assert.Single(Tokens(game.State, "机器人")); await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("ability")] [InlineData("tick")]
    public async Task RecoveryRejectsAnAlteredCapturedActivation(string mutation)
    {
        var game = new Driver(Position()); await game.Act("P1", new ActivateAbilityCommand("GEAR", GoldSkill, []));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        var item = Assert.Single(game.State.StackItems); var context = item.SimpleActivation!;
        var forged = mutation switch {
            "source" => context with { Source = new("ALLY", context.Source.Generation) },
            "ability" => context with { AbilityId = RobotSkill },
            _ => context with { AcquiredTick = game.State.Tick + 1 }
        };
        var corrupt = game.State with { StackItems = [item with { SimpleActivation = forged }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(corrupt));
    }

    private static MatchState Position(string owner = "P1")
        => RadUnitEffectTestFixture.Place(Put(Create(), "GEAR", Gyro, owner, "BASE"), "GEAR", "P1", "BASE");

    private static CardObjectState[] Tokens(MatchState state, string family)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == family).ToArray();
}
