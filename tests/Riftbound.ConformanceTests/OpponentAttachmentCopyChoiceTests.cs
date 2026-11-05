using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OpponentAttachmentCopyChoiceTests
{
    internal static MatchState Position(bool otherFriendly = true)
    {
        var state = ShadySpectaclesTests.Position("SFD·050/221");
        foreach (var id in new[] { "HOST", "GEAR", "COPY-SOURCE" })
        {
            state = LocalDestructionRecallTests.At(state, id, "P2", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { OwnerId = "P2" } } };
        }
        if (!otherFriendly)
            state = LocalDestructionRecallTests.At(state, "COPY-SOURCE", "P1", "BASE");
        state = OfficialHoldSequenceTests.AddUnit(state, "P1-SOURCE", "OGN·001/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "P1-SOURCE", "P1", "BASE");
        return OfficialCopyIdentityTests.AddSpell(state, "TAKE-UP", "SFD·011/221");
    }

    internal static async Task Cast(Driver driver)
    {
        var mana = driver.State.RunePools["P1"].Mana;
        await driver.Act("P1", new PlayCardCommand("TAKE-UP", "SFD·011/221", ["HOST", "GEAR"]));
        Assert.True(driver.State.RunePools["P1"].Mana < mana);
        await driver.Top();
    }

    internal static async Task Finish(Driver driver, MatchState initial)
    {
        Assert.NotNull(driver.State.PendingRuleChoice);
        var request = driver.State.PendingRuleChoice.Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", request.Kind);
        Assert.Equal("COPY:COPY-SOURCE", Assert.Single(request.Options).Id);
        await driver.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("SFD·050/221", driver.State.CardObjects["HOST"].CardNo);
        Assert.Equal("P2", driver.State.CardObjects["HOST"].OwnerId);
        Assert.Equal("P2", driver.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(initial.CardObjects["HOST"].ObjectGeneration, driver.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal("HOST", driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("P2", driver.State.CardObjects["GEAR"].ControllerId);
        Assert.Contains(initial.PlayerZones["P1"].MainDeck[0], driver.State.PlayerZones["P1"].Hand);
        Assert.Null(driver.State.PendingRuleChoice);
        await driver.Replay();
    }

    [Fact]
    public async Task OpponentEquipmentControllerChoosesItsOwnFriendlyCopyAfterTakeUp()
    {
        var initial = Position(); var driver = new Driver(initial);
        await Cast(driver);
        await Finish(driver, initial);
    }

    [Theory]
    [InlineData("P1", "COPY:COPY-SOURCE")]
    [InlineData("P2", "COPY:HOST")]
    [InlineData("P2", "COPY:P1-SOURCE")]
    public async Task CasterSelfAndEnemyChoicesAreAtomicAndDoNotBlockTheLegalChoice(string actor, string selection)
    {
        var initial = Position(); var driver = new Driver(initial);
        await Cast(driver);
        var before = driver.State;
        var request = before.PendingRuleChoice!.Request;
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(before));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(before));
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("invalid-copy", actor, CommandTypes.PayCost),
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selection]), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
        await Finish(driver, initial);
    }

    [Fact]
    public async Task WithoutAnotherEquipmentControllersUnitAttachmentAndCasterDrawStillComplete()
    {
        var initial = Position(false); var driver = new Driver(initial);
        await Cast(driver);
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal("HOST", driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(initial.CardObjects["HOST"].CardNo, driver.State.CardObjects["HOST"].CardNo);
        Assert.Null(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Contains(initial.PlayerZones["P1"].MainDeck[0], driver.State.PlayerZones["P1"].Hand);
        await driver.Replay();
    }
}
