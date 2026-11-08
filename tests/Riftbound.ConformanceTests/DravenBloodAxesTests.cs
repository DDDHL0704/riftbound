using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// CN468.3.c and 468.4: surviving participants earn independent rewards
// before control/conquest. Driver retains original accepted-history checks.
public sealed class DravenBloodAxesTests
{
    [Theory]
    [InlineData("SFD·020/221", false, false)] [InlineData("SFD·020/221", false, true)]
    [InlineData("SFD·020/221", true, false)] [InlineData("SFD·020/221", true, true)]
    [InlineData("SFD·020a/221", false, false)] [InlineData("SFD·020a/221", false, true)]
    [InlineData("SFD·020a/221", true, false)] [InlineData("SFD·020a/221", true, true)]
    public async Task CombatPaysBeforeResponsesAndTheWinningParticipantCreatesDormantGold(
        string printing, bool defend, bool pay)
    {
        var (initial, actor, field) = Position(printing, defend);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { ["red"] = 1 }) } };
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand(defend ? "ENEMY" : "UNIT", "BASE", "BATTLEFIELD:" + field, []));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal("P1", payment.PlayerId); Assert.Equal(1, payment.PowerCost);
        Assert.Equal(1, payment.PowerCostByTrait["red"]);
        Assert.Null(game.State.PriorityPlayerId); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        var cmd = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]);
        await Reject(game.State, "P2", cmd);
        await game.Act("P1", cmd);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(Gold(game.State));
        if (pay)
        {
            Assert.NotNull(game.State.PriorityPlayerId);
            Assert.NotNull(Assert.Single(game.State.StackItems).TriggerCost);
            await game.Top();
        }
        Assert.Equal(pay ? 6 : 4, game.State.CardObjects["UNIT"].Power);
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field,
            [defend ? "ENEMY" : "UNIT"], [defend ? "UNIT" : "ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal("RESULT_TRIGGERS", game.State.PendingBattleResult!.Stage);
        var earned = Assert.Single(game.State.StackItems);
        Assert.Equal(0, earned.UnitBattleWinDraw!.DrawCount);
        Assert.Equal(1, earned.UnitBattleWinDraw.GoldCount);
        Assert.Empty(Gold(game.State));
        var before = game.Events().Length;
        await game.Top();
        var token = Assert.Single(Gold(game.State));
        Assert.True(token.IsExhausted); Assert.Equal("P1", token.OwnerId); Assert.Equal("P1", token.ControllerId);
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
        var events = game.Events().Skip(before).ToArray();
        Assert.Single(events, e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED");
        Assert.DoesNotContain(events, e => e.Event.Kind == "CARD_DRAWN");
        Assert.Null(game.State.PendingBattleResult);
        var created = Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED");
        var closed = Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
        Assert.True(created.Sequence < closed.Sequence);
        if (!defend)
            Assert.True(created.Sequence < Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        await game.Act(actor, new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·020/221", false)] [InlineData("SFD·020/221", true)]
    [InlineData("SFD·020a/221", false)] [InlineData("SFD·020a/221", true)]
    public async Task ABlueOnlyOrEmptyPoolCannotPayTheRedConfirmationCost(string printing, bool blue)
    {
        var (initial, actor, field) = Position(printing, false);
        var pool = blue ? new RunePool(20, 0, new Dictionary<string, int> { ["blue"] = 1 }) : new RunePool(20, 0);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } };
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, []));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var before = MatchStateHasher.HashValue(game.State.RunePools);
        await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal(before, MatchStateHasher.HashValue(game.State.RunePools));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Equal(before, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_CONFIRMED");
        await game.Replay();
    }

    [Theory, InlineData("SFD·020/221"), InlineData("SFD·020a/221")]
    public async Task ARealResponseReturningThePaidSourceCannotBuffItsNewHandIncarnation(string printing)
    {
        var (initial, actor, field) = Position(printing, false);
        initial = Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, []));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var skill = Assert.Single(game.State.StackItems);
        var sourceGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > sourceGeneration);
        Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        Assert.Empty(Gold(game.State));
        await game.Replay();
    }

    [Theory, InlineData("SFD·020/221"), InlineData("SFD·020a/221")]
    public async Task ReturningTheActualWinnerDuringResponsesDoesNotCancelItsEarnedGold(string printing)
    {
        var (initial, actor, field) = Position(printing, false);
        initial = Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, []));
        await Decline(game);
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field, ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        var earned = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(earned, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        Assert.Single(Gold(game.State)); Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Theory]
    [InlineData("loss")] [InlineData("mutual")] [InlineData("empty")]
    [InlineData("base-observer")] [InlineData("other-field-observer")]
    public async Task EmptyConquestsAndNonWinningOrNonParticipatingUnitsEarnNoGold(string scenario)
    {
        var (initial, actor, field) = Position("SFD·020/221", false);
        if (scenario == "loss") initial = Put(initial, "ENEMY", "OGN·142/298", "P2", "BATTLEFIELD", field);
        if (scenario == "mutual") initial = Put(initial, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", field);
        if (scenario == "empty") initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BASE");
        var observer = scenario.EndsWith("observer", StringComparison.Ordinal);
        if (observer)
        {
            initial = Put(initial, "ALLY", "OGN·142/298", "P1", "BASE");
            if (scenario == "other-field-observer")
                initial = RadUnitEffectTestFixture.Place(initial, "UNIT", "P1", "BATTLEFIELD", "BF");
        }
        var game = new Driver(initial); var attacker = observer ? "ALLY" : "UNIT";
        await game.Act(actor, new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:" + field, []));
        await Decline(game);
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        if (scenario != "empty")
            await game.Act(actor, new DeclareBattleCommand(field, [attacker], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitBattleWinDraw is not null);
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.UnitBattleWinDraw is not null);
        Assert.Empty(Gold(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED");
        if (scenario == "loss") Assert.Equal("P2", game.State.BattleResolutions.Last().WinnerPlayerId);
        if (scenario == "mutual") Assert.Contains(game.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT");
        if (observer) Assert.Equal("P1", game.State.BattleResolutions.Last().WinnerPlayerId);
        await game.Replay();
    }

    [Fact]
    public async Task ActualSongTextPaysSeparatelyAndEarnsTwoIndependentGoldRewards()
    {
        var (initial, actor, field) = Position("SFD·020/221", false);
        initial = Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal("SFD·020/221", game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await game.Act(actor, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, []));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var before = game.State.RunePools["P1"].TotalPower; var payments = 0;
        while (game.State.PendingPayment is { } payment)
        {
            Assert.Equal(1, payment.PowerCostByTrait["red"]);
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); payments++;
        }
        Assert.Equal(2, payments); Assert.Equal(before - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(8, game.State.CardObjects["UNIT"].Power);
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field, ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, i => i.UnitBattleWinDraw!.Origin is not null);
        Assert.Single(game.State.StackItems, i => i.UnitBattleWinDraw!.Origin is null);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(2, Gold(game.State).Length); Assert.All(Gold(game.State), c => Assert.True(c.IsExhausted));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ARealImageEarnsGoldWhileTheSamePrintedUnitInBaseDoesNot(bool sourceLeaves)
    {
        var (initial, actor, field) = Position("SFD·020/221", false);
        initial = Put(initial, "TEMPLATE", "SFD·020/221", "P2", "BASE");
        initial = Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        initial = Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal("SFD·020/221", image.CardNo);
        await game.Act(actor, new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:" + field, []));
        await Decline(game); await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field, [image.ObjectId], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(image.ObjectId, skill.UnitBattleWinDraw!.Source.ObjectId);
        if (sourceLeaves)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId]));
            await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
            Assert.DoesNotContain(image.ObjectId, game.State.PlayerZones["P1"].Hand);
            Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
            var captured = skill.UnitBattleWinDraw!;
            foreach (var forgedItem in new[] {
                skill with { UnitBattleWinDraw = captured with { Source = captured.Source with { Generation = captured.Source.Generation + 1 } } },
                skill with { SourceObjectId = "FORGED-MISSING", UnitBattleWinDraw = captured with { Source = new("FORGED-MISSING", 0) } },
                skill with { StackItemId = "ordered-FORGED-BIRTH" }
            })
                await AssertRewardRecoveryRejected(game, initial, game.State with { StackItems = [forgedItem] });
            await game.ColdCheckpoint();
        }
        await game.Top(); Assert.Single(Gold(game.State));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task TheSkillControllerGetsGoldWhenTheWinningUnitHasAnotherOwner()
    {
        var (initial, actor, field) = Position("SFD·020/221", false);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, []));
        await Decline(game); await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field, ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal("P1", Assert.Single(game.State.StackItems).UnitBattleWinDraw!.ControllerId);
        await game.Top(); var gold = Assert.Single(Gold(game.State));
        Assert.Equal("P1", gold.OwnerId); Assert.Equal("P1", gold.ControllerId);
        Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData("count")] [InlineData("draw")] [InlineData("field")] [InlineData("actor")]
    [InlineData("role")] [InlineData("future")] [InlineData("index")] [InlineData("generation")]
    public async Task GoldRewardsRequireTheirExactSpecAndOriginalAcceptedBattleResult(string mutation)
    {
        var (initial, actor, field) = Position("SFD·020/221", false);
        var game = new Driver(initial);
        await game.Act(actor, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:" + field, []));
        await Decline(game); await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act(actor, new DeclareBattleCommand(field, ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        var item = Assert.Single(game.State.StackItems); var context = item.UnitBattleWinDraw!;
        context = mutation switch {
            "count" => context with { GoldCount = 2 },
            "draw" => context with { DrawCount = 1 },
            "field" => context with { BattlefieldId = "BF" },
            "actor" => context with { ControllerId = "P2" },
            "role" => context with { Role = "DEFENDER" },
            "future" => context with { ResultTick = game.State.Tick + 1 },
            "index" => context with { TriggerIndex = 1 },
            _ => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } }
        };
        var forged = game.State with { StackItems = [item with { UnitBattleWinDraw = context }] };
        await AssertRewardRecoveryRejected(game, initial, forged);
        await game.ColdCheckpoint();
    }

    private static async Task AssertRewardRecoveryRejected(Driver game, MatchState initial, MatchState forged)
    {
        var events = game.Events(); var sequence = events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, forged.Tick,
            sequence, snapshots[p], forged.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, game.Commands(), events,
            views, [], forged, initial, spectator) { OpeningSeed = initial.Seed };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.Contains(errors, e => e.Contains("battle-result", StringComparison.Ordinal));
    }

    private static async Task Decline(Driver game)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        while (game.State.PendingPayment is { } payment)
            await game.Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
    }

    private static CardObjectState[] Gold(MatchState state)
        => state.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.EquipmentCard) && c.Tags.Contains("金币")).ToArray();

    private static (MatchState State, string Actor, string Field) Position(string printing, bool defend)
    {
        var state = Put(Create(), "UNIT", printing, "P1", defend ? "BATTLEFIELD" : "BASE", defend ? "BF" : null);
        state = Put(state, "ENEMY", "OGN·175/298", "P2", defend ? "BASE" : "BATTLEFIELD", defend ? null : "OTHER");
        if (defend) state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        return (state, defend ? "P2" : "P1", defend ? "BF" : "OTHER");
    }
}
