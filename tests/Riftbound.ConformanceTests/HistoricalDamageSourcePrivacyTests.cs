using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HistoricalDamageSourcePrivacyTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("current-generation")]
    [InlineData("future-generation")]
    [InlineData("different-source")]
    [InlineData("different-printing")]
    [InlineData("initial-amount")]
    [InlineData("missing-context")]
    public async Task OnlyAValidatedOldGenerationDamageSourceKeepsItsPublicIdentity(string mutation)
    {
        var initial = AttackDamageDistributionTests.Position();
        foreach (var id in new[] { "ENEMY2", "ENEMY3" })
            initial = LocalDestructionRecallTests.At(initial, id, "P2", "BASE");
        initial = LocalDestructionRecallTests.At(
            OfficialHoldSequenceTests.AddUnit(initial, "P1-ALLY", "OGN·001/298", "F", "P1"), "P1-ALLY", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "SOUL", "SFD·163/221", "P1");
        var deck = initial.PlayerZones["P1"].MainDeck;
        initial = initial with
        {
            CardObjects = initial.CardObjects.Where(p => !deck.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => !deck.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } }
        };
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_HISTORICAL_DAMAGE_PRIVACY_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            evidence is null ? null : Path.Combine(evidence, mutation));
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        await driver.Act("P1", new PlayCardCommand("SOUL", "SFD·163/221", ["HOST", "P1-ALLY"]));
        var returned = await driver.Top();
        Assert.Contains(returned.Events, ev => ev.Kind == "BURNOUT_APPLIED");
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Hand);
        var item = Assert.Single(driver.State.StackItems);
        var context = item.DamageDistribution!;
        var currentGeneration = driver.State.CardObjects["HOST"].ObjectGeneration;
        Assert.True(context.Source.Generation < currentGeneration);
        var hash = MatchStateHasher.Hash(driver.State);
        foreach (var viewer in new[] { "P1", "P2" })
            Assert.Equal("HOST", Assert.IsType<Dictionary<string, object?>>(
                Assert.Single(ResolutionResult.BuildSnapshots(driver.State)[viewer].Stack))["sourceObjectId"]);
        Assert.Equal("HOST", Assert.IsType<Dictionary<string, object?>>(
            Assert.Single(ResolutionResult.BuildSpectatorSnapshot(driver.State).Stack))["sourceObjectId"]);
        var opponent = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(driver.State)["P2"],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(opponent.GetProperty("players").GetProperty("P1").GetProperty("objects").TryGetProperty("HOST", out _));
        Assert.Equal(hash, MatchStateHasher.Hash(driver.State));
        var frame = Frame(driver.State, initial, driver);
        Assert.Empty(Errors(frame));
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(hash, MatchStateHasher.Hash(decoded.AuthoritativeState!));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), driver);
        foreach (var viewer in new[] { "P1", "P2" })
            Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(driver.State)[viewer]),
                MatchStateHasher.HashValue(restored.SnapshotFor(viewer)));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("checkpoint.json", new { initial, frame, replay, mutation, currentGeneration,
            capturedGeneration = context.Source.Generation, commands = driver.Commands().Length, events = driver.Events().Length });
        if (mutation == "valid") return;

        var forged = item with { DamageDistribution = mutation switch
        {
            "current-generation" => context with { Source = context.Source with { Generation = currentGeneration } },
            "future-generation" => context with { Source = context.Source with { Generation = currentGeneration + 1 } },
            "different-source" => context with { Source = context.Source with { ObjectId = "ENEMY1" } },
            "different-printing" => context with { CardNo = "OGN·001/298" },
            "initial-amount" => context with { InitialDamage = context.InitialDamage + 1 },
            "missing-context" => null,
            _ => throw new InvalidOperationException(mutation)
        } };
        var corrupt = driver.State with { StackItems = [forged] };
        var projected = Assert.IsType<Dictionary<string, object?>>(
            Assert.Single(ResolutionResult.BuildSnapshots(corrupt)["P2"].Stack));
        Assert.Equal("HIDDEN", projected["sourceObjectId"]);
        var errors = Errors(Frame(corrupt, initial, driver));
        Assert.NotEmpty(errors);
        driver.Export("mutation.json", new { mutation, errors, corrupt, projected });
    }

    private static MatchRecoveryFrame Frame(MatchState state, MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var commands = driver.Commands(); var events = driver.Events();
        var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }

    private static IReadOnlyList<string> Errors(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
}
