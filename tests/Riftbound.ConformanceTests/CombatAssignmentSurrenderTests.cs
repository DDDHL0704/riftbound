using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CombatAssignmentSurrenderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, "P1")]
    [InlineData(false, "P2")]
    [InlineData(true, "P1")]
    [InlineData(true, "P2")]
    public async Task EitherPlayerCanConcedeBeforeUncommittedCombatDamageAfterColdRecovery(bool attackerSubmitted, string player)
    {
        // CN650/651.1 applies during both independent assignment steps.
        // Real room/deck records precede an explicit accepted DEV position;
        // this does not certify an ordinary opening-to-battle sequence.
        var run = await RawEnvelopeTestRun.Create("combat-assignment-surrender-" + attackerSubmitted + "-" + player, "P1", "P2");
        var seeded = await run.Session.SeedScenarioAsync("P1", "assignment-scene", "battle-multi-participant", null, default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        async Task<ResolutionResult> Act(string actor, GameCommand command, string? intent = null)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = run.Session.PromptFor(actor).PromptId; node["snapshotTick"] = run.State.Tick;
            var result = await run.Send(intent ?? "assignment-command-" + run.Entries.Count, JsonSerializer.SerializeToElement(node), actor);
            Assert.True(result.Accepted, result.ErrorMessage); return result;
        }
        await Act("P1", new DeclareBattleCommand("BATTLEFIELD:P1-MAIN",
            ["P1-BATTLE-MULTI-PARTICIPANT-GAREN", "P1-BATTLE-MULTI-PARTICIPANT-YI"],
            ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK", "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.Contains(CommandTypes.AssignCombatDamage, run.Session.PromptFor("P1").EnabledActions());
        if (attackerSubmitted)
        {
            var battle = run.State.BattleState;
            var assignments = CombatTestDriver.Assignments(run.Session.PromptFor("P1"));
            Assert.NotEmpty(assignments);
            var submitted = await Act("P1", new AssignCombatDamageCommand(battle.BattleId!, battle.BattlefieldObjectId!, assignments));
            Assert.Single(submitted.Events, e => e.Kind == "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED");
            Assert.DoesNotContain(submitted.Events, e => e.Kind == "DAMAGE_APPLIED");
        }
        var assigningPlayer = attackerSubmitted ? "P2" : "P1";
        Assert.Contains(CommandTypes.AssignCombatDamage, run.Session.PromptFor(assigningPlayer).EnabledActions());
        var nextAllocation = new AssignCombatDamageCommand(run.State.BattleState.BattleId!, run.State.BattleState.BattlefieldObjectId!,
            CombatTestDriver.Assignments(run.Session.PromptFor(assigningPlayer)));
        var beforeRecovery = await run.Recovery("assignment-before-surrender");
        Assert.Empty(beforeRecovery.Errors); Assert.Empty(beforeRecovery.AuditErrors);
        Assert.True(beforeRecovery.Replay.IsMatch); Assert.Null(beforeRecovery.RegistryFailure); Assert.Equal(1, beforeRecovery.StoreLoads);
        Assert.Contains(CommandTypes.Surrender, run.Session.PromptFor(player).EnabledActions());
        var physical = Physical(run.State); var prefix = JsonSerializer.Serialize(run.Entries, Json); var prefixCount = run.Entries.Count;
        var ended = await Act(player, new SurrenderCommand(), "assignment-concede");
        Assert.Equal(MatchStatuses.Finished, ended.State.Status); Assert.Equal(player == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
        var won = Assert.Single(ended.Events); Assert.Equal("MATCH_WON", won.Kind);
        Assert.Equal("SURRENDER", won.Payload["reason"]); Assert.Equal(player, won.Payload["surrenderedPlayerId"]);
        Assert.Equal(physical, Physical(ended.State));
        Assert.Equal(prefix, JsonSerializer.Serialize(run.Entries.Take(prefixCount), Json));
        Assert.All(ended.Prompts.Values, prompt => Assert.Equal(new[] { "WAIT" }, prompt.EnabledActions()));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(ended);
        var afterRecovery = await run.Recovery("assignment-after-surrender");
        Assert.Empty(afterRecovery.Errors); Assert.Empty(afterRecovery.AuditErrors);
        Assert.True(afterRecovery.Replay.IsMatch); Assert.Null(afterRecovery.RegistryFailure); Assert.Equal(1, afterRecovery.StoreLoads);
        var count = run.Entries.Count; var stateHash = MatchStateHasher.Hash(run.State); var history = JsonSerializer.Serialize(run.Entries, Json);
        var duplicate = await run.Send("assignment-concede", run.Entries[^1].RawCommand!.Value, player);
        Assert.True(duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, run.Entries.Count);
        Assert.Equal(stateHash, MatchStateHasher.Hash(duplicate.State));
        var late = await Assert.ThrowsAsync<MatchSessionException>(() => Act(assigningPlayer, nextAllocation, "assignment-too-late"));
        Assert.Equal(ErrorCodes.MatchFinished, late.Code);
        Assert.Equal(history, JsonSerializer.Serialize(run.Entries, Json));
        Assert.Equal(stateHash, MatchStateHasher.Hash(run.State));
        run.Save("assignment-terminal-proof", new { attackerSubmitted, player, beforeRecovery, afterRecovery,
            prefixCount, finalCommandCount = count, physicalUnchanged = physical == Physical(run.State), stateHash });
    }

    private static string Physical(MatchState state) => MatchStateHasher.HashValue(new {
        state.CardObjects, state.ObjectLocations, state.PlayerZones, state.PlayerScores, state.RunePools,
        state.UntilEndOfTurnEffects, state.DestroyedUnitOwnerIdsThisTurn });
}
