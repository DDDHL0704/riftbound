using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadZiggsCopiedDefenseTests
{
    private static MatchState Position(bool onlySong)
    {
        var state = AhriCombatMightTests.Board("RAD·112/167", defending: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY1", "RAD·112/167", "P2") } };
        foreach (var (id, no) in new[] { ("GEAR", "SFD·059/221"), ("SONG2", "SFD·059/221"),
            ("JUNK", "SFD·022/221") })
        {
            if (onlySong && id != "GEAR") continue;
            state = LocalDestructionRecallTests.At(state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P2") }
            }, id, "P2", "BASE");
        }
        foreach (var spell in new[] { "ATTACH", "ATTACH2", "DETACH" })
            state = OfficialCopyIdentityTests.AddSpell(state, spell, "SFD·011/221");
        return state;
    }

    private static async Task Toggle(Driver game, string spell, string gear)
    {
        if (game.State.PriorityPlayerId is { } priority && priority != "P1")
            await game.Act(priority, new PassPriorityCommand());
        // Take Up may attach another player's armament to their unit. This
        // prepares a real copied defender without changing turns or its journal.
        await game.Act("P1", new PlayCardCommand(spell, "SFD·011/221", ["ENEMY1", gear]));
        await game.Top();
    }

    private static async Task<Driver> Attached(int songs = 1, bool onlySong = false)
    {
        var game = new Driver(Position(onlySong));
        await Toggle(game, "ATTACH", "GEAR");
        Assert.Equal("RAD·112/167", game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        if (songs == 2)
        {
            await Toggle(game, "ATTACH2", "SONG2");
            Assert.Equal("RAD·112/167", game.State.CardObjects["SONG2"].AttachedRuleText!.CardNo);
        }
        return game;
    }

    private static async Task CaptureDefense(Driver game, int skills)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P2", new OrderTriggersCommand(
                OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        Assert.Equal(skills, game.State.StackItems.Count);
        Assert.All(game.State.StackItems, item => {
            Assert.Equal("ENEMY1", item.SourceObjectId);
            Assert.Equal("DEFEND", item.UnitEvent!.Timing);
            Assert.Equal("RAD·112/167", item.UnitEvent.CardNo);
        });
        Assert.True(game.State.CardObjects["ENEMY1"].IsDefending);
        Assert.Null(game.State.PendingRuleChoice);
    }

    private static async Task ResolveSkills(Driver game, params string[] destroyed)
    {
        var initialPower = game.State.CardObjects["ENEMY1"].Power;
        for (var index = 0; index < destroyed.Length; index++)
        {
            await game.Top();
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("DESTRUCTION_SELECTION", choice.Request.Kind);
            Assert.Equal("P2", choice.Request.PlayerId);
            Assert.Contains(choice.Request.Options, option => option.Id == "DESTROY:" + destroyed[index]);
            var firstEvent = game.Events().Length;
            await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["DESTROY:" + destroyed[index]]));
            Assert.Contains(destroyed[index], game.State.PlayerZones["P2"].Graveyard);
            Assert.Equal(initialPower + 2 * (index + 1), game.State.CardObjects["ENEMY1"].Power);
            Assert.Single(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "UNIT_POWER_CHANGED");
        }
        Assert.Empty(game.State.StackItems);
        Assert.True(game.State.CardObjects["ENEMY1"].IsDefending);
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task RealSongsCaptureIndependentDefenseSkillsThatSurviveDestroyingTheirOwnEquipment(int songs)
    {
        var game = await Attached(songs);
        await CaptureDefense(game, songs + 1);
        Assert.Equal(songs, game.State.StackItems.Count(item => item.UnitEvent!.Origin is not null));
        await ResolveSkills(game, songs == 1 ? ["GEAR", "JUNK"] : ["GEAR", "SONG2", "JUNK"]);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        if (songs == 2) Assert.Null(game.State.CardObjects["SONG2"].AttachedRuleText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealDetachmentRemovesFutureDefenseTriggersAndPreservesAlreadyCapturedOnes(bool afterCapture)
    {
        var game = await Attached();
        if (afterCapture) await CaptureDefense(game, 2);
        await Toggle(game, "DETACH", "GEAR");
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        if (!afterCapture) await CaptureDefense(game, 1);
        Assert.Equal(afterCapture ? 1 : 0, game.State.StackItems.Count(item => item.UnitEvent!.Origin is not null));
        await ResolveSkills(game, afterCapture ? ["GEAR", "JUNK"] : ["JUNK"]);
    }

    [Fact]
    public async Task DestroyingTheOnlySongLeavesTheOtherCapturedDefenseSkillWithoutDestructionOrReward()
    {
        var game = await Attached(onlySong: true);
        await CaptureDefense(game, 2);
        var power = game.State.CardObjects["ENEMY1"].Power;
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(new[] { "SKIP", "DESTROY:GEAR" }, choice.Request.Options.Select(option => option.Id));
        await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["DESTROY:GEAR"]));
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(power + 2, game.State.CardObjects["ENEMY1"].Power);
        Assert.Single(game.State.StackItems);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(power + 2, game.State.CardObjects["ENEMY1"].Power);
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "UNIT_POWER_CHANGED");
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
