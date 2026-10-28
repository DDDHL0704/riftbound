using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Fact]
    public async Task VayneRejectsAnUnadvertisedPayAliasBeforeAcceptingThePublishedChoice()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, payment.LegalPaymentChoiceIds);
        var before = game.State;
        var beforeHash = MatchStateHasher.Hash(before);
        var beforeSnapshots = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(before));
        var beforePrompts = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(before));
        var invalid = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(before,
            new("vayne-unadvertised-alias", "P1", invalid.CmdType), invalid, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(beforeHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(beforeSnapshots, MatchStateHasher.HashValue(rejected.Snapshots));
        Assert.Equal(beforePrompts, MatchStateHasher.HashValue(rejected.Prompts));

        var paid = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        await game.Top();
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Hand);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }
}
