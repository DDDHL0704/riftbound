using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 383.4.c-d, 718 and SFD judge FAQ260114 p11.
public sealed class ArmamentHoldConquestAliasTests
{
    private const string Armament = "SFD·030/221";

    private static MatchState Board(string host, bool hold = false, int copies = 1)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·296/298"),
            "HOST", host, "P1", "BASE");
        if (hold) state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < copies; i++)
        {
            var id = "ARMAMENT" + i;
            state = RadGearTestPosition.Put(state, id, Armament, "P1", hold ? "BATTLEFIELD" : "BASE", hold ? "BF" : null);
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { AttachedToObjectId = "HOST" } } };
        }
        if (hold) state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task AHoldDrawIsEarnedByNaturalConquestAndMultipleAliasArmamentsDoNotDuplicateIt(int copies)
    {
        var game = new Driver(Board("SFD·027/221", copies: copies));
        var top = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await Conquer(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(top, game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_HELD");
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task AConquestDrawIsEarnedByNaturalHoldBeforeNormalChannelAndDraw(int copies)
    {
        var game = new Driver(Board("OGN·039/298", hold: true, copies: copies));
        var top = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(top, game.State.PlayerZones["P1"].Hand); // One skill draw, then normal draw.
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Fact]
    public async Task AnUnattachedArmamentDoesNotGiveTheHostItsHoldSkillDuringConquest()
    {
        var state = Board("SFD·027/221", copies: 0);
        state = RadGearTestPosition.Put(state, "LOOSE", Armament, "P1", "BASE");
        var game = new Driver(state); await Conquer(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task TheOfficialYoneOpenBattlefieldConditionStillFailsDuringHolding()
    {
        var game = new Driver(Board("SFD·116/221", hold: true));
        await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHostAfterTheConquestKeepsTheEarnedHoldDrawIndependent()
    {
        var initial = RadGearTestPosition.Put(Board("SFD·027/221"), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); var top = initial.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await Conquer(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.All(top, id => Assert.Contains(id, game.State.PlayerZones["P1"].Hand));
        await game.Replay();
    }
}
