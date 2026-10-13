using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZenithBladeMovementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualConfirmedSpellStunsThenOptionallyMovesTheChosenFriendlyUnit(bool move)
    {
        var initial = Position();
        var driver = Driver(initial, move ? "move" : "decline");
        await driver.Act("P1", new PlayCardCommand("BLADE", "OGN·262/298", move ? ["ENEMY", "HOST"] : ["ENEMY"]));
        var resolved = await driver.Top();
        Assert.Contains("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(move ? "BATTLEFIELD" : "BASE", driver.State.ObjectLocations["HOST"].Zone);
        if (move)
        {
            Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
            Assert.True(driver.State.BattleState.IsActive);
            Assert.True(driver.State.CardObjects["HOST"].IsAttacking);
            var status = resolved.Events.ToList().FindIndex(e => e.Kind == "STATUS_EFFECT_APPLIED");
            var movement = resolved.Events.ToList().FindIndex(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
            Assert.True(status >= 0 && movement > status);
        }
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("same-field")]
    [InlineData("enemy-mover")]
    [InlineData("missing-reference")]
    [InlineData("reverse")]
    public async Task PreviewAndRealSubmissionRejectIllegalOptionalMovementWithoutMutation(string branch)
    {
        var initial = Position();
        if (branch == "same-field") initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F");
        var targets = branch == "enemy-mover" ? new[] { "ENEMY", "ENEMY" }
            : branch == "missing-reference" ? new[] { "HOST" } : branch == "reverse" ? new[] { "HOST", "ENEMY" } : new[] { "ENEMY", "HOST" };
        var command = new PlayCardCommand("BLADE", "OGN·262/298", targets);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", new("quote", prompt.PromptId!, initial.Tick, command));
        Assert.False(quote.IsValid);
        var rejected = await engine.ResolveAsync(initial, new("reject", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReturnResponseInvalidatesOnlyTheAffectedTargetBeforeReferencedMovement(bool referenceLeaves)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", referenceLeaves ? "P2" : "P1");
        initial = initial with { RunePools = new Dictionary<string,RunePool>(initial.RunePools) { ["P2"] = new(20,20) } };
        var driver = Driver(initial, referenceLeaves ? "reference-return" : "mover-return");
        await driver.Act("P1", new PlayCardCommand("BLADE", "OGN·262/298", ["ENEMY", "HOST"]));
        if (referenceLeaves) await driver.Act("P1", new PassPriorityCommand());
        await driver.Act(referenceLeaves ? "P2" : "P1", new PlayCardCommand("RETURN", "OGN·104/298", [referenceLeaves ? "ENEMY" : "HOST"]));
        await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations[referenceLeaves ? "ENEMY" : "HOST"].Zone);
        var resolved = await driver.Top();
        Assert.Equal(!referenceLeaves, driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal(referenceLeaves ? "BASE" : "HAND", driver.State.ObjectLocations["HOST"].Zone);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ExhaustedUnitCanBeMovedByTheSpellAndEquipmentFollowsThroughSharedFlow()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects)
            { ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        initial = AttachedRuleTextTests.AddSong(initial, "MOVE-SONG");
        var driver = Driver(initial, "exhausted-equipped");
        await driver.Act("P1", new AssembleEquipmentCommand("MOVE-SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        await driver.Act("P1", new PlayCardCommand("BLADE", "OGN·262/298", ["ENEMY", "HOST"]));
        await driver.Top();
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", driver.State.ObjectLocations["MOVE-SONG"].BattlefieldObjectId);
        Assert.Equal("HOST", driver.State.CardObjects["MOVE-SONG"].AttachedToObjectId);
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task TwoDeclaredEchoExecutionsRetainIndependentOptionalMovementTargets()
    {
        var initial = Position();
        initial = SacredProtectorTests.Unit(initial, "SECOND", "P1");
        initial = LocalDestructionRecallTests.At(initial, "SECOND", "P1", "BASE");
        initial = initial with { UntilEndOfTurnEffects = [..initial.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo-two-movers");
        await driver.Act("P1", new PlayCardCommand("BLADE", "OGN·262/298", ["ENEMY", "HOST"],
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["ENEMY", "SECOND"])]));
        Assert.Equal(2, Assert.Single(driver.State.StackItems).RepeatExecutions!.Count);
        var resolved = await driver.Top();
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", driver.State.ObjectLocations["SECOND"].BattlefieldObjectId);
        Assert.Equal(2, resolved.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(2, resolved.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ActualVexEntryRestrictionBlocksMovingTheNewUnitWhileTheStunOnlyChoiceRemainsLegal()
    {
        var initial = SacredProtectorTests.Unit(Position(), "VEX", "P2", "UNL-150/219");
        initial = AttackDamageDistributionTests.Spell(initial, "NEW-UNIT", "OGN·175/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects)
            { ["NEW-UNIT"] = initial.CardObjects["NEW-UNIT"] with { Tags = [CardObjectTags.UnitCard], Power = 3, ManaCost = 3 } } };
        var driver = Driver(initial, "actual-movement-restriction");
        await driver.Act("P1", new PlayCardCommand("NEW-UNIT", "OGN·175/298", []));
        Assert.Single(driver.State.StackItems, x => x.UnitEntryContext is not null);
        await driver.Top();
        Assert.Contains(MovementRestrictionRules.ForPlayer("P1"), driver.State.CardObjects["NEW-UNIT"].UntilEndOfTurnEffects);
        var legal = new PlayCardCommand("BLADE", "OGN·262/298", ["ENEMY", "NEW-UNIT"]);
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", new("restricted-move", prompt.PromptId!, driver.State.Tick, legal));
        Assert.True(quote.IsValid, quote.Message);
        await driver.Act("P1", legal);
        var resolved = await driver.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Contains("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal("BASE", driver.State.ObjectLocations["NEW-UNIT"].Zone);
        await Replay(initial, driver);
    }

    internal static MatchState Position()
    {
        var state = SacredProtectorTests.Unit(SacredProtectorTests.Position(), "ENEMY", "P2");
        state = AttackDamageDistributionTests.Spell(state, "BLADE", "OGN·262/298", "P1");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["F"] = state.CardObjects["F"] with { ControllerId = "P2" } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20,20) } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_ZENITH_MOVEMENT_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }

    internal static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (driver.NativeCommands > 0) Assert.Equal(driver.Commands().Length, driver.NativeCommands);
        driver.Export("steps.json", driver.Steps);
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands, accepted = driver.Commands().Length,
            coldRestores = driver.Commands().Length, fullJournalReplay = replay });
    }
}
