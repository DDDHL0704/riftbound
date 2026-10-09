using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialEchoGrantTests
{
    [Theory]
    [InlineData(1, false, true)]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    public async Task GatePaysAndExhaustsBeforeItGrantsAnything(int power, bool exhausted, bool accepted)
    {
        var state = State() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, power), ["P2"] = RunePool.Empty } };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["GATE"] = state.CardObjects["GATE"] with { IsExhausted = exhausted } } };
        if (accepted)
        {
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            Assert.Contains(P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, System.Text.Json.JsonSerializer.Serialize(candidate));
            Assert.Contains("GATE", System.Text.Json.JsonSerializer.Serialize(candidate));
        }
        var result = await new CoreRuleEngine().ResolveAsync(state, new("gate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []), default);
        Assert.Equal(accepted, result.Accepted);
        if (!accepted) { Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State)); return; }
        Assert.True(result.State.CardObjects["GATE"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(result.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1"));
        Assert.Single(result.State.StackItems);
        // An activated ability survives its source leaving while opponents respond.
        result = result with { State = result.State with { PlayerZones = new Dictionary<string, PlayerZones>(result.State.PlayerZones)
            { ["P1"] = result.State.PlayerZones["P1"] with { Base = [], Graveyard = ["GATE"] } } } };
        for (var i = 0; i < 2; i++)
        {
            result = await new CoreRuleEngine().ResolveAsync(result.State, new("pass" + i, result.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Single(result.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1"));
        Assert.Contains("GATE", result.State.PlayerZones["P1"].Graveyard);
    }

    [Theory]
    [InlineData("F", "P1", false, true)]
    [InlineData("OTHER", "P1", false, false)]
    [InlineData("BASE", "P1", false, false)]
    [InlineData("F", "P2", false, false)]
    [InlineData("F", "P1", true, false)]
    public void SyndraGrantsOnlyFromItsOwnActualShowdown(string location, string controller, bool faceDown, bool grants)
    {
        var state = State();
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["F"] = new("F", cardNo: "OGN·280/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["OTHER"] = new("OTHER", cardNo: "OGN·275/298", ownerId: "P2", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["SYN"] = new("SYN", cardNo: "UNL-146/219", ownerId: "P1", controllerId: controller, isFaceDown: faceDown, power: 4, tags: [CardObjectTags.UnitCard]),
            ["A"] = new("A", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", power: 4, tags: [CardObjectTags.UnitCard]),
            ["B"] = new("B", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", power: 4, tags: [CardObjectTags.UnitCard])
        };
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = location == "BASE" ? ["SYN"] : [], Battlefields = ["F", "A"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER", "B"] }
        };
        if (location != "BASE") zones[controller] = zones[controller] with { Battlefields = zones[controller].Battlefields.Append("SYN").ToArray() };
        state = state with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1", CardObjects = cards, PlayerZones = zones,
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            { ["SYN"] = new(controller, location == "BASE" ? "BASE" : "BATTLEFIELD", location == "BASE" ? null : location),
                ["A"] = new("P1", "BATTLEFIELD", "F"), ["B"] = new("P2", "BATTLEFIELD", "F") } };
        Assert.Equal("F", state.SpellDuelState.BattlefieldObjectId);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-061/219", out var spell));
        var costs = EchoCostRules.Available(state, "P1", spell);
        Assert.Equal(grants, costs.Any(c => c.Id == "ECHO:SOURCE:SYN"));
        if (grants) { var cost = costs.Single(c => c.Id == "ECHO:SOURCE:SYN"); Assert.Equal(2, cost.Mana); Assert.Equal(1, cost.TypedPower["purple"]); }
    }

    private static MatchState State() => new("ECHO-GRANT", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
        status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        runePools: new Dictionary<string, RunePool> { ["P1"] = new(5, 5), ["P2"] = RunePool.Empty },
        playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Base = ["GATE"] }, ["P2"] = PlayerZones.Empty },
        cardObjects: new Dictionary<string, CardObjectState> { ["GATE"] = new("GATE", cardNo: "SFD·078/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) });
}
