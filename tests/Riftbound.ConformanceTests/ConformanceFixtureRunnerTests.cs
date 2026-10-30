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

    [Fact]
    public async Task P4GaiBeastTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-gai-beast-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-GAI-BEAST-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-GAI-BEAST"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-GAI-BEAST-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GAI-BEAST-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GAI-BEAST-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-GAI-BEAST-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd180FioraTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-180-fiora-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-180-FIORA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-180-FIORA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-180-FIORA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-180-FIORA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-180-FIORA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-180-FIORA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd180aFioraTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-180a-fiora-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-180A-FIORA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-180A-FIORA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-180A-FIORA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-180A-FIORA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-180A-FIORA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-180A-FIORA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Theory]
    [InlineData("p2-preflight-play-plucky-poro-keyword-unit.fixture.json", "P1-UNIT-PLUCKY-PORO", 2, "CARD_TYPE:UNIT|法盾|魄罗")]
    [InlineData("p2-preflight-play-mighty-poro-keyword-unit.fixture.json", "P1-UNIT-MIGHTY-PORO", 2, "CARD_TYPE:UNIT|坚守|魄罗")]
    [InlineData("p2-preflight-play-assault-poro-keyword-unit.fixture.json", "P1-UNIT-ASSAULT-PORO", 2, "CARD_TYPE:UNIT|强攻|魄罗")]
    [InlineData("p2-preflight-play-fierce-first-mate-keyword-unit.fixture.json", "P1-UNIT-FIERCE-FIRST-MATE", 5, "CARD_TYPE:UNIT|强攻|海盗")]
    [InlineData("p2-preflight-play-zephyr-sage-keyword-unit.fixture.json", "P1-UNIT-ZEPHYR-SAGE", 6, "CARD_TYPE:UNIT|坚守|鸟类")]
    [InlineData("p2-preflight-play-pakaa-cub-keyword-unit.fixture.json", "P1-UNIT-PAKAA-CUB", 3, "CARD_TYPE:UNIT|待命|猫科")]
    [InlineData("p2-preflight-play-navori-scout-keyword-unit.fixture.json", "P1-UNIT-NAVORI-SCOUT", 4, "CARD_TYPE:UNIT|法盾|约德尔人")]
    [InlineData("p2-preflight-play-laurent-swordsman-keyword-unit.fixture.json", "P1-UNIT-LAURENT-SWORDSMAN", 3, "CARD_TYPE:UNIT|强攻2")]
    [InlineData("p2-preflight-play-gluttonous-toadfrog-keyword-unit.fixture.json", "P1-UNIT-GLUTTONOUS-TOADFROG", 5, "CARD_TYPE:UNIT|狩猎3")]
    [InlineData("p2-preflight-play-sentinel-adept-no-optional-assemble.fixture.json", "P1-UNIT-SENTINEL-ADEPT", 3, "CARD_TYPE:UNIT|哨兵|百炼")]
    [InlineData("p2-preflight-play-battle-chef-no-optional-assemble.fixture.json", "P1-UNIT-BATTLE-CHEF", 5, "CARD_TYPE:UNIT|百炼")]
    [InlineData("p2-preflight-play-stout-poro-no-optional-assemble.fixture.json", "P1-UNIT-STOUT-PORO", 2, "CARD_TYPE:UNIT|百炼|魄罗")]
    [InlineData("p2-preflight-play-master-bingwen-no-optional-assemble.fixture.json", "P1-UNIT-MASTER-BINGWEN", 6, "CARD_TYPE:UNIT|百炼")]
    [InlineData("p2-preflight-play-unl-plucky-poro-keyword-unit.fixture.json", "P1-UNIT-UNL-PLUCKY-PORO", 2, "CARD_TYPE:UNIT|法盾|魄罗")]
    [InlineData("p2-preflight-play-unl-stout-poro-keyword-unit.fixture.json", "P1-UNIT-UNL-STOUT-PORO", 2, "CARD_TYPE:UNIT|百炼|魄罗")]
    [InlineData("p2-preflight-play-unl-assault-poro-keyword-unit.fixture.json", "P1-UNIT-UNL-ASSAULT-PORO", 2, "CARD_TYPE:UNIT|强攻|魄罗")]
    [InlineData("p2-preflight-play-mutant-kitten-keyword-unit.fixture.json", "P1-UNIT-MUTANT-KITTEN", 1, "CARD_TYPE:UNIT|坚守2|壁垒|猫科")]
    [InlineData("p2-preflight-play-burly-brawler-keyword-unit.fixture.json", "P1-UNIT-BURLY-BRAWLER", 3, "CARD_TYPE:UNIT|坚守2|壁垒")]
    [InlineData("p2-preflight-play-laurent-bladeguard-keyword-unit.fixture.json", "P1-UNIT-LAURENT-BLADEGUARD", 3, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-garen-keyword-unit.fixture.json", "P1-UNIT-GAREN", 5, "CARD_TYPE:UNIT|坚守2|强攻2|精锐")]
    [InlineData("p2-preflight-play-solari-guard-keyword-unit.fixture.json", "P1-UNIT-SOLARI-GUARD", 3, "CARD_TYPE:UNIT|坚守|壁垒")]
    [InlineData("p2-preflight-play-aerie-head-fan-keyword-unit.fixture.json", "P1-UNIT-AERIE-HEAD-FAN", 3, "CARD_TYPE:UNIT|法盾|约德尔人")]
    [InlineData("p2-preflight-play-vex-keyword-unit.fixture.json", "P1-UNIT-VEX", 5, "CARD_TYPE:UNIT|坚守|壁垒|约德尔人")]
    [InlineData("p2-preflight-play-wildclaw-beastmaster-keyword-unit.fixture.json", "P1-UNIT-WILDCLAW-BEASTMASTER", 7, "CARD_TYPE:UNIT|壁垒|猫科")]
    [InlineData("p2-preflight-play-huge-yordle-keyword-unit.fixture.json", "P1-UNIT-HUGE-YORDLE", 5, "CARD_TYPE:UNIT|坚守5|壁垒|约德尔人")]
    [InlineData("p2-preflight-play-tianna-crownguard-keyword-unit.fixture.json", "P1-UNIT-TIANNA-CROWNGUARD", 4, "CARD_TYPE:UNIT|法盾|精锐")]
    [InlineData("p2-preflight-play-jhin-spellshield-roam-keyword-unit.fixture.json", "P1-UNIT-JHIN-ROAM", 4, "CARD_TYPE:UNIT|法盾|游走")]
    [InlineData("p2-preflight-play-jhin-alt-a-spellshield-roam-keyword-unit.fixture.json", "P1-UNIT-JHIN-ROAM-A", 4, "CARD_TYPE:UNIT|法盾|游走")]
    [InlineData("p2-preflight-play-vi-keyword-unit.fixture.json", "P1-UNIT-VI", 3, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-vi-alt-a-keyword-unit.fixture.json", "P1-UNIT-VI-A", 3, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-leblanc-keyword-unit.fixture.json", "P1-UNIT-LEBLANC", 4, "CARD_TYPE:UNIT|后排")]
    [InlineData("p2-preflight-play-enthusiastic-announcer-keyword-unit.fixture.json", "P1-UNIT-ENTHUSIASTIC-ANNOUNCER", 2, "CARD_TYPE:UNIT|后排|约德尔人")]
    [InlineData("p2-preflight-play-moss-stepper-keyword-unit.fixture.json", "P1-UNIT-MOSS-STEPPER", 3, "CARD_TYPE:UNIT|犬形|狩猎2")]
    [InlineData("p2-preflight-play-trevor-duttonel-keyword-unit.fixture.json", "P1-UNIT-TREVOR-DUTTONEL", 3, "CARD_TYPE:UNIT|坚守|约德尔人")]
    [InlineData("p2-preflight-play-windrunner-fox-keyword-unit.fixture.json", "P1-UNIT-WINDRUNNER-FOX", 3, "CARD_TYPE:UNIT|犬形|狩猎2")]
    [InlineData("p2-preflight-play-crystalhand-hunter-keyword-unit.fixture.json", "P1-UNIT-CRYSTALHAND-HUNTER", 2, "CARD_TYPE:UNIT|狩猎|约德尔人")]
    [InlineData("p2-preflight-play-flameclaw-keyword-unit.fixture.json", "P1-UNIT-FLAMECLAW", 3, "CARD_TYPE:UNIT|犬形|狩猎2")]
    [InlineData("p2-preflight-play-wuji-apprentice-keyword-unit.fixture.json", "P1-UNIT-WUJI-APPRENTICE", 2, "CARD_TYPE:UNIT|狩猎")]
    [InlineData("p2-preflight-play-arena-crowd-favorite-keyword-unit.fixture.json", "P1-UNIT-ARENA-CROWD-FAVORITE", 3, "CARD_TYPE:UNIT|狩猎")]
    [InlineData("p2-preflight-play-unl-yi-hunt-keyword-unit.fixture.json", "P1-UNIT-UNL-YI-HUNT", 4, "CARD_TYPE:UNIT|狩猎2")]
    [InlineData("p2-preflight-play-unl-yi-alt-a-hunt-keyword-unit.fixture.json", "P1-UNIT-UNL-YI-HUNT-A", 4, "CARD_TYPE:UNIT|狩猎2")]
    [InlineData("p2-preflight-play-khazix-hunt-keyword-unit.fixture.json", "P1-UNIT-KHAZIX-HUNT", 5, "CARD_TYPE:UNIT|狩猎")]
    [InlineData("p2-preflight-play-khazix-alt-a-hunt-keyword-unit.fixture.json", "P1-UNIT-KHAZIX-HUNT-A", 5, "CARD_TYPE:UNIT|狩猎")]
    [InlineData("p2-preflight-play-black-rose-agent-keyword-unit.fixture.json", "P1-UNIT-BLACK-ROSE-AGENT", 2, "CARD_TYPE:UNIT|强攻")]
    [InlineData("p2-preflight-play-stunning-guardian-keyword-unit.fixture.json", "P1-UNIT-STUNNING-GUARDIAN", 2, "CARD_TYPE:UNIT|仙灵|狩猎")]
    [InlineData("p2-preflight-play-galio-keyword-unit.fixture.json", "P1-UNIT-GALIO", 6, "CARD_TYPE:UNIT|壁垒|法盾")]
    [InlineData("p2-preflight-play-rell-keyword-unit.fixture.json", "P1-UNIT-RELL", 4, "CARD_TYPE:UNIT|壁垒")]
    [InlineData("p2-preflight-play-sfd-jax-keyword-unit.fixture.json", "P1-UNIT-SFD-JAX", 5, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-jax-alt-a-keyword-unit.fixture.json", "P1-UNIT-SFD-JAX-A", 5, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-giant-arm-kato-keyword-unit.fixture.json", "P1-UNIT-GIANT-ARM-KATO", 3, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-xin-zhao-keyword-unit.fixture.json", "P1-UNIT-XIN-ZHAO", 4, "CARD_TYPE:UNIT|壁垒")]
    [InlineData("p2-preflight-play-sfd-sivir-spellshield2-keyword-unit.fixture.json", "P1-UNIT-SFD-SIVIR-SPELLSHIELD2", 7, "CARD_TYPE:UNIT|法盾2")]
    [InlineData("p2-preflight-play-sfd-sivir-alt-a-spellshield2-keyword-unit.fixture.json", "P1-UNIT-SFD-SIVIR-SPELLSHIELD2-A", 7, "CARD_TYPE:UNIT|法盾2")]
    [InlineData("p2-preflight-play-sfd-draven-keyword-unit.fixture.json", "P1-UNIT-SFD-DRAVEN", 6, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-draven-alt-a-keyword-unit.fixture.json", "P1-UNIT-SFD-DRAVEN-A", 6, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-vayne-keyword-unit.fixture.json", "P1-UNIT-SFD-VAYNE", 2, "CARD_TYPE:UNIT|哨兵|强攻3")]
    [InlineData("p2-preflight-play-sfd-vayne-promo-keyword-unit.fixture.json", "P1-UNIT-SFD-VAYNE-PROMO", 2, "CARD_TYPE:UNIT|哨兵|强攻3")]
    [InlineData("p2-preflight-play-sfd-irelia-keyword-unit.fixture.json", "P1-UNIT-SFD-IRELIA", 4, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-irelia-promo-keyword-unit.fixture.json", "P1-UNIT-SFD-IRELIA-PROMO", 4, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-yone-no-optional-assemble.fixture.json", "P1-UNIT-SFD-YONE", 5, "CARD_TYPE:UNIT|恶魔|百炼")]
    [InlineData("p2-preflight-play-sfd-yone-promo-no-optional-assemble.fixture.json", "P1-UNIT-SFD-YONE-PROMO", 5, "CARD_TYPE:UNIT|恶魔|百炼")]
    [InlineData("p2-preflight-play-sfd-yasuo-keyword-unit.fixture.json", "P1-UNIT-SFD-YASUO", 4, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-sfd-yasuo-promo-keyword-unit.fixture.json", "P1-UNIT-SFD-YASUO-PROMO", 4, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-sfd-darius-trifarian-unit.fixture.json", "P1-UNIT-SFD-DARIUS", 6, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-sfd-darius-promo-trifarian-unit.fixture.json", "P1-UNIT-SFD-DARIUS-PROMO", 6, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-immortal-phoenix-keyword-unit.fixture.json", "P1-UNIT-IMMORTAL-PHOENIX", 3, "CARD_TYPE:UNIT|强攻2|灵体")]
    [InlineData("p2-preflight-play-corpse-flower-predator-keyword-unit.fixture.json", "P1-UNIT-CORPSE-FLOWER-PREDATOR", 8, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-revna-roam-keyword-unit.fixture.json", "P1-UNIT-REVNA", 7, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-jungle-elephant-keyword-unit.fixture.json", "P1-UNIT-JUNGLE-ELEPHANT", 6, "CARD_TYPE:UNIT|强攻")]
    [InlineData("p2-preflight-play-sad-poro-keyword-unit.fixture.json", "P1-UNIT-SAD-PORO", 2, "CARD_TYPE:UNIT|魄罗")]
    [InlineData("p2-preflight-play-unl-sad-poro-keyword-unit.fixture.json", "P1-UNIT-UNL-SAD-PORO", 2, "CARD_TYPE:UNIT|魄罗")]
    [InlineData("p2-preflight-play-scouting-warhawk-keyword-unit.fixture.json", "P1-UNIT-SCOUTING-WARHAWK", 1, "CARD_TYPE:UNIT|鸟类")]
    [InlineData("p2-preflight-play-fearless-vanguard-keyword-unit.fixture.json", "P1-UNIT-FEARLESS-VANGUARD", 4, "CARD_TYPE:UNIT|精锐")]
    [InlineData("p2-preflight-play-sneaky-sailor-keyword-unit.fixture.json", "P1-UNIT-SNEAKY-SAILOR", 2, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-terror-spider-hunt-keyword-unit.fixture.json", "P1-UNIT-TERROR-SPIDER", 6, "CARD_TYPE:UNIT|狩猎2|蜘蛛")]
    [InlineData("p2-preflight-play-bandle-soldier-keyword-unit.fixture.json", "P1-UNIT-BANDLE-SOLDIER", 5, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-fiercewing-keyword-unit.fixture.json", "P1-UNIT-FIERCEWING", 7, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-ogn-miss-fortune-keyword-unit.fixture.json", "P1-UNIT-OGN-MISS-FORTUNE", 4, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-ogn-miss-fortune-alt-a-keyword-unit.fixture.json", "P1-UNIT-OGN-MISS-FORTUNE-A", 4, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-ogn-miss-fortune-alt-b-keyword-unit.fixture.json", "P1-UNIT-OGN-MISS-FORTUNE-B", 4, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-taric-keyword-unit.fixture.json", "P1-UNIT-TARIC", 4, "CARD_TYPE:UNIT|坚守|壁垒")]
    [InlineData("p2-preflight-play-lee-sin-steadfast-keyword-unit.fixture.json", "P1-UNIT-LEE-SIN-STEADFAST", 5, "CARD_TYPE:UNIT|坚守")]
    [InlineData("p2-preflight-play-lee-sin-alt-a-steadfast-keyword-unit.fixture.json", "P1-UNIT-LEE-SIN-STEADFAST-A", 5, "CARD_TYPE:UNIT|坚守")]
    [InlineData("p2-preflight-play-leona-steadfast-keyword-unit.fixture.json", "P1-UNIT-LEONA-STEADFAST", 4, "CARD_TYPE:UNIT|坚守")]
    [InlineData("p2-preflight-play-leona-alt-a-steadfast-keyword-unit.fixture.json", "P1-UNIT-LEONA-STEADFAST-A", 4, "CARD_TYPE:UNIT|坚守")]
    [InlineData("p2-preflight-play-ironclad-vanguard-keyword-unit.fixture.json", "P1-UNIT-IRONCLAD-VANGUARD", 6, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-sfd-lucian-keyword-unit.fixture.json", "P1-UNIT-SFD-LUCIAN", 2, "CARD_TYPE:UNIT|哨兵|强攻")]
    [InlineData("p2-preflight-play-sfd-lucian-alt-a-keyword-unit.fixture.json", "P1-UNIT-SFD-LUCIAN-A", 2, "CARD_TYPE:UNIT|哨兵|强攻")]
    [InlineData("p2-preflight-play-qiyana-spellshield-keyword-unit.fixture.json", "P1-UNIT-QIYANA", 4, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-kayn-roam-keyword-unit.fixture.json", "P1-UNIT-KAYN", 6, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-nocturne-roam-keyword-unit.fixture.json", "P1-UNIT-NOCTURNE", 4, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-ogn-yasuo-roam-keyword-unit.fixture.json", "P1-UNIT-OGN-YASUO", 4, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-ogn-yasuo-alt-a-roam-keyword-unit.fixture.json", "P1-UNIT-OGN-YASUO-A", 4, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-sfd-irelia-spellshield-keyword-unit.fixture.json", "P1-UNIT-SFD-057-IRELIA", 4, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-irelia-alt-a-spellshield-keyword-unit.fixture.json", "P1-UNIT-SFD-057A-IRELIA", 4, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-sfd-lucian-no-optional-assemble.fixture.json", "P1-UNIT-SFD-113-LUCIAN", 3, "CARD_TYPE:UNIT|哨兵|百炼")]
    [InlineData("p2-preflight-play-sfd-lucian-alt-a-no-optional-assemble.fixture.json", "P1-UNIT-SFD-113A-LUCIAN", 3, "CARD_TYPE:UNIT|哨兵|百炼")]
    [InlineData("p2-preflight-play-sfd-yone-precon-no-optional-assemble.fixture.json", "P1-UNIT-SFD-116-YONE", 5, "CARD_TYPE:UNIT|恶魔|百炼")]
    [InlineData("p2-preflight-play-sfd-ornn-no-optional-assemble-spellshield2.fixture.json", "P1-UNIT-SFD-ORNN", 4, "CARD_TYPE:UNIT|法盾2|百炼")]
    [InlineData("p2-preflight-play-sfd-ornn-alt-a-no-optional-assemble-spellshield2.fixture.json", "P1-UNIT-SFD-ORNN-A", 4, "CARD_TYPE:UNIT|法盾2|百炼")]
    [InlineData("p2-preflight-play-akshan-no-optional-assemble-no-orange-extra.fixture.json", "P1-UNIT-AKSHAN", 4, "CARD_TYPE:UNIT|哨兵|百炼")]
    [InlineData("p2-preflight-play-sfd-119-jax-no-optional-assemble.fixture.json", "P1-UNIT-SFD-119-JAX", 3, "CARD_TYPE:UNIT|百炼")]
    [InlineData("p2-preflight-play-sfd-119-jax-alt-a-no-optional-assemble.fixture.json", "P1-UNIT-SFD-119A-JAX", 3, "CARD_TYPE:UNIT|百炼")]
    [InlineData("p2-preflight-play-ogs-garen-elite-static-unit.fixture.json", "P1-UNIT-OGS-GAREN", 5, "CARD_TYPE:UNIT|精锐")]
    [InlineData("p2-preflight-play-rhasa-full-cost-spirit-unit.fixture.json", "P1-UNIT-RHASA", 6, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-leblanc-alt-a-back-row-ephemeral-static.fixture.json", "P1-UNIT-LEBLANC-A-BACKROW", 4, "CARD_TYPE:UNIT|后排")]
    [InlineData("p2-preflight-play-farron-captain-trifarian-static-assault.fixture.json", "P1-UNIT-FARRON-CAPTAIN", 5, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-rude-pirate-no-optional-discard.fixture.json", "P1-UNIT-RUDE-PIRATE", 5, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-raging-drake-next-spell-cost-static.fixture.json", "P1-UNIT-RAGING-DRAKE", 4, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-adaptive-robot-conquer-boon-static.fixture.json", "P1-UNIT-ADAPTIVE-ROBOT", 3, "CARD_TYPE:UNIT|机械")]
    [InlineData("p2-preflight-play-eclipse-vanguard-stun-trigger-static.fixture.json", "P1-UNIT-ECLIPSE-VANGUARD", 7, "CARD_TYPE:UNIT|鸟类")]
    [InlineData("p2-preflight-play-ogn-volibear-spellshield2-keyword-unit.fixture.json", "P1-UNIT-OGN-041-VOLIBEAR", 9, "CARD_TYPE:UNIT|法盾2")]
    [InlineData("p2-preflight-play-ogn-volibear-alt-a-spellshield2-keyword-unit.fixture.json", "P1-UNIT-OGN-041A-VOLIBEAR", 9, "CARD_TYPE:UNIT|法盾2")]
    [InlineData("p2-preflight-play-ogn-volibear-steadfast3-barrier-keyword-unit.fixture.json", "P1-UNIT-OGN-158-VOLIBEAR", 10, "CARD_TYPE:UNIT|坚守3|壁垒")]
    [InlineData("p2-preflight-play-ogn-volibear-alt-a-steadfast3-barrier-keyword-unit.fixture.json", "P1-UNIT-OGN-158A-VOLIBEAR", 10, "CARD_TYPE:UNIT|坚守3|壁垒")]
    [InlineData("p2-preflight-play-shen-reaction-steadfast2-barrier-keyword-unit.fixture.json", "P1-UNIT-SHEN", 3, "CARD_TYPE:UNIT|反应|坚守2|壁垒")]
    [InlineData("p2-preflight-play-sfd-rengar-reaction-overwhelm-keyword-unit.fixture.json", "P1-UNIT-SFD-025-RENGAR", 3, "CARD_TYPE:UNIT|反应|强攻2|猫科")]
    [InlineData("p2-preflight-play-sfd-rengar-promo-reaction-overwhelm-keyword-unit.fixture.json", "P1-UNIT-SFD-025A-RENGAR", 3, "CARD_TYPE:UNIT|反应|强攻2|猫科")]
    [InlineData("p2-preflight-play-ogn-sett-barrier-keyword-unit.fixture.json", "P1-UNIT-OGN-240-SETT", 5, "CARD_TYPE:UNIT|壁垒")]
    [InlineData("p2-preflight-play-ogn-sett-alt-a-barrier-keyword-unit.fixture.json", "P1-UNIT-OGN-240A-SETT", 5, "CARD_TYPE:UNIT|壁垒")]
    [InlineData("p2-preflight-play-azure-glyph-golem-steadfast2-keyword-unit.fixture.json", "P1-UNIT-AZURE-GLYPH-GOLEM", 4, "CARD_TYPE:UNIT|坚守2")]
    [InlineData("p2-preflight-play-azure-glyph-golem-alt-a-steadfast2-keyword-unit.fixture.json", "P1-UNIT-AZURE-GLYPH-GOLEM-A", 4, "CARD_TYPE:UNIT|坚守2")]
    [InlineData("p2-preflight-play-poppy-spellshield-yordle-keyword-unit.fixture.json", "P1-UNIT-POPPY", 5, "CARD_TYPE:UNIT|法盾|约德尔人")]
    [InlineData("p2-preflight-play-poppy-alt-a-spellshield-yordle-keyword-unit.fixture.json", "P1-UNIT-POPPY-A", 5, "CARD_TYPE:UNIT|法盾|约德尔人")]
    [InlineData("p2-preflight-play-rampaging-soul-no-discard-spirit-unit.fixture.json", "P1-UNIT-RAMPAGING-SOUL", 4, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-rampaging-soul-discarded-hand-keyword-unit.fixture.json", "P1-UNIT-RAMPAGING-SOUL", 4, "CARD_TYPE:UNIT|强攻|游走|灵体")]
    [InlineData("p2-preflight-play-noxian-recruit-no-encourage-trifarian-unit.fixture.json", "P1-UNIT-NOXIAN-RECRUIT", 4, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-dangerous-duo-no-encourage-mechanical-unit.fixture.json", "P1-UNIT-DANGEROUS-DUO", 3, "CARD_TYPE:UNIT|机械")]
    [InlineData("p2-preflight-play-trifarian-gloryseeker-no-encourage-unit.fixture.json", "P1-UNIT-TRIFARIAN-GLORYSEEKER", 2, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-undead-legion-hand-spirit-unit.fixture.json", "P1-UNIT-UNDEAD-LEGION", 3, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-junkyard-bully-no-encourage-mechanical-unit.fixture.json", "P1-UNIT-JUNKYARD-BULLY", 5, "CARD_TYPE:UNIT|机械")]
    [InlineData("p2-preflight-play-vanguard-captain-no-encourage-elite-unit.fixture.json", "P1-UNIT-VANGUARD-CAPTAIN", 3, "CARD_TYPE:UNIT|精锐")]
    [InlineData("p2-preflight-play-ogn-darius-no-encourage-trifarian-unit.fixture.json", "P1-UNIT-OGN-DARIUS", 6, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-ogn-darius-alt-a-no-encourage-trifarian-unit.fixture.json", "P1-UNIT-OGN-DARIUS-A", 6, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-steadfast-sentinel-yordle-unit.fixture.json", "P1-UNIT-STEADFAST-SENTINEL", 1, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-alluring-faerie-unit.fixture.json", "P1-UNIT-ALLURING-FAERIE", 1, "CARD_TYPE:UNIT|仙灵")]
    [InlineData("p2-preflight-play-loyal-hound-dog-unit.fixture.json", "P1-UNIT-LOYAL-HOUND", 3, "CARD_TYPE:UNIT|犬形")]
    [InlineData("p2-preflight-play-yvna-assault2-unit.fixture.json", "P1-UNIT-YVNA", 1, "CARD_TYPE:UNIT|强攻2")]
    [InlineData("p2-preflight-play-skyvoice-wyrmling-dragon-unit.fixture.json", "P1-UNIT-SKYVOICE-WYRMLING", 8, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-petal-pixie-faerie-unit.fixture.json", "P1-UNIT-PETAL-PIXIE", 2, "CARD_TYPE:UNIT|仙灵")]
    [InlineData("p2-preflight-play-scarlet-pigeon-bird-unit.fixture.json", "P1-UNIT-SCARLET-PIGEON", 3, "CARD_TYPE:UNIT|鸟类")]
    [InlineData("p2-preflight-play-loyal-poro-unit.fixture.json", "P1-UNIT-LOYAL-PORO", 3, "CARD_TYPE:UNIT|魄罗")]
    [InlineData("p2-preflight-play-unl-leblanc-assault-unit.fixture.json", "P1-UNIT-UNL-LEBLANC", 3, "CARD_TYPE:UNIT|强攻")]
    [InlineData("p2-preflight-play-unl-leblanc-alt-a-assault-unit.fixture.json", "P1-UNIT-UNL-LEBLANC-A", 3, "CARD_TYPE:UNIT|强攻")]
    [InlineData("p2-preflight-play-bad-poro-pirate-unit.fixture.json", "P1-UNIT-BAD-PORO", 2, "CARD_TYPE:UNIT|海盗|魄罗")]
    [InlineData("p2-preflight-play-sfd-bad-poro-pirate-unit.fixture.json", "P1-UNIT-SFD-BAD-PORO", 2, "CARD_TYPE:UNIT|海盗|魄罗")]
    [InlineData("p2-preflight-play-unsung-hero-last-breath-static.fixture.json", "P1-UNIT-UNSUNG-HERO", 2, "CARD_TYPE:UNIT|精锐")]
    [InlineData("p2-preflight-play-yordle-explorer-rune-cost-static.fixture.json", "P1-UNIT-YORDLE-EXPLORER", 4, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-hunting-sea-crew-move-power-static.fixture.json", "P1-UNIT-HUNTING-SEA-CREW", 4, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-ghostly-centaur-friendly-destroyed-static.fixture.json", "P1-UNIT-GHOSTLY-CENTAUR", 5, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-mighty-faerie-move-payment-static.fixture.json", "P1-UNIT-MIGHTY-FAERIE", 4, "CARD_TYPE:UNIT|仙灵")]
    [InlineData("p2-preflight-play-sfd-vex-combat-spell-cost-static.fixture.json", "P1-UNIT-SFD-VEX-COMBAT", 5, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-siege-ram-trifarian-unit.fixture.json", "P1-UNIT-SIEGE-RAM", 5, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-vex-alt-a-keyword-unit.fixture.json", "P1-UNIT-VEX-A", 5, "CARD_TYPE:UNIT|坚守|壁垒|约德尔人")]
    [InlineData("p2-preflight-play-yuumi-faerie-cat-unit.fixture.json", "P1-UNIT-YUUMI", 1, "CARD_TYPE:UNIT|仙灵|猫科")]
    [InlineData("p2-preflight-play-unl-lillia-faerie-unit.fixture.json", "P1-UNIT-UNL-LILLIA", 4, "CARD_TYPE:UNIT|仙灵")]
    [InlineData("p2-preflight-play-unl-lillia-alt-a-faerie-unit.fixture.json", "P1-UNIT-UNL-LILLIA-A", 4, "CARD_TYPE:UNIT|仙灵")]
    [InlineData("p2-preflight-play-vilemaw-spider-unit.fixture.json", "P1-UNIT-VILEMAW", 8, "CARD_TYPE:UNIT|蜘蛛")]
    [InlineData("p2-preflight-play-noxian-saboteur-trifarian-unit.fixture.json", "P1-UNIT-NOXIAN-SABOTEUR", 3, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-reliable-siege-dog-elite-dog-unit.fixture.json", "P1-UNIT-RELIABLE-SIEGE-DOG", 2, "CARD_TYPE:UNIT|犬形|精锐")]
    [InlineData("p2-preflight-play-sfd-rumble-mechanical-yordle-unit.fixture.json", "P1-UNIT-SFD-RUMBLE", 4, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-sfd-rumble-alt-a-mechanical-yordle-unit.fixture.json", "P1-UNIT-SFD-RUMBLE-A", 4, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-prescient-mech-yordle-mechanical-unit.fixture.json", "P1-UNIT-PRESCIENT-MECH", 2, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-speeding-mech-yordle-mechanical-unit.fixture.json", "P1-UNIT-SPEEDING-MECH", 7, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-progress-glory-mechanical-unit.fixture.json", "P1-UNIT-PROGRESS-GLORY", 3, "CARD_TYPE:UNIT|机械")]
    [InlineData("p2-preflight-play-fluft-poro-activated-skill-unit.fixture.json", "P1-UNIT-FLUFT-PORO", 5, "CARD_TYPE:UNIT|魄罗")]
    [InlineData("p2-preflight-play-resonant-soul-destroy-trigger-static.fixture.json", "P1-UNIT-RESONANT-SOUL", 5, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-sharpshooter-pirate-attack-trigger-static.fixture.json", "P1-UNIT-SHARPSHOOTER-PIRATE", 3, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-dune-drake-attack-ready-enemy-static.fixture.json", "P1-UNIT-DUNE-DRAKE", 5, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-noxian-drummer-trifarian-move-trigger-static.fixture.json", "P1-UNIT-NOXIAN-DRUMMER", 3, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-tidecaller-standby-swap-static.fixture.json", "P1-UNIT-TIDE-CALLER", 2, "CARD_TYPE:UNIT|待命")]
    [InlineData("p2-preflight-play-ogn-darius-second-card-trigger-static.fixture.json", "P1-UNIT-OGN-DARIUS-SECOND-CARD", 5, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-ogn-darius-alt-a-second-card-trigger-static.fixture.json", "P1-UNIT-OGN-DARIUS-SECOND-CARD-A", 5, "CARD_TYPE:UNIT|崔法利")]
    [InlineData("p2-preflight-play-ogn-vayne-assault3-static.fixture.json", "P1-UNIT-OGN-VAYNE-ASSAULT3", 2, "CARD_TYPE:UNIT|强攻3")]
    [InlineData("p2-preflight-play-ogn-vi-roam-recycle-static.fixture.json", "P1-UNIT-OGN-VI-ROAM", 3, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-ogn-blitzcrank-barrier-static.fixture.json", "P1-UNIT-OGN-BLITZCRANK-BARRIER", 5, "CARD_TYPE:UNIT|壁垒|机械")]
    [InlineData("p2-preflight-play-ogn-caitlyn-back-row-static.fixture.json", "P1-UNIT-OGN-CAITLYN-BACKROW", 3, "CARD_TYPE:UNIT|后排")]
    [InlineData("p2-preflight-play-ogn-gemstone-seer-predict-static.fixture.json", "P1-UNIT-OGN-GEMSTONE-SEER", 3, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-sfd-050-azir-swap-skill-static.fixture.json", "P1-UNIT-SFD-050-AZIR", 6, "CARD_TYPE:UNIT|鸟类")]
    [InlineData("p2-preflight-play-sfd-050a-azir-swap-skill-static.fixture.json", "P1-UNIT-SFD-050A-AZIR", 6, "CARD_TYPE:UNIT|鸟类")]
    [InlineData("p2-preflight-play-ogn-ava-yordle-standby-static.fixture.json", "P1-UNIT-OGN-AVA-YORDLE", 4, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-ogn-mundo-graveyard-power-static.fixture.json", "P1-UNIT-OGN-MUNDO", 8, "CARD_TYPE:UNIT")]
    [InlineData("p2-preflight-play-ogn-heimerdinger-yordle-static.fixture.json", "P1-UNIT-OGN-HEIMERDINGER", 3, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-ogn-kaisa-roam-conquer-static.fixture.json", "P1-UNIT-OGN-KAISA-ROAM", 6, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-ogn-kaisa-alt-a-roam-conquer-static.fixture.json", "P1-UNIT-OGN-KAISA-ROAM-A", 6, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-ogn-teemo-standby-static.fixture.json", "P1-UNIT-OGN-TEEMO-STANDBY", 2, "CARD_TYPE:UNIT|待命|约德尔人")]
    [InlineData("p2-preflight-play-ogn-teemo-alt-a-standby-static.fixture.json", "P1-UNIT-OGN-TEEMO-STANDBY-A", 2, "CARD_TYPE:UNIT|待命|约德尔人")]
    [InlineData("p2-preflight-play-sfd-230-teemo-standby-static.fixture.json", "P1-UNIT-SFD-230-TEEMO", 2, "CARD_TYPE:UNIT|待命|约德尔人")]
    [InlineData("p2-preflight-play-sfd-230-promo-teemo-standby-static.fixture.json", "P1-UNIT-SFD-230-PROMO-TEEMO", 2, "CARD_TYPE:UNIT|待命|约德尔人")]
    [InlineData("p2-preflight-play-ogn-cithria-boon-trigger-static.fixture.json", "P1-UNIT-OGN-CITHRIA-BOON", 1, "CARD_TYPE:UNIT|精锐")]
    [InlineData("p2-preflight-play-ogn-anivia-attack-damage-static.fixture.json", "P1-UNIT-OGN-ANIVIA-ATTACK", 8, "CARD_TYPE:UNIT|鸟类")]
    [InlineData("p2-preflight-play-ogn-soulsipper-spirit-static.fixture.json", "P1-UNIT-OGN-SOULSIPPER", 5, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-ogn-commander-ledros-spirit-static.fixture.json", "P1-UNIT-OGN-COMMANDER-LEDROS", 8, "CARD_TYPE:UNIT|法盾|游走|灵体")]
    [InlineData("p2-preflight-play-ogn-karma-predict-static.fixture.json", "P1-UNIT-OGN-KARMA-PREDICT", 6, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-sfd-237-karma-predict-static.fixture.json", "P1-UNIT-SFD-237-KARMA", 6, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-sfd-237-promo-karma-predict-static.fixture.json", "P1-UNIT-SFD-237-PROMO-KARMA", 6, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-sfd-fizz-graveyard-spell-static.fixture.json", "P1-UNIT-SFD-FIZZ", 3, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-spring-messenger-experience-static.fixture.json", "P1-UNIT-SPRING-MESSENGER", 4, "CARD_TYPE:UNIT|狩猎")]
    [InlineData("p2-preflight-play-stern-sergeant-experience-static.fixture.json", "P1-UNIT-STERN-SERGEANT", 6, "CARD_TYPE:UNIT|精锐")]
    [InlineData("p2-preflight-play-diana-spell-duel-static.fixture.json", "P1-UNIT-DIANA-SPELL-DUEL", 3, "CARD_TYPE:UNIT|巨神峰")]
    [InlineData("p2-preflight-play-diana-alt-a-spell-duel-static.fixture.json", "P1-UNIT-DIANA-A-SPELL-DUEL", 3, "CARD_TYPE:UNIT|巨神峰")]
    [InlineData("p2-preflight-play-gentle-gem-dragon-rune-ready-static.fixture.json", "P1-UNIT-GENTLE-GEM-DRAGON", 8, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-ghost-matron-spirit-revive-static.fixture.json", "P1-UNIT-GHOST-MATRON", 4, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-ogn-karthus-spirit-last-breath-static.fixture.json", "P1-UNIT-OGN-KARTHUS", 3, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-gloompath-guard-spirit-hold-static.fixture.json", "P1-UNIT-GLOOMPATH-GUARD", 6, "CARD_TYPE:UNIT|灵体")]
    [InlineData("p2-preflight-play-sfd-089-rumble-mechanical-static.fixture.json", "P1-UNIT-SFD-089-RUMBLE", 4, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-sfd-089a-rumble-mechanical-static.fixture.json", "P1-UNIT-SFD-089A-RUMBLE", 4, "CARD_TYPE:UNIT|机械|约德尔人")]
    [InlineData("p2-preflight-play-merfolk-rabble-rouser-standby-static.fixture.json", "P1-UNIT-MERFOLK-RABBLE-ROUSER", 2, "CARD_TYPE:UNIT|待命")]
    [InlineData("p2-preflight-play-vilemaw-alt-a-spider-static.fixture.json", "P1-UNIT-VILEMAW-A", 8, "CARD_TYPE:UNIT|蜘蛛")]
    [InlineData("p2-preflight-play-unl-rengar-cat-static.fixture.json", "P1-UNIT-UNL-RENGAR", 6, "CARD_TYPE:UNIT|猫科")]
    [InlineData("p2-preflight-play-unl-rengar-alt-a-cat-static.fixture.json", "P1-UNIT-UNL-RENGAR-A", 6, "CARD_TYPE:UNIT|猫科")]
    [InlineData("p2-preflight-play-muddy-dredger-mechanical-static.fixture.json", "P1-UNIT-MUDDY-DREDGER", 1, "CARD_TYPE:UNIT|机械")]
    [InlineData("p2-preflight-play-nidalee-cat-static.fixture.json", "P1-UNIT-NIDALEE", 4, "CARD_TYPE:UNIT|猫科")]
    [InlineData("p2-preflight-play-evelynn-standby-back-row-static.fixture.json", "P1-UNIT-EVELYNN", 2, "CARD_TYPE:UNIT|后排|待命|恶魔")]
    [InlineData("p2-preflight-play-pyke-standby-back-row-static.fixture.json", "P1-UNIT-UNL-145-PYKE", 3, "CARD_TYPE:UNIT|后排|待命")]
    [InlineData("p2-preflight-play-pyke-alt-a-standby-back-row-static.fixture.json", "P1-UNIT-UNL-145A-PYKE", 3, "CARD_TYPE:UNIT|后排|待命")]
    [InlineData("p2-preflight-play-poppy-no-experience-ambush-barrier-static.fixture.json", "P1-UNIT-UNL-178-POPPY", 5, "CARD_TYPE:UNIT|壁垒|约德尔人")]
    [InlineData("p2-preflight-play-spirited-poro-attack-move-static.fixture.json", "P1-UNIT-SPIRITED-PORO", 1, "CARD_TYPE:UNIT|魄罗")]
    [InlineData("p2-preflight-play-poppy-alt-a-no-experience-ambush-barrier-static.fixture.json", "P1-UNIT-UNL-178A-POPPY", 5, "CARD_TYPE:UNIT|壁垒|约德尔人")]
    [InlineData("p2-preflight-play-vex-spellshield-stun-static.fixture.json", "P1-UNIT-UNL-150-VEX", 4, "CARD_TYPE:UNIT|法盾|约德尔人")]
    [InlineData("p2-preflight-play-vex-alt-a-spellshield-stun-static.fixture.json", "P1-UNIT-UNL-150A-VEX", 4, "CARD_TYPE:UNIT|法盾|约德尔人")]
    [InlineData("p2-preflight-play-ancient-dragon-lethal-damage-static.fixture.json", "P1-UNIT-ANCIENT-DRAGON", 10, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-ancient-dragon-alt-a-lethal-damage-static.fixture.json", "P1-UNIT-ANCIENT-DRAGON-A", 10, "CARD_TYPE:UNIT|龙")]
    [InlineData("p2-preflight-play-moving-perch-spellshield-token-static.fixture.json", "P1-UNIT-MOVING-PERCH", 6, "CARD_TYPE:UNIT|法盾")]
    [InlineData("p2-preflight-play-safety-inspector-experience-destroy-static.fixture.json", "P1-UNIT-SAFETY-INSPECTOR", 3, "CARD_TYPE:UNIT|约德尔人")]
    [InlineData("p2-preflight-play-starhound-graveyard-return-static.fixture.json", "P1-UNIT-STARHOUND", 6, "CARD_TYPE:UNIT|犬形")]
    [InlineData("p2-preflight-play-masked-attendant-ephemeral-copy-static.fixture.json", "P1-UNIT-MASKED-ATTENDANT", 1, "CARD_TYPE:UNIT|待命|瞬息")]
    [InlineData("p2-preflight-play-atakhan-no-optional-destroy-roam-static.fixture.json", "P1-UNIT-ATAKHAN", 7, "CARD_TYPE:UNIT|游走")]
    [InlineData("p2-preflight-play-ivern-trait-choice-score-static.fixture.json", "P1-UNIT-IVERN-TRAIT", 6, "CARD_TYPE:UNIT|犬形")]
    [InlineData("p2-preflight-play-ivern-alt-a-trait-choice-score-static.fixture.json", "P1-UNIT-IVERN-A-TRAIT", 6, "CARD_TYPE:UNIT|魄罗")]
    [InlineData("p2-preflight-play-sly-salamander-experience-keyword-unit.fixture.json", "P1-UNIT-SLY-SALAMANDER", 5, "CARD_TYPE:UNIT|游走")]
    public async Task CoreRuleEnginePlaysKeywordOnlySourceUnit(
        string fixtureFileName,
        string sourceObjectId,
        int expectedPower,
        string expectedTags)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        if (fixtureFileName.Contains("ancient-dragon") && Environment.GetEnvironmentVariable("RIFTBOUND_ELDER_EVIDENCE") is { } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, fixtureFileName + ".actual.json"), JsonSerializer.Serialize(result));
        }
        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(fixtureFileName.Contains("masked-attendant")
            ? [sourceObjectId, sourceObjectId + "-TOKEN-001", sourceObjectId + "-TOKEN-002"] : [sourceObjectId],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        // DOC-04 363-365: Mundo's continuous graveyard bonus is not printed/base power.
        if (fixtureFileName == "p2-preflight-play-ogn-mundo-graveyard-power-static.fixture.json")
        {
            Assert.Equal(6, result.FinalState.CardObjects[sourceObjectId].Power);
            Assert.Equal(expectedPower, CurrentPowerTargetTests.Snapshot(result.FinalState).GetProperty("players")
                .GetProperty("P1").GetProperty("objects").GetProperty(sourceObjectId).GetProperty("effectivePower").GetInt32());
        }
        else Assert.Equal(expectedPower, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(expectedTags.Split('|'), result.FinalState.CardObjects[sourceObjectId].Tags);
    }

    [Theory]
    [InlineData(2, "P1-UNIT-PLUCKY-PORO", "OGN·013/298", "P1-BASE-PLUCKY-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-MIGHTY-PORO", "OGN·052/298", "P1-BASE-MIGHTY-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-ASSAULT-PORO", "OGN·210/298", "P1-BASE-ASSAULT-PORO-TARGET-001")]
    [InlineData(5, "P1-UNIT-FIERCE-FIRST-MATE", "OGN·215/298", "P1-BASE-FIERCE-FIRST-MATE-TARGET-001")]
    [InlineData(6, "P1-UNIT-ZEPHYR-SAGE", "OGS·005/024", "P1-BASE-ZEPHYR-SAGE-TARGET-001")]
    [InlineData(3, "P1-UNIT-PAKAA-CUB", "OGN·135/298", "P1-BASE-PAKAA-CUB-TARGET-001")]
    [InlineData(4, "P1-UNIT-NAVORI-SCOUT", "SFD·037/221", "P1-BASE-NAVORI-SCOUT-TARGET-001")]
    [InlineData(4, "P1-UNIT-LAURENT-SWORDSMAN", "SFD·156/221", "P1-BASE-LAURENT-SWORDSMAN-TARGET-001")]
    [InlineData(5, "P1-UNIT-GLUTTONOUS-TOADFROG", "UNL-100/219", "P1-BASE-GLUTTONOUS-TOADFROG-TARGET-001")]
    [InlineData(3, "P1-UNIT-SENTINEL-ADEPT", "SFD·008/221", "P1-BASE-SENTINEL-ADEPT-TARGET-001")]
    [InlineData(5, "P1-UNIT-BATTLE-CHEF", "SFD·092/221", "P1-BASE-BATTLE-CHEF-TARGET-001")]
    [InlineData(2, "P1-UNIT-STOUT-PORO", "SFD·099/221", "P1-BASE-STOUT-PORO-TARGET-001")]
    [InlineData(6, "P1-UNIT-MASTER-BINGWEN", "SFD·127/221", "P1-BASE-MASTER-BINGWEN-TARGET-001")]
    [InlineData(2, "P1-UNIT-UNL-PLUCKY-PORO", "UNL-220/219", "P1-BASE-UNL-PLUCKY-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-UNL-STOUT-PORO", "UNL-223/219", "P1-BASE-UNL-STOUT-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-UNL-ASSAULT-PORO", "UNL-225/219", "P1-BASE-UNL-ASSAULT-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-MUTANT-KITTEN", "UNL-036/219", "P1-BASE-MUTANT-KITTEN-TARGET-001")]
    [InlineData(4, "P1-UNIT-BURLY-BRAWLER", "UNL-099/219", "P1-BASE-BURLY-BRAWLER-TARGET-001")]
    [InlineData(3, "P1-UNIT-LAURENT-BLADEGUARD", "SFD·096/221", "P1-BASE-LAURENT-BLADEGUARD-TARGET-001")]
    [InlineData(6, "P1-UNIT-GAREN", "OGS·007/024", "P1-BASE-GAREN-TARGET-001")]
    [InlineData(3, "P1-UNIT-SOLARI-GUARD", "OGN·054/298", "P1-BASE-SOLARI-GUARD-TARGET-001")]
    [InlineData(3, "P1-UNIT-AERIE-HEAD-FAN", "UNL-041/219", "P1-BASE-AERIE-HEAD-FAN-TARGET-001")]
    [InlineData(5, "P1-UNIT-VEX", "UNL-055/219", "P1-BASE-VEX-TARGET-001")]
    [InlineData(6, "P1-UNIT-WILDCLAW-BEASTMASTER", "UNL-057/219", "P1-BASE-WILDCLAW-BEASTMASTER-TARGET-001")]
    [InlineData(10, "P1-UNIT-HUGE-YORDLE", "SFD·055/221", "P1-BASE-HUGE-YORDLE-TARGET-001")]
    [InlineData(7, "P1-UNIT-TIANNA-CROWNGUARD", "SFD·060/221", "P1-BASE-TIANNA-CROWNGUARD-TARGET-001")]
    [InlineData(4, "P1-UNIT-JHIN-ROAM", "UNL-022/219", "P1-BASE-JHIN-ROAM-TARGET-001")]
    [InlineData(4, "P1-UNIT-JHIN-ROAM-A", "UNL-022a/219", "P1-BASE-JHIN-ROAM-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-VI", "UNL-030/219", "P1-BASE-VI-TARGET-001")]
    [InlineData(4, "P1-UNIT-VI-A", "UNL-030a/219", "P1-BASE-VI-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-LEBLANC", "UNL-090/219", "P1-BASE-LEBLANC-TARGET-001")]
    [InlineData(3, "P1-UNIT-ENTHUSIASTIC-ANNOUNCER", "UNL-043/219", "P1-BASE-ENTHUSIASTIC-ANNOUNCER-TARGET-001")]
    [InlineData(3, "P1-UNIT-MOSS-STEPPER", "UNL-047/219", "P1-BASE-MOSS-STEPPER-TARGET-001")]
    [InlineData(3, "P1-UNIT-TREVOR-DUTTONEL", "UNL-048/219", "P1-BASE-TREVOR-DUTTONEL-TARGET-001")]
    [InlineData(3, "P1-UNIT-WINDRUNNER-FOX", "UNL-075/219", "P1-BASE-WINDRUNNER-FOX-TARGET-001")]
    [InlineData(2, "P1-UNIT-CRYSTALHAND-HUNTER", "UNL-094/219", "P1-BASE-CRYSTALHAND-HUNTER-TARGET-001")]
    [InlineData(3, "P1-UNIT-FLAMECLAW", "UNL-016/219", "P1-BASE-FLAMECLAW-TARGET-001")]
    [InlineData(2, "P1-UNIT-WUJI-APPRENTICE", "UNL-040/219", "P1-BASE-WUJI-APPRENTICE-TARGET-001")]
    [InlineData(3, "P1-UNIT-ARENA-CROWD-FAVORITE", "UNL-102/219", "P1-BASE-ARENA-CROWD-FAVORITE-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-YI-HUNT", "UNL-113/219", "P1-BASE-UNL-YI-HUNT-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-YI-HUNT-A", "UNL-113a/219", "P1-BASE-UNL-YI-HUNT-A-TARGET-001")]
    [InlineData(5, "P1-UNIT-KHAZIX-HUNT", "UNL-119/219", "P1-BASE-KHAZIX-HUNT-TARGET-001")]
    [InlineData(5, "P1-UNIT-KHAZIX-HUNT-A", "UNL-119a/219", "P1-BASE-KHAZIX-HUNT-A-TARGET-001")]
    [InlineData(3, "P1-UNIT-BLACK-ROSE-AGENT", "UNL-152/219", "P1-BASE-BLACK-ROSE-AGENT-TARGET-001")]
    [InlineData(2, "P1-UNIT-STUNNING-GUARDIAN", "UNL-162/219", "P1-BASE-STUNNING-GUARDIAN-TARGET-001")]
    [InlineData(3, "P1-UNIT-GALIO", "UNL-171/219", "P1-BASE-GALIO-TARGET-001")]
    [InlineData(4, "P1-UNIT-RELL", "SFD·024/221", "P1-BASE-RELL-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-JAX", "SFD·054/221", "P1-BASE-SFD-JAX-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-JAX-A", "SFD·054a/221", "P1-BASE-SFD-JAX-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-GIANT-ARM-KATO", "SFD·112/221", "P1-BASE-GIANT-ARM-KATO-TARGET-001")]
    [InlineData(3, "P1-UNIT-XIN-ZHAO", "SFD·176/221", "P1-BASE-XIN-ZHAO-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-SIVIR-SPELLSHIELD2", "SFD·120/221", "P1-BASE-SFD-SIVIR-SPELLSHIELD2-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-SIVIR-SPELLSHIELD2-A", "SFD·120a/221", "P1-BASE-SFD-SIVIR-SPELLSHIELD2-A-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-DRAVEN", "SFD·148/221", "P1-BASE-SFD-DRAVEN-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-DRAVEN-A", "SFD·148a/221", "P1-BASE-SFD-DRAVEN-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-VAYNE", "SFD·223/221", "P1-BASE-SFD-VAYNE-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-VAYNE-PROMO", "SFD·223*/221", "P1-BASE-SFD-VAYNE-PROMO-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-IRELIA", "SFD·225/221", "P1-BASE-SFD-IRELIA-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-IRELIA-PROMO", "SFD·225*/221", "P1-BASE-SFD-IRELIA-PROMO-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-YONE", "SFD·233/221", "P1-BASE-SFD-YONE-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-YONE-PROMO", "SFD·233*/221", "P1-BASE-SFD-YONE-PROMO-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-YASUO", "SFD·235/221", "P1-BASE-SFD-YASUO-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-YASUO-PROMO", "SFD·235*/221", "P1-BASE-SFD-YASUO-PROMO-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-DARIUS", "SFD·236/221", "P1-BASE-SFD-DARIUS-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-DARIUS-PROMO", "SFD·236*/221", "P1-BASE-SFD-DARIUS-PROMO-TARGET-001")]
    [InlineData(3, "P1-UNIT-IMMORTAL-PHOENIX", "OGN·037/298", "P1-BASE-IMMORTAL-PHOENIX-TARGET-001")]
    [InlineData(8, "P1-UNIT-CORPSE-FLOWER-PREDATOR", "OGN·161/298", "P1-BASE-CORPSE-FLOWER-PREDATOR-TARGET-001")]
    [InlineData(7, "P1-UNIT-REVNA", "UNL-005/219", "P1-BASE-REVNA-TARGET-001")]
    [InlineData(6, "P1-UNIT-JUNGLE-ELEPHANT", "UNL-008/219", "P1-BASE-JUNGLE-ELEPHANT-TARGET-001")]
    [InlineData(2, "P1-UNIT-SAD-PORO", "SFD·036/221", "P1-BASE-SAD-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-UNL-SAD-PORO", "UNL-221/219", "P1-BASE-UNL-SAD-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-SCOUTING-WARHAWK", "OGN·216/298", "P1-BASE-SCOUTING-WARHAWK-TARGET-001")]
    [InlineData(4, "P1-UNIT-FEARLESS-VANGUARD", "SFD·093/221", "P1-BASE-FEARLESS-VANGUARD-TARGET-001")]
    [InlineData(3, "P1-UNIT-SNEAKY-SAILOR", "OGN·176/298", "P1-BASE-SNEAKY-SAILOR-TARGET-001")]
    [InlineData(6, "P1-UNIT-TERROR-SPIDER", "UNL-117/219", "P1-BASE-TERROR-SPIDER-TARGET-001")]
    [InlineData(4, "P1-UNIT-BANDLE-SOLDIER", "UNL-151/219", "P1-BASE-BANDLE-SOLDIER-TARGET-001")]
    [InlineData(7, "P1-UNIT-FIERCEWING", "SFD·094/221", "P1-BASE-FIERCEWING-TARGET-001")]
    [InlineData(4, "P1-UNIT-ROOSTING-DARK-DRAKE", "SFD·015/221", "P1-BASE-ROOSTING-DARK-DRAKE-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-MISS-FORTUNE", "OGN·193/298", "P1-BASE-OGN-MISS-FORTUNE-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-MISS-FORTUNE-A", "OGN·193a/298", "P1-BASE-OGN-MISS-FORTUNE-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-MISS-FORTUNE-B", "OGN·193b/298", "P1-BASE-OGN-MISS-FORTUNE-B-TARGET-001")]
    [InlineData(4, "P1-UNIT-TARIC", "OGN·074/298", "P1-BASE-TARIC-TARGET-001")]
    [InlineData(5, "P1-UNIT-LEE-SIN-STEADFAST", "OGN·078/298", "P1-BASE-LEE-SIN-STEADFAST-TARGET-001")]
    [InlineData(5, "P1-UNIT-LEE-SIN-STEADFAST-A", "OGN·078a/298", "P1-BASE-LEE-SIN-STEADFAST-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-LEONA-STEADFAST", "OGN·238/298", "P1-BASE-LEONA-STEADFAST-TARGET-001")]
    [InlineData(4, "P1-UNIT-LEONA-STEADFAST-A", "OGN·238a/298", "P1-BASE-LEONA-STEADFAST-A-TARGET-001")]
    [InlineData(6, "P1-UNIT-IRONCLAD-VANGUARD", "SFD·021/221", "P1-BASE-IRONCLAD-VANGUARD-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-LUCIAN", "SFD·028/221", "P1-BASE-SFD-LUCIAN-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-LUCIAN-A", "SFD·028a/221", "P1-BASE-SFD-LUCIAN-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-QIYANA", "OGN·155/298", "P1-BASE-QIYANA-TARGET-001")]
    [InlineData(6, "P1-UNIT-KAYN", "OGN·189/298", "P1-BASE-KAYN-TARGET-001")]
    [InlineData(4, "P1-UNIT-NOCTURNE", "OGN·194/298", "P1-BASE-NOCTURNE-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-YASUO", "OGN·205/298", "P1-BASE-OGN-YASUO-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-YASUO-A", "OGN·205a/298", "P1-BASE-OGN-YASUO-A-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-057-IRELIA", "SFD·057/221", "P1-BASE-SFD-057-IRELIA-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-057A-IRELIA", "SFD·057a/221", "P1-BASE-SFD-057A-IRELIA-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-113-LUCIAN", "SFD·113/221", "P1-BASE-SFD-113-LUCIAN-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-113A-LUCIAN", "SFD·113a/221", "P1-BASE-SFD-113A-LUCIAN-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-116-YONE", "SFD·116/221", "P1-BASE-SFD-116-YONE-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-ORNN", "SFD·085/221", "P1-BASE-SFD-ORNN-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-ORNN-A", "SFD·085a/221", "P1-BASE-SFD-ORNN-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-AKSHAN", "SFD·109/221", "P1-BASE-AKSHAN-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-119-JAX", "SFD·119/221", "P1-BASE-SFD-119-JAX-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-119A-JAX", "SFD·119a/221", "P1-BASE-SFD-119A-JAX-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGS-GAREN", "OGS·013/024", "P1-BASE-OGS-GAREN-TARGET-001")]
    [InlineData(10, "P1-UNIT-RHASA", "OGN·195/298", "P1-BASE-RHASA-TARGET-001")]
    [InlineData(4, "P1-UNIT-LEBLANC-A-BACKROW", "UNL-090a/219", "P1-BASE-LEBLANC-A-BACKROW-TARGET-001")]
    [InlineData(4, "P1-UNIT-FARRON-CAPTAIN", "OGN·015/298", "P1-BASE-FARRON-CAPTAIN-TARGET-001")]
    [InlineData(6, "P1-UNIT-RUDE-PIRATE", "OGN·002/298", "P1-BASE-RUDE-PIRATE-TARGET-001")]
    [InlineData(6, "P1-UNIT-RAGING-DRAKE", "OGN·031/298", "P1-BASE-RAGING-DRAKE-TARGET-001")]
    [InlineData(4, "P1-UNIT-ADAPTIVE-ROBOT", "OGN·056/298", "P1-BASE-ADAPTIVE-ROBOT-TARGET-001")]
    [InlineData(7, "P1-UNIT-ECLIPSE-VANGUARD", "OGN·059/298", "P1-BASE-ECLIPSE-VANGUARD-TARGET-001")]
    [InlineData(10, "P1-UNIT-OGN-041-VOLIBEAR", "OGN·041/298", "P1-BASE-OGN-041-VOLIBEAR-TARGET-001")]
    [InlineData(10, "P1-UNIT-OGN-041A-VOLIBEAR", "OGN·041a/298", "P1-BASE-OGN-041A-VOLIBEAR-TARGET-001")]
    [InlineData(12, "P1-UNIT-OGN-158-VOLIBEAR", "OGN·158/298", "P1-BASE-OGN-158-VOLIBEAR-TARGET-001")]
    [InlineData(12, "P1-UNIT-OGN-158A-VOLIBEAR", "OGN·158a/298", "P1-BASE-OGN-158A-VOLIBEAR-TARGET-001")]
    [InlineData(3, "P1-UNIT-SHEN", "OGN·241/298", "P1-BASE-SHEN-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-025-RENGAR", "SFD·025/221", "P1-BASE-SFD-025-RENGAR-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-025A-RENGAR", "SFD·025a/221·P", "P1-BASE-SFD-025A-RENGAR-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-240-SETT", "OGN·240/298", "P1-BASE-OGN-240-SETT-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-240A-SETT", "OGN·240a/298", "P1-BASE-OGN-240A-SETT-TARGET-001")]
    [InlineData(4, "P1-UNIT-AZURE-GLYPH-GOLEM", "UNL-087/219", "P1-BASE-AZURE-GLYPH-GOLEM-TARGET-001")]
    [InlineData(4, "P1-UNIT-AZURE-GLYPH-GOLEM-A", "UNL-087a/219", "P1-BASE-AZURE-GLYPH-GOLEM-A-TARGET-001")]
    [InlineData(5, "P1-UNIT-POPPY", "UNL-116/219", "P1-BASE-POPPY-TARGET-001")]
    [InlineData(5, "P1-UNIT-POPPY-A", "UNL-116a/219", "P1-BASE-POPPY-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-RAMPAGING-SOUL", "OGN·019/298", "P1-BASE-RAMPAGING-SOUL-TARGET-001")]
    [InlineData(4, "P1-UNIT-NOXIAN-RECRUIT", "OGN·012/298", "P1-BASE-NOXIAN-RECRUIT-TARGET-001")]
    [InlineData(3, "P1-UNIT-DANGEROUS-DUO", "OGN·016/298", "P1-BASE-DANGEROUS-DUO-TARGET-001")]
    [InlineData(2, "P1-UNIT-TRIFARIAN-GLORYSEEKER", "OGN·217/298", "P1-BASE-TRIFARIAN-GLORYSEEKER-TARGET-001")]
    [InlineData(3, "P1-UNIT-UNDEAD-LEGION", "UNL-025/219", "P1-BASE-UNDEAD-LEGION-TARGET-001")]
    [InlineData(5, "P1-UNIT-JUNKYARD-BULLY", "OGN·020/298", "P1-BASE-JUNKYARD-BULLY-TARGET-001")]
    [InlineData(3, "P1-UNIT-VANGUARD-CAPTAIN", "OGN·218/298", "P1-BASE-VANGUARD-CAPTAIN-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-DARIUS", "OGN·243/298", "P1-BASE-OGN-DARIUS-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-DARIUS-A", "OGN·243a/298", "P1-BASE-OGN-DARIUS-A-TARGET-001")]
    [InlineData(1, "P1-UNIT-STEADFAST-SENTINEL", "UNL-111/219", "P1-BASE-STEADFAST-SENTINEL-TARGET-001")]
    [InlineData(2, "P1-UNIT-ALLURING-FAERIE", "UNL-112/219", "P1-BASE-ALLURING-FAERIE-TARGET-001")]
    [InlineData(3, "P1-UNIT-LOYAL-HOUND", "SFD·126/221", "P1-BASE-LOYAL-HOUND-TARGET-001")]
    [InlineData(2, "P1-UNIT-YVNA", "UNL-002/219", "P1-BASE-YVNA-TARGET-001")]
    [InlineData(8, "P1-UNIT-SKYVOICE-WYRMLING", "UNL-027/219", "P1-BASE-SKYVOICE-WYRMLING-TARGET-001")]
    [InlineData(2, "P1-UNIT-PETAL-PIXIE", "UNL-076/219", "P1-BASE-PETAL-PIXIE-TARGET-001")]
    [InlineData(3, "P1-UNIT-SCARLET-PIGEON", "UNL-154/219", "P1-BASE-SCARLET-PIGEON-TARGET-001")]
    [InlineData(3, "P1-UNIT-LOYAL-PORO", "UNL-156/219", "P1-BASE-LOYAL-PORO-TARGET-001")]
    [InlineData(3, "P1-UNIT-UNL-LEBLANC", "UNL-172/219", "P1-BASE-UNL-LEBLANC-TARGET-001")]
    [InlineData(3, "P1-UNIT-UNL-LEBLANC-A", "UNL-172a/219", "P1-BASE-UNL-LEBLANC-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-BAD-PORO", "UNL-222/219", "P1-BASE-BAD-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-SFD-BAD-PORO", "SFD·069/221", "P1-BASE-SFD-BAD-PORO-TARGET-001")]
    [InlineData(2, "P1-UNIT-UNSUNG-HERO", "SFD·167/221", "P1-BASE-UNSUNG-HERO-TARGET-001")]
    [InlineData(4, "P1-UNIT-YORDLE-EXPLORER", "SFD·100/221", "P1-BASE-YORDLE-EXPLORER-TARGET-001")]
    [InlineData(4, "P1-UNIT-HUNTING-SEA-CREW", "SFD·137/221", "P1-BASE-HUNTING-SEA-CREW-TARGET-001")]
    [InlineData(6, "P1-UNIT-GHOSTLY-CENTAUR", "UNL-068/219", "P1-BASE-GHOSTLY-CENTAUR-TARGET-001")]
    [InlineData(4, "P1-UNIT-MIGHTY-FAERIE", "SFD·125/221", "P1-BASE-MIGHTY-FAERIE-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-VEX-COMBAT", "SFD·146/221", "P1-BASE-SFD-VEX-COMBAT-TARGET-001")]
    [InlineData(5, "P1-UNIT-SIEGE-RAM", "SFD·012/221", "P1-BASE-SIEGE-RAM-TARGET-001")]
    [InlineData(5, "P1-UNIT-VEX-A", "UNL-055a/219", "P1-BASE-VEX-A-TARGET-001")]
    [InlineData(3, "P1-UNIT-YUUMI", "UNL-056/219", "P1-BASE-YUUMI-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNL-LILLIA", "UNL-058/219", "P1-BASE-UNL-LILLIA-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNL-LILLIA-A", "UNL-058a/219", "P1-BASE-UNL-LILLIA-A-TARGET-001")]
    [InlineData(8, "P1-UNIT-VILEMAW", "UNL-060/219", "P1-BASE-VILEMAW-TARGET-001")]
    [InlineData(3, "P1-UNIT-NOXIAN-SABOTEUR", "OGN·018/298", "P1-BASE-NOXIAN-SABOTEUR-TARGET-001")]
    [InlineData(2, "P1-UNIT-RELIABLE-SIEGE-DOG", "SFD·159/221", "P1-BASE-RELIABLE-SIEGE-DOG-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-RUMBLE", "SFD·026/221", "P1-BASE-SFD-RUMBLE-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-RUMBLE-A", "SFD·026a/221", "P1-BASE-SFD-RUMBLE-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-PRESCIENT-MECH", "SFD·065/221", "P1-BASE-PRESCIENT-MECH-TARGET-001")]
    [InlineData(8, "P1-UNIT-SPEEDING-MECH", "SFD·071/221", "P1-BASE-SPEEDING-MECH-TARGET-001")]
    [InlineData(4, "P1-UNIT-PROGRESS-GLORY", "SFD·075/221", "P1-BASE-PROGRESS-GLORY-TARGET-001")]
    [InlineData(5, "P1-UNIT-FLUFT-PORO", "UNL-160/219", "P1-BASE-FLUFT-PORO-TARGET-001")]
    [InlineData(6, "P1-UNIT-RESONANT-SOUL", "OGN·118/298", "P1-BASE-RESONANT-SOUL-TARGET-001")]
    [InlineData(3, "P1-UNIT-SHARPSHOOTER-PIRATE", "OGN·130/298", "P1-BASE-SHARPSHOOTER-PIRATE-TARGET-001")]
    [InlineData(5, "P1-UNIT-DUNE-DRAKE", "OGN·131/298", "P1-BASE-DUNE-DRAKE-TARGET-001")]
    [InlineData(3, "P1-UNIT-NOXIAN-DRUMMER", "OGN·222/298", "P1-BASE-NOXIAN-DRUMMER-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-DARIUS-SECOND-CARD", "OGN·027/298", "P1-BASE-OGN-DARIUS-SECOND-CARD-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-DARIUS-SECOND-CARD-A", "OGN·027a/298", "P1-BASE-OGN-DARIUS-SECOND-CARD-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-VAYNE-ASSAULT3", "OGN·035/298", "P1-BASE-OGN-VAYNE-ASSAULT3-TARGET-001")]
    [InlineData(2, "P1-UNIT-OGN-VI-ROAM", "OGN·036/298", "P1-BASE-OGN-VI-ROAM-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-BLITZCRANK-BARRIER", "OGN·067/298", "P1-BASE-OGN-BLITZCRANK-BARRIER-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-CAITLYN-BACKROW", "OGN·068/298", "P1-BASE-OGN-CAITLYN-BACKROW-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-GEMSTONE-SEER", "OGN·100/298", "P1-BASE-OGN-GEMSTONE-SEER-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-050-AZIR", "SFD·050/221", "P1-BASE-SFD-050-AZIR-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-050A-AZIR", "SFD·050a/221", "P1-BASE-SFD-050A-AZIR-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-AVA-YORDLE", "OGN·107/298", "P1-BASE-OGN-AVA-YORDLE-TARGET-001")]
    [InlineData(8, "P1-UNIT-OGN-MUNDO", "OGN·109/298", "P1-BASE-OGN-MUNDO-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-HEIMERDINGER", "OGN·111/298", "P1-BASE-OGN-HEIMERDINGER-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-KAISA-ROAM", "OGN·112/298", "P1-BASE-OGN-KAISA-ROAM-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-KAISA-ROAM-A", "OGN·112a/298", "P1-BASE-OGN-KAISA-ROAM-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-OGN-TEEMO-STANDBY", "OGN·121/298", "P1-BASE-OGN-TEEMO-STANDBY-TARGET-001")]
    [InlineData(2, "P1-UNIT-OGN-TEEMO-STANDBY-A", "OGN·121a/298", "P1-BASE-OGN-TEEMO-STANDBY-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-SFD-230-TEEMO", "SFD·230/221", "P1-BASE-SFD-230-TEEMO-TARGET-001")]
    [InlineData(2, "P1-UNIT-SFD-230-PROMO-TEEMO", "SFD·230*/221", "P1-BASE-SFD-230-PROMO-TEEMO-TARGET-001")]
    [InlineData(2, "P1-UNIT-OGN-CITHRIA-BOON", "OGN·139/298", "P1-BASE-OGN-CITHRIA-BOON-TARGET-001")]
    [InlineData(7, "P1-UNIT-OGN-ANIVIA-ATTACK", "OGN·148/298", "P1-BASE-OGN-ANIVIA-ATTACK-TARGET-001")]
    [InlineData(8, "P1-UNIT-OGN-SOULSIPPER", "OGN·196/298", "P1-BASE-OGN-SOULSIPPER-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-COMMANDER-LEDROS", "OGN·231/298", "P1-BASE-OGN-COMMANDER-LEDROS-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-KARMA-PREDICT", "OGN·235/298", "P1-BASE-OGN-KARMA-PREDICT-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-237-KARMA", "SFD·237/221", "P1-BASE-SFD-237-KARMA-TARGET-001")]
    [InlineData(6, "P1-UNIT-SFD-237-PROMO-KARMA", "SFD·237*/221", "P1-BASE-SFD-237-PROMO-KARMA-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-FIZZ", "SFD·140/221", "P1-BASE-SFD-FIZZ-TARGET-001")]
    [InlineData(4, "P1-UNIT-SPRING-MESSENGER", "UNL-034/219", "P1-BASE-SPRING-MESSENGER-TARGET-001")]
    [InlineData(6, "P1-UNIT-STERN-SERGEANT", "UNL-157/219", "P1-BASE-STERN-SERGEANT-TARGET-001")]
    [InlineData(3, "P1-UNIT-DIANA-SPELL-DUEL", "UNL-079/219", "P1-BASE-DIANA-SPELL-DUEL-TARGET-001")]
    [InlineData(3, "P1-UNIT-DIANA-A-SPELL-DUEL", "UNL-079a/219", "P1-BASE-DIANA-A-SPELL-DUEL-TARGET-001")]
    [InlineData(8, "P1-UNIT-GENTLE-GEM-DRAGON", "UNL-104/219", "P1-BASE-GENTLE-GEM-DRAGON-TARGET-001")]
    [InlineData(4, "P1-UNIT-GHOST-MATRON", "OGN·226/298", "P1-BASE-GHOST-MATRON-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-KARTHUS", "OGN·236/298", "P1-BASE-OGN-KARTHUS-TARGET-001")]
    [InlineData(6, "P1-UNIT-GLOOMPATH-GUARD", "SFD·035/221", "P1-BASE-GLOOMPATH-GUARD-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-089-RUMBLE", "SFD·089/221", "P1-BASE-SFD-089-RUMBLE-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-089A-RUMBLE", "SFD·089a/221", "P1-BASE-SFD-089A-RUMBLE-TARGET-001")]
    [InlineData(2, "P1-UNIT-MERFOLK-RABBLE-ROUSER", "UNL-003/219", "P1-BASE-MERFOLK-RABBLE-ROUSER-TARGET-001")]
    [InlineData(8, "P1-UNIT-VILEMAW-A", "UNL-060a/219", "P1-BASE-VILEMAW-A-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNL-RENGAR", "UNL-120/219", "P1-BASE-UNL-RENGAR-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNL-RENGAR-A", "UNL-120a/219", "P1-BASE-UNL-RENGAR-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-MUDDY-DREDGER", "UNL-153/219", "P1-BASE-MUDDY-DREDGER-TARGET-001")]
    [InlineData(3, "P1-UNIT-NIDALEE", "UNL-114/219", "P1-BASE-NIDALEE-TARGET-001")]
    [InlineData(2, "P1-UNIT-EVELYNN", "UNL-141/219", "P1-BASE-EVELYNN-TARGET-001")]
    [InlineData(3, "P1-UNIT-UNL-145-PYKE", "UNL-145/219", "P1-BASE-UNL-145-PYKE-TARGET-001")]
    [InlineData(3, "P1-UNIT-UNL-145A-PYKE", "UNL-145a/219", "P1-BASE-UNL-145A-PYKE-TARGET-001")]
    [InlineData(6, "P1-UNIT-UNL-178-POPPY", "UNL-178/219", "P1-BASE-UNL-178-POPPY-TARGET-001")]
    [InlineData(2, "P1-UNIT-SPIRITED-PORO", "UNL-137/219", "P1-BASE-SPIRITED-PORO-TARGET-001")]
    [InlineData(6, "P1-UNIT-UNL-178A-POPPY", "UNL-178a/219", "P1-BASE-UNL-178A-POPPY-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-150-VEX", "UNL-150/219", "P1-BASE-UNL-150-VEX-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-150A-VEX", "UNL-150a/219", "P1-BASE-UNL-150A-VEX-TARGET-001")]
    [InlineData(12, "P1-UNIT-ANCIENT-DRAGON", "UNL-118/219", "P1-BASE-ANCIENT-DRAGON-TARGET-001")]
    [InlineData(12, "P1-UNIT-ANCIENT-DRAGON-A", "UNL-118a/219", "P1-BASE-ANCIENT-DRAGON-A-TARGET-001")]
    [InlineData(5, "P1-UNIT-MOVING-PERCH", "UNL-130/219", "P1-BASE-MOVING-PERCH-TARGET-001")]
    [InlineData(5, "P1-UNIT-SAFETY-INSPECTOR", "UNL-164/219", "P1-BASE-SAFETY-INSPECTOR-TARGET-001")]
    [InlineData(5, "P1-UNIT-STARHOUND", "UNL-167/219", "P1-BASE-STARHOUND-TARGET-001")]
    [InlineData(2, "P1-UNIT-MASKED-ATTENDANT", "UNL-081/219", "P1-BASE-MASKED-ATTENDANT-TARGET-001")]
    [InlineData(10, "P1-UNIT-ATAKHAN", "UNL-170/219", "P1-BASE-ATAKHAN-TARGET-001")]
    public Task CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId);

    [Fact]
    public async Task P4SpellshieldTaxDoesNotApplyToFriendlySpellTarget()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-secret-art-mercy-friendly-spellshield-no-tax.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Boon, CardObjectTags.Spellshield],
            result.FinalState.CardObjects["P1-BOON-SPELLSHIELD-UNIT-001"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BOON-SPELLSHIELD-UNIT-001"].Power);
        Assert.Equal(4, SecretArtMercyCurrentTests.Power(result.FinalState, "P1-BOON-SPELLSHIELD-UNIT-001"));
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(3, costPaidEvent.Payload["mana"]);
        Assert.Equal(3, costPaidEvent.Payload["baseMana"]);
        Assert.Equal(0, costPaidEvent.Payload["spellshieldTaxPower"]);
        Assert.Empty(Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public void P4ActivatedAbilityCatalogExposesRepresentativeSkillDefinitions()
    {
        Assert.True(P4ActivatedAbilityCatalog.GetAll().Count >= 2);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            "PAY_2_RED_DOUBLE_POWER",
            out var viDefinition));
        Assert.Equal("UNL-030/219", viDefinition.SourceCardNo);
        Assert.Equal("VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", viDefinition.EffectKind);
        Assert.Equal(2, viDefinition.ManaCost);
        Assert.Equal(1, viDefinition.PowerCost);
        Assert.Equal(0, viDefinition.RequiredTargetCount);
        Assert.False(viDefinition.RequiresBattlefieldSource);
        Assert.False(viDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, viDefinition.DamageAmount);
        Assert.False(viDefinition.AppliesSpellshieldTargetTax);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            "PAY_RED_EXHAUST_DAMAGE_3",
            out var xerathDefinition));
        Assert.Equal("UNL-026/219", xerathDefinition.SourceCardNo);
        Assert.Equal("XERATH_PAY_RED_EXHAUST_DAMAGE_3", xerathDefinition.EffectKind);
        Assert.Equal(0, xerathDefinition.ManaCost);
        Assert.Equal(1, xerathDefinition.PowerCost);
        Assert.Equal(1, xerathDefinition.RequiredTargetCount);
        Assert.True(xerathDefinition.RequiresBattlefieldSource);
        Assert.True(xerathDefinition.ExhaustsSourceAsCost);
        Assert.Equal(3, xerathDefinition.DamageAmount);
        Assert.True(xerathDefinition.AppliesSpellshieldTargetTax);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByEffectKind(
            "XERATH_PAY_RED_EXHAUST_DAMAGE_3",
            out var effectDefinition));
        Assert.Equal(xerathDefinition, effectDefinition);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            out var malzaharDefinition));
        Assert.Equal("OGN·113/298", malzaharDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceAbilityEffectKind, malzaharDefinition.EffectKind);
        Assert.Equal(0, malzaharDefinition.ManaCost);
        Assert.Equal(0, malzaharDefinition.PowerCost);
        Assert.Equal(1, malzaharDefinition.RequiredTargetCount);
        Assert.False(malzaharDefinition.RequiresBattlefieldSource);
        Assert.True(malzaharDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, malzaharDefinition.DamageAmount);
        Assert.False(malzaharDefinition.AppliesSpellshieldTargetTax);
        Assert.True(malzaharDefinition.IsResourceSkill);
        Assert.False(malzaharDefinition.PaymentOnlyResource);
        Assert.Equal(2, malzaharDefinition.GeneratedPower);
        Assert.True(malzaharDefinition.UsesTargetAsCost);
        Assert.Empty(malzaharDefinition.ResourceRestriction);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId,
            out var dragonSoulSageDefinition));
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageCardNo, dragonSoulSageDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityEffectKind, dragonSoulSageDefinition.EffectKind);
        Assert.Equal(0, dragonSoulSageDefinition.ManaCost);
        Assert.Equal(0, dragonSoulSageDefinition.PowerCost);
        Assert.Equal(0, dragonSoulSageDefinition.RequiredTargetCount);
        Assert.False(dragonSoulSageDefinition.RequiresBattlefieldSource);
        Assert.True(dragonSoulSageDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, dragonSoulSageDefinition.DamageAmount);
        Assert.False(dragonSoulSageDefinition.AppliesSpellshieldTargetTax);
        Assert.True(dragonSoulSageDefinition.IsResourceSkill);
        Assert.False(dragonSoulSageDefinition.PaymentOnlyResource);
        Assert.True(dragonSoulSageDefinition.ReactionSpeed);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, dragonSoulSageDefinition.GeneratedMana);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId,
            out var renataDefinition));
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascCardNo, renataDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascDrawAbilityEffectKind, renataDefinition.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascDrawManaCost, renataDefinition.ManaCost);
        Assert.Equal(0, renataDefinition.PowerCost);
        Assert.Equal(0, renataDefinition.RequiredTargetCount);
        Assert.True(renataDefinition.RequiresBattlefieldSource);
        Assert.False(renataDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, renataDefinition.DamageAmount);
        Assert.False(renataDefinition.AppliesSpellshieldTargetTax);
        var renataPowerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(renataDefinition);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascDrawBluePowerCost, renataPowerCostByTrait[RuneTrait.Blue]);
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(renataDefinition, P4ActivatedAbilityCatalog.RenataGlascCardNo));
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(renataDefinition, P4ActivatedAbilityCatalog.RenataGlascAltCardNo));

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId,
            out var renataScoreDefinition));
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascCardNo, renataScoreDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascScoreAbilityEffectKind, renataScoreDefinition.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascScoreManaCost, renataScoreDefinition.ManaCost);
        Assert.Equal(0, renataScoreDefinition.PowerCost);
        Assert.Equal(0, renataScoreDefinition.RequiredTargetCount);
        Assert.True(renataScoreDefinition.RequiresBattlefieldSource);
        Assert.True(renataScoreDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, renataScoreDefinition.DamageAmount);
        Assert.False(renataScoreDefinition.AppliesSpellshieldTargetTax);
        var renataScorePowerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(renataScoreDefinition);
        Assert.Equal(P4ActivatedAbilityCatalog.RenataGlascScoreBluePowerCost, renataScorePowerCostByTrait[RuneTrait.Blue]);
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(renataScoreDefinition, P4ActivatedAbilityCatalog.RenataGlascCardNo));
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(renataScoreDefinition, P4ActivatedAbilityCatalog.RenataGlascAltCardNo));

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
            out var crimsonRoseDefinition));
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseCardNo, crimsonRoseDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind, crimsonRoseDefinition.EffectKind);
        Assert.Equal(0, crimsonRoseDefinition.ManaCost);
        Assert.Equal(0, crimsonRoseDefinition.PowerCost);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyExperienceCost, crimsonRoseDefinition.ExperienceCost);
        Assert.Equal(1, crimsonRoseDefinition.RequiredTargetCount);
        Assert.False(crimsonRoseDefinition.RequiresBattlefieldSource);
        Assert.True(crimsonRoseDefinition.RequiresBaseEquipmentSource);
        Assert.True(crimsonRoseDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, crimsonRoseDefinition.DamageAmount);
        Assert.True(crimsonRoseDefinition.AppliesSpellshieldTargetTax);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId,
            out var fluftPoroDefinition));
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, fluftPoroDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, fluftPoroDefinition.EffectKind);
        Assert.Equal(0, fluftPoroDefinition.ManaCost);
        Assert.Equal(0, fluftPoroDefinition.PowerCost);
        Assert.Equal(0, fluftPoroDefinition.ExperienceCost);
        Assert.Equal(0, fluftPoroDefinition.RequiredTargetCount);
        Assert.True(fluftPoroDefinition.RequiresBattlefieldSource);
        Assert.False(fluftPoroDefinition.RequiresBaseEquipmentSource);
        Assert.True(fluftPoroDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, fluftPoroDefinition.DamageAmount);
        Assert.False(fluftPoroDefinition.AppliesSpellshieldTargetTax);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.ShadowStunAbilityId,
            out var shadowDefinition));
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowCardNo, shadowDefinition.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunAbilityEffectKind, shadowDefinition.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunManaCost, shadowDefinition.ManaCost);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunPowerCost, shadowDefinition.PowerCost);
        Assert.Equal(0, shadowDefinition.ExperienceCost);
        Assert.Equal(1, shadowDefinition.RequiredTargetCount);
        Assert.True(shadowDefinition.RequiresBattlefieldSource);
        Assert.True(shadowDefinition.ExhaustsSourceAsCost);
        Assert.Equal(0, shadowDefinition.DamageAmount);
        Assert.True(shadowDefinition.AppliesSpellshieldTargetTax);
        Assert.True(shadowDefinition.SwiftSpeed);
        Assert.False(shadowDefinition.ReactionSpeed);
    }

    [Fact]
    public async Task P4ActivatedAbilityCatalogListsRegisteredSkillsAndChecksBlossomText()
    {
        Assert.Equal(
            [
                "ANCIENT_STELE_REACTION_PAY_MANA_GAIN_GENERIC_POWER",
                "AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT",
                "BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_GAIN_GENERIC_POWER",
                "BOOK_REMOVE_EMPOWER_DRAW",
                "CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT",
                "DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER",
                "DRAGON_SOUL_SAGE_REACTION_EXHAUST_GAIN_1_MANA",
                "EGG_REACTION_GAIN_MANA",
                "ENERGY_CHANNEL_REACTION_EXHAUST_GAIN_1_MANA",
                "EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE",
                "FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS",
                "FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER",
                "GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD",
                "GLOWING_STONE_TRANSFER",
                "GOLD_TOKEN_SFD_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER",
                "GOLD_TOKEN_UNL_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER",
                "HEXTECH_ANOMALY_REACTION_PAY_GENERIC_POWER_GAIN_MANA",
                "HEXTECH_DISC_CREATE_ROBOT",
                "HEXTECH_EQUATION_EMPOWER_EQUIPMENT",
                "HONEYFRUIT_REACTION_EXHAUST_GAIN_GENERIC_POWER",
                "IMPERIAL_TOOL_POWER",
                "INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER",
                "JHIN_MOVE_TRIGGER_GAIN_1_MANA_1_POWER",
                "LUX_REACTION_EXHAUST_GAIN_2_SPELL_ONLY_MANA",
                "MALZAHAR_EXHAUST_DESTROY_FRIENDLY_GAIN_2_POWER",
                "NEXT_SPELL_ECHO",
                "OGN_090_298_UNIT_POWER_MODIFIER_UNTIL_END_OF_TURN",
                "OGN_242_298_DESTROY_FRIENDLY_UNIT_LOOK_TOP_PLAY_POWER_PLUS_ONE_RECYCLE_REST",
                "OGN_DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER",
                "OGN_FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER",
                "OGN_INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER",
                "OGN_POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER",
                "OGN_RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER",
                "OGN_UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER",
                "PAY_2_RED_DOUBLE_POWER",
                "PAY_RED_EXHAUST_DAMAGE_3",
                "POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER",
                "RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER",
                "RENATA_GLASC_PAY_1_BLUE_DRAW_1",
                "RENATA_GLASC_PAY_4_BLUE4_EXHAUST_SCORE_1",
                "SCRYING_BLOSSOM_DESTROY_INSIGHT_DRAW_EXPERIENCE",
                "SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER",
                "SKY_STRIDER_DISCARD_DAMAGE",
                "TUNDRA_HUNTER_READY",
                "UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER"
            ],
            P4ActivatedAbilityCatalog.GetAll()
                .Select(definition => definition.AbilityId)
                .OrderBy(abilityId => abilityId, StringComparer.Ordinal));

        var officialCatalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var card = officialCatalog.Cards.Single(card => card.CardNo == "UNL-136/219");
        Assert.Contains("摧毁此牌，支付{{1}}，{{横置}}", card.CardEffect);
        Assert.Contains("{{洞察2}}，然后抽一张牌。获得1经验", card.CardEffect);
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, out var ability));
        Assert.Equal(1, ability.ManaCost);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.False(ability.IsResourceSkill);
    }

    [Fact]
    public async Task P4ActivateAbilityRejectsUnknownIdWithoutChangingState()
    {
        var state = OfficialInsightSourceTests.Blossom();
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("unknown-skill", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand("B", "UNKNOWN_ACTIVATED_ABILITY", []), CancellationToken.None);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task P6LegendAbilityCatalogAuditsImplementedLegendActionRepresentativesAgainstOfficialText()
    {
        var surfaces = P6LegendAbilityCatalog.GetImplementedLegendActionSurfaces();

        Assert.Equal(5, surfaces.Count);
        Assert.Empty(P6LegendAbilityCatalog.GetDeferredSurfaces());
        Assert.Contains(surfaces, surface => surface.RequiresTarget);
        Assert.Contains(surfaces, surface => !surface.RequiresTarget);
        Assert.All(surfaces, surface =>
        {
            Assert.False(P4ActivatedAbilityCatalog.TryGetByAbilityId(surface.AbilityId, out _));
            Assert.StartsWith("LEGEND_DEFERRED_", surface.RetiredDeferredSurfaceId, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(surface.Reason));
            Assert.Contains("LEGEND_ACTION_DOMAIN", surface.Reason, StringComparison.Ordinal);
        });

        var officialCatalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var functionalUnits = FunctionalUnitBuilder.Build(officialCatalog.Cards);
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            officialCatalog.Cards,
            CardBehaviorRegistry.GetAll()
                .Select(definition => new ImplementedCardBehavior(
                    definition.CardNo,
                    definition.EffectKind,
                    definition.DisplayName))
                .ToArray());
        var behaviorSpecs = BehaviorSpecCatalogBuilder.Build(
            officialCatalog.Cards,
            functionalUnits,
            implementedBehaviors);

        foreach (var surface in surfaces)
        {
            var officialCard = officialCatalog.Cards.Single(card =>
                string.Equals(card.CardNo, surface.SourceCardNo, StringComparison.Ordinal));
            Assert.Equal("传奇", officialCard.CardCategoryName);
            Assert.Contains(surface.OfficialTextAnchor, officialCard.CardEffect, StringComparison.Ordinal);
            Assert.NotEmpty(RuleTextParser.Parse(officialCard).ActivatedAbilities);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(surface.SourceCardNo, out _));
            Assert.Contains(behaviorSpecs, spec =>
                string.Equals(spec.CardNo, surface.SourceCardNo, StringComparison.Ordinal)
                && string.Equals(spec.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal)
                && string.Equals(spec.ImplementedEffectKind, OfficialRuleDomainBehaviorCatalog.LegendActionDomainEffectKind, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task P79BattlefieldDefendMoveToBaseRejectsAttackerControlledBattlefield()
    {
        var baseState = BattlefieldDefendMoveToBaseState();
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-BATTLEFIELD-PLUNDER-ALLEY"] = cardObjects["P2-BATTLEFIELD-PLUNDER-ALLEY"] with
        {
            ControllerId = "P1"
        };
        var state = baseState with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-defend-move-dirty-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-PLUNDER-ALLEY",
                ["P1-BATTLEFIELD-PLUNDER-ATTACKER"],
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"],
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(["P2-BATTLEFIELD-PLUNDER-ALLEY", "P2-BATTLEFIELD-PLUNDER-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task P79BattlefieldConquerConsumesBoonAndDraws()
    {
        var state = BattlefieldConquerBoonDrawState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-boon-draw", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-SHIRANA-MONASTERY",
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"],
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-SHIRANA-ATTACKER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-SHIRANA-ATTACKER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        var attacker = result.State.CardObjects["P1-BATTLEFIELD-SHIRANA-ATTACKER"];
        Assert.Equal(3, attacker.Power);
        Assert.DoesNotContain(CardObjectTags.Boon, attacker.Tags);
        Assert.Equal(["P1-BATTLEFIELD-BOON-DRAW-CARD"], result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79BattlefieldConquerConsumesControlledBoonWhenDirtyBoonIsOpponentOwned()
    {
        var baseState = BattlefieldConquerBoonDrawState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Battlefields =
            [
                "P1-BATTLEFIELD-SHIRANA-ATTACKER",
                "P1-BATTLEFIELD-DIRTY-P2-BOON",
                "P1-BATTLEFIELD-LEGAL-BOON"
            ]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-SHIRANA-ATTACKER"] = cardObjects["P1-BATTLEFIELD-SHIRANA-ATTACKER"] with
        {
            Tags = [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt]
        };
        cardObjects["P1-BATTLEFIELD-DIRTY-P2-BOON"] = new(
            "P1-BATTLEFIELD-DIRTY-P2-BOON",
            cardNo: "SFD·125/221",
            power: 7,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
            ownerId: "P2",
            controllerId: "");
        cardObjects["P1-BATTLEFIELD-LEGAL-BOON"] = new(
            "P1-BATTLEFIELD-LEGAL-BOON",
            cardNo: "SFD·125/221",
            power: 5,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
            ownerId: "P1",
            controllerId: "P1");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-boon-dirty-target", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-SHIRANA-MONASTERY",
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"],
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-LEGAL-BOON", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-DIRTY-P2-BOON", StringComparison.Ordinal));
        Assert.Contains(CardObjectTags.Boon, result.State.CardObjects["P1-BATTLEFIELD-DIRTY-P2-BOON"].Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["P1-BATTLEFIELD-LEGAL-BOON"].Tags);
        Assert.Equal(7, result.State.CardObjects["P1-BATTLEFIELD-DIRTY-P2-BOON"].Power);
        Assert.Equal(4, result.State.CardObjects["P1-BATTLEFIELD-LEGAL-BOON"].Power);
        Assert.Equal(["P1-BATTLEFIELD-BOON-DRAW-CARD"], result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79BattlefieldConquerMillsTopTwoFromBattlefieldObject()
    {
        var state = BattlefieldConquerMillState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-mill", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-SCRAPYARD",
                ["P1-BATTLEFIELD-CONQUER-ATTACKER"],
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-BATTLEFIELD-MILL-003"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-BATTLEFIELD-MILL-001", "P1-BATTLEFIELD-MILL-002"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_MILL_TOP_TWO", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-SCRAPYARD", triggerEvent.Payload["battlefieldObjectId"]);
        var millEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_MILLED", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-MILL-001", "P1-BATTLEFIELD-MILL-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(millEvent.Payload["cardIds"]));
        var conqueredResolution = Assert.Single(
            result.State.BattlefieldResolutions,
            resolution => string.Equals(resolution.Kind, "CONQUERED", StringComparison.Ordinal));
        Assert.Equal(result.State.Tick, conqueredResolution.Tick);
        Assert.Equal("P1", conqueredResolution.PlayerId);
        Assert.Equal("P1-BATTLEFIELD-SCRAPYARD", conqueredResolution.BattlefieldObjectId);
        Assert.Contains("P1-BATTLEFIELD-CONQUER-ATTACKER", conqueredResolution.ParticipantObjectIds);
        Assert.Contains("P2-BATTLEFIELD-CONQUER-DEFENDER", conqueredResolution.ParticipantObjectIds);
    }

    [Fact]
    public async Task P79BattlefieldConquerDiscardsThenDraws()
    {
        var state = BattlefieldConquerDiscardDrawState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-discard-draw", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-ZAUN-SUMP",
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"],
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-BATTLEFIELD-DRAW-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-DISCARD-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_DISCARD_DRAW", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ZAUN-SUMP", triggerEvent.Payload["battlefieldObjectId"]);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-DISCARD-001", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 1));
    }

    [Fact]
    public async Task P79BattlefieldConquerDiscardDrawSkipsOpponentControlledHandCard()
    {
        var baseState = BattlefieldConquerDiscardDrawState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-BATTLEFIELD-DISCARD-DIRTY-P2-CARD", "P1-BATTLEFIELD-DISCARD-001"]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-DISCARD-DIRTY-P2-CARD"] = new(
            "P1-BATTLEFIELD-DISCARD-DIRTY-P2-CARD",
            ownerId: "P2",
            controllerId: "P2");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-discard-draw-dirty", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-ZAUN-SUMP",
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"],
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-BATTLEFIELD-DISCARD-DIRTY-P2-CARD", "P1-BATTLEFIELD-DRAW-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-DISCARD-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-DISCARD-001", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-DISCARD-DIRTY-P2-CARD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadiesAndDetachesEquipment()
    {
        var state = BattlefieldConquerReadyEquipmentState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ready-equipment", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-MOONVEIL-ALTAR",
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"],
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.BattlefieldConquerReadyEquipment, StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ARMAMENT", triggerEvent.Payload["equipmentObjectId"]);
        Assert.Equal(true, triggerEvent.Payload["detachesArmament"]);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-ARMAMENT", StringComparison.Ordinal));
        var detachEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_DETACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-BATTLEFIELD-ARMAMENT", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-EQUIPMENT-HOST", detachEvent.Payload["previousAttachedToObjectId"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-ARMAMENT"].IsExhausted);
        Assert.Null(result.State.CardObjects["P1-BATTLEFIELD-ARMAMENT"].AttachedToObjectId);
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyEquipmentSkipsOpponentOwnedEquipment()
    {
        var baseState = BattlefieldConquerReadyEquipmentState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base =
            [
                "P1-BATTLEFIELD-EQUIPMENT-HOST",
                "P1-BATTLEFIELD-AAA-DIRTY-P2-ARMAMENT",
                "P1-BATTLEFIELD-ARMAMENT"
            ]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-AAA-DIRTY-P2-ARMAMENT"] = new(
            "P1-BATTLEFIELD-AAA-DIRTY-P2-ARMAMENT",
            cardNo: "SFD·022/221",
            isExhausted: true,
            tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
            attachedToObjectId: "P1-BATTLEFIELD-EQUIPMENT-HOST",
            ownerId: "P2",
            controllerId: "");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ready-equipment-dirty", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-MOONVEIL-ALTAR",
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"],
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.BattlefieldConquerReadyEquipment, StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ARMAMENT", triggerEvent.Payload["equipmentObjectId"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-ARMAMENT"].IsExhausted);
        Assert.Null(result.State.CardObjects["P1-BATTLEFIELD-ARMAMENT"].AttachedToObjectId);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-AAA-DIRTY-P2-ARMAMENT"].IsExhausted);
        Assert.Equal(
            "P1-BATTLEFIELD-EQUIPMENT-HOST",
            result.State.CardObjects["P1-BATTLEFIELD-AAA-DIRTY-P2-ARMAMENT"].AttachedToObjectId);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-AAA-DIRTY-P2-ARMAMENT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79PetalPixieCountsFriendlyEphemeralUnitsAtSameBattlefieldForBattlePower()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-PETAL",
                        "P1-PETAL-PIXIE",
                        "P1-EPHEMERAL-SAME",
                        "P1-EPHEMERAL-STANDBY",
                        "P1-BATTLEFIELD-OTHER",
                        "P1-EPHEMERAL-OTHER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEFENDER", "P2-EPHEMERAL-SAME"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PETAL"] = new(
                    "P1-BATTLEFIELD-PETAL",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PETAL-PIXIE"] = new(
                    "P1-PETAL-PIXIE",
                    cardNo: "UNL-076/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "仙灵"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EPHEMERAL-SAME"] = new(
                    "P1-EPHEMERAL-SAME",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EPHEMERAL-STANDBY"] = new CardObjectState(
                    "P1-EPHEMERAL-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1") with { IsFaceDown = true },
                ["P1-BATTLEFIELD-OTHER"] = new(
                    "P1-BATTLEFIELD-OTHER",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EPHEMERAL-OTHER"] = new(
                    "P1-EPHEMERAL-OTHER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-EPHEMERAL-SAME"] = new(
                    "P2-EPHEMERAL-SAME",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PETAL"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-PETAL"),
                ["P1-PETAL-PIXIE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-PETAL"),
                ["P1-EPHEMERAL-SAME"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-PETAL"),
                ["P1-EPHEMERAL-STANDBY"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-PETAL"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-PETAL"),
                ["P2-EPHEMERAL-SAME"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-PETAL"),
                ["P1-BATTLEFIELD-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER"),
                ["P1-EPHEMERAL-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-PETAL")]
        };

        var staticAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-PETAL-PIXIE", StringComparison.Ordinal));
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("P1-PETAL-PIXIE", staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal("SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER", staticAura.EffectKind);
        Assert.Equal("UNL-076/219", staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal(["P1-EPHEMERAL-SAME"], staticAura.ParticipantObjectIds);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-petal-pixie-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-PETAL",
                ["P1-PETAL-PIXIE"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        CombatDamageAssignmentDto[] chosen = [new("P1-PETAL-PIXIE", "P2-DEFENDER", 1),
            new("P1-PETAL-PIXIE", "P2-EPHEMERAL-SAME", 2), new("P1-EPHEMERAL-SAME", "P2-DEFENDER", 1)];
        result = await new CoreRuleEngine().ResolveAsync(result.State, new("petal-allocation", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand(result.State.BattleState.BattleId!, result.State.BattleState.BattlefieldObjectId!, chosen), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-PETAL-PIXIE");
        Assert.Equal("P2-DEFENDER", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
    }

    [Theory]
    [InlineData("OGN·240/298")] [InlineData("RAD·183*/167")] [InlineData("RAD·183/167")]
    public async Task P79SettCountsFriendlyBoonUnitsAtSameBattlefieldForBattlePower(string cardNo)
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-SETT",
                        "P1-SETT",
                        "P1-BOON-SAME-1",
                        "P1-BOON-SAME-2",
                        "P1-NON-BOON-SAME",
                        "P1-BOON-STANDBY",
                        "P1-BATTLEFIELD-OTHER",
                        "P1-BOON-OTHER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEFENDER", "P2-BOON-SAME"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SETT"] = new(
                    "P1-BATTLEFIELD-SETT",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SETT"] = new(
                    "P1-SETT",
                    cardNo: cardNo,
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BOON-SAME-1"] = new(
                    "P1-BOON-SAME-1",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BOON-SAME-2"] = new(
                    "P1-BOON-SAME-2",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-NON-BOON-SAME"] = new(
                    "P1-NON-BOON-SAME",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BOON-STANDBY"] = new(
                    "P1-BOON-STANDBY",
                    isFaceDown: true,
                    cardNo: "OGN·121/298",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-OTHER"] = new(
                    "P1-BATTLEFIELD-OTHER",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BOON-OTHER"] = new(
                    "P1-BOON-OTHER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 10,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BOON-SAME"] = new(
                    "P2-BOON-SAME",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SETT"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P1-SETT"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P1-BOON-SAME-1"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P1-BOON-SAME-2"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P1-NON-BOON-SAME"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P1-BOON-STANDBY"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P2-BOON-SAME"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-SETT"),
                ["P1-BATTLEFIELD-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER"),
                ["P1-BOON-OTHER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-OTHER")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-SETT")]
        };

        var staticAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-SETT", StringComparison.Ordinal));
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("P1-SETT", staticAura.TargetObjectId);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal("SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER", staticAura.EffectKind);
        Assert.Equal(cardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal(["P1-BOON-SAME-1", "P1-BOON-SAME-2"], staticAura.ParticipantObjectIds);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sett-boon-count-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-SETT",
                ["P1-SETT"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-SETT");
        Assert.Equal("P2-DEFENDER", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(5, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(7, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(7, attackerDamageEvent.Payload["damage"]);
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
