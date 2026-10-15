using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttachedRecycleLifecycleTests
{
    [Theory]
    [InlineData("OGN·235/298", false)]
    [InlineData("OGN·235/298", true)]
    [InlineData("SFD·237/221", false)]
    [InlineData("SFD·237/221", true)]
    [InlineData("SFD·237*/221", false)]
    [InlineData("SFD·237*/221", true)]
    public async Task ActualAttachedCopyOrdersConfirmsAndSurvivesItsCopiedHostCeasing(string card, bool reverse)
    {
        var initial = AttachedRuleTextTests.AddSong(RecycledCardBoonLifecycleTests.Position(card), "SONG");
        var copied = await OfficialCopyIdentityTests.Copy(initial);
        var assembled = await Act(copied.State, "P1", new AssembleEquipmentCommand("SONG", "C-TOKEN-001", ["ASSEMBLE_GREEN"]));
        var current = (await EquipTestDriver.FinishAsync(assembled)).State;
        Assert.Equal(card, current.CardObjects["SONG"].AttachedRuleText!.CardNo);
        var attachmentTick = current.CardObjects["SONG"].AttachedRuleText!.CapturedTick;
        var opened = await Top((await Act(current, "P1", new PlayCardCommand("LOOK", "UNL-063/219", ["TARGET"]))).State);
        var choice = opened.State.PendingCardChoice!;
        current = (await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]))).State;
        Assert.Equal(2, current.TriggerQueue.Count); Assert.Empty(current.StackItems); Restore(current);
        var triggers = current.TriggerQueue.Select(t => t.TriggerId).ToArray();
        await Reject(current, "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"]));
        await Reject(current, "P2", new OrderTriggersCommand(OrderedTriggerIds: triggers));
        await Reject(current, "P1", new OrderTriggersCommand(OrderedTriggerIds: [triggers[0], triggers[0]]));
        if (reverse) Array.Reverse(triggers);
        current = (await Act(current, "P1", new OrderTriggersCommand(OrderedTriggerIds: triggers))).State;
        Assert.Empty(current.TriggerQueue); Assert.Equal(2, current.StackItems.Count);
        for (var index = 0; index < 2; index++) {
            choice = current.PendingCardChoice!;
            await Reject(current, "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"]));
            await Reject(current, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            current = (await Act(current, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]))).State;
        }
        var returned = await Top((await Act(current, "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"]))).State);
        current = returned.State;
        Assert.False(current.CardObjects.ContainsKey("C-TOKEN-001"));
        Assert.Null(current.CardObjects["SONG"].AttachedToObjectId); Assert.Null(current.CardObjects["SONG"].AttachedRuleText);
        Assert.Equal(2, current.StackItems.Count); Restore(current);
        var copiedText = Assert.Single(current.StackItems, i => i.FieldContext!.Origin is not null);
        var context = copiedText.FieldContext!;
        Assert.Equal("SONG", context.Origin!.Equipment.ObjectId);
        Assert.Equal(attachmentTick, context.RecycledBoon!.CeasedSource!.CapturedTick);
        Assert.Equal(card, context.RecycledBoon.CeasedSource.CardNo);
        Assert.True(context.RecycledBoon.CapturedTick > attachmentTick);
        foreach (var origin in new[] { context.Origin with { Equipment = context.Origin.Equipment with { Generation = 99 } },
            context.Origin with { Revision = context.Origin.Revision + 1 }, context.Origin with { CapturedTick = context.Origin.CapturedTick + 1 } }) {
            var forged = current with { StackItems = current.StackItems.Select(i => i.StackItemId == copiedText.StackItemId
                ? i with { FieldContext = context with { Origin = origin } } : i).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("field trigger", StringComparison.Ordinal));
        }
        var first = await Top(current); var second = await Top(first.State);
        Assert.Contains(CardObjectTags.Boon, second.State.CardObjects["ALLY"].Tags);
        Assert.Equal(initial.CardObjects["ALLY"].Power + 1, second.State.CardObjects["ALLY"].Power);
        Assert.True((bool)Assert.Single(second.Events, e => e.Kind == "BOON_GRANTED").Payload["alreadyHadBoon"]!);
        Assert.Empty(second.State.StackItems); Restore(second.State);
    }

    private static async Task Reject(MatchState state, string actor, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-boundary", actor, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
