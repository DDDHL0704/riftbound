using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DiscardCostTriggerTimingConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Current CN DOC-01: 383.3, 423.1.b and 423.3. The discard is paid now;
    // its ordinary non-resource trigger resolves through the chain.
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ActualPlayCostDiscardQueuesTheOrdinaryJinxTriggerBeforeEitherEffectResolves(bool ownTrigger)
    {
        var owner = ownTrigger ? "P1" : "P2"; var initial = Position(owner); var game = new Driver(initial);
        try
        {
            Assert.True(game.State.CardObjects["JINX"].IsExhausted); Assert.Equal(5, Might(game.State, owner));
            await game.Act("P1", new PlayCardCommand("SPELL", "VEN·008", ["ENEMY"], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DISCARDED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "FODDER"));
            Assert.True(game.State.CardObjects["JINX"].IsExhausted);
            Assert.Equal(5, Might(game.State, owner));
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "JINX"));
            if (ownTrigger)
            {
                Assert.Contains(game.State.StackItems, item => item.SourceObjectId == "JINX");
                await game.Top();
                Assert.False(game.State.CardObjects["JINX"].IsExhausted); Assert.Equal(6, Might(game.State, owner));
                Assert.Equal(1, Assert.Single(game.State.CardObjects["JINX"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
                Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
                Assert.Contains(game.State.StackItems, item => item.SourceObjectId == "SPELL");
            }
            else Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == "JINX");
            await game.Top(); Assert.Empty(game.State.StackItems);
            if (ownTrigger)
            {
                await game.Act("P1", new EndTurnCommand()); Assert.Equal(5, Might(game.State, owner));
                Assert.Empty(game.State.CardObjects["JINX"].UntilEndOfTurnPowerModifiers);
            }
            else Assert.True(game.State.CardObjects["JINX"].IsExhausted);
            await game.Replay();
        }
        finally { await Save("discard-trigger-" + ownTrigger, initial, game); }
    }

    private static MatchState Position(string owner)
    {
        var s = VenUnitEntryBasicTests.Position("OGN·096/298"); var cards = s.CardObjects.ToDictionary(); var locations = s.ObjectLocations.ToDictionary(); var zones = s.PlayerZones.ToDictionary();
        void Put(string id, string no, string player, string zone)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player); locations[id] = new(player, zone);
            zones[player] = zone == "HAND" ? zones[player] with { Hand = [..zones[player].Hand, id] } : zones[player] with { Base = [..zones[player].Base, id] };
        }
        Put("SPELL", "VEN·008", "P1", "HAND"); Put("FODDER", "VEN·049", "P1", "HAND"); Put("JINX", "OGN·202/298", owner, "BASE");
        cards["JINX"] = cards["JINX"] with { IsExhausted = true };
        locations["ENEMY"] = new("P2", "BATTLEFIELD", "OTHER");
        zones["P2"] = zones["P2"] with { Base = zones["P2"].Base.Where(id => id != "ENEMY").ToArray(), Battlefields = [..zones["P2"].Battlefields, "ENEMY"] };
        return s with { RoomId = "DISCARD-COST-TRIGGER-EXPLICIT-MIDGAME", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
    private static int Might(MatchState state, string owner) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty("JINX").GetProperty("effectivePower").GetInt32();
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DISCARD_TRIGGER_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame actual play-cost discard; retained original failure is not a whole-card certificate",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), runtime, ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
