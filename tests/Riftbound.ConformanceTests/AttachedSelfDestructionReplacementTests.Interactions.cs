using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    [Theory]
    [InlineData("OGN·210/298", 3)] [InlineData("OGN·096/298", 2)]
    public async Task ActualLethalDamageDestroysTheArmamentThenFullyHealsAndExhaustsWithoutAHostDeath(string hostNo, int might)
    {
        var initial = AddInitial(AddInitial(AddInitial(Position(hostNo, battlefield: true), "READY", "OGN·146/298", "P1"),
            "RAY", "OGN·009/298", "P2"), "P2-RED", "UNL-R01", "P2", true);
        var run = new Driver(initial); await HostAndAngel(run, hostNo, "BATTLEFIELD:HOME");
        await run.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"])); await run.Top();
        Assert.False(run.State.CardObjects["HOST"].IsExhausted); Assert.Equal(might, Might(run.State, "HOST", "P1"));
        var deck = run.State.PlayerZones["P1"].MainDeck.ToArray();
        await run.Act("P1", new EndTurnCommand());
        var runes = run.State.PlayerZones["P2"].Base.Where(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        await Tap(run, runes); await run.Act("P2", new RecycleRuneCommand("P2-RED"));
        await run.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["HOST"])); await run.Top();
        AssertHostRecalled(run, "P1"); Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(deck, run.State.PlayerZones["P1"].MainDeck); Assert.Empty(run.State.StackItems);
        Assert.DoesNotContain(run.State.DeathLedger.Counts, pair => pair.Key == "P1" && pair.Value != 0);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == "HOST");
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "TRIGGER_QUEUED" && e.Event.Payload.GetValueOrDefault("sourceObjectId") as string == "HOST");
        var damage = Assert.Single(run.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Target(e.Event) == "HOST");
        Assert.Equal(3, Convert.ToInt32(damage.Event.Payload["damage"]));
        AssertHostEventSource(run, "P1"); await run.Replay(); Export(initial, run, true, "lethal-" + hostNo.Replace('/', '_'));
    }

    [Theory]
    [InlineData("ANGEL")] [InlineData("ANGEL-2")]
    public async Task TwoActualGuardiansAreDistinctAndOnlyOneAppliesPerDestruction(string first)
    {
        var initial = AddInitial(AddInitial(Position(), "ANGEL-2", "SFD·051/221", "P1"), "KILL-2", "OGN·229/298", "P2");
        var run = new Driver(initial); await HostAndAngel(run);
        await run.Act("P1", new PlayCardCommand("ANGEL-2", "SFD·051/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("ANGEL-2", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        Assert.Equal(4, Might(run.State, "HOST", "P1"));
        await FundYellow(run, 4);
        await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        var request = run.State.PendingRuleChoice!.Request; Assert.Equal("P1", request.PlayerId); Assert.Equal(2, request.Options.Count);
        var selected = request.Options.Single(o => o.ObjectIds!.Contains(first));
        var before = MatchStateHasher.Hash(run.State);
        var wrong = await new CoreRuleEngine().ResolveAsync(run.State, new("c9-wrong-controller", "P2", CommandTypes.PayCost),
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected.Id]), default);
        Assert.False(wrong.Accepted); Assert.Empty(wrong.Events); Assert.Equal(before, MatchStateHasher.Hash(wrong.State));
        await run.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected.Id]));
        var survivor = first == "ANGEL" ? "ANGEL-2" : "ANGEL";
        AssertHostRecalled(run, "P1"); Assert.Contains(first, run.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(survivor, run.State.PlayerZones["P1"].Base); Assert.Equal("HOST", run.State.CardObjects[survivor].AttachedToObjectId);
        Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST");
        await run.Act("P2", new PlayCardCommand("KILL-2", "OGN·229/298", ["HOST"])); await run.Top();
        AssertHostRecalled(run, "P1"); Assert.All(new[] { "ANGEL", "ANGEL-2" }, id => Assert.Contains(id, run.State.PlayerZones["P1"].Graveyard));
        Assert.Equal(2, run.Events().Count(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST"));
        AssertHostEventSource(run, "P1"); await run.Replay(); Export(initial, run, true, "two-guardians-" + first);
    }

    [Fact]
    public async Task ActualHostControlChangeKeepsForeignOwnedAndControlledGuardianButTheHostExecutesItsText()
    {
        var initial = AddInitial(Position(battlefield: true, richOpponent: true), "TAKE", "OGN·203/298", "P2");
        var run = new Driver(initial); await HostAndAngel(run, destination: "BATTLEFIELD:HOME");
        await run.Act("P1", new EndTurnCommand());
        var runes = run.State.PlayerZones["P2"].Base.ToArray(); await Tap(run, runes);
        var purple = Enumerable.Range(1, 3).Select(i => "P2-PURPLE-" + i).ToArray();
        await run.Act("P2", new RecycleRuneCommand(purple[0], purple));
        await run.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["HOST"])); await run.Top();
        Assert.Equal("P2", run.State.CardObjects["HOST"].ControllerId); Assert.Equal("P1", run.State.CardObjects["HOST"].OwnerId);
        Assert.Equal("P1", run.State.CardObjects["ANGEL"].ControllerId); Assert.Equal("P1", run.State.CardObjects["ANGEL"].OwnerId);
        Assert.Equal("HOST", run.State.CardObjects["ANGEL"].AttachedToObjectId);
        AssertGroupAtBase(run.State, "P2", "HOST", "ANGEL");
        Export(initial, run, true, "control-before-kill");
        var yellow = new[] { "P2-FUND-1", "P2-FUND-2" }; await run.Act("P2", new RecycleRuneCommand(yellow[0], yellow));
        await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        Export(initial, run, true, "control-after-kill");
        AssertHostRecalled(run, "P2"); Assert.Equal("P1", run.State.CardObjects["HOST"].OwnerId);
        Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        AssertHostEventSource(run, "P2"); await run.Replay(); Export(initial, run, true, "actual-foreign-owner-controller");
    }

    [Theory]
    [InlineData("ANGEL")] [InlineData("ZHONYA")]
    public async Task ARealHourglassAndAttachedGuardianCannotBothReplaceTheSameInheritedEvent(string selectedGear)
    {
        var initial = AddInitial(Position(), "ZHONYA", "OGN·077/298", "P1");
        var run = new Driver(initial); await HostAndAngel(run);
        await run.Act("P1", new PlayCardCommand("ZHONYA", "OGN·077/298", [], Destination: "BASE"));
        await FundYellow(run, 2);
        await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        var request = run.State.PendingRuleChoice!.Request; Assert.Equal(2, request.Options.Count);
        var option = request.Options.Single(o => o.ObjectIds!.Contains(selectedGear));
        await run.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
        AssertHostRecalled(run, "P1"); Assert.Contains(selectedGear, run.State.PlayerZones["P1"].Graveyard);
        var other = selectedGear == "ANGEL" ? "ZHONYA" : "ANGEL";
        Assert.Contains(other, run.State.PlayerZones["P1"].Base);
        Assert.Single(run.Events(), e => e.Event.Kind == "EQUIPMENT_DESTROYED");
        Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST");
        if (selectedGear == "ANGEL") AssertHostEventSource(run, "P1");
        await run.Replay(); Export(initial, run, true, "guardian-and-hourglass-" + selectedGear);
    }

    private static async Task HostAndAngel(Driver run, string hostNo = "OGN·210/298", string destination = "BASE")
    {
        await run.Act("P1", new PlayCardCommand("HOST", hostNo, [], Destination: destination));
        await run.Act("P1", new PlayCardCommand("ANGEL", "SFD·051/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("ANGEL", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
    }
    private static async Task FundYellow(Driver run, int power)
    {
        await run.Act("P1", new EndTurnCommand()); var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray();
        await Tap(run, runes); await run.Act("P2", new RecycleRuneCommand(runes[0], runes.Take(power).ToArray()));
    }
    private static async Task Tap(Driver run, IReadOnlyList<string> runes)
    { foreach (var batch in runes.Chunk(12)) await run.Act("P2", new TapRuneCommand(batch[0], batch)); }
    private static int Might(MatchState state, string id, string player)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], Json)
            .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static string? Target(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") as string;
    private static void AssertHostRecalled(Driver run, string controller)
    {
        Assert.Null(run.State.PendingRuleChoice); Assert.Contains("HOST", run.State.PlayerZones[controller].Base);
        var host = run.State.CardObjects["HOST"]; Assert.True(host.IsExhausted); Assert.Equal(0, host.Damage);
        Assert.Null(host.DamageMarkedByPlayerIds); Assert.Equal(1, host.ObjectGeneration); Assert.Equal(controller, host.ControllerId);
        Assert.Equal(0, host.MovementCountThisTurn);
    }
    private static void AssertHostEventSource(Driver run, string controller)
    {
        foreach (var value in run.Events().Where(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST")) {
            Assert.Equal("HOST", value.Event.Payload["sourceObjectId"]); Assert.Equal(controller, value.Event.Payload["controllerId"]);
            Assert.Equal(ReplacementKinds.AttachedUnitDestroyedDestroySourceRecallExhausted, value.Event.Payload["replacementEffectId"]);
        }
        foreach (var value in run.Events().Where(e => e.Event.Kind == "EQUIPMENT_DESTROYED" && (Target(e.Event) is "ANGEL" or "ANGEL-2")))
            Assert.Equal("HOST", value.Event.Payload["sourceObjectId"]);
    }
}
