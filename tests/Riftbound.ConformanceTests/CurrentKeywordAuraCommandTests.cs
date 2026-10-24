using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.CurrentKeywordAuraParticipantTests;
using static Riftbound.ConformanceTests.CurrentKeywordAuraProjectionTests;

namespace Riftbound.ConformanceTests;

public sealed class CurrentKeywordAuraCommandTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData("intrinsic", 4)]
    [InlineData("temporary", 3)]
    [InlineData("continuous", 4)]
    public async Task OrdinaryBirthGrantAndLossUseColdRestoreDuplicatesAndTheOriginalActionLog(string mode, int expectedProjectionPower)
    {
        // Explicit midgame opening. All tested units/spells begin in HAND;
        // token birth, grants and departures must be earned by normal commands.
        // The power aura metadata is supplied to the shared seam, not registered.
        var initial = Opening(mode);
        var run = new Driver(initial);
        string target;
        if (mode == "continuous")
        {
            await run.Act("P1", new PlayCardCommand("SOURCE", "UNL-058/219", [], Destination: "BASE"));
            await run.Act("P1", new PlayCardCommand("TOKEN-SPELL", "OGN·094/298", []));
            await Drain(run);
            target = Assert.Single(run.State.CardObjects.Values, c => P6TokenFactoryCatalog.IsTokenFactory(c.TokenFactoryCardNo ?? c.CardNo ?? "")
                && c.Tags.Contains(CardObjectTags.UnitCard)).ObjectId;
            Assert.Equal(3, run.State.CardObjects[target].Power);
            await run.Act("P1", new MoveUnitCommand(target, "BASE", "BATTLEFIELD:F", []));
        }
        else
        {
            target = "TARGET";
            await run.Act("P1", new PlayCardCommand(target, mode == "intrinsic" ? "OGN·054/298" : "OGN·210/298", [], Destination: "BATTLEFIELD:F"));
            if (mode == "temporary")
            {
                Assert.False(Inspect(run.State, run.State.CardObjects[target]).Matches);
                await run.Act("P1", new PlayCardCommand("GRANT", "OGN·057/298", [target]));
                await Drain(run);
            }
        }

        var query = Inspect(run.State, run.State.CardObjects[target]);
        Assert.True(query.Matches);
        Assert.Equal(mode == "continuous" ? new[] { "SOURCE" } : [], query.PublicKeywordSourceIds);
        Assert.True(AuthorityMatches(run.State, Aura(CurrentBulwark), run.State.CardObjects[target]));
        var effect = Assert.Single(Effects(run.State, Aura(CurrentBulwark)));
        Assert.Equal(target, effect.TargetObjectId); Assert.Equal(1, effect.PowerDelta);
        Assert.Equal(expectedProjectionPower, effect.EffectivePower);
        Assert.Equal(mode == "continuous" ? new[] { "F", "SOURCE" } : ["F"], effect.SourceDependencyObjectIds);
        if (initial.CardObjects.TryGetValue(target, out var original))
            Assert.True(run.State.CardObjects[target].ObjectGeneration > original.ObjectGeneration);

        // A fresh invalid intent retains state, events, both views and prompts.
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "PLAY_CARD", sourceObjectId = "PRIVATE-P2-DRAW-1",
            cardNo = "OGN·219/298", targetObjectIds = Array.Empty<string>(),
            promptId = ResolutionResult.BuildPrompts(run.State)["P1"].PromptId, snapshotTick = run.State.Tick });
        await run.ActRetained(new("P1", "n21-private-source-reject", raw), false);
        Assert.Contains("出牌只能选择当前允许的来源", Assert.Single(run.Commands(), command => !command.Accepted).ErrorMessage);

        if (mode == "temporary")
        {
            await run.Act("P1", new EndTurnCommand());
            Assert.False(Inspect(run.State, run.State.CardObjects[target]).Matches);
            Assert.Empty(Effects(run.State, Aura(CurrentBulwark)));
            Assert.Contains("GRANT", run.State.PlayerZones["P1"].Graveyard);
        }
        if (mode == "continuous")
        {
            var sourceGeneration = run.State.CardObjects["SOURCE"].ObjectGeneration;
            await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
            await Drain(run);
            Assert.Contains("SOURCE", run.State.PlayerZones["P1"].Hand);
            Assert.True(run.State.CardObjects["SOURCE"].ObjectGeneration > sourceGeneration);
            Assert.False(Inspect(run.State, run.State.CardObjects[target]).Matches);
            Assert.Empty(Inspect(run.State, run.State.CardObjects[target]).PublicKeywordSourceIds);
            Assert.Empty(Effects(run.State, Aura(CurrentBulwark)));
        }
        await run.Replay();
        output.WriteLine($"{mode}: commands={run.Commands().Length}; events={run.Events().Length}; finalHash={MatchStateHasher.Hash(run.State)}");
        if (Environment.GetEnvironmentVariable("RIFTBOUND_N21_EVIDENCE") is { } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, mode + ".json"), JsonSerializer.Serialize(new
                { initial, commands = run.Commands(), events = run.Events(), final = run.State, sampledPowerAura = effect },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    private static async Task Drain(Driver run)
    {
        for (var i = 0; run.State.StackItems.Count > 0; i++)
        {
            Assert.InRange(i, 0, 7);
            await run.Top();
        }
        Assert.Empty(run.State.TriggerQueue);
        Assert.Null(run.State.PendingCardChoice);
    }

    private static MatchState Opening(string mode)
    {
        var state = Position("temporary");
        var cards = new Dictionary<string, CardObjectState> { ["F"] = state.CardObjects["F"] with { ControllerId = "P1" } };
        var locations = new Dictionary<string, ObjectLocationState> { ["F"] = new("P1", "BATTLEFIELD", "F") };
        var zones = new Dictionary<string, PlayerZones>();
        var hand = mode == "continuous" ? new[] { "SOURCE", "TOKEN-SPELL", "RETURN" } : mode == "temporary" ? ["TARGET", "GRANT"] : ["TARGET"];
        void Put(string id, string no, string player, string zone)
        { cards[id] = Printed(id, no, player) with { ObjectGeneration = 0 }; locations[id] = new(player, zone); }
        Put("TARGET", mode == "intrinsic" ? "OGN·054/298" : "OGN·210/298", "P1", "HAND");
        if (mode == "continuous") { cards.Remove("TARGET"); locations.Remove("TARGET"); }
        foreach (var id in hand.Where(id => id != "TARGET"))
            Put(id, id == "SOURCE" ? "UNL-058/219" : id == "TOKEN-SPELL" ? "OGN·094/298" : id == "RETURN" ? "OGN·104/298" : "OGN·057/298", "P1", "HAND");
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => "PRIVATE-" + player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = Enumerable.Range(1, 3).Select(i => "PRIVATE-" + player + "-RUNE-" + i).ToArray();
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = PlayerZones.Empty with { Hand = player == "P1" ? hand : [], MainDeck = deck,
                RuneDeck = runes, Battlefields = player == "P1" ? ["F"] : [] };
        }
        return state with { RoomId = "N21-ACTUAL-GRANT-" + mode, CardObjects = cards, ObjectLocations = locations,
            PlayerZones = zones, ReadyPlayerIds = ["P1", "P2"],
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 10, new Dictionary<string, int> { [RuneTrait.Green] = 10, [RuneTrait.Blue] = 10 }), ["P2"] = RunePool.Empty },
            CardPlays = new(5, [], []) };
    }
}
