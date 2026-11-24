using Riftbound.Api.Hubs;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GameLogTextTests
{
    [Fact]
    public async Task HoldLogsNameTheBattlefieldSkillAndMinionInLiveAndReplayedEvents()
    {
        var initial = OfficialHoldSequenceTests.State("OGN·275/298");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PassPriorityCommand());
        await game.Top();

        var events = game.Events().Select(entry => entry.Event).ToArray();
        Assert.Contains(events, e => e.Kind == "BATTLEFIELD_HELD" && e.Description.Contains("据守团结圣坛"));
        Assert.Contains(events, e => e.Kind == "TRIGGER_QUEUED" && e.Description == "团结圣坛的据守技能触发");
        Assert.Contains(events, e => e.Kind == "STACK_ITEM_RESOLVED" && e.Description == "团结圣坛的技能结算");
        Assert.Contains(events, e => e.Kind == "UNIT_TOKEN_CREATED" && e.Description == "团结圣坛打出随从");
        AssertReadable(events);
        await game.Replay();
    }

    [Fact]
    public async Task SharedStandbyEventsKeepTheHiddenCardNameOutOfTheOpponentLog()
    {
        const string cardNo = "OGN·057/298";
        var initial = StandbySourceRulesTests.Position(cardNo);
        var game = new StandbyDefendObserverTests.Driver(initial);
        var hidden = await game.Act("P1", new HideCardCommand("SHADOW", cardNo, "BATTLEFIELD:F", ["STANDBY_A"]));

        // The hub broadcasts this same event projection to both players.
        var events = GameEventObjectRefProjector.ProjectEvents(hidden.Events, hidden.State);
        var publicDescription = Assert.Single(events, e => e.Kind == "CARD_HIDDEN").Description;
        Assert.Contains("背面待命牌", publicDescription);
        var hiddenName = OfficialCardSourceIdentityGroups.KnownDisplayName(cardNo)!;
        Assert.DoesNotContain(events, e => e.Description.Contains(hiddenName, StringComparison.Ordinal)
            || e.Description.Contains("SHADOW", StringComparison.Ordinal));
        Assert.Equal("背面待命牌", GameEventText.ObjectName(hidden.State, "SHADOW"));
        Assert.Equal("一张牌", GameEventText.ObjectName(initial, "SHADOW"));
        Assert.Equal("一张牌", GameEventText.ObjectName(initial, initial.PlayerZones["P1"].MainDeck[0]));
        AssertReadable(events);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, game.Events());
        Assert.True(replay.IsMatch, string.Join(Environment.NewLine, replay.Errors));
    }

    [Fact]
    public async Task StarfallRejectsIncompleteTargetsInChineseWithoutChangingTheState()
    {
        const string spellId = "P1-MAIN-OGN-029-298-01";
        var initial = AttackDamageDistributionTests.Spell(ArmamentMightTests.Position(), spellId, "OGN·029/298", "P1");
        var command = new PlayCardCommand(spellId, "OGN·029/298", ["HOST"]);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("starfall-incomplete", "P1", command.CmdType), command, default);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("星落需要选择 2 个单位作为目标。", result.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);

        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", command with { TargetObjectIds = ["HOST", "HOST"] });
        await game.Top();
        var events = game.Events().Select(entry => entry.Event).ToArray();
        Assert.Contains(events, e => e.Kind == "STACK_ITEM_RESOLVED" && e.Description == "星落结算");
        AssertReadable(events);
        await game.Replay();
    }

    private static void AssertReadable(IEnumerable<GameEvent> events)
    {
        Assert.All(events, e =>
        {
            Assert.Matches("[\u4e00-\u9fff]", e.Description);
            Assert.DoesNotMatch(@"-MAIN-|-RUNE-|-BATTLEFIELD-|STACK-|ordered-|RECYCLE-MIGHT", e.Description);
        });
    }
}
