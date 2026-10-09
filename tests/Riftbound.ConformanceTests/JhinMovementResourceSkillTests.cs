using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 429.2 and UNL-022: movement gain is automatic and retains action rights.
public sealed class JhinMovementResourceSkillTests
{
    [Theory]
    [InlineData("UNL-022/219", false, false)]
    [InlineData("UNL-022a/219", false, false)]
    [InlineData("UNL-022/219", true, false)]
    [InlineData("UNL-022/219", false, true)]
    public async Task MovementImmediatelyGrantsResourcesWithRealBattlefields(string card, bool roaming, bool returning)
    {
        var state = Position();
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["A"] = cards["A"] with { CardNo = card, Tags = new[] { CardObjectTags.UnitCard, "游走", "法盾" }.Order(StringComparer.Ordinal).ToArray() };
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        if (roaming || returning)
        {
            cards["HOME"] = cards["BF"] with { ObjectId = "HOME", OwnerId = "P1", ControllerId = "P1" };
            zones["P1"] = zones["P1"] with { Base = ["B"], Battlefields = ["HOME", "A"] };
            locations["HOME"] = new("P1", "BATTLEFIELD", "HOME");
            locations["A"] = new("P1", "BATTLEFIELD", "HOME");
        }
        state = state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var engine = new CoreRuleEngine();
        var move = new MoveUnitCommand("A", Destination: returning ? "BASE" : "BATTLEFIELD:BF", SourceObjectIds: ["A"]);
        var result = await engine.ResolveAsync(state, new("move", "P1", move.CmdType), move, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].Power);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.EffectKind == P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        if (!returning) Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Single(result.Events, e => e.Kind == "MANA_GAINED");
        Assert.Single(result.Events, e => e.Kind == "POWER_GAINED");
        var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(recovered));
        var activate = new ActivateAbilityCommand("A", P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, []);
        var duplicate = await engine.ResolveAsync(recovered, new("manual", "P1", activate.CmdType), activate, default);
        Assert.False(duplicate.Accepted);
        Assert.Equal(MatchStateHasher.Hash(recovered), MatchStateHasher.Hash(duplicate.State));
    }

    private static MatchState Position() => new("GROUP-MOVE", 0, 3, "P1",
        new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
        status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
        playerZones: new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = ["A", "B"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["BF", "D"] }
        },
        cardObjects: new Dictionary<string, CardObjectState>
        {
            ["A"] = new("A", power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["B"] = new("B", power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["D"] = new("D", power: 2, cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2"),
            ["BF"] = new("BF", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2")
        },
        objectLocations: new Dictionary<string, ObjectLocationState>
        {
            ["A"] = new("P1", "BASE"), ["B"] = new("P1", "BASE"),
            ["D"] = new("P2", "BATTLEFIELD", "BF"), ["BF"] = new("P2", "BATTLEFIELD", "BF")
        });
}
