using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHeimerdingerCopyFaqTests
{
    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task TheAttendantsImagesBecomeRobotsBeforeTheEmbeddedSkillCopiesTheAttendant(string destination)
    {
        const string attendant = "UNL-081/219";
        var state = VenUnitEntryBasicTests.Position(attendant);
        state = state with {
            RoomId = "RAD-HEIMER-ATTENDANT-FAQ",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HEIMER"] = OfficialGarenConquestBattlefieldCountTests.Printed("HEIMER", "RAD·132/167", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["HEIMER"] = new("P1", "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = [..state.PlayerZones["P1"].Battlefields, "HEIMER"] } }
        };
        var game = new Driver(state) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("UNIT", attendant, [], Destination: destination));
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Empty(Tokens(game.State));
        Assert.Equal("UNIT", Assert.Single(game.State.StackItems).SelfCopySource!.ObjectId);
        await game.ColdCheckpoint();
        await game.Top();
        for (var index = 0; index < 2; index++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow);
            Assert.Equal(new[] { "HEIMER" }, choice.LegalObjectIds);
            Assert.Empty(Tokens(game.State));
            await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HEIMER"]));
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
            await game.ColdCheckpoint();
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HEIMER"]));
        }
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingRuleChoice);
        var robots = Tokens(game.State);
        Assert.Equal(2, robots.Length);
        var location = game.State.ObjectLocations["UNIT"];
        foreach (var robot in robots)
        {
            Assert.Equal("SFD·T01", robot.TokenFactoryCardNo);
            Assert.Equal("SFD·T01", robot.CardNo);
            Assert.Equal(3, robot.Power);
            Assert.Contains("机械", robot.Tags);
            Assert.DoesNotContain(CardObjectTags.Ephemeral, robot.Tags);
            Assert.True(robot.IsExhausted);
            Assert.Equal(location, game.State.ObjectLocations[robot.ObjectId]);
        }
        var copy = Assert.Single(game.State.StackItems).ReflexiveCopy!;
        Assert.True(copy.CopySourceIsSelf);
        Assert.Equal(new ObjectBinding("UNIT", game.State.CardObjects["UNIT"].ObjectGeneration), copy.CopySource);
        Assert.Equal(robots.Select(r => r.ObjectId).Order(StringComparer.Ordinal), copy.Recipients.Select(r => r.ObjectId).Order(StringComparer.Ordinal));
        Assert.All(copy.Recipients, r => Assert.Equal(game.State.CardObjects[r.ObjectId].ObjectGeneration, r.Generation));
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        await game.ColdCheckpoint();
        await game.Top();
        foreach (var original in robots)
        {
            var copied = game.State.CardObjects[original.ObjectId];
            Assert.Equal("SFD·T01", copied.TokenFactoryCardNo);
            Assert.Equal(attendant, copied.CardNo);
            Assert.Equal(1, copied.Power);
            Assert.Equal(2, copied.ManaCost);
            Assert.Contains(CardObjectTags.UnitCard, copied.Tags);
            Assert.Contains(CardObjectTags.Ephemeral, copied.Tags);
            Assert.Contains("待命", copied.Tags);
            Assert.DoesNotContain("机械", copied.Tags);
            Assert.True(copied.IsExhausted);
            Assert.Equal("P1", copied.OwnerId);
            Assert.Equal("P1", copied.ControllerId);
            Assert.Equal(original.ObjectGeneration, copied.ObjectGeneration);
            Assert.Equal(location, game.State.ObjectLocations[copied.ObjectId]);
        }
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_COPY_RESOLVED"));
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingPayment);
        await game.ColdCheckpoint();
        await game.Replay();
    }

    private static CardObjectState[] Tokens(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is not null).ToArray();

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("heimer-faq-rejected", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
