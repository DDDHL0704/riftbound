using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 810, 144 and the printed "here" clauses: location, not battlefield ownership.
public sealed class BattlefieldLocalPermissionTests
{
    [Theory]
    [InlineData("P1", "HILL", true)]
    [InlineData("P2", "HILL", true)]
    [InlineData("P1", "OTHER", false)]
    [InlineData("P2", "OTHER", false)]
    public async Task WindHillGrantsBothPlayersOnlyUnitsActuallyHere(string player, string origin, bool allowed)
    {
        var state = Position("OGN·297/298", player, origin);
        Assert.Equal(allowed, MoveDestinations(state, player).Contains("BATTLEFIELD:" + (origin == "HILL" ? "OTHER" : "HILL")));
        var result = await Move(state, player, "BATTLEFIELD:" + (origin == "HILL" ? "OTHER" : "HILL"));
        Assert.True(allowed == result.Accepted, result.ErrorMessage ?? $"Expected accepted={allowed}");
        if (!allowed) Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        else
        {
            Assert.True(result.State.CardObjects["UNIT"].IsExhausted);
            Assert.DoesNotContain("游走", result.State.CardObjects["UNIT"].Tags);
            var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(recovered));
            Assert.DoesNotContain(recovered.ContinuousEffects, e => e.TargetObjectId == "UNIT" && e.SourceObjectId == "HILL");
        }
    }

    [Theory]
    [InlineData("P1", "HILL", false)]
    [InlineData("P2", "HILL", false)]
    [InlineData("P1", "OTHER", true)]
    [InlineData("P2", "OTHER", true)]
    public async Task VilemawRestrictsBothPlayersOnlyUnitsActuallyHere(string player, string origin, bool allowed)
    {
        var state = Position("OGN·295/298", player, origin);
        Assert.Equal(allowed, MoveDestinations(state, player).Contains("BASE"));
        var result = await Move(state, player, "BASE");
        Assert.True(allowed == result.Accepted, result.ErrorMessage ?? $"Expected accepted={allowed}");
        if (!allowed) Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("P1", "HILL", true)]
    [InlineData("P2", "HILL", true)]
    [InlineData("P1", "OTHER", false)]
    [InlineData("P2", "OTHER", false)]
    public async Task MutationGardenGrantsAbilityOnlyAtItsLocation(string player, string origin, bool allowed)
    {
        var state = Position("UNL-213/219", player, origin);
        var prompt = Prompt(state, player);
        var abilities = prompt.GetProperty("candidates").EnumerateArray().Where(c => c.GetProperty("action").GetString() == "ACTIVATE_ABILITY")
            .SelectMany(c => c.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray());
        Assert.Equal(allowed, abilities.Any(a => a.GetProperty("sourceObjectId").GetString() == "UNIT" && a.GetProperty("abilityId").GetString() == "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE"));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("garden", player, CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("UNIT", "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE", []), default);
        Assert.True(allowed == result.Accepted, result.ErrorMessage ?? $"Expected accepted={allowed}");
        if (allowed) Assert.True(result.State.CardObjects["UNIT"].IsExhausted);
        else Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("P1", "HILL", false)]
    [InlineData("P2", "HILL", false)]
    [InlineData("P1", "OTHER", true)]
    [InlineData("P2", "OTHER", true)]
    public async Task FallingRocksBlocksOnlyItsOwnDestinationRegardlessOfOwner(string player, string destination, bool allowed)
    {
        var state = Position("SFD·216/221", player, "OTHER");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["HILL"] = cards["HILL"] with { ControllerId = player };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = player };
        cards["SPELL"] = new("SPELL", cardNo: "OGN·012/298", ownerId: player, controllerId: player);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones[player] = zones[player] with { Hand = ["SPELL"] };
        state = PrintedCostFixture.Add(state with { PlayerZones = zones, CardObjects = cards }, player, "OGN·012/298");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("ambush", player, CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "OGN·012/298", [], Destination: "BATTLEFIELD:" + destination), default);
        Assert.True(allowed == result.Accepted, result.ErrorMessage ?? $"Expected accepted={allowed}");
        if (!allowed) Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("P1", "HILL", 4)]
    [InlineData("P2", "HILL", 4)]
    [InlineData("P1", "OTHER", 3)]
    [InlineData("P2", "OTHER", 3)]
    public async Task VoidGateDamageBonusUsesTargetLocationNotOwner(string targetController, string origin, int damage)
    {
        var caster = targetController == "P1" ? "P2" : "P1";
        var state = Position("OGN·296/298", targetController, origin) with { ActivePlayerId = caster, TurnPlayerId = caster };
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["SPELL"] = new("SPELL", cardNo: "UNL-007/219", ownerId: caster, controllerId: caster);
        cards["UNIT"] = cards["UNIT"] with { Power = 8 };
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones[caster] = zones[caster] with { Hand = ["SPELL"] };
        state = PrintedCostFixture.Add(state with { PlayerZones = zones, CardObjects = cards }, caster, "UNL-007/219");
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("damage", caster, CommandTypes.PlayCard), new PlayCardCommand("SPELL", "UNL-007/219", ["UNIT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var i = 0; i < 2; i++)
        {
            result = await engine.ResolveAsync(result.State, new("pass" + i, result.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Equal(damage, result.State.CardObjects["UNIT"].Damage);
    }

    [Fact]
    public async Task RoamingUnitKeepsEquipmentAtTheSameDestination()
    {
        var state = Position("OGN·297/298", "P2", "HILL");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["GEAR"] = new("GEAR", cardNo: "SFD·022/221", tags: [CardObjectTags.EquipmentCard], ownerId: "P2", controllerId: "P2", attachedToObjectId: "UNIT");
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P2"] = zones["P2"] with { Battlefields = zones["P2"].Battlefields.Append("GEAR").ToArray() };
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        locations["GEAR"] = new("P2", "BATTLEFIELD", "HILL");
        var result = await Move(state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations }, "P2", "BATTLEFIELD:OTHER");
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("OTHER", result.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal("UNIT", result.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ForgeDiscountFollowsConquerorNotPhysicalOwner(string player)
    {
        var state = Position("SFD·213/221", player, "HILL");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["GEAR"] = new("GEAR", cardNo: "SFD·022/221", ownerId: player, controllerId: player);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones[player] = zones[player] with { Hand = ["GEAR"] };
        state = PrintedCostFixture.Add(state with { CardObjects = cards, PlayerZones = zones }, player, "SFD·022/221");
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value);
        pools[player] = new(1, pools[player].Power, pools[player].PowerByTrait);
        state = state with { RunePools = pools };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forge", player, CommandTypes.PlayCard),
            new PlayCardCommand("GEAR", "SFD·022/221", ["UNIT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools[player].Mana);
        Assert.Contains(result.Events, e => e.Kind == "COST_PAID" && Equals(e.Payload["battlefieldEquipmentCostReductionMana"], 1));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task MaraiEchoDiscountFollowsConquerorNotPhysicalOwner(string player)
    {
        var state = Position("SFD·211/221", player, "HILL");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["SPELL"] = new("SPELL", cardNo: "UNL-061/219", ownerId: player, controllerId: player);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones[player] = zones[player] with { Hand = ["SPELL"] };
        state = PrintedCostFixture.Add(state with { CardObjects = cards, PlayerZones = zones }, player, "UNL-061/219");
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value);
        pools[player] = new(3, pools[player].Power, pools[player].PowerByTrait);
        state = state with { RunePools = pools };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("echo", player, CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "UNL-061/219", [], OptionalCosts: ["ECHO"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools[player].Mana);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    [InlineData(null)]
    public void HighroadWinningThresholdIsGlobalEvenWhenUncontrolled(string? controller)
    {
        var state = Position("OGN·276/298", "P1", "OTHER");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["HILL"] = cards["HILL"] with { ControllerId = controller };
        foreach (var snapshot in ResolutionResult.BuildSnapshots(state with { CardObjects = cards }).Values)
            Assert.Equal(9, snapshot.Timing["winningScore"]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task HiddenOrRemovedBattlefieldDoesNotGrantRoam(bool hidden, bool removed)
    {
        var state = Position("OGN·297/298", "P1", "HILL");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["HILL"] = cards["HILL"] with { IsFaceDown = hidden };
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        if (removed) zones["P1"] = zones["P1"] with { Battlefields = ["UNIT"] };
        state = state with { CardObjects = cards, PlayerZones = zones };
        Assert.DoesNotContain("BATTLEFIELD:OTHER", MoveDestinations(state, "P1"));
        var result = await Move(state, "P1", "BATTLEFIELD:OTHER");
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    public static MatchState Position(string battlefieldCard, string player, string origin) => new("LOCAL-BATTLEFIELD", 0, 3, player,
        new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        runePools: new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = new(10, 10) },
        playerZones: new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Battlefields = player == "P1" ? ["HILL", "UNIT"] : ["HILL"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = player == "P2" ? ["OTHER", "UNIT"] : ["OTHER"] }
        },
        cardObjects: new Dictionary<string, CardObjectState>
        {
            ["HILL"] = new("HILL", cardNo: battlefieldCard, tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: origin == "HILL" ? player : "P1"),
            ["OTHER"] = new("OTHER", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: origin == "OTHER" ? player : "P2"),
            ["UNIT"] = new("UNIT", cardNo: "SFD·069/221", power: 2, tags: [CardObjectTags.UnitCard], ownerId: player, controllerId: player)
        },
        objectLocations: new Dictionary<string, ObjectLocationState>
        {
            ["HILL"] = new("P1", "BATTLEFIELD", "HILL"), ["OTHER"] = new("P2", "BATTLEFIELD", "OTHER"),
            ["UNIT"] = new(player, "BATTLEFIELD", origin)
        });

    private static JsonElement Prompt(MatchState state, string player) => JsonSerializer.SerializeToElement(new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance).PromptFor(player), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static string[] MoveDestinations(MatchState state, string player) => Prompt(state, player).GetProperty("candidates").EnumerateArray()
        .Where(c => c.GetProperty("action").GetString() == "MOVE_UNIT")
        .SelectMany(c => c.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray())
        .Where(r => r.GetProperty("sourceObjectId").GetString() == "UNIT")
        .SelectMany(r => r.GetProperty("destinationChoices").EnumerateArray()).Select(d => d.GetProperty("id").GetString()!).ToArray();
    private static ValueTask<ResolutionResult> Move(MatchState state, string player, string destination) => new CoreRuleEngine().ResolveAsync(state,
        new("move", player, CommandTypes.MoveUnit), new MoveUnitCommand("UNIT", Destination: destination, SourceObjectIds: ["UNIT"]), default);
}
