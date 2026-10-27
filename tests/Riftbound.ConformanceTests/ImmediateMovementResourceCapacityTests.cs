using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// These are server representation boundaries, not a game rule limiting the rune pool.
public sealed class ImmediateMovementResourceCapacityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static IEnumerable<object[]> Cases()
        => from copies in new[] { 0, 1, 2 }
           from dimension in new[] { "mana", "power" }
           from fits in new[] { false, true } select new object[] { copies, dimension, fits };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RealCopiedMovementIncomeMustFitBeforeTheMoveCanCommit(int copies, string dimension, bool fits)
    {
        var initial = CopiedMovementObserverTests.Position(P4ActivatedAbilityCatalog.JhinCardNo, copies);
        var available = int.MaxValue - (fits ? 1 : 0);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P2"] = new(dimension == "mana" ? available : copies,
                dimension == "power" ? available - copies : 0,
                new Dictionary<string, int> { [RuneTrait.Green] = copies }) } };
        Assert.True(initial.RunePools["P2"].HasSupportedAmounts);
        var game = new Driver(initial); MatchState? before = null; ResolutionResult? core = null;
        string? coreFailure = null, sessionFailure = null;
        try
        {
            await CopiedMovementObserverTests.Assemble(game, copies); before = game.State;
            Assert.True(before.RunePools["P2"].HasSupportedAmounts);
            var move = new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []);
            const string intent = "actual-copied-movement-capacity";
            try { core = await new CoreRuleEngine().ResolveAsync(before, new(intent, "P2", move.CmdType), move, default); }
            catch (Exception e) { coreFailure = e.ToString(); }
            var wire = JsonSerializer.SerializeToNode(move, move.GetType(), Json)!.AsObject();
            wire["promptId"] = game.PromptFor("P2").PromptId; wire["snapshotTick"] = before.Tick;
            var retained = new ConformanceCommand("P2", intent, JsonSerializer.SerializeToElement(wire, Json));
            try { await game.ActRetained(retained, fits); }
            catch (Exception e) { sessionFailure = e.ToString(); }

            // Both actual boundaries are observed before checking either result, so the
            // original failed run retains the Session response as well as the Core output.
            Assert.Null(coreFailure); Assert.NotNull(core); Assert.Equal(fits, core.Accepted);
            Assert.True(core.State.RunePools["P2"].HasSupportedAmounts);
            if (fits)
            {
                Assert.Equal((long)before.RunePools["P2"].Mana + copies + 1, core.State.RunePools["P2"].Mana);
                Assert.Equal((long)before.RunePools["P2"].TotalPower + copies + 1, core.State.RunePools["P2"].TotalPower);
                Assert.Equal(1, core.State.CardObjects["HOST"].MovementCountThisTurn);
                Assert.Equal("BATTLEFIELD", core.State.ObjectLocations["HOST"].Zone);
                Assert.Equal(copies + 1, core.Events.Count(e => e.Kind == "MANA_GAINED"));
                Assert.Equal(copies + 1, core.Events.Count(e => e.Kind == "POWER_GAINED"));
            }
            else
            {
                Assert.Equal(ErrorCodes.UnsupportedCardBehavior, core.ErrorCode); Assert.Empty(core.Events);
                Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(core.State));
            }
            Assert.Null(sessionFailure); Assert.NotNull(game.LastRetainedResult);
            Assert.Equal(fits, game.LastRetainedResult.Accepted);
            Assert.Equal(MatchStateHasher.Hash(core.State), MatchStateHasher.Hash(game.State));
            if (!fits) Assert.Equal(ErrorCodes.UnsupportedCardBehavior, game.LastRetainedResult.ErrorCode);
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_COPIED_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, $"capacity-{copies}-{dimension}-{fits}.json"), JsonSerializer.Serialize(new {
                    scope = "Explicit turn5 supported integer boundary, real assembly and move. Capacity rejection is an implementation boundary, not an official resource limit. Both actual Core and Session results retained, including failures before driver state update. No ordinary opening or external acceptance.",
                    initial, before, core, coreFailure, sessionFailure, driverState = game.State,
                    lastRetainedResponse = game.LastRetainedResult, commands = game.Commands(), events = game.Events() }, Json));
            }
        }
    }
}
