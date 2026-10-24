using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN429.5c: a later non-damaging resolution cannot reuse an earlier spell's
// nonlethal damage as spell-kill attribution when a battle bonus expires.
public sealed class BattleExpirySpellKillAttributionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhoenixDistinguishesImmediateSpellLethalFromLaterExpiryAfterDraw(bool immediateLethal)
    {
        BattleLimitedTestSupport.Runtime(output);
        var initial = Opening();
        var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new EndTurnCommand());
        var funds = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(funds[0], funds));
        await run.Act("P2", new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2", new PlayCardCommand("OBSERVER", "UNL-068/219", [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("ATTACKER", "SFD·110/221", [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"]));
        await run.Top();
        await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
        await run.Top();
        Assert.Equal(6, run.State.CardObjects["ATTACKER"].Power);
        if (run.State.FocusPlayerId != "P1") await run.Act(run.State.FocusPlayerId!, new PassFocusCommand());
        var p1funds = Enumerable.Range(1, 6).Select(i => "P1-FUND-" + i)
            .Concat(Enumerable.Range(1, 3).Select(i => "P1-ORANGE-" + i)).Append("P1-RED").ToArray();
        await run.Act("P1", new TapRuneCommand(p1funds[0], p1funds));
        foreach (var id in p1funds.Where(id => id.StartsWith("P1-ORANGE-", StringComparison.Ordinal) || id == "P1-RED"))
            await run.Act("P1", new RecycleRuneCommand(id));
        for (var i = 0; run.State.FocusPlayerId is not null; i++)
        { Assert.InRange(i, 0, 3); await run.Act(run.State.FocusPlayerId, new PassFocusCommand()); }
        await run.Act("P2", new DeclareBattleCommand("HOME", ["ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal("RESULT_TRIGGERS", run.State.PendingBattleResult!.Stage);
        Assert.Equal(0, run.State.CardObjects["ATTACKER"].Damage);

        for (var i = 1; i <= (immediateLethal ? 3 : 2); i++)
        {
            if (run.State.PriorityPlayerId != "P1") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Act("P1", new PlayCardCommand("DAMAGE-" + i, "OGN·127/298", []));
            await run.Top();
            if (i < 3)
            {
                Assert.Equal(i * 2, run.State.CardObjects["ATTACKER"].Damage);
                Assert.Equal(6, run.State.CardObjects["ATTACKER"].Power);
                Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
                    && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ATTACKER"));
            }
        }

        if (immediateLethal)
        {
            var death = Death(run);
            var cause = Assert.IsType<DestructionAttribution>(death.Event.Payload["destructionAttribution"]);
            Assert.True(cause.FromSpell);
            Assert.Equal("P1", cause.ControllerId); Assert.Equal("P1", cause.ResponsiblePlayerId);
            Assert.Equal("DAMAGE-3", cause.Source.ObjectId); Assert.Equal("OGN·127/298", cause.CardNo);
            var damage = Assert.Single(run.Events(), e => e.Event.Kind == "STACK_ITEM_RESOLVED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "DAMAGE-3"));
            Assert.Equal(damage.Event.Payload["stackItemId"], cause.InstanceId);
            Assert.Single(run.Events(), e => PhoenixQueued(e, "P1"));
            Assert.DoesNotContain(run.Events(), e => PhoenixQueued(e, "P2"));
            BattleLimitedCleanupTests.Save("expiry-spell-kill-positive-earned", initial, run);
        }
        else
        {
            await run.Top(); // Resolve the already-earned, non-damaging legend draw.
            var death = Death(run);
            Assert.False(death.Event.Payload.ContainsKey("destructionAttribution"));
            var lastResolved = run.Events().Last(e => e.Sequence < death.Sequence && e.Event.Kind == "STACK_ITEM_RESOLVED");
            Assert.Equal("LEGEND", lastResolved.Event.Payload["sourceObjectId"]);
            Assert.Equal("CAPTURED_LEGEND_BATTLE_WIN_DRAW", lastResolved.Event.Payload["effectKind"]);
            Assert.Contains(run.Events(), e => e.Sequence < death.Sequence && e.Event.Kind == "BATTLE_CLOSED");
            Assert.DoesNotContain(run.Events(), e => PhoenixQueued(e, "P1") || PhoenixQueued(e, "P2"));
        }

        // Decline only the genuinely earned positive-control offer, then finish
        // the same ordinary observer and original result continuation.
        for (var i = 0; run.State.PendingBattleResult is not null || run.State.StackItems.Count > 0
            || run.State.TriggerQueue.Count > 0 || run.State.PendingPayment is not null; i++)
        {
            Assert.InRange(i, 0, 10);
            if (run.State.PendingPayment is { } cost)
            {
                Assert.True(immediateLethal); Assert.Equal("TRIGGER_COST_CONFIRMATION", cost.PaymentWindow);
                await run.Act("P1", new PayCostCommand(cost.PaymentId, cost.PaymentWindow, ["DECLINE"]));
            }
            else if (run.State.PriorityPlayerId is null && run.State.TriggerQueue.Count > 1)
            {
                var choices = ResolutionResult.BuildPrompts(run.State).SelectMany(pair =>
                    pair.Value.Candidates!.Where(c => c.Action == CommandTypes.OrderTriggers && c.Enabled)
                        .Select(candidate => (Player: pair.Key, Candidate: candidate)));
                var choice = Assert.Single(choices);
                var metadata = JsonSerializer.SerializeToElement(choice.Candidate.Metadata,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var ids = metadata.GetProperty("orderedTriggerIds").Deserialize<string[]>()!;
                Assert.Equal(run.State.TriggerQueue.Where(t => t.ControllerId == choice.Player)
                    .Select(t => t.TriggerId).Order(), metadata.GetProperty("reorderableTriggerIds").Deserialize<string[]>()!.Order());
                await run.Act(choice.Player, new OrderTriggersCommand(OrderedTriggerIds: ids));
            }
            else if (run.State.StackItems.Count > 0) await run.Top();
            else Assert.Fail("The earned battle continuation has no playable item.");
        }
        Assert.False(run.State.BattleState.IsActive);
        Assert.Equal(1, run.State.DeathLedger.Counts.GetValueOrDefault("P2"));
        Assert.Equal(7, run.State.CardObjects["OBSERVER"].Power);
        Assert.Single(run.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "OBSERVER"));
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Contains("PHOENIX-" + player, run.State.PlayerZones[player].Graveyard);
            Assert.DoesNotContain("PHOENIX-" + player, run.State.PlayerZones[player].Base);
        }
        Assert.DoesNotContain(run.Events(), e => e.Event.Payload.ContainsKey("capturedUnitDeath"));
        await run.Replay();
        BattleLimitedCleanupTests.Save("expiry-spell-kill-" + immediateLethal, initial, run);
    }

    private static RecoveredEvent Death(Driver run) => Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
        && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ATTACKER"));
    private static bool PhoenixQueued(RecoveredEvent e, string player) => e.Event.Kind == "TRIGGER_QUEUED"
        && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "PHOENIX-" + player);

    private static MatchState Opening()
    {
        var initial = BattleLimitedTestSupport.Position("plain");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects)
        { ["ATTACKER"] = BattleLimitedCleanupTests.Printed("ATTACKER", "SFD·110/221", "P2") };
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        void Put(string id, string no, string owner, string zone)
        {
            cards[id] = BattleLimitedCleanupTests.Printed(id, no, owner); locations[id] = new(owner, zone);
            zones[owner] = zone switch {
                "BASE" => zones[owner] with { Base = [..zones[owner].Base, id] },
                "LEGEND" => zones[owner] with { LegendZone = [..zones[owner].LegendZone, id] },
                "GRAVEYARD" => zones[owner] with { Graveyard = [..zones[owner].Graveyard, id] },
                _ => zones[owner] with { Hand = [..zones[owner].Hand, id] } };
        }
        Put("P2-ORANGE", "UNL-R04", "P2", "BASE");
        Put("OBSERVER", "UNL-068/219", "P2", "HAND"); Put("LEGEND", "SFD·185/221", "P2", "LEGEND");
        foreach (var i in Enumerable.Range(1, 3))
        { Put("DAMAGE-" + i, "OGN·127/298", "P1", "HAND"); Put("P1-ORANGE-" + i, "UNL-R04", "P1", "BASE"); }
        foreach (var i in Enumerable.Range(1, 6)) Put("P1-FUND-" + i, "UNL-R06", "P1", "BASE");
        Put("P1-RED", "UNL-R01", "P1", "BASE");
        foreach (var player in new[] { "P1", "P2" }) Put("PHOENIX-" + player, "OGN·037/298", player, "GRAVEYARD");
        // The initial graveyards are explicit midgame inputs, not a legal-opening birth proof.
        return initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }
}
