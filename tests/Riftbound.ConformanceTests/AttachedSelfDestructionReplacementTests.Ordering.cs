using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    [Theory]
    [InlineData("SFD·173/221", false, false)] [InlineData("SFD·173/221", false, true)]
    [InlineData("SFD·173/221", true, false)] [InlineData("SFD·173/221", true, true)]
    [InlineData("SFD·239/221", false, false)] [InlineData("SFD·239/221", false, true)]
    [InlineData("SFD·239/221", true, false)] [InlineData("SFD·239/221", true, true)]
    [InlineData("SFD·239*/221", false, false)] [InlineData("SFD·239*/221", false, true)]
    [InlineData("SFD·239*/221", true, false)] [InlineData("SFD·239*/221", true, true)]
    public async Task ActualSorakaGuardianOrderUsesOnlyTheUnstartedCurrentGroup(string no, bool guardianFirst, bool copied)
    {
        var initial = OrderingInitial(no, copied); var run = new Driver(initial);
        await OrderingSetup(run, no, copied); await OpenOrderingDestruction(run);
        var first = guardianFirst ? "ATTACHED_GEAR:HOST:ANGEL" : "LOCAL_RECALL:FIELD-1:HOST";
        await Choose(run, first);
        if (run.State.PendingRuleChoice is { } pending) {
            var expected = guardianFirst ? "LOCAL_RECALL:BASE-1:HOST" : CopiedOption(run.State, "BASE-1");
            Assert.Contains(pending.Request.Options, option => option.Id == expected);
            await Choose(run, expected);
        }
        AssertOrderingResult(run, !guardianFirst, guardianFirst || copied);
        var events = run.Events();
        var guardianDeath = events.Single(e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == "ANGEL").Sequence;
        if (!guardianFirst) foreach (var id in new[] { "FIELD-1", "FIELD-2" })
            Assert.True(events.Single(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == id).Sequence < guardianDeath);
        AssertHostEventSource(run, "P1"); await run.Replay(); Export(initial, run, true, "soraka-" + no.Replace('/', '_').Replace('*', 's') + "-" + guardianFirst + "-" + copied);
    }

    [Fact]
    public async Task AnActiveActualCopiedSorakaGroupCannotBeInterruptedByTheSecondTargetsGuardian()
    {
        const string no = "SFD·173/221";
        var initial = AddInitial(OrderingInitial(no, true), "SMALL-ANGEL", "SFD·051/221", "P1");
        var run = new Driver(initial); await OrderingSetup(run, no, true);
        await run.Act("P1", new PlayCardCommand("SMALL-ANGEL", "SFD·051/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("SMALL-ANGEL", "FIELD-2", ["ASSEMBLE_GREEN"])); await run.Top();
        Assert.Equal(3, Might(run.State, "FIELD-2", "P1"));
        await OpenOrderingDestruction(run);
        Assert.Contains(run.State.PendingRuleChoice!.Request.Options, o => o.Id == "ATTACHED_GEAR:FIELD-2:SMALL-ANGEL");
        await Choose(run, CopiedOption(run.State, "FIELD-1"));
        if (run.State.PendingRuleChoice is not null) await Choose(run, "LOCAL_RECALL:BASE-1:HOST");
        AssertOrderingResult(run, true, true);
        Assert.Contains("SMALL-ANGEL", run.State.PlayerZones["P1"].Base);
        Assert.Equal("FIELD-2", run.State.CardObjects["SMALL-ANGEL"].AttachedToObjectId);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == "SMALL-ANGEL");
        var recalled = Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "FIELD-2");
        Assert.Equal(ReplacementKinds.OtherFriendlyLowerPowerUnitHereDestroyedRecallExhausted, recalled.Event.Payload["replacementEffectId"]);
        Assert.True(recalled.Sequence < run.Events().Single(e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == "ANGEL").Sequence);
        await run.Replay(); Export(initial, run, true, "active-copied-group-not-interrupted");
    }

    private static MatchState OrderingInitial(string no, bool copied)
    {
        var initial = Position(no, battlefield: true);
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) initial = AddInitial(initial, id, "OGN·210/298", "P1");
        initial = AddInitial(initial, "AOE", "UNL-180/219", "P2");
        return copied ? AddInitial(initial, "SONG", "SFD·059/221", "P1") : initial;
    }
    private static async Task OrderingSetup(Driver run, string no, bool copied)
    {
        await HostAndAngel(run, no, "BATTLEFIELD:HOME");
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" })
            await run.Act("P1", new PlayCardCommand(id, "OGN·210/298", [], Destination: id.StartsWith("FIELD", StringComparison.Ordinal) ? "BATTLEFIELD:HOME" : "BASE"));
        if (copied) {
            await run.Act("P1", new PlayCardCommand("SONG", "SFD·059/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        }
    }
    private static async Task OpenOrderingDestruction(Driver run)
    {
        await FundYellow(run, 3);
        await run.Act("P2", new PlayCardCommand("AOE", "UNL-180/219", [])); await run.Top();
        Assert.NotNull(run.State.PendingRuleChoice);
    }
    private static async Task Choose(Driver run, string id)
    {
        var request = run.State.PendingRuleChoice!.Request; Assert.Contains(request.Options, option => option.Id == id);
        await run.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]));
    }
    private static string CopiedOption(MatchState state, string target)
        => $"LOCAL_RECALL:{target}:HOST:ATTACHED:SONG:{state.CardObjects["SONG"].ObjectGeneration}:{state.CardObjects["SONG"].AttachmentRevision}";
    private static void AssertOrderingResult(Driver run, bool fieldSaved, bool baseSaved)
    {
        AssertHostRecalled(run, "P1"); Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, Might(run.State, "HOST", "P1"));
        foreach (var (prefix, saved) in new[] { ("FIELD", fieldSaved), ("BASE", baseSaved) })
            foreach (var id in new[] { prefix + "-1", prefix + "-2" }) {
                Assert.Equal(saved, run.State.PlayerZones["P1"].Base.Contains(id)); Assert.Equal(!saved, run.State.PlayerZones["P1"].Graveyard.Contains(id));
            }
    }
}
