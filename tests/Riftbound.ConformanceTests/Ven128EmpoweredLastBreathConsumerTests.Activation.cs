using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    [Theory]
    [InlineData("P1", "mana")] [InlineData("P2", "mana")]
    [InlineData("P1", "wrong-trait")] [InlineData("P2", "wrong-trait")]
    [InlineData("P1", "repeated")] [InlineData("P2", "repeated")]
    [InlineData("P1", "target")] [InlineData("P2", "target")]
    [InlineData("P1", "controller")] [InlineData("P2", "controller")]
    public async Task NativeActivationRejectsActualResourceStatusAndAuthorityViolationsWithoutSpendingOrRewritingHistory(string owner, string branch)
    {
        var initial = Position(owner);
        if (branch == "wrong-trait") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [owner + "-RUNE-1"] = OfficialGarenConquestBattlefieldCountTests.Printed(owner + "-RUNE-1", "UNL-R02", owner) } };
        var game = new Driver(initial); var actor = owner;
        try
        {
            var funding = Enumerable.Range(1, branch == "mana" ? 2 : 4).Select(i => owner + "-RUNE-" + i).ToArray();
            await game.Act(owner, new TapRuneCommand(funding[0], funding));
            await game.Act(owner, new PlayCardCommand("UNIT", No, [])); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (branch == "controller")
            {
                await game.Act(owner, new EndTurnCommand()); actor = owner == "P1" ? "P2" : "P1";
                await Tap(game, actor); await game.Act(actor, new RecycleRuneCommand(actor + "-RUNE-1"));
                Assert.Equal(owner, game.State.CardObjects["UNIT"].ControllerId); Assert.Equal(actor, game.State.TurnPlayerId);
            }
            else
            {
                await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-1"));
                if (branch == "repeated")
                {
                    await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-2"));
                    await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
                }
            }
            var pool = game.State.RunePools[actor];
            Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].IsEmpowered);
            if (branch == "mana") { Assert.Equal(0, pool.Mana); Assert.Equal(1, pool.PowerByTrait[RuneTrait.Yellow]); }
            else { Assert.True(pool.Mana >= 1); Assert.Equal(1, pool.TotalPower); }
            if (branch == "wrong-trait") Assert.Equal(1, pool.PowerByTrait[RuneTrait.Green]);
            else Assert.Equal(1, pool.PowerByTrait[RuneTrait.Yellow]);
            var rejected = await RejectActivation(game, actor, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []));
            Assert.Equal(pool, game.State.RunePools[actor]); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].IsEmpowered); JsonCold(initial, game);

            if (branch is "mana" or "wrong-trait")
            {
                if (branch == "mana") await game.Act(owner, new TapRuneCommand(owner + "-RUNE-3"));
                else await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-2"));
                var count = game.Commands().Length; await game.ActRetained(rejected, false); Assert.Equal(count, game.Commands().Length);
            }
            if (branch != "controller" && branch != "repeated")
            {
                var mana = game.State.RunePools[owner].Mana;
                await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(mana - 1, game.State.RunePools[owner].Mana);
                Assert.Equal(branch == "wrong-trait" ? 1 : 0, game.State.RunePools[owner].TotalPower);
                if (branch == "wrong-trait") Assert.Equal(1, game.State.RunePools[owner].PowerByTrait[RuneTrait.Green]);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); JsonCold(initial, game); await game.Top();
                Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            }
            Assert.Equal(2, game.State.CardObjects["UNIT"].Power); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Empty(Minions(game.State)); JsonCold(initial, game); await game.Replay();
        }
        finally { await Export("native-activation-" + owner + "-" + branch, initial, game); }
    }

    private static async Task<ConformanceCommand> RejectActivation(Driver game, string actor, GameCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        node["promptId"] = ResolutionResult.BuildPrompts(game.State)[actor].PromptId; node["snapshotTick"] = game.State.Tick;
        var raw = new ConformanceCommand(actor, "ven128-reject-" + game.Commands().Length, JsonSerializer.SerializeToElement(node, Json));
        var count = game.Commands().Length; await game.ActRetained(raw, false); Assert.Equal(count + 1, game.Commands().Length);
        await game.ActRetained(raw, false); Assert.Equal(count + 1, game.Commands().Length); return raw;
    }

    private static void JsonCold(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var actor in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[actor]), MatchStateHasher.HashValue(restored.SnapshotFor(actor)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[actor]), MatchStateHasher.HashValue(restored.PromptFor(actor)));
        }
    }
}
