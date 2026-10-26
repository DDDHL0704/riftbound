using System.Text;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdRecoveryRejectsForgedAcceptedBattleDeclarationOrder(bool removeDeclaration)
    {
        var initial = SingleDefenderWireState(true) with { Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, [SecondAttackerObjectId, AttackerObjectId],
            [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
        var marker = Assert.Single(driver.State.UntilEndOfTurnEffects, e => e.StartsWith(prefix, StringComparison.Ordinal));
        var declaration = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker[prefix.Length..])))!.AsObject();
        declaration["AttackerObjectIds"] = new JsonArray(AttackerObjectId, SecondAttackerObjectId);
        var forged = driver.State with { UntilEndOfTurnEffects = driver.State.UntilEndOfTurnEffects
            .Where(e => e != marker).Concat(removeDeclaration ? [] : new[] {
                prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(declaration.ToJsonString())) }).ToArray() };
        var events = driver.Events();
        var sequence = events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged);
        var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, forged.Tick, sequence, snapshots[id], forged.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, driver.Commands(), events, views,
            [], forged, initial, spectator);

        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance,
            new ConquestContinuationRecoveryStore(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync(forged.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(forged.RoomId, default));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, JournalRoundTrip(events));
        Assert.False(replay.IsMatch);
        Assert.NotEqual(replay.ExpectedStateHash, replay.ReplayedStateHash);
    }

    private sealed class ConquestContinuationRecoveryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssignedCombatRetainsDeclaredSourceWithoutDroppingOtherConquerors(bool reverseDeclaration)
    {
        var initial = SingleDefenderWireState(true) with { Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" } };
        var cards = initial.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards[BulwarkDefenderObjectId] = cards[BulwarkDefenderObjectId] with { Power = 1 };
        cards[BattlefieldObjectId] = cards[BattlefieldObjectId] with { ControllerId = "P2" };
        initial = initial with { CardObjects = cards };
        var attackers = reverseDeclaration ? new[] { SecondAttackerObjectId, AttackerObjectId }
            : new[] { AttackerObjectId, SecondAttackerObjectId };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, attackers,
            [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(SecondAttackerObjectId, BulwarkDefenderObjectId, 2)]));
        var settled = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, AttackerObjectId, 1)]));

        Assert.Equal(attackers[0], Assert.Single(settled.Events, e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["sourceObjectId"]);
        Assert.All(attackers, id => Assert.Contains(id, driver.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(1, driver.State.PlayerScores["P1"]);
        Assert.False(driver.State.BattleState.IsActive);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, JournalRoundTrip(driver.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task SurvivingVayneConquersIndependentlyOfDeclaredSource(bool vayneFirst, bool vayneDies, bool decline)
    {
        var initial = SingleDefenderWireState(true) with { Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" } };
        var cards = initial.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards[BattlefieldObjectId] = cards[BattlefieldObjectId] with { ControllerId = "P2" };
        var vayne = vayneFirst ? AttackerObjectId : SecondAttackerObjectId;
        var other = vayneFirst ? SecondAttackerObjectId : AttackerObjectId;
        cards[vayne] = cards[vayne] with { CardNo = "OGN·035/298", Power = 2, Tags = [CardObjectTags.UnitCard, "强攻3"] };
        cards[BulwarkDefenderObjectId] = cards[BulwarkDefenderObjectId] with { Power = vayneDies ? 5 : 1 };
        initial = initial with { CardObjects = cards, RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(1, 0), ["P2"] = RunePool.Empty } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId, SecondAttackerObjectId],
            [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(vayne, BulwarkDefenderObjectId, 5), new(other, BulwarkDefenderObjectId, 2)]));
        var settled = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, vayneDies ? vayne : other, vayneDies ? 5 : 1)]));

        Assert.Contains(other, driver.State.PlayerZones["P1"].Battlefields);
        if (vayneDies)
        {
            Assert.Contains(vayne, driver.State.PlayerZones["P1"].Graveyard);
            Assert.Null(driver.State.PendingPayment);
            Assert.DoesNotContain(settled.Events, e => e.Kind == "PAYMENT_WINDOW_OPENED");
        }
        else
        {
            var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            Assert.Equal(vayne, Assert.Single(settled.Events, e => e.Kind == "PAYMENT_WINDOW_OPENED").Payload["sourceObjectId"]);
            Assert.Equal(1, payment.ManaCost);
            Assert.Contains(vayne, driver.State.PlayerZones["P1"].Battlefields);
            Assert.Equal(1, driver.State.RunePools["P1"].Mana);
            await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
                [decline ? "DECLINE" : "SPEND_MANA:1"]));
            Assert.Null(driver.State.PendingPayment);
            Assert.Equal(decline ? 1 : 0, driver.State.RunePools["P1"].Mana);
            Assert.Contains(vayne, decline ? driver.State.PlayerZones["P1"].Battlefields : driver.State.PlayerZones["P1"].Hand);
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, JournalRoundTrip(driver.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }
}
