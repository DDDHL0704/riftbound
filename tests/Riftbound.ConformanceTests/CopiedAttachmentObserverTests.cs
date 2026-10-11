using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CopiedAttachmentObserverTests
{
    internal static async Task<MatchState> JaxWithSong(string card = "SFD·119/221")
    {
        var first = await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position(card, 0));
        // Attach precedes copying: the newly granted text cannot observe this event.
        Assert.NotNull(first.State.PendingPayment);
        Assert.Single(first.State.StackItems, i => i.AttachmentObserver is not null);
        Assert.False(first.State.StackItems.Single(i => i.AttachmentObserver is not null).AttachmentObserver!.CopiedInstance);
        return (await AttachmentObserverTests.Pay(first.State, "DECLINE")).State;
    }

    internal static MatchState AddSword(MatchState state)
    {
        var s = AttachedRuleTextTests.AddSong(state, "SWORD");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SWORD"] = s.CardObjects["SWORD"] with { CardNo = "SFD·022/221" } } };
    }

    [Theory]
    [InlineData("SFD·119/221")]
    [InlineData("SFD·119a/221")]
    public async Task LaterActualWeaponAttachmentCapturesNativeAndExistingCopiedObservers(string card)
    {
        var s = AddSword(await JaxWithSong(card));
        var equip = await Act(s, "P1", new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"]));
        var result = await EquipTestDriver.FinishAsync(equip);
        Assert.Equal(2, result.State.TriggerQueue.Count);
        Assert.Equal(2, result.Events.Count(e => e.Kind == "TRIGGER_QUEUED"));
        Assert.Equal(2, result.State.TriggerQueue.Select(i => i.TriggerId).Distinct().Count());
        Assert.All(result.State.TriggerQueue, i => Assert.NotNull(i.AttachmentObserver));
        var copied = Assert.Single(result.State.TriggerQueue, i => i.AttachmentObserver!.CopiedInstance);
        Assert.Equal("GEAR", copied.AttachmentObserver!.Origin!.Equipment.ObjectId);
        Assert.Equal("SWORD", copied.AttachmentObserver.Equipment.ObjectId);
        Assert.Equal("HOST", result.State.CardObjects["SWORD"].AttachedToObjectId);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task SecondSongAttachmentSeesOnlyTheCopiesAlreadyPresentAtTheEvent()
    {
        var s = AttachedRuleTextTests.AddSong(await JaxWithSong(), "SONG2");
        var result = await AttachedRuleTextTests.Attach(s, "SONG2");
        Assert.Equal(2, result.State.TriggerQueue.Count);
        Assert.Equal(2, result.State.CardObjects.Values.Count(c => c.AttachedRuleText is not null));
        Assert.Equal(2, result.Events.Count(e => e.Kind == "TRIGGER_QUEUED"));
        var copied = Assert.Single(result.State.TriggerQueue, i => i.AttachmentObserver!.CopiedInstance);
        Assert.Equal("GEAR", copied.AttachmentObserver!.Origin!.Equipment.ObjectId);
        Assert.Equal("SONG2", copied.AttachmentObserver.Equipment.ObjectId);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    internal static async Task<ResolutionResult> OpenBoth(string first = "native")
    {
        var s = AddSword(await JaxWithSong());
        var result = await EquipTestDriver.FinishAsync(await Act(s, "P1", new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"])));
        var triggers = result.State.TriggerQueue.OrderBy(i => i.AttachmentObserver!.CopiedInstance == (first == "copy") ? 0 : 1).ToArray();
        return await Act(result.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: triggers.Select(i => i.TriggerId).ToArray()));
    }

    [Theory]
    [InlineData("native")] [InlineData("copy")]
    public async Task EachInstanceSeparatelyPaysRespondsAndDraws(string first)
    {
        var result = await OpenBoth(first); var before = result.State.PlayerZones["P1"].Hand.Count;
        var mana = result.State.RunePools["P1"].Mana;
        Assert.Equal(2, result.State.StackItems.Count);
        Assert.Equal(first == "copy", result.State.StackItems.Last().AttachmentObserver!.CopiedInstance);
        for (var index = 0; index < 2; index++) {
            Assert.Equal(before, result.State.PlayerZones["P1"].Hand.Count);
            result = await AttachmentObserverTests.Pay(result.State);
            Assert.Equal(mana - index - 1, result.State.RunePools["P1"].Mana);
            Assert.DoesNotContain(result.Events, e => e.Kind == "CARD_DRAWN");
            Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
            if (index == 0) { Assert.NotNull(result.State.PendingPayment); Assert.Null(result.State.PriorityPlayerId); }
        }
        Assert.Null(result.State.PendingPayment);
        for (var index = 0; index < 2; index++) {
            result = await Top(result.State);
            Assert.Equal(before + index + 1, result.State.PlayerZones["P1"].Hand.Count);
            Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
        }
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingPayment);
    }

    [Fact]
    public async Task DecliningOneInstancePreservesTheOtherIndependentPayment()
    {
        var result = await OpenBoth("copy"); var mana = result.State.RunePools["P1"].Mana;
        result = await AttachmentObserverTests.Pay(result.State, "DECLINE");
        Assert.Single(result.State.StackItems); Assert.NotNull(result.State.PendingPayment);
        Assert.Equal(mana, result.State.RunePools["P1"].Mana);
        result = await Top((await AttachmentObserverTests.Pay(result.State)).State);
        Assert.Single(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(mana - 1, result.State.RunePools["P1"].Mana);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task PaidCopiedObserverStillResolvesAfterActualDetachRemovesItsGrant()
    {
        var result = await OpenBoth("copy");
        result = await AttachmentObserverTests.Pay(result.State);
        result = await AttachmentObserverTests.Pay(result.State);
        var s = AttachedRuleTextTests.WithSpell(result.State, "P1", "SFD·011/221");
        var reaction = await Act(s, "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]));
        result = await Top(reaction.State);
        Assert.Null(result.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(2, result.State.StackItems.Count); Assert.Single(result.State.PlayerZones["P1"].Hand);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
        result = await Top(result.State); Assert.Equal(2, result.State.PlayerZones["P1"].Hand.Count);
        result = await Top(result.State); Assert.Equal(3, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(result.State.StackItems); Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("origin")] [InlineData("flag")] [InlineData("revision")]
    [InlineData("grant-generation")] [InlineData("unit-generation")] [InlineData("card")]
    public async Task RecoveryRejectsForgedCopiedAttachmentOrigins(string change)
    {
        var result = await OpenBoth("copy"); var item = result.State.StackItems.Last(); var context = item.AttachmentObserver!;
        var origin = context.Origin!;
        var forged = change switch {
            "origin" => context with { Origin = null },
            "flag" => context with { CopiedInstance = false },
            "revision" => context with { Origin = origin with { Revision = 99 } },
            "grant-generation" => context with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } },
            "unit-generation" => context with { Origin = origin with { Unit = origin.Unit with { Generation = 99 } } },
            _ => context with { Origin = origin with { CardNo = "OGN·001/298" } }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(result.State with {
            StackItems = result.State.StackItems.Select(i => i.StackItemId == item.StackItemId ? i with { AttachmentObserver = forged } : i).ToArray() }));
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
