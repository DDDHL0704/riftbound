using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven007DiscardTriggerConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ActualRequiredDiscardPaysNowAndJinxResolvesBeforeTheEmpowerEffect(bool ownTrigger)
    {
        var owner = ownTrigger ? "P1" : "P2"; var initial = Position(owner); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·007", []));
            var paid = game.State.CardObjects["FODDER"].ObjectGeneration;
            await game.Act("P1", Activate());
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var empower = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "UNIT");
            Assert.Equal(new ObjectBinding("FODDER", paid), empower.EmpowerActivation!.DiscardedHandCard);
            Assert.Empty(empower.TargetObjectIds); Assert.True(game.State.CardObjects["JINX"].IsExhausted); Assert.Equal(5, Might(game.State, "JINX", owner));
            Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DISCARDED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "FODDER"));
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "JINX"));
            Restore(initial, game);
            if (ownTrigger)
            {
                Assert.Contains(game.State.StackItems, item => item.SourceObjectId == "JINX"); await game.Top();
                Assert.False(game.State.CardObjects["JINX"].IsExhausted); Assert.Equal(6, Might(game.State, "JINX", owner));
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal("UNIT", Assert.Single(game.State.StackItems).SourceObjectId);
                Restore(initial, game);
            }
            else Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == "JINX");
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, Might(game.State, "UNIT", "P1"));
            Assert.Empty(game.State.StackItems);
            if (ownTrigger)
            {
                await game.Act("P1", new EndTurnCommand()); Assert.Equal(5, Might(game.State, "JINX", owner));
                Assert.Empty(game.State.CardObjects["JINX"].UntilEndOfTurnPowerModifiers);
            }
            else Assert.True(game.State.CardObjects["JINX"].IsExhausted);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("required-discard-jinx-" + ownTrigger, initial, game); }
    }

    [Theory]
    [InlineData("JINX")] [InlineData("UNIT")]
    public async Task ActualReturnDuringTheResponseWindowPreservesThePaidDiscardAndIndependentTrigger(string returned)
    {
        var initial = Position("P1"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·007", []));
            await game.Act("P1", Activate()); var generation = game.State.CardObjects[returned].ObjectGeneration;
            Assert.True(game.State.CardObjects["JINX"].IsExhausted); Assert.Equal(5, Might(game.State, "JINX", "P1"));
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top();
            Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
            Assert.Equal(generation + 1, game.State.CardObjects[returned].ObjectGeneration);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(17, game.State.RunePools["P1"].Mana);
            Assert.Contains(game.State.StackItems, item => item.SourceObjectId == "JINX"); Restore(initial, game);
            await game.Top();
            Assert.Equal(returned == "JINX" ? 5 : 6, Might(game.State, "JINX", "P1"));
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Top(); Assert.Empty(game.State.StackItems);
            Assert.Equal(returned != "UNIT", game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(17, game.State.RunePools["P1"].Mana);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("required-discard-response-return-" + returned, initial, game); }
    }

    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);
    private static MatchState Position(string owner)
    {
        var state = VenUnitEntryBasicTests.Position("VEN·007");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        foreach (var (id, no, player, zone) in new[] { ("JINX", "OGN·202/298", owner, "BASE"),
            ("FODDER", "VEN·049", "P1", "HAND"), ("RETURN", "OGN·104/298", "P1", "HAND") })
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player); locations[id] = new(player, zone);
            zones[player] = zone == "HAND" ? zones[player] with { Hand = [..zones[player].Hand, id] } : zones[player] with { Base = [..zones[player].Base, id] };
        }
        cards["JINX"] = cards["JINX"] with { IsExhausted = true };
        return state with { RoomId = "VEN007-REQUIRED-DISCARD-JINX-EXPLICIT-MIDGAME", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
    private static int Might(MatchState state, string id, string owner) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static void Restore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN007_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame VEN007 actual required payment plus ordinary Jinx trigger; not canonical opening or whole-card certification",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
