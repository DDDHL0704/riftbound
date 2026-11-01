using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class RadGearTestPosition
{
    internal static MatchState Create(string field = "OGN·296/298")
    {
        var state = new MatchState("RAD-GEAR-FIELDS", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty, ["P2"] = PlayerZones.Empty },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) }, seed: 564);
        state = Put(state, "BF", field, "P1", "BATTLEFIELD", "BF");
        state = Put(state, "OTHER", "OGN·296/298", "P2", "BATTLEFIELD", "OTHER");
        state = Put(state, "ALLY", "OGN·219/298", "P1", "BASE");
        state = Put(state, "ENEMY", "OGN·219/298", "P2", "BASE");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 6; i++) state = Put(state, player + "-D" + i, "OGN·219/298", player, "MAIN_DECK");
        return state;
    }

    internal static MatchState Put(MatchState state, string id, string no, string owner, string zone, string? field = null)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) with { ControllerId = owner } } };
        if (zone == "RUNE_DECK")
            return state with {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    [owner] = state.PlayerZones[owner] with { RuneDeck = [..state.PlayerZones[owner].RuneDeck, id] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) } };
        return RadUnitEffectTestFixture.Place(state, id, owner, zone, field);
    }

    internal static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rad-gear-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }
}
