using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadDreamRootTests
{
    private const string Root = "RAD·024/167";

    [Fact]
    public async Task OfficialEquipmentDeploysWithoutInventingAPlaySkill()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Root);
        Assert.Equal(39517, card.Id); Assert.Equal(2, card.Energy);
        var game = new Driver(Put(Create(), "ROOT", Root, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("ROOT", Root, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Empty(game.State.StackItems);
        Assert.Equal("OTHER", game.State.ObjectLocations["ROOT"].BattlefieldObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["ROOT"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:missing")]
    public Task IllegalDeploymentDoesNotSpendResources(string destination)
        => Reject(Put(Create(), "ROOT", Root, "P1", "HAND"), "P1",
            new PlayCardCommand("ROOT", Root, [], Destination: destination));

    [Theory]
    [InlineData("attack", 1)] [InlineData("defend", 1)] [InlineData("two-sources", 2)]
    [InlineData("opponent-source", 0)] [InlineData("elsewhere", 0)]
    [InlineData("mutual", 0)] [InlineData("empty", 0)]
    public async Task OnlyTheWinningPlayersObserversAtTheActualBattleDraw(string scenario, int draws)
    {
        var (initial, actor, attacker, defender, field) = Position(scenario);
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:" + field, []));
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        if (scenario != "empty")
            await game.Act(actor, new DeclareBattleCommand(field, [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
        if (draws > 0)
        {
            Assert.Equal("RESULT_TRIGGERS", game.State.PendingBattleResult!.Stage);
            Assert.Empty(game.State.PlayerZones["P1"].Hand);
            Assert.Equal(actor == "P1" ? "P2" : "P1", game.State.CardObjects[field].ControllerId);
            if (game.State.TriggerQueue.Count > 1)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(draws, game.State.StackItems.Count);
            Assert.All(game.State.StackItems, i => Assert.True(i.UnitBattleWinDraw!.ObservesBattlefield));
            while (game.State.StackItems.Count > 0) await game.Top();
        }
        Assert.Null(game.State.PendingBattleResult);
        Assert.Equal(draws, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.PlayerZones["P2"].Hand);
        var events = game.Events();
        Assert.Equal(draws, events.Count(e => e.Event.Kind == "CARD_DRAWN"));
        if (draws > 0)
        {
            var result = Assert.Single(events, e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
            var lastDraw = events.Last(e => e.Event.Kind == "CARD_DRAWN");
            Assert.True(result.Sequence < lastDraw.Sequence);
            Assert.True(lastDraw.Sequence < events.Single(e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
            if (scenario != "defend")
                Assert.True(lastDraw.Sequence < events.Single(e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        }
        if (scenario == "mutual") Assert.Contains(events, e => e.Event.Kind == "BATTLE_NO_RESULT");
        if (scenario == "empty") Assert.DoesNotContain(events, e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        await game.Replay();
    }

    [Theory]
    [InlineData("count")] [InlineData("field")] [InlineData("generation")] [InlineData("participant")]
    public async Task RecoveryBindsTheEarnedDrawToItsOriginalResultAndTrigger(string mutation)
    {
        var (initial, actor, attacker, defender, field) = Position("attack");
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:" + field, []));
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field, [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
        var item = Assert.Single(game.State.StackItems);
        var original = item.UnitBattleWinDraw!;
        var changed = mutation switch {
            "count" => original with { DrawCount = 2 },
            "field" => original with { BattlefieldId = "BF" },
            "generation" => original with { Source = original.Source with { Generation = original.Source.Generation + 1 } },
            _ => original with { ObservesBattlefield = false }
        };
        var state = game.State with { StackItems = [item with { UnitBattleWinDraw = changed }] };
        var sequence = game.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views,
            [], state, initial, spectator) { OpeningSeed = initial.Seed };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        // A coherent incarnation change requires original accepted history,
        // just as it does for the participating unit battle-win skills.
        if (mutation == "generation") Assert.Empty(errors);
        else Assert.Contains(errors, e => e.Contains("battle-result skill", StringComparison.Ordinal));
        var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.Contains(replayErrors, e => e.Contains("battle-result skill", StringComparison.Ordinal));
        await game.Top(); await game.Replay();
    }

    private static (MatchState State, string Actor, string Attacker, string Defender, string Field) Position(string scenario)
    {
        var state = Create();
        state = Put(state, "ALLY", scenario == "mutual" ? "OGN·219/298" : "OGN·142/298", "P1", "BASE");
        state = Put(state, "ENEMY", scenario == "mutual" ? "OGN·219/298" : "OGN·175/298", "P2", "BATTLEFIELD", "OTHER");
        var defend = scenario == "defend";
        if (defend)
        {
            state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
            state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
            state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BASE");
        }
        if (scenario == "empty") state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BASE");
        var field = defend ? "BF" : "OTHER";
        state = Put(state, "ROOT", Root, scenario == "opponent-source" ? "P2" : "P1", "BATTLEFIELD",
            scenario == "elsewhere" ? "BF" : field);
        if (scenario == "two-sources") state = Put(state, "ROOT-2", Root, "P1", "BATTLEFIELD", field);
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Value.CardNo == Root
            ? p.Value with { Tags = [..p.Value.Tags, CardEquipmentKeywordNames.Deploy] } : p.Value) };
        return (state, defend ? "P2" : "P1", defend ? "ENEMY" : "ALLY", defend ? "ALLY" : "ENEMY", field);
    }
}
