using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    [Fact]
    public async Task AnActualSongCopiesPrintedTextWithoutRetainingTheConsumedGuardiansAddedReplacement()
    {
        var initial = AddInitial(AddInitial(Position(), "SONG", "SFD·059/221", "P1"), "KILL-2", "OGN·229/298", "P2");
        var run = new Driver(initial); await HostAndAngel(run);
        await run.Act("P1", new PlayCardCommand("SONG", "SFD·059/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        Assert.Equal("OGN·210/298", run.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await FundYellow(run, 4); await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        AssertHostRecalled(run, "P1"); Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        AssertGroupAtBase(run.State, "P1", "HOST", "SONG"); Assert.Equal("HOST", run.State.CardObjects["SONG"].AttachedToObjectId);
        await run.Act("P2", new PlayCardCommand("KILL-2", "OGN·229/298", ["HOST"])); await run.Top();
        Assert.Null(run.State.PendingRuleChoice); Assert.Contains("HOST", run.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("SONG", run.State.PlayerZones["P1"].Base); Assert.Null(run.State.CardObjects["SONG"].AttachedToObjectId);
        Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST");
        Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == "HOST");
        await run.Replay(); Export(initial, run, false, "song-does-not-copy-granted-guardian-text");
    }

    [Fact]
    public async Task AnotherActualUnattachedGuardianCannotReplaceTheBoundGuardianNamedByItsLowerBox()
    {
        var initial = AddInitial(AddInitial(Position(), "ANGEL-2", "SFD·051/221", "P1"), "ZHONYA", "OGN·077/298", "P1");
        var run = new Driver(initial); await HostAndAngel(run);
        await run.Act("P1", new PlayCardCommand("ANGEL-2", "SFD·051/221", [], Destination: "BASE"));
        await run.Act("P1", new PlayCardCommand("ZHONYA", "OGN·077/298", [], Destination: "BASE"));
        await FundYellow(run, 2); await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        var pending = run.State.PendingRuleChoice!;
        Assert.Equal(new[] { "ATTACHED_GEAR:HOST:ANGEL", "GEAR:HOST:ZHONYA" }, pending.Request.Options.Select(o => o.Id).Order(StringComparer.Ordinal));
        var before = MatchStateHasher.Hash(run.State);
        var wrong = await new CoreRuleEngine().ResolveAsync(run.State, new("c9-wrong-granting-instance", "P1", CommandTypes.PayCost),
            new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["ATTACHED_GEAR:HOST:ANGEL-2"]), default);
        Assert.False(wrong.Accepted); Assert.Empty(wrong.Events); Assert.Equal(before, MatchStateHasher.Hash(wrong.State));
        await Choose(run, "ATTACHED_GEAR:HOST:ANGEL"); AssertHostRecalled(run, "P1");
        Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard); Assert.Contains("ANGEL-2", run.State.PlayerZones["P1"].Base);
        Assert.Null(run.State.CardObjects["ANGEL-2"].AttachedToObjectId); Assert.Contains("ZHONYA", run.State.PlayerZones["P1"].Base);
        await run.Replay(); Export(initial, run, true, "specific-granting-guardian-faq-q1");
    }
}
