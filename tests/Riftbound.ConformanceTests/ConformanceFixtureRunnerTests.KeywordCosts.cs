using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public void P4PermissionKeywordTimingSeparatesSwiftReactionAndOrdinaryWindows()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·004/298", out var swiftDefinition));
        // 训练有素 has printed Reaction. Use the ordinary 星芒凝汇 here.
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·105/298", out var ordinaryDefinition));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·064/298", out var reactionDefinition));

        var spellDuelState = P4SpellDuelFocusState();
        var swiftDecision = CardPermissionKeywordRules.EvaluatePlayTiming(
            spellDuelState,
            "P1",
            swiftDefinition);
        Assert.True(swiftDecision.IsAllowed);
        Assert.Equal(CardPermissionKeywordNames.Swift, swiftDecision.PermissionKeyword);

        var ordinaryDecision = CardPermissionKeywordRules.EvaluatePlayTiming(
            spellDuelState,
            "P1",
            ordinaryDefinition);
        Assert.False(ordinaryDecision.IsAllowed);
        Assert.Equal(CardPermissionKeywordNames.None, ordinaryDecision.PermissionKeyword);

        var priorityState = spellDuelState with
        {
            TimingState = TimingStates.NeutralClosed,
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            PriorityPlayerId = "P2",
            StackItems =
            [
                new StackItemState(
                    "STACK-P4-001",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-001"],
                    2)
            ]
        };

        var reactionDecision = CardPermissionKeywordRules.EvaluatePlayTiming(
            priorityState,
            "P2",
            reactionDefinition);
        Assert.True(reactionDecision.IsAllowed);
        Assert.Equal(CardPermissionKeywordNames.Reaction, reactionDecision.PermissionKeyword);

        var swiftDuringPriorityDecision = CardPermissionKeywordRules.EvaluatePlayTiming(
            priorityState,
            "P2",
            swiftDefinition);
        Assert.False(swiftDuringPriorityDecision.IsAllowed);
    }

    [Fact]
    public async Task P4SwiftKeywordAllowsCleaveInSpellDuelFocusWindow()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-swift-cleave-in-spell-duel-focus.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(TimingStates.SpellDuelOpen, result.FinalState.TimingState);
        Assert.Equal("P2", result.FinalState.FocusPlayerId);
        Assert.Empty(result.FinalState.PassedFocusPlayerIds);
    }

    [Fact]
    public async Task P6SwiftKeywordAllowsHextechRayInSpellDuelFocusWindow()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p6-play-swift-hextech-ray-in-spell-duel-focus.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(TimingStates.SpellDuelOpen, result.FinalState.TimingState);
        Assert.Equal("P2", result.FinalState.FocusPlayerId);
        Assert.Empty(result.FinalState.PassedFocusPlayerIds);
    }

    [Fact]
    public async Task P6ReactionKeywordAllowsProphetsOmenInPriorityWindow()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p6-play-reaction-prophets-omen-in-priority.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(TimingStates.NeutralOpen, result.FinalState.TimingState);
    }

    [Fact]
    public async Task P7SpellDuelReactionInheritsStackTimingContextWhenItCountersLastSpell()
    {
        var state = new MatchState(
            "p7-spell-duel-reaction-counter-room",
            1,
            12,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-NIGHTFALL-LULLABY"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            priorityPlayerId: "P2",
            stackItems:
            [
                new StackItemState(
                    "STACK-SPELL-DUEL-001",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-001"],
                    2,
                    timingContext: TimingStates.SpellDuelOpen)
            ]);
        state = PrintedCostFixture.Add(state, "P2", "UNL-190/219");

        var engine = new CoreRuleEngine();
        var playReaction = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-nightfall", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-NIGHTFALL-LULLABY",
                "UNL-190/219",
                ["STACK-SPELL-DUEL-001"]),
            CancellationToken.None);
        Assert.True(playReaction.Accepted, playReaction.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, playReaction.State.StackItems[^1].TimingContext);

        var p2Pass = await engine.ResolveAsync(
            playReaction.State,
            new PlayerIntent("intent-nightfall-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-nightfall-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.Empty(p1Pass.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, p1Pass.State.TimingState);
        Assert.Equal("P1", p1Pass.State.FocusPlayerId);
        Assert.Equal("P1", p1Pass.State.ActivePlayerId);
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_COUNTERED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("p2-preflight-play-wind-wall-counter-spell.fixture.json")]
    [InlineData("p2-preflight-play-blazing-drake-no-optional-haste.fixture.json")]
    [InlineData("p2-preflight-play-baby-shark-no-optional-haste.fixture.json")]
    [InlineData("p4-play-blazing-drake-haste-ready-insufficient-power-rejected.fixture.json")]
    [InlineData("p4-play-baby-shark-haste-ready.fixture.json")]
    [InlineData("p4-play-legion-rearguard-haste-ready.fixture.json")]
    [InlineData("p4-play-mr-root-haste-ready.fixture.json")]
    [InlineData("p4-play-mech-maniac-haste-ready.fixture.json")]
    [InlineData("p4-play-xersai-fish-haste-ready.fixture.json")]
    [InlineData("p4-play-karina-veraze-haste-ready.fixture.json")]
    [InlineData("p4-play-crimson-signet-treant-haste-ready.fixture.json")]
    [InlineData("p4-play-crimson-signet-treant-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-rengar-haste-ready.fixture.json")]
    [InlineData("p4-play-rengar-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-nilah-haste-ready.fixture.json")]
    [InlineData("p4-play-miss-fortune-haste-ready.fixture.json")]
    [InlineData("p4-play-miss-fortune-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-sivir-haste-ready.fixture.json")]
    [InlineData("p4-play-sivir-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-lillia-haste-ready.fixture.json")]
    [InlineData("p4-play-lillia-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-azir-haste-ready.fixture.json")]
    [InlineData("p4-play-azir-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-jinx-haste-ready.fixture.json")]
    [InlineData("p4-play-jinx-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-tasty-faerie-haste-ready.fixture.json")]
    [InlineData("p4-play-ekko-haste-ready.fixture.json")]
    [InlineData("p4-play-armed-assaulter-haste-ready.fixture.json")]
    [InlineData("p4-play-ancient-berserker-haste-ready.fixture.json")]
    [InlineData("p4-play-kraken-hunter-haste-ready.fixture.json")]
    [InlineData("p4-play-lee-sin-haste-ready.fixture.json")]
    [InlineData("p4-play-lee-sin-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-thousand-tailed-watcher-haste-ready.fixture.json")]
    [InlineData("p4-play-kaisa-haste-ready.fixture.json")]
    [InlineData("p4-play-kaisa-alt-a-haste-ready.fixture.json")]
    [InlineData("p4-play-reksai-haste-ready.fixture.json")]
    [InlineData("p4-play-reksai-alt-a-haste-ready.fixture.json")]
    public async Task P4PermissionKeywordsKeepExistingP2FixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForSivir()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·143/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);
        Assert.Equal(RuneTrait.Purple, hasteDefinition.HasteReadyPowerTrait);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sivir-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·143a/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);
        Assert.Equal(RuneTrait.Purple, hasteDefinition.HasteReadyPowerTrait);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sivir-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SIVIR"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    5,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            }
        };
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-sivir-haste-wrong-trait", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SIVIR",
                "SFD·143/221",
                [],
                OptionalCosts: [HasteOptionalCostNames.HasteReady]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(["P1-UNIT-SIVIR"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(5, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.DoesNotContain(RuneTrait.Purple, result.State.RunePools["P1"].PowerByTrait.Keys);
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForLillia()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-082/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-lillia-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForLilliaAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-082a/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-lillia-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForAzir()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·177/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-azir-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForAzirAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·177a/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-azir-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForJinxDiscardBranch()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·030/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-jinx-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-JINX-DISCARD-001", "P1-JINX-DISCARD-002"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("急速", result.FinalState.CardObjects["P1-UNIT-JINX"].Tags);
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForJinxAltADiscardBranch()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·030a/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-jinx-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-JINX-A-DISCARD-001", "P1-JINX-A-DISCARD-002"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("急速", result.FinalState.CardObjects["P1-UNIT-JINX-A"].Tags);
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForMissFortuneAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·162a/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-miss-fortune-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForMissFortune()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·162/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-miss-fortune-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForRengarAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-024a/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-rengar-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForRengar()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-024/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-rengar-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForNilah()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-115/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-nilah-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForCrimsonSignetTreantAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-029a/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-crimson-signet-treant-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForKaisaAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·039a/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-kaisa-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForReksaiAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·029a/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-reksai-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForReksai()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·029/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-reksai-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForKaisa()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·039/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-kaisa-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForEkko()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·110/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ekko-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·002/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-armed-assaulter-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForAncientBerserker()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·131/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ancient-berserker-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForKrakenHunter()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·150/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-kraken-hunter-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForLeeSin()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·151/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-lee-sin-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForLeeSinAltA()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·151a/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-lee-sin-alt-a-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·116/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-thousand-tailed-watcher-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForTastyFaerie()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·075/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-tasty-faerie-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForCrimsonSignetTreant()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-029/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-crimson-signet-treant-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForKarinaVeraze()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·179/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-karina-veraze-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForXersaiFish()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·103/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-xersai-fish-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForMechManiac()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·068/221", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mech-maniac-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForMrRoot()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-127/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mr-root-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForBabyShark()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-006/219", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-baby-shark-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForRepresentative()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·001/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-blazing-drake-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costPaidEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-UNIT-BLAZING-DRAKE", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("BLAZING_DRAKE_PLAY_UNIT_NO_OPTIONAL_HASTE", costPaidEvent.Payload["reason"]);
        Assert.Equal(5, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(6, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costPaidEvent.Payload["genericPower"]);
        Assert.Equal(1, costPaidEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingMana"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingPower"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costPaidEvent.Payload["powerByTrait"]);
        Assert.Equal(1, powerByTrait[RuneTrait.Red]);
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchPaysManaAndPowerForLegionRearguard()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·010/298", out var hasteDefinition));
        var profile = CardPermissionKeywordRules.BuildProfile(hasteDefinition);
        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-legion-rearguard-haste-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchRejectsInsufficientPower()
    {
        var state = PunishmentState(mana: 6) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-BLAZING-DRAKE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-haste-ready-branch", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-BLAZING-DRAKE",
                "OGN·001/298",
                [],
                OptionalCosts: [HasteOptionalCostNames.HasteReady]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-BLAZING-DRAKE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HasteOptionalReadyBranchRejectsInsufficientPowerFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-blazing-drake-haste-ready-insufficient-power-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-BLAZING-DRAKE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public void EchoKeywordProfileReadsOfficialResourceCost()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-061/219", out var centerStageDefinition));

        var profile = CardInteractionKeywordRules.BuildEchoProfile(centerStageDefinition);
        Assert.True(profile.HasEcho);
        Assert.Equal(2, profile.EchoManaCost);
        Assert.Equal(EchoKeywordProfileStatuses.Implemented, profile.Status);
        var cost = Assert.IsType<EchoCostRules.Cost>(EchoCostRules.PrintedFor(centerStageDefinition.CardNo));
        Assert.Equal(2, cost.Mana);
        Assert.Equal(0, cost.GenericPower);
        Assert.Empty(cost.TypedPower);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-007/219", out var punishmentDefinition));
        Assert.False(CardInteractionKeywordRules.BuildEchoProfile(punishmentDefinition).HasEcho);
        Assert.Null(EchoCostRules.PrintedFor(punishmentDefinition.CardNo));
    }

    [Theory]
    [InlineData("p2-preflight-play-center-stage-echo-draw-stack.fixture.json")]
    [InlineData("p4-play-center-stage-echo-insufficient-mana-rejected.fixture.json")]
    [InlineData("p4-play-punishment-illegal-echo-rejected.fixture.json")]
    [InlineData("p2-preflight-play-the-curtain-rises-echo-ready-unit.fixture.json")]
    [InlineData("p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json")]
    public async Task P4EchoKeywordKeepsExistingP2FixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Theory]
    [InlineData("p2-preflight-play-prophets-omen-draw-stack.fixture.json")]
    [InlineData("p2-preflight-play-incinerate-damage-stack.fixture.json")]
    [InlineData("p2-preflight-play-vengeance-destroy-unit-stack.fixture.json")]
    [InlineData("p2-preflight-play-rune-prison-stun-stack.fixture.json")]
    [InlineData("p2-preflight-play-cleave-overwhelm-attacking-power.fixture.json")]
    public async Task P4PrimitiveExecutorRepresentativeFixturesStayGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Theory]
    [InlineData("p2-preflight-play-assault-poro-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-mighty-poro-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-garen-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-mutant-kitten-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-leblanc-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-laurent-bladeguard-keyword-unit.fixture.json")]
    [InlineData("p4-move-unit-command-premodel-rejected.fixture.json")]
    [InlineData("p4-move-unit-roam-precise-battlefield.fixture.json")]
    [InlineData("p4-move-unit-combatant-source-rejected.fixture.json")]
    [InlineData("p4-declare-battle-single-combatants.fixture.json")]
    [InlineData("p4-declare-battle-bulwark-back-row-assignment.fixture.json")]
    [InlineData("p4-declare-battle-hunt-conquest-experience.fixture.json")]
    [InlineData("p4-declare-battle-empty-attackers-rejected.fixture.json")]
    [InlineData("p4-declare-battle-empty-defenders-rejected.fixture.json")]
    [InlineData("p4-declare-battle-missing-battlefield-rejected.fixture.json")]
    [InlineData("p4-declare-battle-unknown-battlefield-rejected.fixture.json")]
    [InlineData("p4-declare-battle-priority-window-rejected.fixture.json")]
    [InlineData("p4-declare-battle-unsupported-optional-cost-rejected.fixture.json")]
    [InlineData("p4-declare-battle-missing-optional-cost-rejected.fixture.json")]
    [InlineData("p4-declare-battle-attacker-outside-battlefield-rejected.fixture.json")]
    [InlineData("p4-declare-battle-defender-outside-battlefield-rejected.fixture.json")]
    [InlineData("p4-declare-battle-opponent-attacker-rejected.fixture.json")]
    [InlineData("p4-declare-battle-current-player-defender-rejected.fixture.json")]
    [InlineData("p4-declare-battle-duplicate-attacker-rejected.fixture.json")]
    [InlineData("p4-declare-battle-duplicate-defender-rejected.fixture.json")]
    [InlineData("p4-declare-battle-attacker-defender-overlap-rejected.fixture.json")]
    [InlineData("p4-declare-battle-non-active-player-rejected.fixture.json")]
    [InlineData("p4-declare-battle-non-main-phase-rejected.fixture.json")]
    [InlineData("p4-declare-battle-unknown-attacker-rejected.fixture.json")]
    [InlineData("p4-declare-battle-unknown-defender-rejected.fixture.json")]
    public async Task P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Theory]
    [InlineData("p2-preflight-play-gluttonous-toadfrog-keyword-unit.fixture.json")]
    [InlineData("p4-declare-battle-hunt-conquest-experience.fixture.json")]
    [InlineData("p2-preflight-play-moss-stepper-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-noxian-recruit-no-encourage-trifarian-unit.fixture.json")]
    [InlineData("p2-preflight-play-dangerous-duo-no-encourage-mechanical-unit.fixture.json")]
    [InlineData("p2-preflight-play-trifarian-gloryseeker-no-encourage-unit.fixture.json")]
    [InlineData("p2-preflight-play-plucky-poro-keyword-unit.fixture.json")]
    [InlineData("p2-preflight-play-sfd-ornn-no-optional-assemble-spellshield2.fixture.json")]
    [InlineData("p4-play-incinerate-spellshield-tax.fixture.json")]
    [InlineData("p4-play-incinerate-spellshield-tax-insufficient-rejected.fixture.json")]
    [InlineData("p4-play-noxian-recruit-encourage-cost-reduction.fixture.json")]
    [InlineData("p4-play-dangerous-duo-encourage-target-temp-might.fixture.json")]
    [InlineData("p4-play-junkyard-bully-encourage-discard-draw.fixture.json")]
    [InlineData("p4-play-vanguard-captain-encourage-create-minions.fixture.json")]
    [InlineData("p4-play-trifarian-gloryseeker-encourage-self-boon.fixture.json")]
    [InlineData("p4-play-moss-stepper-level3-spellshield.fixture.json")]
    [InlineData("p4-play-windrunner-fox-level3-roam.fixture.json")]
    [InlineData("p4-play-wuji-apprentice-level6-draw.fixture.json")]
    [InlineData("p4-play-unl-yi-level6-spellshield-roam.fixture.json")]
    [InlineData("p4-play-unl-yi-alt-a-level6-spellshield-roam.fixture.json")]
    [InlineData("p4-play-spirit-fire-multiple-spellshield-tax.fixture.json")]
    [InlineData("p4-play-spirit-fire-multiple-spellshield-tax-insufficient-rejected.fixture.json")]
    [InlineData("p4-play-secret-art-mercy-friendly-spellshield-no-tax.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill-target-rejected.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill-optional-cost-rejected.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill-power-missing-rejected.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill-non-vi-source-rejected.fixture.json")]
    [InlineData("p4-play-then-activate-vi-double-power-skill.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill-opponent-source-rejected.fixture.json")]
    [InlineData("p4-activate-vi-double-power-skill-source-not-field-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-spellshield-tax.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-spellshield-tax-insufficient-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-friendly-spellshield-no-tax.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-exhausted-source-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-missing-target-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-too-many-targets-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-optional-cost-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-non-xerath-source-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-non-unit-target-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-source-not-battlefield-rejected.fixture.json")]
    [InlineData("p4-activate-xerath-damage-skill-opponent-source-rejected.fixture.json")]
    public async Task P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen(string fixtureFileName)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task P4LevelThresholdAppliesMossStepperPowerAndSpellshieldAtThreeExperience()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-moss-stepper-level3-spellshield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-MOSS-STEPPER"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "犬形", "狩猎2"],
            result.FinalState.CardObjects["P1-UNIT-MOSS-STEPPER"].Tags);
        Assert.Equal(3, result.FinalState.PlayerExperience["P1"]);
    }

    [Fact]
    public async Task P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-wuji-apprentice-level6-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-WUJI-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-WUJI-APPRENTICE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(6, result.FinalState.PlayerExperience["P1"]);
        Assert.Contains("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task P4LevelThresholdAppliesYiSpellshieldAndRoamAtSixExperience()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-unl-yi-level6-spellshield-roam.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-UNL-YI-HUNT"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "游走", "狩猎2"],
            result.FinalState.CardObjects["P1-UNIT-UNL-YI-HUNT"].Tags);
        Assert.Equal(6, result.FinalState.PlayerExperience["P1"]);
    }

    [Fact]
    public async Task P4LevelThresholdAppliesYiAltASpellshieldAndRoamAtSixExperience()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-unl-yi-alt-a-level6-spellshield-roam.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-UNL-YI-HUNT-A"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "游走", "狩猎2"],
            result.FinalState.CardObjects["P1-UNIT-UNL-YI-HUNT-A"].Tags);
        Assert.Equal(6, result.FinalState.PlayerExperience["P1"]);
    }

    [Fact]
    public async Task P4LevelThresholdAppliesWindrunnerFoxPowerAndRoamAtThreeExperience()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-windrunner-fox-level3-roam.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-WINDRUNNER-FOX"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "游走", "犬形", "狩猎2"],
            result.FinalState.CardObjects["P1-UNIT-WINDRUNNER-FOX"].Tags);
        Assert.Equal(3, result.FinalState.PlayerExperience["P1"]);
    }

    [Fact]
    public async Task P4EncourageCostReductionPaysReducedManaAfterAnotherCardThisTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-noxian-recruit-encourage-cost-reduction.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(
            ["P1-EQUIPMENT-RAGE-SIGIL", "P1-UNIT-NOXIAN-RECRUIT"],
            result.FinalState.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P4EncourageSelfBoonGrantsBoonAfterAnotherCardThisTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-trifarian-gloryseeker-encourage-self-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-TRIFARIAN-GLORYSEEKER"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Boon, "崔法利"],
            result.FinalState.CardObjects["P1-UNIT-TRIFARIAN-GLORYSEEKER"].Tags);
        Assert.Contains("BOON_GRANTED", result.EventKinds);
    }

    [Fact]
    public async Task P4EncourageTargetTempMightRequiresPriorCardAndTarget()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dangerous-duo-encourage-target-temp-might.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-DANGEROUS-DUO-TARGET-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-BASE-DANGEROUS-DUO-TARGET-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-DANGEROUS-DUO"].Power);
        Assert.Contains("POWER_MODIFIED_UNTIL_END_OF_TURN", result.EventKinds);
    }

    [Fact]
    public async Task P4EncourageDiscardDrawRequiresPriorCardAndTwoHandTargets()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-junkyard-bully-encourage-discard-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(
            ["P1-JUNKYARD-DRAW-001", "P1-JUNKYARD-DRAW-002"],
            result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-JUNKYARD-DISCARD-001", "P1-JUNKYARD-DISCARD-002"],
            result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(5, result.FinalState.CardObjects["P1-UNIT-JUNKYARD-BULLY"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "机械"],
            result.FinalState.CardObjects["P1-UNIT-JUNKYARD-BULLY"].Tags);
        Assert.Equal(2, result.EventKinds.Count(kind => string.Equals(kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Contains("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task P4EncourageMinionCreationRequiresPriorCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-vanguard-captain-encourage-create-minions.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(
            [
                "P1-EQUIPMENT-RAGE-SIGIL",
                "P1-UNIT-VANGUARD-CAPTAIN",
                "P1-UNIT-VANGUARD-CAPTAIN-TOKEN-001",
                "P1-UNIT-VANGUARD-CAPTAIN-TOKEN-002"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-VANGUARD-CAPTAIN"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "精锐"],
            result.FinalState.CardObjects["P1-UNIT-VANGUARD-CAPTAIN"].Tags);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-VANGUARD-CAPTAIN-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily, "随从"],
            result.FinalState.CardObjects["P1-UNIT-VANGUARD-CAPTAIN-TOKEN-001"].Tags);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-VANGUARD-CAPTAIN-TOKEN-002"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily, "随从"],
            result.FinalState.CardObjects["P1-UNIT-VANGUARD-CAPTAIN-TOKEN-002"].Tags);
        Assert.Equal(2, result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsNoxianRecruitEncourageReductionWithoutPriorCardThisTurn()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-NOXIAN-RECRUIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-encourage-insufficient-without-prior-card", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-NOXIAN-RECRUIT",
                "OGN·012/298",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.False(result.State.PlayerCardsPlayedThisTurn.ContainsKey("P1"));
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-NOXIAN-RECRUIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDangerousDuoEncourageWhenPriorCardButMissingTarget()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 1
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-DANGEROUS-DUO"],
                    Base = ["P1-BASE-DANGEROUS-DUO-TARGET-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-DANGEROUS-DUO-TARGET-001"] = new(
                    "P1-BASE-DANGEROUS-DUO-TARGET-001",
                    power: 1,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-dangerous-duo-prior-card-missing-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-DANGEROUS-DUO",
                "OGN·016/298",
                []),
            CancellationToken.None);

        // CN 355.5.b: the permanent enters; its encourage skill chooses afterward.
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(2, result.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-UNIT-DANGEROUS-DUO", result.State.PlayerZones["P1"].Base);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Contains("P1-BASE-DANGEROUS-DUO-TARGET-001", choice.LegalObjectIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Single(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsJunkyardBullyEncourageWhenPriorCardButMissingDiscardTargets()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 1
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand =
                    [
                        "P1-UNIT-JUNKYARD-BULLY",
                        "P1-JUNKYARD-DISCARD-001",
                        "P1-JUNKYARD-DISCARD-002"
                    ],
                    MainDeck = ["P1-JUNKYARD-DRAW-001", "P1-JUNKYARD-DRAW-002"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-junkyard-bully-prior-card-missing-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-JUNKYARD-BULLY",
                "OGN·020/298",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(1, result.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-UNIT-JUNKYARD-BULLY", "P1-JUNKYARD-DISCARD-001", "P1-JUNKYARD-DISCARD-002"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JUNKYARD-DRAW-001", "P1-JUNKYARD-DRAW-002"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4CardsPlayedThisTurnMemoryResetsWhenTurnEnds()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 2,
                ["P2"] = 1
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-cards-played-reset-end-turn", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Empty(result.State.PlayerCardsPlayedThisTurn);
    }

    [Fact]
    public async Task P4SpellshieldTaxAddsPowerForEnemySpellTarget()
    {
        var state = P4SpellshieldTaxState(mana: 2, wardPower: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-spellshield-tax", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-INCINERATE",
                "OGS·003/024",
                ["P2-SPELLSHIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Single(result.State.StackItems);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costPaidEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-SPELL-INCINERATE", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, costPaidEvent.Payload["mana"]);
        Assert.Equal(2, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(2, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costPaidEvent.Payload["genericPower"]);
        Assert.Equal(1, costPaidEvent.Payload["totalPowerCost"]);
        Assert.Equal(1, costPaidEvent.Payload["spellshieldTaxPower"]);
        Assert.Equal(
            ["P2-SPELLSHIELD-UNIT-001"],
            Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task P4SpellshieldTaxAggregatesMultipleEnemySpellTargets()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-spirit-fire-multiple-spellshield-tax.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(
            ["P2-SPIRIT-FIRE-SPELLSHIELD-001", "P2-SPIRIT-FIRE-SPELLSHIELD2-001"],
            result.FinalState.PlayerZones["P2"].Graveyard);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(3, costPaidEvent.Payload["mana"]);
        Assert.Equal(3, costPaidEvent.Payload["baseMana"]);
        Assert.Equal(3, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(3, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(3, costPaidEvent.Payload["spellshieldTaxPower"]);
        Assert.Equal(
            ["P2-SPIRIT-FIRE-SPELLSHIELD-001", "P2-SPIRIT-FIRE-SPELLSHIELD2-001"],
            Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }
}
