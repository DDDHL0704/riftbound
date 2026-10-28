using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldConquestMillTimingTests
{
    [Theory]
    [InlineData("P1", 0)] [InlineData("P1", 1)] [InlineData("P1", 2)] [InlineData("P1", 3)]
    [InlineData("P2", 0)] [InlineData("P2", 1)] [InlineData("P2", 2)] [InlineData("P2", 3)]
    public async Task ConquestQueuesTheMillBeforeEitherPlayerCanRespond(string player, int available)
    {
        var initial = Position(player, available);
        AssertCanonicalComponent(initial);
        var result = await Conquer(initial, player);
        WriteDiagnostic($"pre-response-{player}-{available}", initial, result);
        Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.DoesNotContain(result.Events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED");
        Assert.Equal(initial.PlayerZones[player].MainDeck, result.State.PlayerZones[player].MainDeck);
        Assert.Equal(initial.PlayerZones[player].Graveyard, result.State.PlayerZones[player].Graveyard);
        Assert.True(result.State.StackItems.Any(i => i.EffectKind == TriggerKinds.BattlefieldConquerMill && i.SourceObjectId == "BF")
            || result.State.TriggerQueue.Any(t => t.EffectKind == TriggerKinds.BattlefieldConquerMill && t.SourceObjectId == "BF"));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task BattlefieldWithoutTheAbilityDoesNotQueueOrMill(string player)
    {
        var initial = Position(player, 3);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { CardNo = "OGN·296/298" } };
        initial = initial with { CardObjects = cards };
        var result = await Conquer(initial, player);
        Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.DoesNotContain(result.Events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED");
        Assert.DoesNotContain(result.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.EffectKind == TriggerKinds.BattlefieldConquerMill);
        Assert.Equal(initial.PlayerZones[player].MainDeck, result.State.PlayerZones[player].MainDeck);
    }

    private static MatchState Position(string player, int available)
    {
        var state = ConquestLifecycleRegressionTests.State("SFD·212/221", 3);
        var zones = state.PlayerZones.ToDictionary();
        var deck = new[] { "DRAW1", "DRAW2", "DRAW3" };
        zones["P1"] = zones["P1"] with { MainDeck = deck.Take(available).ToArray(), Graveyard = deck.Skip(available).ToArray() };
        var cards = state.CardObjects.ToDictionary();
        if (player == "P2")
        {
            static string? Swap(string? id) => id == "P1" ? "P2" : id == "P2" ? "P1" : id;
            cards = cards.ToDictionary(entry => entry.Key, entry => entry.Value with {
                OwnerId = Swap(entry.Value.OwnerId), ControllerId = Swap(entry.Value.ControllerId) });
            zones = new Dictionary<string, PlayerZones> { ["P1"] = zones["P2"], ["P2"] = zones["P1"] };
        }
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        foreach (var (owner, area) in zones)
        {
            foreach (var id in area.Base) locations[id] = new(owner, "BASE");
            foreach (var id in area.Hand) locations[id] = new(owner, "HAND");
            foreach (var id in area.MainDeck) locations[id] = new(owner, "MAIN_DECK");
            foreach (var id in area.Graveyard) locations[id] = new(owner, "GRAVEYARD");
            foreach (var id in area.Battlefields) locations[id] = new(owner, "BATTLEFIELD", id);
        }
        Assert.Equal(cards.Keys.Order(StringComparer.Ordinal), locations.Keys.Order(StringComparer.Ordinal));
        return state with { RoomId = $"SFD212-{player}-{available}", ActivePlayerId = player, TurnPlayerId = player,
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
    }

    private static void AssertCanonicalComponent(MatchState state)
    {
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(restored));
    }

    private static async Task<ResolutionResult> Conquer(MatchState state, string player)
    {
        var other = player == "P1" ? "P2" : "P1";
        var moved = await Act(state, player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, player, new PassFocusCommand());
        return await Act(first.State, other, new PassFocusCommand());
    }

    private static async Task<ResolutionResult> Act(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent($"sfd212-{player}-{state.Tick}", player, command.CmdType), command, CancellationToken.None);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static void WriteDiagnostic(string name, MatchState initial, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_SFD212_DIAGNOSTICS") is not { } folder) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(new {
            initial, result.Accepted, result.ErrorMessage, final = result.State, result.Events, result.Prompts
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
