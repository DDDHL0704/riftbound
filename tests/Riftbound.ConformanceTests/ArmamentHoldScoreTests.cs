using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentHoldScoreTests
{
    private const string Trinity = "SFD·115/221";
    private static MatchState Position(int copies = 1, bool attached = true, bool hostAtBase = false)
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", "OGN·001/298", "P1",
            hostAtBase ? "BASE" : "BATTLEFIELD", hostAtBase ? null : "BF");
        s = RadUnitEffectTestFixture.Place(s, "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < copies; i++)
        {
            var id = "TRINITY" + i;
            s = RadGearTestPosition.Put(s, id, Trinity, "P1", attached && !hostAtBase ? "BATTLEFIELD" : "BASE",
                attached && !hostAtBase ? "BF" : null);
            if (attached) s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                [id] = s.CardObjects[id] with { AttachedToObjectId = "HOST" } } };
        }
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(s with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
    }
    private static async Task Hold(Driver game)
    {
        await game.Act("P2", new EndTurnCommand());
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task EachAttachedTrinityEarnsOneIndependentPointAfterTheNormalHoldAndResponses(int copies)
    {
        var game = new Driver(Position(copies)); await Hold(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Equal(copies, game.State.StackItems.Count);
        Assert.All(game.State.StackItems, item => {
            Assert.Equal("HOLD", item.UnitEvent!.Timing); Assert.Equal(Trinity, item.CardNo);
            Assert.True(item.UnitEvent.Origin!.ArmamentGranted);
        });
        for (var i = 0; i < copies; i++) { await game.Top(); Assert.Equal(2 + i, game.State.PlayerScores["P1"]); }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnUnattachedWeaponOrAHostAtBaseDoesNotEarnThePoint(bool hostAtBase)
    {
        var game = new Driver(Position(attached: hostAtBase, hostAtBase: hostAtBase)); await Hold(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.DoesNotContain(game.State.StackItems, item => item.CardNo == Trinity); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ReturningTheHostOrDetachingTheWeaponPreservesAnAlreadyEarnedPoint(bool host)
    {
        var no = host ? "OGN·104/298" : "SFD·011/221";
        var s = RadGearTestPosition.Put(Position(), "RETURN", no, "P1", "HAND");
        for (var i = 0; i < 2; i++) s = RadGearTestPosition.Put(s, "RUNE" + i, "VEN·R01", "P1", "BASE");
        var game = new Driver(s); await Hold(game); await game.TapRunes("P1", ["RUNE0", "RUNE1"]);
        await game.Act("P1", new PlayCardCommand("RETURN", no, host ? ["HOST"] : ["HOST", "TRINITY0"])); await game.Top();
        Assert.Null(game.State.CardObjects["TRINITY0"].AttachedToObjectId);
        var start = game.Events().Length; await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]);
        if (host) Assert.DoesNotContain(game.Events().Skip(start), e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheSharedPointGainPreventionAlsoAppliesToTheExtraHoldPoint(bool onField)
    {
        var s = RadGearTestPosition.Put(Position(), "TIANNA", "SFD·060/221", "P2",
            onField ? "BATTLEFIELD" : "BASE", onField ? "OTHER" : null);
        var game = new Driver(s); await Hold(game);
        Assert.Equal(onField ? 0 : 1, game.State.PlayerScores["P1"]); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Equal(onField ? 0 : 2, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Fact]
    public async Task TheExtraHoldPointCanBeTheWinningPoint()
    {
        var s = Position() with { PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 0 } };
        var game = new Driver(s); await Hold(game); Assert.Equal(7, game.State.PlayerScores["P1"]);
        await game.Top(); Assert.Equal(8, game.State.PlayerScores["P1"]); Assert.Equal("P1", game.State.WinnerPlayerId); await game.Replay();
    }

    [Fact]
    public async Task NaturalConquestDoesNotTriggerTheHoldOnlyBox()
    {
        var s = Position(hostAtBase: true) with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        var game = new Driver(s); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.DoesNotContain(game.State.StackItems, item => item.CardNo == Trinity); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsErasingTheOriginalGrantedHoldPoint()
    {
        var game = new Driver(Position()); await Hold(game);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [] })); await game.Replay();
    }

    [Fact]
    public async Task ActualOrangeAssemblyAlsoGrantsTheNextNaturalHoldPoint()
    {
        var s = Position(attached: false) with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        var game = new Driver(s);
        await game.Act("P1", new AssembleEquipmentCommand("TRINITY0", "HOST", ["ASSEMBLE_ORANGE"])); await game.Top();
        for (var n = 0; game.State.SpellDuelState.IsActive; n++)
        {
            Assert.InRange(n, 0, 8);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
        await game.Act("P1", new EndTurnCommand()); await Hold(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]); await game.Replay();
    }
}
