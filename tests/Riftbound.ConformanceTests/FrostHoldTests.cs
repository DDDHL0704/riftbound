using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;

public sealed class FrostHoldTests
{
    internal static MatchState Position(bool empty = false, bool elder = false)
    {
        var s = OfficialHoldSequenceTests.State("UNL-212/219");
        s = OfficialHoldSequenceTests.AddUnit(s, "TARGET", "OGN·001/298", empty ? "OTHER" : "F", "P2");
        s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["TARGET"] = s.CardObjects["TARGET"] with { Power = 8 },
            ["ELDER"] = new("ELDER", cardNo: "UNL-118/219", power: 10, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
        return elder ? At(s, "ELDER", "P1", "BASE") : s;
    }
    internal static Task<ResolutionResult> Start(MatchState s) => Act(s, s.TurnPlayerId, new PassPriorityCommand());
    [Fact]
    public async Task BattlefieldStartDamageRespondsBeforeScoreChannelAndDraw()
    {
        var opened = await Start(Position());
        var item = Assert.Single(opened.State.StackItems);
        Assert.Equal("SCORE", opened.State.TurnStartStep);
        Assert.Equal("P1", item.ControllerId);
        Assert.Equal(0, opened.State.CardObjects["TARGET"].Damage);
        Assert.Empty(opened.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, opened.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(0, opened.State.PlayerScores["P1"]);
        Restore(opened.State);
        var first = await Act(opened.State, "P1", new PassPriorityCommand());
        Assert.Equal(0, first.State.CardObjects["TARGET"].Damage);
        var resolved = await Act(first.State, "P2", new PassPriorityCommand());
        Assert.Equal(1, resolved.State.CardObjects["TARGET"].Damage);
        Assert.Equal(new[]{"P1"}, resolved.State.CardObjects["TARGET"].DamageMarkedByPlayerIds);
        Assert.Equal(MatchPhases.Main, resolved.State.Phase);
        Assert.Single(resolved.State.PlayerZones["P1"].Hand);
        Assert.Single(resolved.State.PlayerZones["P1"].RuneDeck);
        Restore(resolved.State);
    }
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task EmptyBattlefieldStillQueuesAndIncomingEnemyUsesTurnPlayersElder(string fieldController)
    {
        var s = Position(empty: true, elder: true);
        s = At(s, "F", fieldController, "BATTLEFIELD", "F");
        var opened = await Start(s);
        Assert.Single(opened.State.StackItems);
        Assert.Equal(fieldController, opened.State.StackItems[0].ControllerId);
        // Isolated response-state mutation tests resolution-time location lookup.
        // Actual reaction command coverage is separate and must not be inferred here.
        var incoming = At(opened.State, "TARGET", "P2", "BATTLEFIELD", "F");
        Restore(incoming);
        var resolved = await Top(incoming);
        if (fieldController == "P1") Assert.Contains("TARGET", resolved.State.PlayerZones["P2"].Graveyard);
        else {
            Assert.Contains("TARGET", resolved.State.PlayerZones["P2"].Battlefields);
            Assert.Equal(1, resolved.State.CardObjects["TARGET"].Damage);
            Assert.Equal(new[] { "P2" }, resolved.State.CardObjects["TARGET"].DamageMarkedByPlayerIds);
        }
        Restore(resolved.State);
    }
    [Theory]
    [InlineData("BASE")] [InlineData("OTHER")] [InlineData("hidden")]
    public async Task ResponseLocationChangesExcludeUnitsNoLongerPubliclyHere(string destination)
    {
        var opened = await Start(Position());
        var state = destination == "BASE" ? At(opened.State, "TARGET", "P2", "BASE")
            : destination == "OTHER" ? At(opened.State, "TARGET", "P2", "BATTLEFIELD", "OTHER") : opened.State;
        if (destination == "hidden") state = state with { CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) {
            ["TARGET"] = state.CardObjects["TARGET"] with { IsFaceDown = true, Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] } } };
        Restore(state);
        var resolved = await Top(state);
        Assert.Equal(0, resolved.State.CardObjects["TARGET"].Damage);
        Assert.Null(resolved.State.CardObjects["TARGET"].DamageMarkedByPlayerIds);
        Restore(resolved.State);
    }
    [Fact]
    public async Task CapturedSkillKeepsItsFieldControllerAndAmountAfterSourceChanges()
    {
        var opened = await Start(Position(elder: true));
        var state = At(opened.State, "F", "P2", "BATTLEFIELD", "F");
        state = state with { CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { CardNo = "OGN·294/298", ObjectGeneration = state.CardObjects["F"].ObjectGeneration + 1 } } };
        Restore(state);
        var done = await Top(state);
        Assert.Contains("TARGET", done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(done.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED" && Equals(e.Payload["playerId"], "P1"));
        Restore(done.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TwoBattlefieldsUseOrderedSeparateResponseWindows(bool reverse)
    {
        var s = Position();
        s = OfficialHoldSequenceTests.AddUnit(s, "OTHER-TARGET", "OGN·001/298", "OTHER", "P2");
        s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["OTHER"] = s.CardObjects["OTHER"] with { CardNo = "UNL-212/219", ControllerId = "P1" },
            ["OTHER-TARGET"] = s.CardObjects["OTHER-TARGET"] with { Power = 8 } } };
        var opened = await Start(s);
        Assert.Equal(2, opened.State.TriggerQueue.Count); Assert.Empty(opened.State.StackItems);
        Restore(opened.State);
        var ids = opened.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        if (reverse) Array.Reverse(ids);
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        Restore(ordered.State);
        var firstField = ordered.State.StackItems[^1].BattlefieldStart!.BattlefieldObjectId;
        var first = await Top(ordered.State);
        Assert.Single(first.State.StackItems); Assert.Equal("SCORE", first.State.TurnStartStep);
        Assert.Equal(firstField == "F" ? 1 : 0, first.State.CardObjects["TARGET"].Damage);
        Assert.Equal(firstField == "OTHER" ? 1 : 0, first.State.CardObjects["OTHER-TARGET"].Damage);
        Assert.Empty(first.State.PlayerZones["P1"].Hand); Restore(first.State);
        var done = await Top(first.State);
        Assert.Equal(1, done.State.CardObjects["TARGET"].Damage);
        Assert.Equal(1, done.State.CardObjects["OTHER-TARGET"].Damage); Restore(done.State);
    }
    [Fact]
    public async Task SecondPlayersTurnOwnsDamageRegardlessOfBattlefieldController()
    {
        var s = Position(); s = At(s, "TARGET", "P1", "BATTLEFIELD", "F");
        s = s with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var opened = await Start(s); Assert.Equal("P1", Assert.Single(opened.State.StackItems).ControllerId);
        var done = await Top(opened.State);
        Assert.Equal(new[]{"P1"}, done.State.CardObjects["TARGET"].DamageMarkedByPlayerIds); Restore(done.State);
    }
    [Theory]
    [InlineData("global")] [InlineData("next")] [InlineData("immune")]
    public async Task PreventedBattlefieldSkillDamageDoesNotTriggerElderLethality(string prevention)
    {
        var opened = await Start(Position(elder: true)); var s = opened.State;
        if (prevention == "global") s = s with { UntilEndOfTurnEffects = [..s.UntilEndOfTurnEffects, "PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN"] };
        else s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["TARGET"] = prevention == "next" ? s.CardObjects["TARGET"] with { UntilEndOfTurnEffects = ["PREVENT_NEXT_DAMAGE_THIS_TURN"] }
                : s.CardObjects["TARGET"] with { CardNo = "OGN·189/298", MovementCountThisTurn = 2 } } };
        var done = await Top(s);
        Assert.Contains("TARGET", done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, done.State.CardObjects["TARGET"].Damage); Assert.Null(done.State.CardObjects["TARGET"].DamageMarkedByPlayerIds); Restore(done.State);
    }
    [Theory]
    [InlineData("amount")] [InlineData("card")] [InlineData("field")] [InlineData("controller")] [InlineData("missing")]
    public async Task RecoveryRejectsForgedCapturedBattlefieldStartAbility(string change)
    {
        var opened = await Start(Position()); var item = opened.State.StackItems[0];
        var context = item.BattlefieldStart!;
        var bad = item with { ControllerId = change == "controller" ? "P2" : item.ControllerId,
            BattlefieldStart = change switch {
                "amount" => context with { Damage = 7 }, "card" => context with { CardNo = "OGN·294/298" },
                "field" => context with { BattlefieldObjectId = "OTHER" }, "missing" => null, _ => context } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with { StackItems = [bad] }), e => e.Contains("captured battlefield start"));
    }
    [Fact]
    public async Task BothPlayersAndSpectatorRejectAlteredPublicBattlefieldContext()
    {
        var opened = await Start(Position()); var s = opened.State;
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,s.Tick,0,opened.Snapshots[id],s.Tick,0,opened.Prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        Assert.Empty(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator));
        foreach (var snapshot in views.Values.Select(v => v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var payload = Assert.IsType<Dictionary<string,object?>>(Assert.Single(snapshot.Stack));
            var original = payload["battlefieldStart"];
            payload["battlefieldStart"] = s.StackItems[0].BattlefieldStart! with { BattlefieldObjectId = "OTHER" };
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator), e => e.Contains("battlefield start context differs"));
            payload["battlefieldStart"] = original;
        }
    }

    [Fact]
    public async Task QueuedBattlefieldContextRejectsPrivateAndPublicTampering()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { CardNo = "UNL-212/219" } } };
        var opened = await Start(state); state = opened.State;
        Assert.Equal(2,state.TriggerQueue.Count); Restore(state);
        var queue = state.TriggerQueue.ToArray();
        queue[0] = queue[0] with { BattlefieldStart = queue[0].BattlefieldStart! with { Damage = 7 } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { TriggerQueue = queue }), e => e.Contains("captured battlefield start trigger"));
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,state.Tick,0,opened.Snapshots[id],state.Tick,0,opened.Prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId,state.Tick,0,[],state);
        foreach (var snapshot in views.Values.Select(v => v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var entries = Assert.IsAssignableFrom<IEnumerable<Dictionary<string,object?>>>(snapshot.Timing!["triggerQueue"]);
            var payload = entries.First(); var original = payload["battlefieldStart"];
            payload["battlefieldStart"] = null;
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId,0,[],[],views,state,state.Tick,spectator), e => e.Contains("battlefield start context differs"));
            payload["battlefieldStart"] = original;
        }
    }

}
