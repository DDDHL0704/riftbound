using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData("SPEND_MANA:1")]
    [InlineData("PAY")]
    public async Task VaynePaysAtConfirmationButReturnsOnlyAfterResponses(string choice)
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.ManaCost);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(1, game.State.RunePools["P1"].Mana);

        var paid = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [choice]));
        var cost = Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(payment.PaymentId, cost.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, cost.Payload["paymentWindow"]);
        Assert.Equal(1, Convert.ToInt32(cost.Payload["totalManaCost"]));
        Assert.Equal(1, Convert.ToInt32(cost.Payload["mana"]));
        Assert.Equal(AttackerObjectId, cost.Payload["sourceObjectId"]);
        Assert.Equal(TriggerKinds.UnitConquestPayReturnSelfToHand, cost.Payload["reason"]);
        Assert.Equal(new[] { choice }, Assert.IsType<string[]>(cost.Payload["paymentChoiceIds"]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        var item = Assert.Single(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        Assert.Equal(new ObjectBinding(AttackerObjectId, initial.CardObjects[AttackerObjectId].ObjectGeneration), item.TriggerCost!.Source);
        Assert.Equal(1, item.TriggerCost.Mana);

        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("P2", game.State.PriorityPlayerId);
        await game.Act("P2", new PlayCardCommand("VAYNE-RESPONSE", "OGN·104/298", [AttackerObjectId]));
        await game.Top();
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.CardObjects[AttackerObjectId].ObjectGeneration + 1, game.State.CardObjects[AttackerObjectId].ObjectGeneration);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Contains(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        var resolved = await game.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task VayneDeclineDoesNotPayOrReturnTheSource()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var declined = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(declined.Events, e => e.Kind is "COST_PAID" or "UNIT_RETURNED_TO_HAND");
        Assert.DoesNotContain(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData("SPEND_MANA:1")]
    [InlineData("PAY")]
    public async Task VayneInsufficientConfirmationIsAtomic(string choice)
    {
        var (_, game, _) = await OpenVaynePaymentTimingBattle(mana: 0);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var before = MatchStateHasher.Hash(game.State);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [choice]);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("vayne-unfunded", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.NotNull(rejected.State.PendingPayment);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoVayneConquerorsAndTheBattlefieldKeepIndependentPendingTriggers(bool minefield)
    {
        var (_, game, settled) = await OpenVaynePaymentTimingBattle(twoVaynes: true, minefield: minefield);
        var sources = game.State.TriggerQueue.Select(t => t.SourceObjectId)
            .Concat(game.State.StackItems.Select(s => s.SourceObjectId)).ToHashSet(StringComparer.Ordinal);
        if (game.State.PendingPayment?.Reason is { } reason)
            foreach (var id in new[] { AttackerObjectId, SecondAttackerObjectId })
                if (reason.EndsWith("|" + id, StringComparison.Ordinal)) sources.Add(id);
        Assert.Contains(AttackerObjectId, sources);
        Assert.Contains(SecondAttackerObjectId, sources);
        Assert.All(new[] { AttackerObjectId, SecondAttackerObjectId },
            id => Assert.Contains(id, game.State.PlayerZones["P1"].Battlefields));
        Assert.DoesNotContain(settled.Events, e => e.Kind is "COST_PAID" or "UNIT_RETURNED_TO_HAND");
        if (minefield)
        {
            Assert.Contains(BattlefieldObjectId, sources);
            Assert.DoesNotContain(settled.Events, e => e.Kind == "CARDS_MILLED");
            Assert.Equal(2, game.State.PlayerZones["P2"].MainDeck.Count);
        }
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Game, ResolutionResult Settled)>
        OpenVaynePaymentTimingBattle(int mana = 1, bool twoVaynes = false, bool minefield = false)
    {
        var initial = SingleDefenderWireState(true) with { Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" } };
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [BattlefieldObjectId] = initial.CardObjects[BattlefieldObjectId] with {
                ControllerId = "P2", CardNo = minefield ? "SFD·212/221" : "OGN·275/298" },
            [AttackerObjectId] = initial.CardObjects[AttackerObjectId] with {
                CardNo = "OGN·035/298", Power = 2, Tags = [CardObjectTags.UnitCard, "强攻3"] },
            [BulwarkDefenderObjectId] = initial.CardObjects[BulwarkDefenderObjectId] with { Power = 1 }
        };
        if (twoVaynes) cards[SecondAttackerObjectId] = cards[SecondAttackerObjectId] with {
            CardNo = "OGN·035/298", Power = 2, Tags = [CardObjectTags.UnitCard, "强攻3"] };
        initial = initial with { CardObjects = cards, RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(mana, 0), ["P2"] = new(8, 4) } };
        if (minefield)
        {
            var withDeck = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["VAYNE-DECK-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("VAYNE-DECK-1", "OGN·096/298", "P2"),
                ["VAYNE-DECK-2"] = OfficialGarenConquestBattlefieldCountTests.Printed("VAYNE-DECK-2", "OGN·096/298", "P2") };
            initial = initial with { CardObjects = withDeck,
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["VAYNE-DECK-1", "VAYNE-DECK-2"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["VAYNE-DECK-1"] = new("P2", "MAIN_DECK"), ["VAYNE-DECK-2"] = new("P2", "MAIN_DECK") } };
        }
        initial = AttackDamageDistributionTests.Spell(initial, "VAYNE-RESPONSE", "OGN·104/298", "P2");
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId, SecondAttackerObjectId],
            [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        await game.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 5),
                new(SecondAttackerObjectId, BulwarkDefenderObjectId, twoVaynes ? 5 : 2)]));
        var settled = await game.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, AttackerObjectId, 1)]));
        return (initial, game, settled);
    }
}
