using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GangplankRuleInstanceConsumerTests
{
    [Theory]
    [InlineData("VEN·086", false)] [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)] [InlineData("VEN·181", true)]
    public async Task TwoRealSongsOfferIndependentInstancesButEachSelectedActionStillGivesOnlyThree(string no, bool lastFirst)
    {
        var initial = Add(Position(no), "SONG-1", "SFD·059/221");
        initial = Add(initial, "SONG-2", "SFD·059/221");
        initial = Add(initial, "SECOND", "UNL-066/219");
        var driver = await Play(initial, no, "BASE"); await Empower(driver, "UNIT");
        foreach (var id in new[] { "SONG-1", "SONG-2" }) {
            await driver.Act("P1", new PlayCardCommand(id, "SFD·059/221", []));
            await driver.Act("P1", new AssembleEquipmentCommand(id, "UNIT", ["ASSEMBLE_GREEN"])); await driver.Top();
            Assert.Equal("UNIT", driver.State.CardObjects[id].AttachedToObjectId);
            Assert.Equal(no, driver.State.CardObjects[id].AttachedRuleText!.CardNo);
        }
        for (var occurrence = 0; occurrence < 2; occurrence++) {
            var id = occurrence == 0 ? "ACTION" : "SECOND";
            await driver.Act("P1", new PlayCardCommand(id, "UNL-066/219", ["UNIT"])); await driver.Top();
            GangplankTargetReplacementConsumerTests.Export($"songs-frontier-{no}-{lastFirst}-{occurrence}", initial, driver,
                new { expectedChoices = 3, expectedFinalMight = 9 + 3 * occurrence });
            var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
            Assert.Equal("TARGETED_ACTION_REPLACEMENT", pending.Request.Kind);
            Assert.Equal("P1", pending.Request.PlayerId); Assert.Equal(3, pending.Request.Options.Count);
            Assert.Equal(3, pending.Request.Options.Select(o => o.Id).Distinct().Count());
            var options = pending.Request.Options;
            var option = (lastFirst == (occurrence == 0)) ? options.Last() : options.First();
            await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]));
            Assert.Null(driver.State.PendingRuleChoice); Assert.Empty(driver.State.StackItems);
            Assert.Equal(9 + 3 * occurrence, driver.State.CardObjects["UNIT"].Power);
            Assert.Equal(occurrence + 1, driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers.Count);
            Assert.All(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers, m => Assert.Equal(3, m.PowerDelta));
        }
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export($"songs-complete-{no}-{lastFirst}", initial, driver,
            new { replacements = 2, mightBeforeCleanup = 12, mightAfterCleanup = 6 });
    }

    [Theory]
    [InlineData("VEN·086", false)] [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)] [InlineData("VEN·181", true)]
    public async Task ARealWholeUnitCopyUsesTheReceiversOwnEmpowerStatus(string no, bool empowerCopy)
    {
        var initial = CopyPosition(no); var driver = await Copy(initial, no);
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(no, driver.State.CardObjects["HOST"].CardNo); Assert.False(driver.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(6, driver.State.CardObjects["HOST"].Power);
        if (empowerCopy) await Empower(driver, "HOST");
        await driver.Act("P1", new PlayCardCommand("ACTION", "UNL-066/219", ["HOST"])); await driver.Top(); await driver.Replay();
        GangplankTargetReplacementConsumerTests.Export($"whole-copy-{no}-{empowerCopy}", initial, driver,
            new { expectedMight = empowerCopy ? 9 : -4, expectedOwnStatus = empowerCopy });
        Assert.Equal(empowerCopy ? 9 : -4, driver.State.CardObjects["HOST"].Power);
        Assert.Equal(empowerCopy, driver.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal("HOST", driver.State.CardObjects["GLASSES"].AttachedToObjectId);
        Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        if (empowerCopy) {
            var entry = Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(3, entry.PowerDelta); Assert.Null(entry.ChosenNegativeMight);
        }
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task RealDetachmentEndsTheCopiedReplacementEvenThoughTheEmpoweredStatusRemains(string no)
    {
        var initial = Add(CopyPosition(no), "DETACH", "SFD·011/221"); var driver = await Copy(initial, no);
        await Empower(driver, "HOST"); Assert.True(driver.State.CardObjects["HOST"].IsEmpowered);
        await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GLASSES"])); await driver.Top();
        Assert.Equal("OGN·096/298", driver.State.CardObjects["HOST"].CardNo); Assert.Equal(1, driver.State.CardObjects["HOST"].Power);
        Assert.True(driver.State.CardObjects["HOST"].IsEmpowered); Assert.Null(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
        await driver.Act("P1", new PlayCardCommand("ACTION", "UNL-066/219", ["HOST"])); await driver.Top();
        Assert.Equal(-9, driver.State.CardObjects["HOST"].Power); Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(-10, Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("copied-face-detached-" + no, initial, driver,
            new { expectedMight = -9, physicalFace = "OGN·096/298", empoweredWithoutReplacementText = true });
    }

    [Theory]
    [InlineData("VEN·086", "P1")] [InlineData("VEN·086", "P2")]
    [InlineData("VEN·181", "P1")] [InlineData("VEN·181", "P2")]
    public async Task ARealReactionAbovePendingEmpowerDoesNotGainTheUnresolvedReplacement(string no, string caster)
    {
        var initial = GangplankTargetReplacementConsumerTests.Position(no, caster, "negative");
        var driver = await Play(initial, no, "BASE");
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        if (caster == "P2") await driver.Act("P1", new PassPriorityCommand());
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        await driver.Act(caster, new PlayCardCommand("ACTION", "UNL-066/219", ["UNIT"])); await driver.Top();
        Assert.Equal(-4, driver.State.CardObjects["UNIT"].Power); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Single(driver.State.StackItems); await driver.Top();
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(-4, driver.State.CardObjects["UNIT"].Power);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export($"above-pending-empower-{no}-{caster}", initial, driver,
            new { expectedMight = -4, empowerResolvedAfterNegativeAction = true });
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task AnActualEnemyAttackTriggerRemembersTheAttackerWithoutSelectingIt(string no)
    {
        var initial = Position(no);
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["FOX"] = OfficialGarenConquestBattlefieldCountTests.Printed("FOX", "OGN·255/298", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FOX"] = new("P2", "LEGEND") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2" ? p.Value with { LegendZone = ["FOX"] } : p.Value) };
        var driver = await Play(initial, no, "BASE"); await Empower(driver, "UNIT");
        await driver.Act("P1", new EndTurnCommand()); await driver.Act("P2", new EndTurnCommand());
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        var trigger = Assert.Single(driver.State.StackItems);
        Assert.Equal("ENEMY_ATTACK_MIGHT", trigger.FieldContext!.Kind); Assert.Empty(trigger.TargetObjectIds);
        Assert.Equal("UNIT", trigger.FieldContext.AffectedUnit!.ObjectId);
        await driver.Top(); Assert.Equal(5, driver.State.CardObjects["UNIT"].Power);
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(-1, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Replay(); GangplankTargetReplacementConsumerTests.Export("remembered-attacker-" + no, initial, driver,
            new { expectedMight = 5, explicitlyChosen = false });
    }

    private static MatchState Position(string no)
    {
        var state = GangplankTargetReplacementConsumerTests.Position(no, "P1", "negative");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = state.RunePools["P1"] with { Mana = 60,
                PowerByTrait = new Dictionary<string, int>(state.RunePools["P1"].PowerByTrait) { [RuneTrait.Green] = 8, [RuneTrait.Yellow] = 4, [RuneTrait.Orange] = 8 } } } };
    }
    private static MatchState CopyPosition(string no) => Add(Add(Position(no), "HOST", "OGN·096/298"), "GLASSES", "VEN·137");
    private static MatchState Add(MatchState state, string id, string no)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value) };
    private static async Task<Driver> Play(MatchState initial, string no, string destination)
    {
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination)); return driver;
    }
    private static async Task Empower(Driver driver, string id)
    {
        string? instance = null;
        if (id != "UNIT") {
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
            instance = Assert.IsType<string>(Assert.Single(rows, r => Equals(r["sourceObjectId"], id))["abilityInstanceId"]);
        }
        await driver.Act("P1", new ActivateAbilityCommand(id, "EMPOWER:0:0", []) { AbilityInstanceId = instance }); await driver.Top();
    }
    private static async Task<Driver> Copy(MatchState initial, string no)
    {
        var driver = await Play(initial, no, "BASE"); await Empower(driver, "UNIT");
        await driver.Act("P1", new PlayCardCommand("HOST", "OGN·096/298", []));
        await driver.Act("P1", new PlayCardCommand("GLASSES", "VEN·137", []));
        await driver.Act("P1", new AssembleEquipmentCommand("GLASSES", "HOST", ["ASSEMBLE_YELLOW"])); await driver.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", pending.Request.Kind);
        await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:UNIT"])); return driver;
    }
}
