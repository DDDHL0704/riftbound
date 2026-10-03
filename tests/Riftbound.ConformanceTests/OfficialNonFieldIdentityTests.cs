using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 108.2.d, 124.1 and 141.1.b: leaving play ends temporary state,
// not the physical card's identity or its public graveyard information.
public sealed class OfficialNonFieldIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestroyedCardKeepsPublicIdentityAndReturnsToItsOwner(bool stolen)
    {
        var state = OfficialPooledCombatDamageTests.Position();
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["A1"] = cards["A1"] with { OwnerId = stolen ? "P2" : "P1" };
        state = state with { CardObjects = cards };
        var result = await CombatTestDriver.FinishAsync(new(true, null, state, [],
            ResolutionResult.BuildSnapshots(state), ResolutionResult.BuildPrompts(state)));
        var owner = stolen ? "P2" : "P1";
        Assert.Contains("A1", result.State.PlayerZones[owner].Graveyard);
        AssertPrintedIdentity(result.State, "A1", owner);
        foreach (var viewer in new[] { "P1", "P2" })
            Assert.Equal("SFD·125/221", Snapshot(result, viewer).GetProperty("players")
                .GetProperty(owner).GetProperty("objects").GetProperty("A1").GetProperty("cardNo").GetString());
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
        AssertPrintedIdentity(restored, "A1", owner);
    }

    [Theory]
    [InlineData("OGN·187/298", null, "HAND", false)]
    [InlineData("OGN·187/298", null, "HAND", true)]
    [InlineData("UNL-204/219", "OWNER_MAIN_DECK_TOP", "MAIN_DECK", false)]
    [InlineData("UNL-204/219", "OWNER_MAIN_DECK_BOTTOM", "MAIN_DECK", true)]
    public async Task ReturnEffectsPreserveIdentityAndClearTemporaryState(string spell, string? mode, string destination, bool stolen)
    {
        var owner = stolen ? "P1" : "P2";
        var state = new MatchState("ZONE-IDENTITY", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["TARGET"] }
            }, cardObjects: new Dictionary<string, CardObjectState>
            {
                ["SPELL"] = new("SPELL", cardNo: spell, tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"),
                ["TARGET"] = new("TARGET", cardNo: "SFD·125/221", power: 9, damage: 2, untilEndOfTurnPowerModifier: 5,
                    isExhausted: true, untilEndOfTurnEffects: ["STUNNED"], tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: owner, controllerId: "P2")
            });
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("return-identity", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", spell, ["TARGET"], Mode: mode ?? string.Empty), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var step = 0; result.State.StackItems.Count > 0 && step < 6; step++)
        {
            var player = result.State.PriorityPlayerId ?? throw new InvalidOperationException("Missing priority");
            result = await engine.ResolveAsync(result.State, new($"pass-{step}", player, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Empty(result.State.StackItems);
        var zone = destination == "HAND" ? result.State.PlayerZones[owner].Hand : result.State.PlayerZones[owner].MainDeck;
        Assert.Contains("TARGET", zone);
        AssertPrintedIdentity(result.State, "TARGET", owner);
        var enemy = owner == "P1" ? "P2" : "P1";
        Assert.False(Snapshot(result, enemy).GetProperty("players").GetProperty(owner).GetProperty("objects").TryGetProperty("TARGET", out _));
        if (destination == "HAND")
            Assert.Equal("SFD·125/221", Snapshot(result, owner).GetProperty("players").GetProperty(owner)
                .GetProperty("objects").GetProperty("TARGET").GetProperty("cardNo").GetString());
    }

    [Fact]
    public async Task AUnitDestroyedInCombatCanActuallyBeRevivedAndPlayedAgain()
    {
        var state = OfficialPooledCombatDamageTests.Position();
        var afterBattle = await CombatTestDriver.FinishAsync(new(true, null, state, [],
            ResolutionResult.BuildSnapshots(state), ResolutionResult.BuildPrompts(state)));
        var cards = afterBattle.State.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["REVIVE"] = new("REVIVE", cardNo: "OGN·170/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        var zones = afterBattle.State.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P1"] = zones["P1"] with { Hand = ["REVIVE"] };
        var prepared = afterBattle.State with { CardObjects = cards, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = RunePool.Empty } };
        var engine = new CoreRuleEngine();
        var revived = await engine.ResolveAsync(prepared, new("revive-after-death", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("REVIVE", "OGN·170/298", ["A1"]), default);
        Assert.True(revived.Accepted, revived.ErrorMessage);
        revived = await ResolveChain(engine, revived);
        Assert.Contains("A1", revived.State.PlayerZones["P1"].Hand);
        AssertPrintedIdentity(revived.State, "A1", "P1");
        var replayed = await engine.ResolveAsync(revived.State, new("replay-revived-unit", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("A1", "SFD·125/221", []), default);
        Assert.True(replayed.Accepted, replayed.ErrorMessage);
        replayed = await ResolveChain(engine, replayed);
        Assert.Contains("A1", replayed.State.PlayerZones["P1"].Base);
        Assert.Equal("SFD·125/221", replayed.State.CardObjects["A1"].CardNo);
        Assert.Equal(4, replayed.State.CardObjects["A1"].Power);
        Assert.True(replayed.State.CardObjects["A1"].IsExhausted);
    }

    private static async Task<ResolutionResult> ResolveChain(CoreRuleEngine engine, ResolutionResult current)
    {
        for (var step = 0; current.State.StackItems.Count > 0 && step < 6; step++)
        {
            current = await engine.ResolveAsync(current.State,
                new($"identity-chain-{current.State.Tick}", current.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(current.Accepted, current.ErrorMessage);
        }
        Assert.Empty(current.State.StackItems);
        return current;
    }

    private static JsonElement Snapshot(ResolutionResult result, string player)
        => JsonSerializer.SerializeToElement(result.Snapshots[player], new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static void AssertPrintedIdentity(MatchState state, string id, string owner)
    {
        Assert.True(state.CardObjects.TryGetValue(id, out var card), "A real card must keep its identity outside play.");
        Assert.Equal("SFD·125/221", card.CardNo);
        Assert.Equal(owner, card.OwnerId);
        Assert.Equal(owner, card.ControllerId);
        Assert.Equal(4, card.Power);
        Assert.Equal(4, card.ManaCost);
        Assert.Equal(0, card.Damage);
        Assert.Equal(0, card.UntilEndOfTurnPowerModifier);
        Assert.Empty(card.UntilEndOfTurnEffects);
        Assert.Empty(card.UntilEndOfTurnPowerModifiers);
        Assert.False(card.IsAttacking || card.IsDefending || card.IsExhausted || card.IsFaceDown);
        Assert.Null(card.AttachedToObjectId);
        Assert.DoesNotContain("壁垒", card.Tags);
        Assert.Contains(CardObjectTags.UnitCard, card.Tags);
        Assert.Contains("仙灵", card.Tags);
    }
}
