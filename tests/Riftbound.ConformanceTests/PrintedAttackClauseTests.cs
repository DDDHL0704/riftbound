using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedAttackClauseTests
{
    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = Printed("ENEMY", "OGN·088/298", "P2") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(40, 20, new Dictionary<string, int> {
                    ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 }),
                ["P2"] = new(40, 20, new Dictionary<string, int> {
                    ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 }) } };
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
    }

    private static MatchState WoundedEnemies(string no)
    {
        var state = Position(no);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·013/298", out var wardDefinition));
        // Seed the target with its official post-entry keyword set; the new
        // attacking source itself is played through the real command below.
        var ward = Printed("WARD", "OGN·013/298", "P2");
        ward = ward with { Tags = ward.Tags.Concat(wardDefinition.SourceUnitTags.Split('|')).Distinct(StringComparer.Ordinal).ToArray() };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["WOUND"] = Printed("WOUND", "OGN·219/298", "P2") with { Damage = 1 },
            ["WARD"] = ward with { Damage = 1 },
            ["ELSEWHERE"] = Printed("ELSEWHERE", "OGN·219/298", "P2") with { Damage = 1 },
            ["ALLY"] = state.CardObjects["ALLY"] with { Damage = 1 } } };
        foreach (var id in new[] { "WOUND", "WARD" })
            state = LocalDestructionRecallTests.At(state, id, "P2", "BATTLEFIELD", "OTHER");
        return LocalDestructionRecallTests.At(state, "ELSEWHERE", "P2", "BASE");
    }

    private static async Task Attack(Driver game, string no, bool withAlly = false)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        if (game.State.CardObjects["UNIT"].IsExhausted)
        {
            await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"]));
            await game.Top();
        }
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", [],
            withAlly ? ["UNIT", "ALLY"] : ["UNIT"]));
    }

    private static async Task SelectEnemy(Driver game, string id = "ENEMY")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT", choice.SourceObjectId);
        Assert.Contains(id, choice.LegalObjectIds);
        foreach (var illegal in new[] { "ALLY", "UNIT", "MISSING" })
        {
            var before = game.State;
            var rejected = await new CoreRuleEngine().ResolveAsync(before,
                new("bad-attack-target-" + illegal, choice.PlayerId, CommandTypes.ChooseCards),
                new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [illegal]), default);
            Assert.False(rejected.Accepted);
            Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        }
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    [Theory]
    [InlineData("OGN·076/298")]
    [InlineData("OGN·076a/298")]
    public async Task YasuoDeclaresAnEnemyHereBeforeRespondingAndDealsCurrentMight(string no)
    {
        var game = new Driver(Position(no));
        await Attack(game, no);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.DoesNotContain("ALLY", choice.LegalObjectIds);
        await SelectEnemy(game);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(6, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·076/298")]
    [InlineData("OGN·076a/298")]
    public async Task YasuoReadsHisMightAfterABuffResponse(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "BUFF", "SFD·034/221", "P1"));
        await Attack(game, no);
        await SelectEnemy(game);
        await game.Act("P1", new PlayCardCommand("BUFF", "SFD·034/221", ["UNIT"]));
        await game.Top();
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY")
            && Equals(entry.Event.Payload.GetValueOrDefault("damage"), 8));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·076/298")]
    [InlineData("OGN·076a/298")]
    public async Task RemovingYasuoInResponseLeavesNoSourceMight(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P1"));
        await Attack(game, no);
        await SelectEnemy(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·159/298")]
    [InlineData("ARC-004/006")]
    public async Task WarwickDestroysEveryWoundedEnemyHereWithoutTargetingOrChargingSpellshield(string no)
    {
        var game = new Driver(WoundedEnemies(no));
        Assert.Contains("法盾", game.State.CardObjects["WARD"].Tags);
        await Attack(game, no, withAlly: true);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("ATTACK", item.UnitEvent!.Timing);
        Assert.Empty(item.TargetObjectIds);
        Assert.Null(game.State.PendingCardChoice);
        foreach (var forged in new[] {
            item with { TargetObjectIds = ["WOUND"] },
            item with { TargetObjectIds = ["WOUND"], TargetGenerations = new Dictionary<string, long> { ["WOUND"] = game.State.CardObjects["WOUND"].ObjectGeneration } },
            item with { UnitEvent = item.UnitEvent with { Timing = "DEFEND" } },
            item with { UnitEvent = item.UnitEvent with { SourceGeneration = item.UnitEvent.SourceGeneration + 1 } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Top();
        Assert.Contains("WOUND", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("WARD", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("ELSEWHERE", game.State.PlayerZones["P2"].Base);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·159/298")]
    [InlineData("ARC-004/006")]
    public async Task WarwickReadsWoundsCreatedByARealResponseAfterHisAttackTriggerWasCaptured(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "WOUND", "OGN·133/298", "P1"));
        await Attack(game, no);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Act("P1", new PlayCardCommand("WOUND", "OGN·133/298", []));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Battlefields);
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·159/298")]
    [InlineData("ARC-004/006")]
    public async Task WarwickCannotDestroyEnemiesAtHisOldLocationAfterHeLeaves(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(WoundedEnemies(no), "RETURN", "OGN·104/298", "P1"));
        await Attack(game, no, withAlly: true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("WOUND", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("WARD", game.State.PlayerZones["P2"].Battlefields);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·076/298")]
    [InlineData("OGN·076a/298")]
    [InlineData("OGN·159/298")]
    [InlineData("ARC-004/006")]
    public async Task AttackOnlyPrintedSkillsDoNotTriggerWhileTheirSourceDefends(string no)
    {
        var initial = Position(no);
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } },
            ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = LocalDestructionRecallTests.At(initial, "UNIT", "P1", "BATTLEFIELD", "BF");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF"));
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }
}
