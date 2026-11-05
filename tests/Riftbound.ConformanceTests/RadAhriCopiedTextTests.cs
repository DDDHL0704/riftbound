using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadAhriCopiedTextTests
{
    private static async Task<Driver> Position(int songs, bool changeFace)
    {
        var state = RadAhriMightReductionTests.Position();
        for (var index = 1; index <= songs; index++)
            state = AttachedRuleTextTests.AddSong(state, "SONG" + index);
        state = OfficialCopyIdentityTests.AddSpell(state, "REDUCE", "OGN·095/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        state = LocalDestructionRecallTests.At(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GLASSES"] = new("GLASSES", cardNo: "VEN·137", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]),
                ["COPY"] = OfficialGarenConquestBattlefieldCountTests.Printed("COPY", "OGN·096/298", "P1") }
        }, "GLASSES", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "COPY", "P1", "BASE");
        var game = new Driver(state);
        for (var index = 1; index <= songs; index++)
        {
            await game.Act("P1", new AssembleEquipmentCommand("SONG" + index, "HOST", ["ASSEMBLE_GREEN"]));
            await game.Top();
        }
        if (changeFace)
        {
            await game.Act("P1", new AssembleEquipmentCommand("GLASSES", "HOST", ["ASSEMBLE_YELLOW"]));
            await game.Top();
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY"]));
            Assert.Equal("OGN·096/298", game.State.CardObjects["HOST"].CardNo);
        }
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        // Each copied Disarm is separate. Decline every ward payment so the
        // battle starts without an earlier reduction or earned Ahri skill.
        for (var step = 0; step < 20; step++)
        {
            if (game.State.TriggerQueue.Count > 0) await Order(game);
            else if (game.State.PendingPayment is { } payment)
                await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
            else if (game.State.PendingCardChoice is { } target)
                await game.Act("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY2"]));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else break;
        }
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingPayment); Assert.Null(game.State.PendingCardChoice);
        Assert.True(game.State.SpellDuelState.IsActive);
        return game;
    }

    private static Task Order(Driver game)
        => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask
            : game.Act("P1", new OrderTriggersCommand(
                OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));

    private static async Task Reduce(Driver game)
    {
        if (game.State.FocusPlayerId is { } focus && focus != "P1") await game.Act(focus, new PassFocusCommand());
        await game.Act("P1", new PlayCardCommand("REDUCE", "OGN·095/298", ["ENEMY1"]));
        await game.Top();
        await Order(game);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(1, true)]
    public async Task EachLiveTextInstanceEarnsOneMightTrigger(int songs, bool changeFace)
    {
        var game = await Position(songs, changeFace);
        var power = game.State.CardObjects["HOST"].Power;
        await Reduce(game);
        var count = songs + (changeFace ? 0 : 1);
        Assert.Equal(count, game.State.StackItems.Count);
        Assert.Equal(songs, game.State.StackItems.Count(item => item.UnitEvent?.Origin is not null));
        Assert.All(game.State.StackItems, item => Assert.Equal("RAD·038/167", item.UnitEvent!.CardNo));
        for (var index = 0; index < count; index++) await game.Top();
        Assert.Equal(power + count, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DetachmentPreservesCapturedSkillsAndRemovesFutureTriggers(bool beforeReduction)
    {
        var game = await Position(1, changeFace: true);
        var power = game.State.CardObjects["HOST"].Power;
        if (!beforeReduction) await Reduce(game);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "SONG1"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["SONG1"].AttachedRuleText);
        if (beforeReduction) await Reduce(game);
        Assert.Equal(beforeReduction ? 0 : 1, game.State.StackItems.Count);
        if (!beforeReduction) await game.Top();
        Assert.Equal(power + (beforeReduction ? 0 : 1), game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task EndingACopyKeepsTheCapturedSkillWithoutRetroactivelyTriggeringRestoredText()
    {
        var game = await Position(1, changeFace: true);
        await Reduce(game);
        var captured = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<UnitEventContext>(captured.UnitEvent);
        Assert.NotNull(context.Origin);
        Assert.Equal("OGN·096/298", context.MightReduction!.After.CardObjects["HOST"].CardNo);

        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GLASSES"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GLASSES"].AttachedToObjectId);
        Assert.Equal("RAD·038/167", game.State.CardObjects["HOST"].CardNo);
        Assert.NotNull(game.State.CardObjects["SONG1"].AttachedRuleText);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(captured.StackItemId, Assert.Single(game.State.StackItems).StackItemId);

        // Recovery must retain the face at the reduction, even after the
        // source regains its original text through an actual detach response.
        var restoredRevision = game.State.CardObjects["HOST"].CopyFaceTransitions!.Count;
        Assert.True(restoredRevision > context.FaceRevision);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [captured with { UnitEvent = context with { FaceRevision = restoredRevision } }] }),
            error => error.Contains("invalid captured unit event stack"));

        var power = game.State.CardObjects["HOST"].Power;
        await game.Top();
        Assert.Equal(power + 1, game.State.CardObjects["HOST"].Power);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }
}
