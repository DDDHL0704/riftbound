using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class N9ReturnedSourceCardinalityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Type Leaf = Assembly.Load("Riftbound.PostgresAcceptance").GetType("VenEmpoweredSelfSqlAcceptance", throwOnError: true)!;
    private static readonly BindingFlags Methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static N9ReturnedSourceCardinalityTests()
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_N9_CARDINALITY_RUNTIME") is not { Length: > 0 } path) return;
        Assembly[] assemblies = [typeof(N9ReturnedSourceCardinalityTests).Assembly, Leaf.Assembly,
            typeof(CoreRuleEngine).Assembly, typeof(Riftbound.CardCatalog.OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly];
        using var output = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(output, new { processId = Environment.ProcessId, ruleInputs = RuleInputIdentity.Current,
            assemblies = assemblies.Select(assembly => new { name = assembly.GetName().Name, path = assembly.Location,
                mvid = assembly.ManifestModule.ModuleVersionId,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant() }) }, Json);
    }

    [Theory]
    [InlineData("public")] [InlineData("private")] [InlineData("revealed")]
    public async Task ActualRecipeKeepsOnePendingEmpowerAlongsideTheRealRevealChoice(string prefix)
    {
        var state = await Prefix(prefix);
        Pending(state, prefix);
        Assert.Equal(prefix == "revealed" ? 2 : 1, state.StackItems.Count);
        Assert.Equal(prefix == "public" ? 1 : 2, state.CardObjects["N9-UNIT"].ObjectGeneration);
        Assert.False(state.CardObjects["N9-UNIT"].IsEmpowered);
        if (prefix == "revealed") {
            Assert.Equal("REVEALED_HAND_EFFECT", state.PendingCardChoice!.ChoiceWindow);
            Assert.Contains(state.StackItems, item => item.EffectKind == "LAST_BREATH_REVEAL_HAND_LOOK_FACEDOWN");
            Pending(state with { StackItems = state.StackItems.Reverse().ToArray() }, prefix);
        }
    }

    [Theory]
    [InlineData("public", false)] [InlineData("public", true)]
    [InlineData("private", false)] [InlineData("private", true)]
    [InlineData("revealed", false)] [InlineData("revealed", true)]
    public async Task TheActualMandatoryVerifierGuardRejectsZeroOrMultipleEmpowerMatches(string prefix, bool duplicate)
    {
        var state = await Prefix(prefix); Pending(state, prefix);
        var original = state.StackItems.First(item => item.EmpowerActivation is not null);
        var bad = state with { StackItems = duplicate
            ? [..state.StackItems, original with { StackItemId = original.StackItemId + "-forged-extra" }]
            : state.StackItems.Where(item => item.EmpowerActivation is null).ToArray() };
        var exception = Assert.Throws<TargetInvocationException>(() => Pending(bad, prefix));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Pending(state, prefix);
    }

    private static void Pending(MatchState state, string prefix)
        => Leaf.GetMethod("CheckPending", Methods)!.Invoke(null, [state, "VEN·043", prefix == "private" ? "HIDDEN" : "VISIBLE"]);

    private static async Task<MatchState> Prefix(string prefix)
    {
        var session = new MatchSession("N9-CARDINALITY-" + Guid.NewGuid().ToString("N"), new CoreRuleEngine(), new Journal());
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        var seeded = await session.SeedScenarioAsync("P1", "n9-setup", "ven-empowered-self-043",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "ven-empowered-self-043" }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage); var state = seeded.State; var intent = 0;
        async Task Act(string player, GameCommand command, bool accepted)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node, Json);
            var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state), Json);
            var prompts = JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state), Json);
            var result = await session.SubmitAsync(player, "n9-cardinality-" + intent++, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.Equal(accepted, result.Accepted); state = result.State; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (!accepted) {
                Assert.Equal(hash, MatchStateHasher.Hash(state)); Assert.Empty(result.Events);
                Assert.True(JsonNode.DeepEquals(views, JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state), Json)));
                Assert.True(JsonNode.DeepEquals(prompts, JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state), Json)));
            }
        }
        Task Checkpoint(string label)
        {
            if (label == (prefix == "public" ? "public-pending" : prefix == "private" ? "private-pending" : "revealed-pending"))
                throw new PrefixReached();
            return Task.CompletedTask;
        }
        var task = (Task)Leaf.GetMethod("ExerciseAsync", Methods)!.Invoke(null, ["VEN·043", "revealed-return",
            (Func<string, GameCommand, bool, Task>)Act, (Func<MatchState>)(() => state), (Func<string, Task>)Checkpoint])!;
        await Assert.ThrowsAsync<PrefixReached>(() => task);
        return state;
    }

    private sealed class PrefixReached : Exception { }
    private sealed class Journal : IMatchJournal
    {
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
