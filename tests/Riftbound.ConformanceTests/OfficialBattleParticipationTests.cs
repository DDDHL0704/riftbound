using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 464.2.c.3: every unit controlled by either combatant participates.
public sealed class OfficialBattleParticipationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThreeOrdinaryUnitsPerSideAllParticipateEvenWhenClientOmitsUnits(bool submitSubset)
    {
        var state = Position();
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("all-participants", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand("BF", submitSubset ? ["A3"] : ["A1", "A2", "A3"],
                submitSubset ? ["D3"] : ["D1", "D2", "D3"], ["COMBAT_ASSIGNMENT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var declaration = Assert.Single(result.Events, e => e.Kind == "BATTLE_DECLARED");
        Assert.Equal(3, Assert.IsAssignableFrom<IReadOnlyList<string>>(declaration.Payload["attackerObjectIds"]).Count);
        Assert.Equal(3, Assert.IsAssignableFrom<IReadOnlyList<string>>(declaration.Payload["defenderObjectIds"]).Count);
        Assert.Equal(3, result.State.PlayerZones["P1"].Graveyard.Count);
        Assert.Equal(3, result.State.PlayerZones["P2"].Graveyard.Count);
        Assert.False(result.State.BattleState.IsActive);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        var originalJson = JsonSerializer.SerializeToElement(result.State);
        var restoredJson = JsonSerializer.SerializeToElement(restored);
        foreach (var property in originalJson.EnumerateObject())
            Assert.True(MatchStateHasher.HashValue(property.Value) == MatchStateHasher.HashValue(restoredJson.GetProperty(property.Name)),
                $"Recovery changed {property.Name}: {property.Value} => {restoredJson.GetProperty(property.Name)}");
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
    }

    [Fact]
    public void PromptRequiresAllParticipantsWithoutTwoUnitLimitOrKeywordGate()
    {
        var state = Position();
        var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.DeclareBattle);
        Assert.True(candidate.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]);
        Assert.All(requirements, requirement =>
        {
            Assert.Equal(3, requirement["minAttackerCount"]);
            Assert.Equal(3, requirement["maxAttackerCount"]);
            Assert.Equal(3, requirement["minDefenderCount"]);
            Assert.Equal(3, requirement["maxDefenderCount"]);
        });
    }

    private static MatchState Position()
    {
        var cards = new Dictionary<string, CardObjectState>
        {
            ["BF"] = new("BF", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2")
        };
        var locations = new Dictionary<string, ObjectLocationState> { ["BF"] = new("P2", "BATTLEFIELD", "BF") };
        foreach (var (prefix, player) in new[] { ("A", "P1"), ("D", "P2") })
            for (var i = 1; i <= 3; i++)
            {
                var id = prefix + i;
                cards[id] = new(id, power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard],
                    ownerId: player, controllerId: player, isExhausted: true);
                locations[id] = new(player, "BATTLEFIELD", "BF");
            }
        return new("ALL-COMBATANTS", 9, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["A1", "A2", "A3"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["BF", "D1", "D2", "D3"] }
            }, cardObjects: cards, objectLocations: locations,
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF")]);
    }
}
