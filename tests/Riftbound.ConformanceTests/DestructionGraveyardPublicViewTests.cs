using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DestructionGraveyardFamilyTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionGraveyardPublicViewTests
{
    [Theory]
    [InlineData("MISSING")]
    [InlineData("NULL")]
    [InlineData("ARRAY")]
    [InlineData("UNKNOWN_MEMBER")]
    [InlineData("GENERATION")]
    [InlineData("SOURCE")]
    [InlineData("ACTOR")]
    [InlineData("PRINTING")]
    [InlineData("SOURCE_FIELD")]
    [InlineData("EFFECT")]
    public async Task PrivateProjectionCannotDropForgeOrExtendCapturedDelayedInstruction(string attack)
    {
        var run = new StandbyDefendObserverTests.Driver(Position());
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"])); await run.Top();
        var state = run.State; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var rows = snapshots["P1"].Stack.ToArray();
        var row = JsonSerializer.SerializeToNode(rows[0], new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        switch (attack) {
            case "MISSING": row.Remove("damageDestruction"); break;
            case "NULL": row["damageDestruction"] = null; break;
            case "ARRAY": row["damageDestruction"] = new JsonArray(); break;
            case "UNKNOWN_MEMBER": row["damageDestruction"]!["untrustedRule"] = true; break;
            case "GENERATION": row["damageDestruction"]!["damagedUnit"]!["generation"] = 1000; break;
            case "SOURCE": row["damageDestruction"]!["source"]!["objectId"] = "UNKNOWN"; break;
            case "PRINTING": row["cardNo"] = Guillotine; break;
            case "SOURCE_FIELD": row["sourceObjectId"] = "RAY"; break;
            case "EFFECT": row["effectKind"] = "ANOTHER_SKILL"; break;
            default: row["controllerId"] = "P2"; break;
        }
        rows[0] = JsonSerializer.SerializeToElement(row);
        var sequence = run.Commands()[^1].CompletedEventSequence;
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            p == "P1" ? snapshots[p] with { Stack = rows } : snapshots[p], state.Tick, sequence, prompts[p]));
        Assert.NotEmpty(MatchRecoveryValidator.Validate(state.RoomId, sequence, run.Commands(), run.Events(), views, state, state.Tick));
    }
}
