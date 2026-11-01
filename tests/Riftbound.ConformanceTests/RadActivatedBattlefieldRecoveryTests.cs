using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadActivatedBattlefieldRecoveryTests
{
    [Theory]
    [InlineData("removed")]
    [InlineData("player")]
    [InlineData("source")]
    [InlineData("generation")]
    [InlineData("ability")]
    public async Task AcceptedUseCannotBeErasedOrReboundDuringRecovery(string mutation)
    {
        var game = new Driver(Create(P4ActivatedAbilityCatalog.BlackMarketCardNo));
        await game.Act("P1", new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []));
        await game.Top();
        Assert.Empty(Errors(game, game.State));
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects,
            effect => effect.StartsWith("PRINTED_ACTIVATION_USED:", StringComparison.Ordinal));
        Assert.DoesNotContain(marker, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)));
        Assert.DoesNotContain(marker, JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(game.State)));
        var changed = mutation switch {
            "player" => marker.Replace(":PLAYER:P1", ":PLAYER:P2", StringComparison.Ordinal),
            "source" => marker.Replace(":BF:", ":OTHER:", StringComparison.Ordinal),
            "generation" => marker.Replace($":BF:{game.State.CardObjects["BF"].ObjectGeneration}:", ":BF:999:", StringComparison.Ordinal),
            "ability" => marker.Replace(P4ActivatedAbilityCatalog.BlackMarketAbilityId, "UNKNOWN_ABILITY", StringComparison.Ordinal),
            _ => null
        };
        var forged = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects
            .Where(effect => effect != marker).Concat(changed is null ? [] : new[] { changed }).ToArray() };
        Assert.Contains(Errors(game, forged), error => error.Contains("per-player activation use history", StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task TurnExpiryClearsTheOldUseAndTheOtherPlayerCanPayForANewUse()
    {
        var initial = Create(P4ActivatedAbilityCatalog.BlackMarketCardNo);
        foreach (var id in new[] { "P2-R0", "P2-R1" }) initial = Put(initial, id, "OGN·007/298", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []));
        await game.Top();
        var old = Assert.Single(game.State.UntilEndOfTurnEffects,
            effect => effect.StartsWith("PRINTED_ACTIVATION_USED:", StringComparison.Ordinal));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.DoesNotContain(old, game.State.UntilEndOfTurnEffects);
        Assert.Empty(Errors(game, game.State));
        var forged = game.State with { UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects, old] };
        Assert.Contains(Errors(game, forged), error => error.Contains("per-player activation use history", StringComparison.Ordinal));
        await game.Act("P2", new RecycleRuneCommand("P2-R0", ["P2-R0", "P2-R1"]));
        await game.Act("P2", new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []));
        await game.Top();
        var current = Assert.Single(game.State.UntilEndOfTurnEffects,
            effect => effect.StartsWith("PRINTED_ACTIVATION_USED:", StringComparison.Ordinal));
        Assert.EndsWith(":PLAYER:P2", current);
        Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
        await Reject(game.State, "P2", new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []));
        await game.Replay();
    }

    [Fact]
    public async Task SerializedHistoryPreservesTheUseAndStillRejectsASecondActivation()
    {
        var game = new Driver(Create(P4ActivatedAbilityCatalog.BlackMarketCardNo));
        var activation = new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []);
        await game.Act("P1", activation);
        await game.Top();
        var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(game.State))!;
        var commands = JsonSerializer.Deserialize<RecoveredCommand[]>(JsonSerializer.Serialize(game.Commands()))!;
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events()))!;
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, commands[^1].CompletedEventSequence,
            commands, events, new Dictionary<string, RecoveredPlayerView>(), state, state.Tick));
        await Reject(state, "P1", activation);
        await game.Replay();
    }

    [Fact]
    public async Task AUseWithoutAnAcceptedActivationIsRejected()
    {
        var game = new Driver(Create(P4ActivatedAbilityCatalog.BlackMarketCardNo));
        await game.Act("P1", new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []));
        await game.Top();
        var commands = game.Commands().Select(command => command.CommandType == CommandTypes.ActivateAbility
            ? command with { Accepted = false } : command).ToArray();
        var errors = MatchRecoveryValidator.Validate(game.State.RoomId, commands[^1].CompletedEventSequence,
            commands, game.Events(), new Dictionary<string, RecoveredPlayerView>(), game.State, game.State.Tick);
        Assert.Contains(errors, error => error.Contains("per-player activation use history", StringComparison.Ordinal));
        await game.Replay();
    }

    private static IReadOnlyList<string> Errors(Driver game, MatchState state)
    {
        var commands = game.Commands();
        return MatchRecoveryValidator.Validate(state.RoomId, commands[^1].CompletedEventSequence,
            commands, game.Events(), new Dictionary<string, RecoveredPlayerView>(), state, state.Tick);
    }
}
