using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{
    [Fact]
    public async Task FixtureRunnerReplaysCommandLogAndChecksExpectedShape()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p1-placeholder-pass-priority.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public void LegacyPromptActionsAllowAdditionalServerPromptActions()
    {
        var fixture = BuildPromptComparisonFixture(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["P1"] = ["END_TURN"],
                ["P2"] = ["WAIT"]
            });
        var result = BuildPromptComparisonResult(["PLAY_CARD", "ACTIVATE_ABILITY", "END_TURN"]);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public void LegacyPromptActionsStillFailWhenExpectedActionIsMissing()
    {
        var fixture = BuildPromptComparisonFixture(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["P1"] = ["END_TURN"],
                ["P2"] = ["WAIT"]
            });
        var result = BuildPromptComparisonResult(["PLAY_CARD"]);

        var mismatches = ConformanceFixtureRunner.CompareExpected(fixture, result);

        Assert.Contains(mismatches, mismatch => mismatch.Contains("promptActions.P1", StringComparison.Ordinal));
        Assert.Contains(mismatches, mismatch => mismatch.Contains("missing expected action END_TURN", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyPromptActionsKeepWaitPromptsExact()
    {
        var fixture = BuildPromptComparisonFixture(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["P2"] = ["WAIT"]
            });
        var result = BuildPromptComparisonResult(
            ["END_TURN"],
            ["WAIT", "END_TURN"]);

        var mismatches = ConformanceFixtureRunner.CompareExpected(fixture, result);

        Assert.Contains(mismatches, mismatch => mismatch.Contains("promptActions.P2", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyPromptActionsIgnoreGlobalSurrenderOnWaitPrompts()
    {
        var fixture = BuildPromptComparisonFixture(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["P2"] = ["WAIT"]
            });
        var result = BuildPromptComparisonResult(
            ["END_TURN"],
            ["WAIT", "SURRENDER"]);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public void ExpectedPromptsCanRequireExactActionsWhenOptedIn()
    {
        var fixture = BuildPromptComparisonFixture(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["P1"] = ["END_TURN"]
            },
            new Dictionary<string, ConformanceExpectedPrompt>(StringComparer.Ordinal)
            {
                ["P1"] = new(Actionable: true, Actions: ["END_TURN"], ExactActions: true)
            });
        var result = BuildPromptComparisonResult(["PLAY_CARD", "END_TURN"]);

        var mismatches = ConformanceFixtureRunner.CompareExpected(fixture, result);

        Assert.Contains(mismatches, mismatch => mismatch.Contains("prompts.P1.actions", StringComparison.Ordinal));
    }

    [Fact]
    public void ExpectedPromptsDefaultToRequiredActionsForLegacyFixtures()
    {
        var fixture = BuildPromptComparisonFixture(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            new Dictionary<string, ConformanceExpectedPrompt>(StringComparer.Ordinal)
            {
                ["P1"] = new(Actionable: true, Actions: ["END_TURN"])
            });
        var result = BuildPromptComparisonResult(["PLAY_CARD", "END_TURN"]);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task LoadsLegacyJavaFixtureMetadataBeforeRulesAreAudited()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "java-oracle", "java-oracle-p1-pass.fixture.json"),
            CancellationToken.None);

        Assert.Equal("java-oracle-p1-pass", fixture.FixtureId);
        Assert.Equal("java-oracle", fixture.Source);
        Assert.Equal("rules-260330", fixture.RulesVersion);
        Assert.Equal("official-2026-04-27", fixture.CatalogVersion);
        Assert.Equal("75bf7cf", fixture.JavaCommit);
        Assert.Equal(2603301001L, fixture.Seed);
        Assert.Equal(new[] { "TURN_ENDED" }, fixture.Expected.EventKinds);
        Assert.True(fixture.RequiresRuleAudit);
        Assert.NotEmpty(fixture.RulesEvidence ?? []);
        Assert.True(fixture.HasLegacyOracle);
        Assert.True(fixture.HasCompatibilityOracle);
    }

    [Fact]
    public async Task LoadsP2PreflightFixtureSchema()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start.fixture.json"),
            CancellationToken.None);

        Assert.Equal(2, fixture.SchemaVersion);
        Assert.Equal("p2-preflight-turn-start-runes-and-draw", fixture.FixtureId);
        Assert.False(fixture.RequiresRuleAudit);
        Assert.NotNull(fixture.InitialState);
        Assert.Equal("P2", fixture.InitialState.TurnPlayerId);
        Assert.Equal("TURN_START", fixture.InitialState.Phase);
        Assert.Equal(new RunePool(1, 1), fixture.InitialState.RunePools?["P2"]);
        Assert.NotNull(fixture.InitialState.Players);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, fixture.InitialState.Players["P2"].RuneDeck);
        Assert.NotNull(fixture.Expected.FinalState);
        Assert.Equal("MAIN", fixture.Expected.FinalState.Phase);
        Assert.Equal(new RunePool(0, 0), fixture.Expected.FinalState.RunePools?["P2"]);
        Assert.Equal(
            new[] { "TURN_START_BEGAN", "RUNES_CALLED", "CARD_DRAWN", "RUNE_POOL_CLEARED", "MAIN_PHASE_BEGAN" },
            fixture.Expected.Events?.Select(gameEvent => gameEvent.Kind));
        Assert.False(fixture.Expected.Prompts?["P1"].Actionable);
        Assert.Equal(new[] { "END_TURN" }, fixture.Expected.Prompts?["P2"].Actions);
    }

    [Fact]
    public async Task RunnerAppliesP2InitialStateBeforeFirstCommand()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start.fixture.json"),
            CancellationToken.None);
        var ruleEngine = new CapturingRuleEngine();

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            ruleEngine,
            CancellationToken.None);

        var captured = Assert.IsType<MatchState>(ruleEngine.CapturedState);
        Assert.Equal(4, captured.TurnNumber);
        Assert.Equal("P2", captured.ActivePlayerId);
        Assert.Equal("P2", captured.TurnPlayerId);
        Assert.Equal("TURN_START", captured.Phase);
        Assert.Equal("NEUTRAL_CLOSED", captured.TimingState);
        Assert.Equal(MatchStatuses.InProgress, captured.Status);
        Assert.Equal(new RunePool(1, 1), captured.RunePools["P2"]);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, captured.PlayerZones["P2"].RuneDeck);
        Assert.Equal(new[] { "P2-MAIN-001" }, captured.PlayerZones["P2"].MainDeck);
        Assert.Equal(1, result.FinalTick);
        Assert.Equal("TURN_START", result.FinalState.Phase);
    }

    [Theory]
    [InlineData("java-oracle-p1-pass.fixture.json")]
    [InlineData("java-oracle-p1-end-turn.fixture.json")]
    [InlineData("java-oracle-p1-duplicate-pass.fixture.json")]
    public async Task LegacyJavaFixtureMatchesCurrentRuleSkeletonButStillRequiresRuleAudit(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "java-oracle", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new PlaceholderRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        var mismatches = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches)
            + "\nactual event kinds: " + string.Join(", ", result.EventKinds)
            + "\nactual stack: " + JsonSerializer.Serialize(result.FinalState.StackItems)
            + "\nactual source: " + JsonSerializer.Serialize(result.FinalState.CardObjects.GetValueOrDefault("SHADOW")));

        Assert.True(fixture.RequiresRuleAudit);
        Assert.NotEmpty(fixture.RulesEvidence ?? []);
        Assert.True(fixture.HasLegacyOracle);
        Assert.True(fixture.HasCompatibilityOracle);
    }

    [Fact]
    public void CanonicalJsonKeepsStableCamelCaseEnvelope()
    {
        var json = CanonicalJson.Serialize(new WsServerMessage(
            MessageType.ERROR,
            "room",
            "P1",
            7,
            new ErrorDto(ErrorCodes.UnsupportedCommand, "sample")));

        Assert.Equal(
            """{"type":11,"roomId":"room","playerId":"P1","serverTick":7,"payload":{"code":"UNSUPPORTED_COMMAND","message":"sample"},"protocolVersion":1,"schemaVersion":1}""",
            json);
    }
}
