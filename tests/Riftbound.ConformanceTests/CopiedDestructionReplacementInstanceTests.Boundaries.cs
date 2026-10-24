using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDestructionReplacementInstanceTests
{
    [Theory]
    [InlineData("SFD·173/221", "SONG")] [InlineData("SFD·173/221", "SONG-2")]
    [InlineData("SFD·239/221", "SONG")] [InlineData("SFD·239/221", "SONG-2")]
    [InlineData("SFD·239*/221", "SONG")] [InlineData("SFD·239*/221", "SONG-2")]
    public async Task TwoActualSongsOfferIndependentUnusedGroupsWithoutApplyingEitherTwice(string no, string selectedSong)
    {
        var initial = AddInitial(Position(no, true), "SONG-2", "SFD·059/221", "P1");
        var run = new Driver(initial); await SetUp(run, no, ["SONG", "SONG-2"]); await DestroyAll(run);
        await Select(run, "LOCAL_RECALL:FIELD-1:S"); await Select(run, "GEAR:S:G1");
        var request = run.State.PendingRuleChoice!.Request;
        var expected = new[] { "BASE-1", "BASE-2" }.SelectMany(target => new[] { "SONG", "SONG-2" }
            .Select(gear => CopyChoice(run.State, target, "S", gear))).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, request.Options.Select(o => o.Id).Order(StringComparer.Ordinal));
        Assert.Equal("P1", request.PlayerId);
        await Select(run, CopyChoice(run.State, "BASE-1", "S", selectedSong));
        AssertSaved(run, true);
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" })
            Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == id);
        await run.Replay(); Export("double-" + selectedSong, initial, run, no, true);
    }

    [Theory]
    [InlineData("detached")] [InlineData("returned-equipment")] [InlineData("reattached")]
    public async Task ActualDetachOrReturnEndsTheOldSongAndReattachmentCreatesOnlyOneFreshInstance(string mode)
    {
        const string no = "SFD·173/221";
        var initial = AddInitial(AddInitial(Position(no, true), "DETACH", "SFD·011/221", "P1"), "RETURN-GEAR", "SFD·135/221", "P1");
        var run = new Driver(initial); await SetUp(run, no, ["SONG"]);
        var old = run.State.CardObjects["SONG"].AttachedRuleText!;
        var generation = run.State.CardObjects["SONG"].ObjectGeneration;
        var returned = mode == "returned-equipment";
        await run.Act("P1", returned ? new PlayCardCommand("RETURN-GEAR", "SFD·135/221", ["SONG"])
            : new PlayCardCommand("DETACH", "SFD·011/221", ["S", "SONG"])); await run.Top();
        Assert.Null(run.State.CardObjects["SONG"].AttachedToObjectId); Assert.Null(run.State.CardObjects["SONG"].AttachedRuleText);
        Assert.Equal(returned ? generation + 1 : generation, run.State.CardObjects["SONG"].ObjectGeneration);
        if (mode == "reattached") {
            await run.Act("P1", new AssembleEquipmentCommand("SONG", "S", ["ASSEMBLE_GREEN"])); await run.Top();
            Assert.Equal(old.Revision + 1, run.State.CardObjects["SONG"].AttachedRuleText!.Revision);
        }
        await DestroyAll(run); await Select(run, "LOCAL_RECALL:FIELD-1:S"); await Select(run, "GEAR:S:G1");
        if (mode == "reattached") {
            var options = run.State.PendingRuleChoice!.Request.Options;
            Assert.Equal(2, options.Count);
            Assert.DoesNotContain(options, o => o.Id.EndsWith($"ATTACHED:SONG:{generation}:{old.Revision}", StringComparison.Ordinal));
            await Select(run, CopyChoice(run.State, "BASE-1", "S", "SONG"));
        }
        AssertSaved(run, mode == "reattached"); await run.Replay(); Export(mode, initial, run, no, mode == "reattached");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualUnitReturnAndReplayCannotReuseThePreviousGenerationSongBinding(bool reattach)
    {
        const string no = "SFD·173/221";
        var initial = AddInitial(Position(no, true), "RETURN-SOURCE", "OGN·104/298", "P1");
        var run = new Driver(initial); await SetUp(run, no, ["SONG"]);
        var old = run.State.CardObjects["SONG"].AttachedRuleText!;
        await run.Act("P1", new PlayCardCommand("RETURN-SOURCE", "OGN·104/298", ["S"])); await run.Top();
        Assert.Contains("S", run.State.PlayerZones["P1"].Hand); Assert.Null(run.State.CardObjects["SONG"].AttachedRuleText);
        await run.Act("P1", new PlayCardCommand("S", no, [], Destination: "BATTLEFIELD:HOME"));
        Assert.True(run.State.CardObjects["S"].ObjectGeneration > old.Unit.Generation);
        if (reattach) {
            await run.Act("P1", new AssembleEquipmentCommand("SONG", "S", ["ASSEMBLE_GREEN"])); await run.Top();
            var current = run.State.CardObjects["SONG"].AttachedRuleText!;
            Assert.True(current.Revision > old.Revision); Assert.Equal(run.State.CardObjects["S"].ObjectGeneration, current.Unit.Generation);
        }
        await DestroyAll(run); await Select(run, "LOCAL_RECALL:FIELD-1:S"); await Select(run, "GEAR:S:G1");
        if (reattach) await Select(run, CopyChoice(run.State, "BASE-1", "S", "SONG"));
        AssertSaved(run, reattach); await run.Replay(); Export("unit-new-generation-" + reattach, initial, run, no, reattach);
    }

    [Theory]
    [InlineData("SFD·173/221", false)] [InlineData("SFD·173/221", true)]
    [InlineData("SFD·239/221", false)] [InlineData("SFD·239/221", true)]
    [InlineData("SFD·239*/221", false)] [InlineData("SFD·239*/221", true)]
    public async Task ActualWholeUnitCopyUsesItsCurrentFaceAndLosesTheRecallWhenTheGlassesDetach(string no, bool detach)
    {
        var initial = AddInitial(AddInitial(AddInitial(Position(no, false), "GLASSES", "VEN·137", "P1"),
            "DETACH", "SFD·011/221", "P1"), "KILL", "OGN·229/298", "P2");
        var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("FIELD-1", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new PlayCardCommand("S", no, [], Destination: "BASE"));
        await run.Act("P1", new PlayCardCommand("FIELD-2", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new PlayCardCommand("GLASSES", "VEN·137", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("GLASSES", "FIELD-1", ["ASSEMBLE_YELLOW"])); await run.Top();
        await Select(run, "COPY:S");
        Assert.Equal(no, run.State.CardObjects["FIELD-1"].CardNo); Assert.Equal(4, run.State.CardObjects["FIELD-1"].Power);
        Assert.NotNull(run.State.CardObjects["FIELD-1"].AttachmentUnitCopies);
        if (detach) {
            await run.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["FIELD-1", "GLASSES"])); await run.Top();
            Assert.Equal("OGN·210/298", run.State.CardObjects["FIELD-1"].CardNo);
            Assert.Equal(2, run.State.CardObjects["FIELD-1"].Power); Assert.Null(run.State.CardObjects["FIELD-1"].AttachmentUnitCopies);
        }
        await FundOpponent(run);
        await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["FIELD-2"])); await run.Top();
        Assert.Null(run.State.PendingRuleChoice);
        Assert.Equal(!detach, run.State.PlayerZones["P1"].Base.Contains("FIELD-2"));
        Assert.Equal(detach, run.State.PlayerZones["P1"].Graveyard.Contains("FIELD-2"));
        Assert.Equal(!detach, run.Events().Any(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "FIELD-2"));
        await run.Replay(); Export("whole-copy-detached-" + detach, initial, run, no, !detach);
    }

    [Fact]
    public async Task ActualControlChangeUsesTheHostControllerForBothPrintedAndForeignOwnedCopiedText()
    {
        const string no = "SFD·173/221";
        var initial = AddInitial(Position(no, true), "TAKE", "OGN·203/298", "P2");
        foreach (var id in new[] { "P2-BASE-1", "P2-BASE-2" }) initial = AddInitial(initial, id, "OGN·210/298", "P2");
        for (var i = 1; i <= 3; i++) initial = AddInitial(initial, "P2-PURPLE-" + i, "UNL-R05", "P2", true);
        for (var i = 10; i <= 18; i++) initial = AddInitial(initial, "P2-FUND-" + i, "UNL-R06", "P2", true);
        var run = new Driver(initial); await SetUp(run, no, ["SONG"], false);
        await run.Act("P1", new EndTurnCommand());
        var runes = run.State.PlayerZones["P2"].Base.Where(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        foreach (var batch in runes.Chunk(12)) await run.Act("P2", new TapRuneCommand(batch[0], batch));
        var purple = Enumerable.Range(1, 3).Select(i => "P2-PURPLE-" + i).ToArray();
        await run.Act("P2", new RecycleRuneCommand(purple[0], purple));
        foreach (var id in new[] { "P2-BASE-1", "P2-BASE-2" }) await run.Act("P2", new PlayCardCommand(id, "OGN·210/298", [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["S"])); await run.Top();
        Assert.Equal("P2", run.State.CardObjects["S"].ControllerId); Assert.Equal("P1", run.State.CardObjects["S"].OwnerId);
        Assert.Equal("P1", run.State.CardObjects["SONG"].OwnerId); Assert.NotNull(run.State.CardObjects["SONG"].AttachedRuleText);
        var yellow = Enumerable.Range(1, 3).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new RecycleRuneCommand(yellow[0], yellow));
        await run.Act("P2", new PlayCardCommand("AOE", "UNL-180/219", [])); await run.Top();
        var pending = run.State.PendingRuleChoice!; Assert.Equal("P2", pending.Request.PlayerId);
        Assert.Equal(4, pending.Request.Options.Count);
        await AssertRejected(run.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [pending.Request.Options[0].Id]));
        await Select(run, CopyChoice(run.State, "P2-BASE-1", "S", "SONG"));
        Assert.Null(run.State.PendingRuleChoice);
        foreach (var id in new[] { "P2-BASE-1", "P2-BASE-2" }) Assert.Contains(id, run.State.PlayerZones["P2"].Base);
        foreach (var id in new[] { "S", "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) Assert.Contains(id, run.State.PlayerZones["P1"].Graveyard);
        await run.Replay(); Export("actual-controller", initial, run, no, true);
    }

    private static async Task SetUp(Driver run, string no, IReadOnlyList<string> songs, bool zhonya = true)
    {
        await run.Act("P1", new PlayCardCommand("S", no, [], Destination: "BATTLEFIELD:HOME"));
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" })
            await run.Act("P1", new PlayCardCommand(id, "OGN·210/298", [], Destination: id.StartsWith("FIELD", StringComparison.Ordinal) ? "BATTLEFIELD:HOME" : "BASE"));
        if (zhonya) await run.Act("P1", new PlayCardCommand("G1", "OGN·077/298", [], Destination: "BASE"));
        foreach (var gear in songs) {
            await run.Act("P1", new PlayCardCommand(gear, "SFD·059/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand(gear, "S", ["ASSEMBLE_GREEN"])); await run.Top();
            Assert.Equal(no, run.State.CardObjects[gear].AttachedRuleText!.CardNo);
        }
    }
    private static async Task FundOpponent(Driver run)
    {
        await run.Act("P1", new EndTurnCommand());
        var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(runes[0], runes));
        await run.Act("P2", new RecycleRuneCommand(runes[0], runes.Take(3).ToArray()));
    }
    private static async Task DestroyAll(Driver run)
    { await FundOpponent(run); await run.Act("P2", new PlayCardCommand("AOE", "UNL-180/219", [])); await run.Top(); }
    private static string CopyChoice(MatchState state, string target, string source, string gear)
        => $"LOCAL_RECALL:{target}:{source}:ATTACHED:{gear}:{state.CardObjects[gear].ObjectGeneration}:{state.CardObjects[gear].AttachmentRevision}";
    private static string? Target(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") as string;
    private static void AssertSaved(Driver run, bool copied)
    {
        Assert.Null(run.State.PendingRuleChoice); Assert.Contains("S", run.State.PlayerZones["P1"].Base);
        Assert.Contains("G1", run.State.PlayerZones["P1"].Graveyard);
        foreach (var id in new[] { "FIELD-1", "FIELD-2" }) Assert.Contains(id, run.State.PlayerZones["P1"].Base);
        foreach (var id in new[] { "BASE-1", "BASE-2" }) {
            Assert.Equal(copied, run.State.PlayerZones["P1"].Base.Contains(id)); Assert.Equal(!copied, run.State.PlayerZones["P1"].Graveyard.Contains(id));
        }
    }
    private static MatchState AddInitial(MatchState state, string id, string no, string owner, bool rune = false)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, rune ? "BASE" : "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = rune
                ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] }
                : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static async Task AssertRejected(MatchState state, string actor, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("c8-illegal", actor, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
