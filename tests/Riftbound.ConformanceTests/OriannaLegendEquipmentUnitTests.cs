using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed class OriannaLegendEquipmentUnitTests
{
    private const string Ability = LegendActionAbilityCatalog.OriannaLegendAbilityId;

    private static MatchState Position(
        string legendNo = "RAD·145/167",
        int equipmentManaCost = 2,
        int equipmentPower = 7,
        bool includeSecond = true)
    {
        var state = ArmamentMightTests.Position("SFD·022/221", power: 4);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["LEGEND"] = new("LEGEND", cardNo: legendNo, ownerId: "P1", controllerId: "P1", tags: ["传奇"]),
            ["GEAR"] = state.CardObjects["GEAR"] with { ManaCost = equipmentManaCost, Power = equipmentPower },
            ["ENEMY-GEAR"] = new("ENEMY-GEAR", cardNo: "SFD·033/221", ownerId: "P2", controllerId: "P2",
                manaCost: 1, power: 3, tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon])
        };
        var p1Base = state.PlayerZones["P1"].Base.Append("GEAR").Distinct(StringComparer.Ordinal).ToArray();
        var p2Base = state.PlayerZones["P2"].Base.Append("ENEMY-GEAR").Distinct(StringComparer.Ordinal).ToArray();
        if (includeSecond)
        {
            cards["OTHER-GEAR"] = new("OTHER-GEAR", cardNo: "SFD·033/221", ownerId: "P1", controllerId: "P1",
                manaCost: 3, power: 5, tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]);
            p1Base = p1Base.Append("OTHER-GEAR").ToArray();
        }
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
        {
            ["LEGEND"] = new("P1", "LEGEND"),
            ["ENEMY-GEAR"] = new("P2", "BASE")
        };
        if (includeSecond) locations["OTHER-GEAR"] = new("P1", "BASE");
        return state with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"], Base = p1Base },
                ["P2"] = state.PlayerZones["P2"] with { Base = p2Base }
            },
            CardObjects = cards,
            ObjectLocations = locations
        };
    }

    private static LegendActCommand Activate(string target, IReadOnlyList<string>? costs = null)
        => new("LEGEND", Ability, [target], costs ?? ["SPEND_MANA:1"]);

    private static async Task<ResolutionResult> Act(MatchState state, string player, GameCommand command)
        => await OfficialInsightAndSpellLockTests.Act(state, player, command);

    private static async Task<ResolutionResult> ResolveTop(MatchState state)
        => await OfficialInsightAndSpellLockTests.ResolveTop(state);

    [Theory]
    [InlineData("RAD·145/167")]
    [InlineData("RAD·171*/167")]
    [InlineData("RAD·171/167")]
    public async Task EachPrintedLegendPaysOneAndTurnsFriendlyEquipmentIntoAUnitAfterResponses(string legendNo)
    {
        var initial = Position(legendNo, equipmentManaCost: 2, equipmentPower: 7, includeSecond: false);
        var activated = await Act(initial, "P1", Activate("GEAR"));

        Assert.Equal(initial.RunePools["P1"].Mana - 1, activated.State.RunePools["P1"].Mana);
        Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(7, activated.State.CardObjects["GEAR"].Power);
        Assert.DoesNotContain(CardObjectTags.UnitCard, activated.State.CardObjects["GEAR"].Tags);
        var item = Assert.Single(activated.State.StackItems);
        Assert.Equal(Ability, item.LegendUnitEffect!.AbilityId);
        Assert.Equal(initial.CardObjects["GEAR"].ObjectGeneration, item.TargetGenerations!["GEAR"]);
        ArmamentMightTests.PublicRecovery(activated.State);

        var resolved = await ResolveTop(activated.State);
        var gear = resolved.State.CardObjects["GEAR"];
        Assert.Equal(2, gear.Power);
        Assert.Contains(CardObjectTags.EquipmentCard, gear.Tags);
        Assert.Contains(CardObjectTags.UnitCard, gear.Tags);
        Assert.Equal(new ObjectBinding("LEGEND", resolved.State.CardObjects["LEGEND"].ObjectGeneration),
            gear.LegendEquipmentUnit!.SourceLegend);
        Assert.Equal(7, gear.LegendEquipmentUnit.OriginalPower);
        Assert.True(gear.LegendEquipmentUnit.CapturedTick > 0);
        Restore(resolved.State);
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task ChoosingAnotherEquipmentRestoresTheOldEquipmentAndCapturesTheNewOne()
    {
        var initial = Position(equipmentManaCost: 2, equipmentPower: 7);
        var first = await Act(initial, "P1", Activate("GEAR"));
        var firstResolved = await ResolveTop(first.State);
        Assert.True(firstResolved.State.PendingCleanupTasks.Count == 0,
            string.Join(";", firstResolved.State.PendingCleanupTasks.Select(task => $"{task.Kind}:{task.ObjectId}:{task.PlayerId}")));
        Assert.Equal(2, firstResolved.State.CardObjects["GEAR"].Power);
        Assert.Contains(CardObjectTags.UnitCard, firstResolved.State.CardObjects["GEAR"].Tags);

        var readyAgain = firstResolved.State with
        {
            CardObjects = new Dictionary<string, CardObjectState>(firstResolved.State.CardObjects)
            {
                ["LEGEND"] = firstResolved.State.CardObjects["LEGEND"] with { IsExhausted = false }
            }
        };
        var second = await Act(readyAgain, "P1", Activate("OTHER-GEAR"));
        Assert.Equal(18, second.State.RunePools["P1"].Mana);
        var done = await ResolveTop(second.State);

        var old = done.State.CardObjects["GEAR"];
        Assert.Equal(7, old.Power);
        Assert.Contains(CardObjectTags.EquipmentCard, old.Tags);
        Assert.DoesNotContain(CardObjectTags.UnitCard, old.Tags);
        Assert.Null(old.LegendEquipmentUnit);

        var current = done.State.CardObjects["OTHER-GEAR"];
        Assert.Equal(3, current.Power);
        Assert.Contains(CardObjectTags.EquipmentCard, current.Tags);
        Assert.Contains(CardObjectTags.UnitCard, current.Tags);
        Assert.Equal(5, current.LegendEquipmentUnit!.OriginalPower);
        Assert.Contains(done.Events, e => e.Kind == "LEGEND_EQUIPMENT_UNIT_ENDED");
        ArmamentMightTests.PublicRecovery(done.State);
        Restore(done.State);
    }

    [Theory]
    [InlineData("non-equipment", "HOST")]
    [InlineData("enemy-equipment", "ENEMY-GEAR")]
    [InlineData("missing-target", "MISSING")]
    public async Task IllegalTargetNeverPaysOrExhaustsTheLegend(string scenario, string target)
    {
        var state = Position(includeSecond: false);
        var command = Activate(target);
        var rejected = await new CoreRuleEngine().ResolveAsync(state,
            new("orianna-illegal-" + scenario, "P1", command.CmdType), command, default);

        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(20, rejected.State.RunePools["P1"].Mana);
        Assert.False(rejected.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Theory]
    [InlineData("zero-mana")]
    [InlineData("wrong-cost")]
    [InlineData("extra-cost")]
    [InlineData("exhausted-source")]
    [InlineData("wrong-source")]
    public async Task IllegalCostOrSourceNeverMutatesState(string scenario)
    {
        var state = Position(includeSecond: false);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var costs = (IReadOnlyList<string>)["SPEND_MANA:1"];
        if (scenario == "zero-mana")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (scenario == "wrong-cost") costs = [];
        if (scenario == "extra-cost") costs = ["SPEND_MANA:1", "EXTRA"];
        if (scenario == "exhausted-source") cards["LEGEND"] = cards["LEGEND"] with { IsExhausted = true };
        if (scenario == "wrong-source") cards["LEGEND"] = cards["LEGEND"] with { CardNo = "OGN·268/298" };
        state = state with { CardObjects = cards };
        var command = Activate("GEAR", costs);
        var rejected = await new CoreRuleEngine().ResolveAsync(state,
            new("orianna-illegal-cost-" + scenario, "P1", command.CmdType), command, default);

        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task AResponseChangingTheTargetGenerationMakesTheEffectNoOpWithoutRefund()
    {
        var activated = await Act(Position(includeSecond: false), "P1", Activate("GEAR"));
        var changed = activated.State with
        {
            CardObjects = new Dictionary<string, CardObjectState>(activated.State.CardObjects)
            {
                ["GEAR"] = activated.State.CardObjects["GEAR"] with { ObjectGeneration = 3 }
            }
        };
        var resolved = await ResolveTop(changed);

        Assert.Equal(19, resolved.State.RunePools["P1"].Mana);
        Assert.True(resolved.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(7, resolved.State.CardObjects["GEAR"].Power);
        Assert.DoesNotContain(CardObjectTags.UnitCard, resolved.State.CardObjects["GEAR"].Tags);
        Assert.Contains(resolved.Events, e => e.Kind == "ABILITY_NO_EFFECT");
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("missing-context")]
    [InlineData("wrong-ability")]
    [InlineData("bad-source-generation")]
    [InlineData("bad-target-generation")]
    public async Task RecoveryRejectsForgedCapturedLegendUnitEffect(string change)
    {
        var activated = await Act(Position(includeSecond: false), "P1", Activate("GEAR"));
        var item = Assert.Single(activated.State.StackItems);
        var context = item.LegendUnitEffect!;
        var forged = item with
        {
            LegendUnitEffect = change switch
            {
                "missing-context" => null,
                "wrong-ability" => context with { AbilityId = "FORGED" },
                "bad-source-generation" => context with { SourceGeneration = -1 },
                _ => context
            },
            TargetGenerations = change == "bad-target-generation"
                ? new Dictionary<string, long> { ["GEAR"] = -1 }
                : item.TargetGenerations
        };

        Assert.Contains("invalid captured legend unit effect",
            Errors(activated.State with { StackItems = [forged] }));
    }

    [Fact]
    public async Task ActivationAndResolutionReplayThroughRecovery()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("RAD·171/167", equipmentManaCost: 3, equipmentPower: 1, includeSecond: false));
        await game.Act("P1", Activate("GEAR"));
        await game.Top();

        var gear = game.State.CardObjects["GEAR"];
        Assert.Equal(3, gear.Power);
        Assert.Contains(CardObjectTags.EquipmentCard, gear.Tags);
        Assert.Contains(CardObjectTags.UnitCard, gear.Tags);
        await game.Replay();
    }
}
