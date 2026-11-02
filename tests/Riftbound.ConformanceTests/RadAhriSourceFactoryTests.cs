using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed class RadAhriSourceFactoryTests
{
    private static async Task<(MatchState State, string Source)> Capture(bool token = false, bool returning = false)
    {
        var state = AttachedRuleTextTests.AddSong(RadAhriMightReductionTests.Position(), "SONG");
        state = OfficialCopyIdentityTests.AddSpell(state, "REDUCE", "OGN·095/298");
        if (returning) state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        var source = "HOST";
        if (token)
        {
            state = OfficialCopyIdentityTests.AddSpell(state, "COPY", "UNL-200/219");
            state = (await OfficialCopyIdentityTests.Copy(state, "HOST", "COPY")).State;
            source = Assert.Single(state.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo).ObjectId;
        }
        state = (await EquipTestDriver.FinishAsync(await Act(state, "P1",
            new AssembleEquipmentCommand("SONG", source, ["ASSEMBLE_GREEN"])))).State;
        state = (await Act(state, "P1", new MoveUnitCommand(source, "BASE", "BATTLEFIELD:F", []))).State;
        for (var step = 0; step < 20; step++)
        {
            if (state.TriggerQueue.Count > 0)
                state = (await Act(state, "P1", new OrderTriggersCommand(
                    OrderedTriggerIds: state.TriggerQueue.Select(item => item.TriggerId).ToArray()))).State;
            else if (state.PendingPayment is { } payment)
                state = (await Act(state, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]))).State;
            else if (state.PendingCardChoice is { } target)
                state = (await Act(state, "P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY2"]))).State;
            else if (state.StackItems.Count > 0) state = (await ResolveTop(state)).State;
            else break;
        }
        Assert.Empty(state.StackItems);
        Assert.Empty(state.TriggerQueue);
        Assert.True(state.SpellDuelState.IsActive);
        if (state.FocusPlayerId is { } focus && focus != "P1")
            state = (await Act(state, focus, new PassFocusCommand())).State;
        state = (await Act(state, "P1", new PlayCardCommand("REDUCE", "OGN·095/298", ["ENEMY1"]))).State;
        state = (await ResolveTop(state)).State;
        Assert.Equal(2, state.TriggerQueue.Count);
        return (Restore(state), source);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public async Task RecoveryRejectsForgedFactoryForNativeAndCopiedTriggers(bool queued, bool copied, bool forgeFrame)
    {
        var (state, _) = await Capture();
        if (!queued)
            state = (await Act(state, "P1", new OrderTriggersCommand(
                OrderedTriggerIds: state.TriggerQueue.Select(item => item.TriggerId).ToArray()))).State;
        state = Restore(state);
        var context = queued
            ? Assert.Single(state.TriggerQueue, item => (item.UnitEvent!.Origin is not null) == copied).UnitEvent!
            : Assert.Single(state.StackItems, item => (item.UnitEvent!.Origin is not null) == copied).UnitEvent!;
        Assert.Null(context.TokenFactoryCardNo);
        Assert.Null(context.MightReduction!.After.CardObjects["HOST"].TokenFactoryCardNo);
        const string wrongFactory = "OGN·273/298";
        var forged = context with { TokenFactoryCardNo = wrongFactory };
        if (forgeFrame)
        {
            LastKnownMightFrame Forge(LastKnownMightFrame frame) => frame with {
                CardObjects = new Dictionary<string, CardObjectState>(frame.CardObjects) {
                    ["HOST"] = frame.CardObjects["HOST"] with { TokenFactoryCardNo = wrongFactory } } };
            forged = forged with { MightReduction = context.MightReduction with {
                Before = Forge(context.MightReduction.Before), After = Forge(context.MightReduction.After) } };
        }
        var corrupted = queued
            ? state with { TriggerQueue = state.TriggerQueue.Select(item => item.UnitEvent == context
                ? item with { UnitEvent = forged } : item).ToArray() }
            : state with { StackItems = state.StackItems.Select(item => item.UnitEvent == context
                ? item with { UnitEvent = forged } : item).ToArray() };
        Assert.Contains(Errors(corrupted), error => error.Contains(queued
            ? "invalid captured unit event trigger" : "invalid captured unit event stack"));
        Assert.Empty(Errors(state));
    }

    [Fact]
    public async Task ARealImageKeepsItsFactoryForNativeAndSongSkills()
    {
        var (state, source) = await Capture(token: true);
        Assert.All(state.TriggerQueue, item => Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, item.UnitEvent!.TokenFactoryCardNo));
        state = (await Act(state, "P1", new OrderTriggersCommand(
            OrderedTriggerIds: state.TriggerQueue.Select(item => item.TriggerId).ToArray()))).State;
        state = Restore(state);
        foreach (var item in state.StackItems)
        {
            Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, item.UnitEvent!.TokenFactoryCardNo);
            foreach (var wrongFactory in new string?[] { null, "OGN·273/298" })
                Assert.Contains(Errors(state with { StackItems = state.StackItems.Select(other => other.StackItemId == item.StackItemId
                    ? other with { UnitEvent = item.UnitEvent with { TokenFactoryCardNo = wrongFactory } } : other).ToArray() }),
                    error => error.Contains("invalid captured unit event stack"));
        }
        var power = state.CardObjects[source].Power;
        state = Restore((await ResolveTop(state)).State);
        state = Restore((await ResolveTop(state)).State);
        Assert.Equal(power + 2, state.CardObjects[source].Power);
        Assert.Empty(state.StackItems);
    }

    private static async Task<(MatchState State, string Source)> CaptureDepartedImage()
    {
        var (state, source) = await Capture(token: true, returning: true);
        state = (await Act(state, "P1", new OrderTriggersCommand(
            OrderedTriggerIds: state.TriggerQueue.Select(item => item.TriggerId).ToArray()))).State;
        state = Restore(state);
        state = (await Act(state, "P1", new PlayCardCommand("RETURN", "OGN·104/298", [source]))).State;
        state = Restore((await ResolveTop(state)).State);
        Assert.False(state.CardObjects.ContainsKey(source));
        Assert.False(state.ObjectLocations.ContainsKey(source));
        Assert.Null(state.CardObjects["SONG"].AttachedToObjectId);
        Assert.Equal(2, state.StackItems.Count);
        return (state, source);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task ADepartedImageRejectsForgedFactoriesInItsNativeAndSongSkills(bool copied, bool forgeFrames)
    {
        var (state, source) = await CaptureDepartedImage();
        var item = Assert.Single(state.StackItems, entry => (entry.UnitEvent!.Origin is not null) == copied);
        var context = item.UnitEvent!;
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, context.TokenFactoryCardNo);
        foreach (var wrongFactory in new string?[] { null, P6TokenFactoryCatalog.ZaunMinionTokenCardNo })
        {
            var forged = context with { TokenFactoryCardNo = wrongFactory };
            if (forgeFrames)
            {
                var fact = context.MightReduction!;
                LastKnownMightFrame Forge(LastKnownMightFrame frame) => frame with {
                    CardObjects = new Dictionary<string, CardObjectState>(frame.CardObjects) {
                        [source] = frame.CardObjects[source] with { TokenFactoryCardNo = wrongFactory } } };
                forged = forged with { MightReduction = fact with {
                    Before = Forge(fact.Before), After = Forge(fact.After) } };
            }
            var corrupted = state with { StackItems = state.StackItems.Select(other => other.StackItemId == item.StackItemId
                ? other with { UnitEvent = forged } : other).ToArray() };
            Assert.Contains(Errors(corrupted), error => error.Contains("invalid captured unit event stack"));
        }
        Assert.Empty(Errors(state));
    }

}
