using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RideTheWindCurrentMovementTests
{
    internal const string Card = "OGN·173/298";

    [Theory]
    [InlineData("BASE", "BATTLEFIELD:F")]
    [InlineData("BATTLEFIELD:F", "BASE:P1")]
    [InlineData("BATTLEFIELD:F", "BATTLEFIELD:OTHER")]
    public async Task ActualFriendlyMovementUsesTheDeclaredDestinationThenReadies(string origin, string destination)
    {
        var initial = Position(origin);
        var driver = Driver(initial, origin.Replace(':','-') + "-to-" + destination.Replace(':','-'));
        await driver.Act("P1", new PlayCardCommand("WIND", Card, ["HOST"], Destination: destination));
        var result = await driver.Top();
        Assert.Equal(destination.StartsWith("BASE") ? "BASE" : "BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(destination.StartsWith("BASE") ? null : destination[12..], driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(0, driver.State.CardObjects["HOST"].ObjectGeneration);
        var events = result.Events.ToList();
        var moved = events.FindIndex(e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        var ready = events.FindIndex(e => e.Kind == "UNIT_READIED");
        Assert.True(moved >= 0 && ready > moved);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MovementProhibitionKeepsTheDeclaredBaseLegalButSkipsLinkedReady(bool temporary)
    {
        var initial = Position("BATTLEFIELD:F");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects)
            { ["F"] = initial.CardObjects["F"] with { CardNo = temporary ? initial.CardObjects["F"].CardNo : "OGN·295/298" },
              ["HOST"] = initial.CardObjects["HOST"] with { UntilEndOfTurnEffects = temporary ? [MovementRestrictionRules.ForPlayer("P1")] : [] } } };
        var command = new PlayCardCommand("WIND", Card, ["HOST"], Destination: "BASE:P1");
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", new("quote", prompt.PromptId!, initial.Tick, command));
        Assert.True(quote.IsValid, quote.Message);
        var driver = Driver(initial, temporary ? "temporary-prohibition" : "vilemaw-prohibition");
        await driver.Act("P1", command);
        var resolved = await driver.Top();
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(resolved.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_READIED");
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BASE:P2")]
    [InlineData("BATTLEFIELD:F")]
    [InlineData("BATTLEFIELD:FORGED")]
    [InlineData("HAND:P1")]
    public async Task MissingForeignUnchangedAndForgedDestinationsRejectWithoutMutation(string destination)
    {
        var initial = Position("BATTLEFIELD:F");
        var command = new PlayCardCommand("WIND", Card, ["HOST"], Destination: destination);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", new("quote", prompt.PromptId!, initial.Tick, command));
        Assert.False(quote.IsValid);
        var rejected = await engine.ResolveAsync(initial, new("bad", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task ActualReturnResponseMakesTheOldUnitAnIllegalTargetForMovementAndReady()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var driver = Driver(initial, "return-response");
        await driver.Act("P1", new PlayCardCommand("WIND", Card, ["HOST"], Destination: "BATTLEFIELD:F"));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        var generation = driver.State.CardObjects["HOST"].ObjectGeneration;
        var resolved = await driver.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD" or "UNIT_READIED");
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(generation, driver.State.CardObjects["HOST"].ObjectGeneration);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task EquipmentAndMarkedDamageFollowSuccessfulMovementBeforeTheUnitReadies()
    {
        var initial = AttachedRuleTextTests.AddSong(Position(), "SONG");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects)
            { ["HOST"] = initial.CardObjects["HOST"] with { Damage = 1 } } };
        var driver = Driver(initial, "equipment-and-damage");
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        await driver.Act("P1", new PlayCardCommand("WIND", Card, ["HOST"], Destination: "BATTLEFIELD:F"));
        await driver.Top();
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", driver.State.ObjectLocations["SONG"].BattlefieldObjectId);
        Assert.Equal("HOST", driver.State.CardObjects["SONG"].AttachedToObjectId);
        Assert.Equal(1, driver.State.CardObjects["HOST"].Damage);
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task OwnTurnSpellKeepsTheDeclaredDestinationWhenTheTargetMovesElsewhereInResponse()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("BATTLEFIELD:F"), "FLASH", "OGS·011/024", "P1");
        var driver = Driver(initial, "moves-in-response");
        await driver.Act("P1", new PlayCardCommand("WIND", Card, ["HOST"], Destination: "BATTLEFIELD:OTHER"));
        await driver.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        await driver.Top();
        Assert.Equal("BASE", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(0, driver.State.CardObjects["HOST"].ObjectGeneration);
        var resolved = await driver.Top();
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Contains(resolved.Events, e => e.Kind == "UNIT_READIED");
        await Replay(initial, driver);
    }

    [Fact]
    public async Task IndependentEchoExecutionsCanMoveTheSameUnitToDifferentDeclaredLocations()
    {
        var initial = Position();
        initial = initial with { UntilEndOfTurnEffects = [..initial.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo-two-locations");
        await driver.Act("P1", new PlayCardCommand("WIND", Card, ["HOST"], Destination: "BATTLEFIELD:F", OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["HOST"], Destination: "BATTLEFIELD:OTHER")]));
        var executions = Assert.Single(driver.State.StackItems).RepeatExecutions!;
        Assert.Equal(2, executions.Count);
        Assert.Equal("BATTLEFIELD:F", executions[0].Destination);
        Assert.Equal("BATTLEFIELD:OTHER", executions[1].Destination);
        var resolved = await driver.Top();
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(2, resolved.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(2, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    internal static MatchState Position(string origin = "BASE")
    {
        var s = AttackDamageDistributionTests.Spell(SacredProtectorTests.Position(), "WIND", Card, "P1");
        if (origin.StartsWith("BATTLEFIELD:")) s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BATTLEFIELD", origin[12..]);
        return s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects)
            { ["HOST"] = s.CardObjects["HOST"] with { IsExhausted = true } },
            RunePools = new Dictionary<string,RunePool>(s.RunePools) { ["P1"] = new(20,20), ["P2"] = new(20,20) } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_RIDE_WIND_EVIDENCE");
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
