using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class FriendlyMovementFollowTests
{
    private const string Pursuer = "OGN·177/298";
    private static MatchState Position(bool battlefield = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "FOLLOWER", Pursuer, "P1", "BASE");
        if (battlefield) {
            state = RadUnitEffectTestFixture.Place(state, "FOLLOWER", "P1", "BATTLEFIELD", "BF");
            state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        }
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FOLLOWER"] = state.CardObjects["FOLLOWER"] with { IsExhausted = true } } };
    }
    private static MoveUnitCommand Move(bool battlefield = false)
        => new("ALLY", battlefield ? "BATTLEFIELD" : "BASE", battlefield ? "BASE" : "BATTLEFIELD:BF", []);
    private static async Task Decide(Driver game, string choice)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("FRIENDLY_MOVEMENT_FOLLOW", request.Kind); Assert.Equal("P1", request.PlayerId);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice]));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task ASeparateRespondableTriggerMayFollowEachActualFriendlyMovementWithoutExhaustingAgain(bool battlefield, bool follow)
    {
        var game = new Driver(Position(battlefield)); var origin = game.State.ObjectLocations["FOLLOWER"];
        await game.Act("P1", Move(battlefield));
        var context = Assert.Single(game.State.StackItems).MovementObserver!;
        Assert.Equal(TriggerKinds.FriendlyUnitMovedFromHereMayFollow, context.Kind);
        Assert.Equal(origin, context.SourceLocation);
        Assert.Equal(origin, game.State.ObjectLocations["FOLLOWER"]);
        Assert.Null(game.State.PendingRuleChoice);
        await game.Top();
        Assert.Equal(origin, game.State.ObjectLocations["FOLLOWER"]);
        await Decide(game, follow ? "FOLLOW" : "DECLINE");
        Assert.Equal(follow ? game.State.ObjectLocations["ALLY"] : origin, game.State.ObjectLocations["FOLLOWER"]);
        Assert.True(game.State.CardObjects["FOLLOWER"].IsExhausted);
        Assert.Equal(follow ? 1 : 0, game.Events().Count(entry => entry.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "FOLLOWER")));
        await game.Replay();
    }

    [Theory]
    [InlineData("HAND")] [InlineData("BATTLEFIELD")]
    public async Task ASourceInHandOrAtAnotherOriginDoesNotObserveTheMove(string sourceZone)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "FOLLOWER", "P1", sourceZone, sourceZone == "BATTLEFIELD" ? "OTHER" : null);
        var game = new Driver(initial);
        await game.Act("P1", Move());
        Assert.DoesNotContain(game.State.StackItems, item => item.MovementObserver?.Kind == TriggerKinds.FriendlyUnitMovedFromHereMayFollow);
        await game.Replay();
    }

    [Fact]
    public async Task MovingThePursuerItselfCannotTriggerItsOwnFollowSkill()
    {
        var initial = Position() with { CardObjects = new Dictionary<string, CardObjectState>(Position().CardObjects) {
            ["FOLLOWER"] = Position().CardObjects["FOLLOWER"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("FOLLOWER", "BASE", "BATTLEFIELD:BF", []));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheCapturedMoveDestinationSurvivesAMoveOrReturnResponseToTheOriginalMover(bool returned)
    {
        var no = returned ? "OGN·104/298" : "OGS·011/024";
        var game = new Driver(RadGearTestPosition.Put(Position(), "RESPONSE", no, "P1", "HAND"));
        await game.Act("P1", Move());
        await game.Act("P1", new PlayCardCommand("RESPONSE", no, ["ALLY"]));
        await game.Top();
        Assert.Equal(returned ? "HAND" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        await game.Top();
        await Decide(game, "FOLLOW");
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations["FOLLOWER"]);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceCancelsItsMoveWithoutLinkingItsNewPrivateIdentity()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", Move());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FOLLOWER"]));
        await game.Top();
        var before = game.Events().Length;
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice); Assert.Contains("FOLLOWER", game.State.PlayerZones["P1"].Hand);
        var resolved = game.Events().Skip(before).Where(entry => entry.Event.Kind == "TRIGGER_RESOLVED");
        Assert.DoesNotContain(resolved, entry => Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "FOLLOWER"));
        await game.Replay();
    }

    [Fact]
    public async Task ASourceAlreadyAtTheCapturedDestinationDoesNotMoveASecondTime()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(true), "FLASH", "OGS·011/024", "P1", "HAND"));
        await game.Act("P1", Move(true));
        await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["FOLLOWER"]));
        await game.Top();
        var before = game.Events().Length;
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.Events().Skip(before), entry => entry.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        await game.Replay();
    }

    [Fact]
    public async Task FriendshipUsesTheMovedUnitsControllerInsteadOfItsOwner()
    {
        var initial = Position() with { CardObjects = new Dictionary<string, CardObjectState>(Position().CardObjects) {
            ["ALLY"] = Position().CardObjects["ALLY"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", Move());
        await game.Top();
        await Decide(game, "FOLLOW");
        Assert.Equal("BF", game.State.ObjectLocations["FOLLOWER"].BattlefieldObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task AnotherPlayersUnitAtTheSameBattlefieldDoesNotTriggerFollow()
    {
        var initial = Position(true);
        initial = RadUnitEffectTestFixture.Place(initial, "FOLLOWER", "P2", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", Move(true));
        Assert.DoesNotContain(game.State.StackItems, item => item.MovementObserver?.Kind == TriggerKinds.FriendlyUnitMovedFromHereMayFollow);
        await game.Replay();
    }

    [Fact]
    public async Task TwoActualFriendlyMovesCanTriggerTheSameSourceAgainAfterFollowing()
    {
        var game = new Driver(Position());
        await game.Act("P1", Move()); await game.Top(); await Decide(game, "FOLLOW");
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", Move(true)); await game.Top(); await Decide(game, "FOLLOW");
        Assert.Equal("BASE", game.State.ObjectLocations["FOLLOWER"].Zone);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("observerKind"), TriggerKinds.FriendlyUnitMovedFromHereMayFollow)));
        await game.Replay();
    }

    [Fact]
    public async Task CopiedPrintedTextCreatesIndependentFollowSkillsWithoutMovingTwiceToTheSamePlace()
    {
        var game = new Driver(AttachedRuleTextTests.AddSong(Position(), "SONG"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "FOLLOWER", ["ASSEMBLE_GREEN"]));
        await game.Top();
        await game.Act("P1", Move());
        var triggers = game.State.TriggerQueue.Where(trigger => trigger.MovementObserver?.Kind
            == TriggerKinds.FriendlyUnitMovedFromHereMayFollow).ToArray();
        Assert.Equal(2, triggers.Length); Assert.Single(triggers, trigger => trigger.MovementObserver!.Origin is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        var steps = 0;
        while (game.State.StackItems.Count > 0) {
            Assert.True(steps++ < 3);
            await game.Top();
            if (game.State.PendingRuleChoice is not null) await Decide(game, "FOLLOW");
        }
        Assert.Equal("BF", game.State.ObjectLocations["FOLLOWER"].BattlefieldObjectId);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "FOLLOWER"));
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentMovingTheFriendlyUnitStillEarnsTheControllersFollowSkill()
    {
        var initial = RadGearTestPosition.Put(Position(true), "FORCE", "OGN·168/298", "P2", "HAND")
            with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("FORCE", "OGN·168/298", ["ALLY"])); await game.Top();
        var context = Assert.Single(game.State.StackItems).MovementObserver!;
        Assert.Equal("P2", context.Unit.MovingPlayerId); Assert.Equal("P1", context.Unit.ControllerId);
        await game.Top(); await Decide(game, "FOLLOW");
        Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations["FOLLOWER"]);
        await game.Replay();
    }

    [Fact]
    public async Task FollowingIsASeparateMovementInstructionThatPaysItsOwnImposedCost()
    {
        var initial = RadGearTestPosition.Put(Position(), "TAX", "RAD·062/167", "P2", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TAX"] = initial.CardObjects["TAX"] with { Tags = [..initial.CardObjects["TAX"].Tags, CardEquipmentKeywordNames.Deploy] } } };
        var game = new Driver(initial);
        await game.Act("P1", Move());
        Assert.Equal(29, game.State.RunePools["P1"].Mana); Assert.Equal(29, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await Decide(game, "FOLLOW");
        if (game.State.PendingRuleChoice is { } fee) {
            Assert.Equal(1, fee.Request.ManaCost); Assert.Equal(1, fee.Request.PowerCost);
            Assert.Contains(fee.Request.Options, option => option.Id == "PAY");
            await game.Act("P1", new PayCostCommand(fee.Request.Id, "RULE_REPLACEMENT", ["PAY"]));
        }
        Assert.Equal(28, game.State.RunePools["P1"].Mana); Assert.Equal(28, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("BF", game.State.ObjectLocations["FOLLOWER"].BattlefieldObjectId);
        await game.Replay();
    }

    [Theory]
    [InlineData("source-generation")] [InlineData("friendship")] [InlineData("origin")] [InlineData("tick")]
    public async Task RecoveryRejectsForgedFollowFactsAndSourceBindings(string changed)
    {
        var game = new Driver(Position()); await game.Act("P1", Move());
        var item = Assert.Single(game.State.StackItems); var context = item.MovementObserver!;
        context = changed switch {
            "source-generation" => context with { SourceGeneration = context.SourceGeneration + 1 },
            "friendship" => context with { Unit = context.Unit with { ControllerId = "P2", Origin = context.Unit.Origin with { PlayerId = "P2" } } },
            "origin" => context with { SourceLocation = new("P1", "BATTLEFIELD", "BF") },
            _ => context with { CapturedTick = game.State.Tick + 1 } };
        var forged = game.State with { StackItems = [item with { MovementObserver = context }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }
}
