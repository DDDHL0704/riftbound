using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven078EmpoweredRuneLastBreathConsumerTests
{
    [Theory]
    [InlineData("P1", "mana")] [InlineData("P2", "mana")]
    [InlineData("P1", "power")] [InlineData("P2", "power")]
    [InlineData("P1", "repeated")] [InlineData("P2", "repeated")]
    [InlineData("P1", "target")] [InlineData("P2", "target")]
    [InlineData("P1", "controller")] [InlineData("P2", "controller")]
    public async Task ActualGenericActivationRejectsInsufficientResourcesRepeatedStatusExtraTargetsAndForeignAuthority(string owner, string branch)
    {
        var initial = Position(owner); var game = new Driver(initial); var actor = owner;
        try
        {
            for (var i = 1; i <= (branch == "mana" ? 4 : 6); i++) await game.Act(owner, new TapRuneCommand(owner + "-RUNE-" + i));
            await game.Act(owner, new PlayCardCommand("UNIT", No, [])); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (branch == "controller")
            {
                await game.Act(owner, new EndTurnCommand()); actor = Other(owner); await Tap(game, actor);
                await game.Act(actor, new RecycleRuneCommand(actor + "-RUNE-1")); await game.Act(actor, new RecycleRuneCommand(actor + "-RUNE-2"));
            }
            else
            {
                await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-1"));
                if (branch != "power") await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-2"));
                if (branch == "repeated")
                {
                    await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
                    await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-3")); await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-4"));
                }
            }
            var pool = game.State.RunePools[actor]; Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(branch == "power" ? 1 : 2, pool.TotalPower);
            if (branch == "mana") Assert.Equal(0, pool.Mana); else Assert.True(pool.Mana >= 1);
            var rejected = await Reject(game, actor, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []));
            Assert.Equal(pool, game.State.RunePools[actor]); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(branch == "repeated" ? 6 : 4, Might(game.State, owner)); Cold(initial, game);
            if (branch is "mana" or "power")
            {
                if (branch == "mana") await game.Act(owner, new TapRuneCommand(owner + "-RUNE-5"));
                else await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-2"));
                var count = game.Commands().Length; await game.ActRetained(rejected, false); Assert.Equal(count, game.Commands().Length);
            }
            if (branch is not "controller" and not "repeated")
            {
                var mana = game.State.RunePools[owner].Mana;
                await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(mana - 1, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State, owner)); Cold(initial, game);
                await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(game.State, owner));
            }
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Cold(initial, game); await game.Replay();
        }
        finally { await Export("native-boundary-" + owner + "-" + branch, initial, game); }
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P1", true)]
    [InlineData("P2", false)] [InlineData("P2", true)]
    public async Task ReturnIsNotDeathAndAReplayedIncarnationLosesBothEmpowerAndItsConditionalSelfMight(string owner, bool empower)
    {
        var initial = Hand(Position(owner), "RETURN", "OGN·104/298", owner); var game = new Driver(initial);
        try
        {
            await Tap(game, owner); await game.Act(owner, new PlayCardCommand("UNIT", No, []));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empower) { await Empower(game, owner); await game.Top(); Assert.Equal(6, Might(game.State, owner)); }
            var mana = game.State.RunePools[owner].Mana;
            await game.Act(owner, new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
            Assert.Equal(mana - 1, game.State.RunePools[owner].Mana); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Hand); Assert.DoesNotContain("UNIT", game.State.PlayerZones[owner].Graveyard);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED" && Equals(e.Event.Payload.GetValueOrDefault("reason"), TriggerKinds.UnitLastBreathCallRuneOne));
            Cold(initial, game); await game.Act(owner, new EndTurnCommand()); await game.Act(Other(owner), new EndTurnCommand());
            await Tap(game, owner); var before = game.State.RunePools[owner].Mana;
            await game.Act(owner, new PlayCardCommand("UNIT", No, [])); Assert.Equal(before - 4, game.State.RunePools[owner].Mana);
            Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(4, Might(game.State, owner));
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Cold(initial, game); await game.Replay();
        }
        finally { await Export("return-new-incarnation-" + owner + "-" + empower, initial, game); }
    }

    private static async Task<ConformanceCommand> Reject(Driver game, string actor, GameCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        node["promptId"] = ResolutionResult.BuildPrompts(game.State)[actor].PromptId; node["snapshotTick"] = game.State.Tick;
        var raw = new ConformanceCommand(actor, "ven078-reject-" + game.Commands().Length, JsonSerializer.SerializeToElement(node, Json));
        var count = game.Commands().Length; await game.ActRetained(raw, false); Assert.Equal(count + 1, game.Commands().Length);
        await game.ActRetained(raw, false); Assert.Equal(count + 1, game.Commands().Length); return raw;
    }
    private static MatchState Hand(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
}
