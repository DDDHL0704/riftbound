using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    [Theory]
    [InlineData("detached")] [InlineData("equipment-return")] [InlineData("unit-generation")]
    public async Task ActualReattachmentRestoresOnlyTheCurrentHostAndEquipmentBinding(string mode)
    {
        var initial = AddInitial(AddInitial(AddInitial(Position(), "DETACH", "SFD·011/221", "P1"),
            "RETURN-GEAR", "SFD·135/221", "P1"), "RETURN-UNIT", "OGN·104/298", "P1");
        var run = new Driver(initial); await HostAndAngel(run);
        var hostGeneration = run.State.CardObjects["HOST"].ObjectGeneration;
        var gearGeneration = run.State.CardObjects["ANGEL"].ObjectGeneration;
        if (mode == "detached") await run.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "ANGEL"]));
        if (mode == "equipment-return") await run.Act("P1", new PlayCardCommand("RETURN-GEAR", "SFD·135/221", ["ANGEL"]));
        if (mode == "unit-generation") await run.Act("P1", new PlayCardCommand("RETURN-UNIT", "OGN·104/298", ["HOST"]));
        await run.Top(); Assert.Null(run.State.CardObjects["ANGEL"].AttachedToObjectId);
        if (mode == "equipment-return") await run.Act("P1", new PlayCardCommand("ANGEL", "SFD·051/221", [], Destination: "BASE"));
        if (mode == "unit-generation") await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BASE"));
        if (mode == "unit-generation") Assert.True(run.State.CardObjects["HOST"].ObjectGeneration > hostGeneration);
        else Assert.Equal(hostGeneration, run.State.CardObjects["HOST"].ObjectGeneration);
        if (mode == "equipment-return") Assert.True(run.State.CardObjects["ANGEL"].ObjectGeneration > gearGeneration);
        else Assert.Equal(gearGeneration, run.State.CardObjects["ANGEL"].ObjectGeneration);
        var currentHostGeneration = run.State.CardObjects["HOST"].ObjectGeneration;
        await run.Act("P1", new AssembleEquipmentCommand("ANGEL", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        Assert.Equal("HOST", run.State.CardObjects["ANGEL"].AttachedToObjectId);
        await FundYellow(run, 2); await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        Assert.Null(run.State.PendingRuleChoice); Assert.Contains("HOST", run.State.PlayerZones["P1"].Base);
        Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(currentHostGeneration, run.State.CardObjects["HOST"].ObjectGeneration);
        AssertHostEventSource(run, "P1"); await run.Replay(); Export(initial, run, true, "reattached-" + mode);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualWholeUnitFaceCopyAndItsRemovalKeepTheIndependentGuardianBinding(bool detachGlasses)
    {
        var initial = AddInitial(AddInitial(AddInitial(Position(), "DONOR", "SFD·173/221", "P1"),
            "GLASSES", "VEN·137", "P1"), "DETACH", "SFD·011/221", "P1");
        var run = new Driver(initial); await HostAndAngel(run);
        await run.Act("P1", new PlayCardCommand("DONOR", "SFD·173/221", [], Destination: "BASE"));
        await run.Act("P1", new PlayCardCommand("GLASSES", "VEN·137", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("GLASSES", "HOST", ["ASSEMBLE_YELLOW"])); await run.Top();
        await Choose(run, "COPY:DONOR");
        Assert.Equal("SFD·173/221", run.State.CardObjects["HOST"].CardNo);
        Assert.Equal(4, run.State.CardObjects["HOST"].Power); Assert.NotNull(run.State.CardObjects["HOST"].AttachmentUnitCopies);
        if (detachGlasses) {
            await run.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GLASSES"])); await run.Top();
            Assert.Equal("OGN·210/298", run.State.CardObjects["HOST"].CardNo); Assert.Null(run.State.CardObjects["HOST"].AttachmentUnitCopies);
        }
        Assert.Equal("HOST", run.State.CardObjects["ANGEL"].AttachedToObjectId);
        await FundYellow(run, 2); await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        if (detachGlasses) {
            Assert.Equal(new[] { "ATTACHED_GEAR:HOST:ANGEL", "LOCAL_RECALL:HOST:DONOR" },
                run.State.PendingRuleChoice!.Request.Options.Select(option => option.Id).Order(StringComparer.Ordinal));
            await Choose(run, "ATTACHED_GEAR:HOST:ANGEL");
        }
        AssertHostRecalled(run, "P1"); Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(detachGlasses ? null : "HOST", run.State.CardObjects["GLASSES"].AttachedToObjectId);
        AssertGroupAtBase(run.State, "P1", "HOST", "GLASSES");
        AssertHostEventSource(run, "P1"); await run.Replay(); Export(initial, run, true, "unit-face-copy-detached-" + detachGlasses);
    }

    [Theory]
    [InlineData("none")] [InlineData("guardian")] [InlineData("guardian-and-song")]
    public async Task ActualControlRecallRelocatesTheWholeGroupWithoutChangingForeignEquipmentState(string attachments)
    {
        var initial = AddInitial(AddInitial(AddInitial(Position("SFD·173/221", battlefield: true, richOpponent: true),
            "TAKE", "OGN·203/298", "P2"), "SONG", "SFD·059/221", "P1"), "BASE-UNIT", "OGN·210/298", "P1");
        var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("HOST", "SFD·173/221", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new PlayCardCommand("BASE-UNIT", "OGN·210/298", [], Destination: "BASE"));
        var group = new List<string> { "HOST" };
        if (attachments != "none") {
            await run.Act("P1", new PlayCardCommand("ANGEL", "SFD·051/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand("ANGEL", "HOST", ["ASSEMBLE_GREEN"])); await run.Top(); group.Add("ANGEL");
        }
        if (attachments == "guardian-and-song") {
            await run.Act("P1", new PlayCardCommand("SONG", "SFD·059/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await run.Top(); group.Add("SONG");
        }
        await run.Act("P1", new EndTurnCommand()); await Tap(run, run.State.PlayerZones["P2"].Base);
        var purple = Enumerable.Range(1, 3).Select(i => "P2-PURPLE-" + i).ToArray();
        await run.Act("P2", new RecycleRuneCommand(purple[0], purple));
        foreach (var target in new[] { "BASE-UNIT", "HOME", "P2-FUND-5" }) {
            var before = MatchStateHasher.Hash(run.State);
            var rejected = await new CoreRuleEngine().ResolveAsync(run.State, new("c9-invalid-control-" + target, "P2", CommandTypes.PlayCard),
                new PlayCardCommand("TAKE", "OGN·203/298", [target]), default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        }
        var equipment = group.Skip(1).ToDictionary(id => id, id => JsonSerializer.Serialize(run.State.CardObjects[id], Json));
        var host = run.State.CardObjects["HOST"];
        await run.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["HOST"])); await run.Top();
        AssertGroupAtBase(run.State, "P2", group.ToArray());
        Assert.Equal("P2", run.State.CardObjects["HOST"].ControllerId); Assert.Equal("P1", run.State.CardObjects["HOST"].OwnerId);
        Assert.Equal(host.ObjectGeneration, run.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(host.MovementCountThisTurn, run.State.CardObjects["HOST"].MovementCountThisTurn);
        foreach (var (id, before) in equipment) Assert.Equal(before, JsonSerializer.Serialize(run.State.CardObjects[id], Json));
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_MOVED" && group.Contains(Target(e.Event) ?? ""));
        var frame = GuardianFrame(initial, run); Assert.Empty(GuardianErrors(frame));
        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in new[] { "P1", "P2" })
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)[player], Json), JsonSerializer.Serialize(cold.SnapshotFor(player), Json));
        await run.Replay(); Export(initial, run, attachments != "none", "control-group-" + attachments);
    }

    private static void AssertGroupAtBase(MatchState state, string player, params string[] group)
    {
        foreach (var id in group) {
            Assert.Single(state.PlayerZones.SelectMany(p => p.Value.Base.Concat(p.Value.Battlefields).Concat(p.Value.Hand)
                .Concat(p.Value.MainDeck).Concat(p.Value.Graveyard)), value => value == id);
            Assert.Contains(id, state.PlayerZones[player].Base);
            Assert.Equal(new ObjectLocationState(player, "BASE"), state.ObjectLocations[id]);
        }
    }
}
