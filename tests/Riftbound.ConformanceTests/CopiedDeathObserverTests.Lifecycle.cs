using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDeathObserverTests
{
    [Theory]
    [InlineData("UNL-068/219")] [InlineData("UNL-129/219")]
    [InlineData("OGN·118/298")] [InlineData("ARC-006/006")]
    public async Task ActualReturnToPrivateHandPreservesEarnedCopiesAndCannotBuffANewGeneration(string no)
    {
        var s = LocalDestructionRecallTests.At(Position(no, 1), "HOST", "P2", "BATTLEFIELD", "BF");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Power = 3 } } };
        s = HandDiscardObserverTests.Put(s, "RETURN", "OGN·169/298", "P1", "HAND");
        var game = new Driver(s);
        try
        {
            await Assemble(game, 1); await Kill(game); await Order(game);
            var earned = game.State.StackItems.Where(t => t.DeathObserver is not null).ToArray();
            Assert.Equal(2, earned.Length); var generation = earned[0].DeathObserver!.SourceGeneration;
            await game.Act("P2", new PassPriorityCommand()); await game.Act("P1", new TapRuneCommand("PAY1"));
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"])); await game.Top();
            Assert.Contains("HOST", game.State.PlayerZones["P2"].Hand);
            Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
            Assert.Null(game.State.CardObjects["GEAR0"].AttachedRuleText);
            var snapshots = ResolutionResult.BuildSnapshots(game.State);
            foreach (var snapshot in new[] { snapshots["P1"], MatchReplayRedactor.BuildSpectatorFrame(
                game.State.RoomId, game.State.Tick, 0, [], game.State).SpectatorSnapshot })
            {
                var rows = snapshot.Stack;
                Assert.Equal(2, rows.Count);
                Assert.All(rows, value => {
                    var row = Assert.IsType<Dictionary<string, object?>>(value);
                    Assert.Equal("HIDDEN", row["sourceObjectId"]); Assert.Equal("HIDDEN", row["sourceVisibility"]);
                    Assert.Equal(no, row["cardNo"]); Assert.Equal("FRIENDLY_UNIT_DEATH_OBSERVER", row["effectKind"]);
                });
            }
            Assert.Equal(earned.Select(t => MatchStateHasher.HashValue(t.DeathObserver)),
                game.State.StackItems.Select(t => MatchStateHasher.HashValue(t.DeathObserver)));
            var returned = MatchStateHasher.HashValue(game.State.CardObjects["HOST"]);
            var hand = game.State.PlayerZones["P2"].Hand.Count;
            await Finish(game);
            Assert.Equal(returned, MatchStateHasher.HashValue(game.State.CardObjects["HOST"]));
            Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "TRIGGER_RESOLVED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")));
            if (no == "UNL-129/219") Assert.Equal(2, game.State.PlayerExperience["P2"]);
            if (no == "OGN·118/298") Assert.Equal(hand + 2, game.State.PlayerZones["P2"].Hand.Count);
            if (no == "ARC-006/006") Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
            await game.Replay();
        }
        finally { SaveLifecycle(game, s, "return-" + no.Replace('/', '_')); }
    }

    [Theory]
    [InlineData("UNL-068/219")] [InlineData("UNL-129/219")]
    [InlineData("OGN·118/298")] [InlineData("ARC-006/006")]
    public async Task SameActionSourceDeathSuppressesNativeAndAttachedText(string no)
    {
        // Explicit component initial state: no accepted history is imported or changed.
        var s = Position(no, 1);
        s = s with { ActivePlayerId = "P1", TurnPlayerId = "P1",
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["GEAR0"] = s.CardObjects["GEAR0"] with { AttachedToObjectId = "HOST", AttachmentRevision = 1,
                    AttachedRuleText = new(new("HOST", 0), no, 1, 0, 0) } } };
        var done = await OfficialSettReplacementTests.Open(s, "UNL-180/219");
        Assert.Contains("HOST", done.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(done.Events, e => e.Kind == "TRIGGER_QUEUED"
            && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.DoesNotContain(done.State.TriggerQueue, t => t.SourceObjectId == "HOST");
        Assert.DoesNotContain(done.State.StackItems, t => t.SourceObjectId == "HOST");
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task CopiedViktorDoesNotObserveAMinionDeath()
    {
        var s = Position("ARC-006/006", 2);
        var token = P6TokenFactoryCatalog.GetAll().Single(t => t.CardNo == P6TokenFactoryCatalog.ZaunMinionTokenCardNo)
            .CreateObject("D", "P2", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = token } };
        var game = new Driver(s); await Assemble(game, 2); await Kill(game);
        Assert.False(game.State.CardObjects.ContainsKey("D")); Assert.Equal(1, game.State.DeathLedger.Counts["P2"]);
        Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED"); await game.Replay();
    }

    [Fact]
    public async Task NativeAndCopiedDeathFactsUseTheVictimsControllerAndKeepItsDifferentOwner()
    {
        var s = Position("UNL-129/219", 1);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { OwnerId = "P1" } } };
        var game = new Driver(s); await Assemble(game, 1); await Kill(game);
        Assert.Contains("D", game.State.PlayerZones["P1"].Graveyard);
        Assert.All(game.State.TriggerQueue, t => {
            Assert.Equal("P2", t.ControllerId); Assert.Equal("P2", t.DeathObserver!.Destroyed.ControllerId);
            Assert.Equal("P1", t.DeathObserver.Destroyed.OwnerId); });
        Assert.Equal(2, game.State.TriggerQueue.Count); await Finish(game);
        Assert.Equal(2, game.State.PlayerExperience["P2"]); Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        await game.Replay();
    }
    private static void SaveLifecycle(Driver game, MatchState initial, string label)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_DEATH_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame initial state; subsequent original accepted commands are unchanged",
            initial, final = game.State, commands = game.Commands(), events = game.Events()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
