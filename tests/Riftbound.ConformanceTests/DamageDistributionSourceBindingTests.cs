using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DamageDistributionSourceBindingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public DamageDistributionSourceBindingTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    [Fact]
    public async Task ActualCurrentFieldSourceRestoresAtItsActualCapturedLocation()
    {
        var (initial, run) = await Confirm("current-field");
        var item = Assert.Single(run.State.StackItems);
        Assert.Equal(run.State.CardObjects["HOST"].ObjectGeneration, item.DamageDistribution!.Source.Generation);
        Assert.Equal(run.State.ObjectLocations["HOST"], item.DamageDistribution.SourceLocation);
        Assert.Equal("F", item.DamageDistribution.SourceLocation.BattlefieldObjectId);
        await VerifyOriginal(initial, run);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("other-field")]
    [InlineData("missing-location")]
    [InlineData("printing")]
    public async Task SameGenerationRequiresTheActualFieldLocationAndPrinting(string mutation)
    {
        var (initial, run) = await Confirm(mutation, otherField: mutation == "other-field");
        var original = Frame(run.State, initial, run);
        Assert.Empty(Errors(original));
        var item = Assert.Single(run.State.StackItems);
        var context = item.DamageDistribution!;
        var corrupt = mutation switch
        {
            "missing-location" => run.State with
                { ObjectLocations = run.State.ObjectLocations.Where(x => x.Key != "HOST").ToDictionary() },
            "printing" => run.State with { CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects)
                { ["HOST"] = run.State.CardObjects["HOST"] with { CardNo = "OGN·041a/298" } } },
            _ => run.State with { StackItems = [item with { DamageDistribution = context with
                { SourceLocation = mutation == "base" ? new("P1", "BASE") : new("P1", "BATTLEFIELD", "UNCONTESTED-OTHER-FIELD") } }] }
        };
        var frame = Frame(corrupt, initial, run);
        var errors = Errors(frame);
        run.Export("mutation.json", new { mutation, initial, original, frame, errors });
        Assert.Contains(errors, e => e.Contains("distributed damage", StringComparison.Ordinal));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(
            frame with { ValidationErrors = errors }, new CoreRuleEngine(), run));
        Assert.Equal(JsonSerializer.Serialize(original.Commands), JsonSerializer.Serialize(frame.Commands));
        Assert.Equal(JsonSerializer.Serialize(original.Events), JsonSerializer.Serialize(frame.Events));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualBurnoutKeepsTheOldSourceAndRejectsTheCurrentPrivateGeneration(bool forgeCurrentGeneration)
    {
        var (initial, run) = await Confirm(forgeCurrentGeneration ? "current-private" : "historical");
        await run.Act("P1", new PlayCardCommand("SOUL", "SFD·163/221", ["HOST", "P1-ALLY"]));
        var returned = await run.Top();
        Assert.Contains(returned.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Contains("HOST", run.State.PlayerZones["P1"].Hand);
        var item = Assert.Single(run.State.StackItems);
        var context = item.DamageDistribution!;
        var currentGeneration = run.State.CardObjects["HOST"].ObjectGeneration;
        Assert.Equal(0, context.Source.Generation);
        // The unit crosses three non-field zones here: battlefield -> graveyard,
        // graveyard -> main deck during burnout, then main deck -> hand.
        Assert.Equal(3, currentGeneration);
        Assert.Equal(5, run.Commands().Length);
        Assert.Equal(18, run.Events().Length);
        Assert.Equal("HAND", run.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("BATTLEFIELD", context.SourceLocation.Zone);
        await VerifyOriginal(initial, run);
        var opponent = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P2"], Web);
        Assert.False(opponent.GetProperty("players").GetProperty("P1").GetProperty("objects").TryGetProperty("HOST", out _));
        Assert.Equal(0, opponent.GetProperty("stack")[0].GetProperty("damageDistribution")
            .GetProperty("source").GetProperty("generation").GetInt64());
        if (!forgeCurrentGeneration) return;

        var corrupt = run.State with { StackItems = [item with { DamageDistribution = context with
            { Source = context.Source with { Generation = currentGeneration } } }] };
        var frame = Frame(corrupt, initial, run);
        var errors = Errors(frame);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), corrupt,
            new CoreRuleEngine(), default, run.Events());
        run.Export("current-generation-mutation.json", new { initial, frame, errors, replay,
            currentGeneration, capturedGeneration = context.Source.Generation, originalCommands = run.Commands(), originalEvents = run.Events() });
        Assert.Contains(errors, e => e.Contains("distributed damage", StringComparison.Ordinal));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(
            frame with { ValidationErrors = errors }, new CoreRuleEngine(), run));
        Assert.False(replay.IsMatch);
        Assert.Equal(JsonSerializer.Serialize(run.Commands()), JsonSerializer.Serialize(frame.Commands));
        Assert.Equal(JsonSerializer.Serialize(run.Events()), JsonSerializer.Serialize(frame.Events));
    }

    private static async Task<(MatchState Initial, Driver Run)> Confirm(string name, bool otherField = false)
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
            CardObjects = initial.CardObjects.Where(x => !deck.Contains(x.Key)).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(x => !deck.Contains(x.Key)).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } }
        };
        if (otherField)
            initial = initial with
            {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
                    { ["UNCONTESTED-OTHER-FIELD"] = new("UNCONTESTED-OTHER-FIELD", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2") },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
                    { ["UNCONTESTED-OTHER-FIELD"] = new("P2", "BATTLEFIELD", "UNCONTESTED-OTHER-FIELD") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                    { ["P2"] = initial.PlayerZones["P2"] with { Battlefields = [..initial.PlayerZones["P2"].Battlefields, "UNCONTESTED-OTHER-FIELD"] } }
            };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_DISTRIBUTION_SOURCE_174_EVIDENCE");
        var run = new Driver(initial, root is null ? null : Path.Combine(root, name));
        await run.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
        await run.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        return (initial, run);
    }

    private static MatchRecoveryFrame Frame(MatchState state, MatchState initial, Driver run)
    {
        var commands = run.Commands(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return new(state.RoomId, state.Tick, sequence, commands, run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
    }

    private static IReadOnlyList<string> Errors(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static async Task VerifyOriginal(MatchState initial, Driver run)
    {
        var frame = Frame(run.State, initial, run); Assert.Empty(Errors(frame));
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame))!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var viewer in new[] { "P1", "P2" })
            Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(run.State)[viewer]),
                MatchStateHasher.HashValue(restored.SnapshotFor(viewer)));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), run.State,
            new CoreRuleEngine(), default, run.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        run.Export("original-checkpoint.json", new { initial, frame, replay });
    }
}
