using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConformanceFixtureRunnerTests
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
        foreach (var (playerId, actions) in fixture.Expected.PromptActions)
        {
            Assert.Equal(actions, result.Prompts[playerId].Actions);
        }
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
    public async Task CoreRuleEngineResolvesP2TurnStartPreflightFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.NotNull(fixture.Expected.FinalState);
        Assert.Equal(fixture.Expected.FinalState.TurnNumber, result.FinalState.TurnNumber);
        Assert.Equal(fixture.Expected.FinalState.ActivePlayerId, result.FinalState.ActivePlayerId);
        Assert.Equal(fixture.Expected.FinalState.TurnPlayerId, result.FinalState.TurnPlayerId);
        Assert.Equal(fixture.Expected.FinalState.Phase, result.FinalState.Phase);
        Assert.Equal(fixture.Expected.FinalState.TimingState, result.FinalState.TimingState);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Empty(result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Empty(result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            new[] { "P2-RUNE-001", "P2-RUNE-002" },
            result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineCallsAvailableRunesWhenRuneDeckIsShort()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-short-rune-deck.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Empty(result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Equal(new[] { "P2-RUNE-ONLY" }, result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineCallsExtraRuneForSecondActionPlayersFirstTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-first-p2-extra-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(new[] { "P2-RUNE-004" }, result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Equal(
            new[] { "P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003" },
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineAppliesBurnoutDuringTurnStartDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-burnout.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(1, result.FinalState.PlayerScores["P1"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P2"]);
        Assert.Empty(result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(new[] { "P2-RECYCLE-001" }, result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineAppliesRepeatedBurnoutWinImmediately()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-turn-start-burnout-empty-graveyard-wins.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal(MatchStatuses.Finished, result.FinalState.Status);
        Assert.Equal("P1", result.FinalState.WinnerPlayerId);
        Assert.Equal(8, result.FinalState.PlayerScores["P1"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P2"]);
        Assert.Equal("TURN_START", result.FinalState.Phase);
        Assert.Empty(result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(new[] { "WAIT" }, result.Prompts["P1"].Actions);
        Assert.Equal(new[] { "WAIT" }, result.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task CoreRuleEngineAdvancesEndTurnToNextTurnStart()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-end-turn-advances-to-next-start.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.NotNull(fixture.Expected.FinalState);
        Assert.Equal(fixture.Expected.FinalState.TurnNumber, result.FinalState.TurnNumber);
        Assert.Equal(fixture.Expected.FinalState.ActivePlayerId, result.FinalState.ActivePlayerId);
        Assert.Equal(fixture.Expected.FinalState.TurnPlayerId, result.FinalState.TurnPlayerId);
        Assert.Equal(fixture.Expected.FinalState.Phase, result.FinalState.Phase);
        Assert.Equal(fixture.Expected.FinalState.TimingState, result.FinalState.TimingState);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P1"]);
        Assert.Equal(0, result.FinalState.PlayerScores["P2"]);
        Assert.Equal(new[] { "P2-RUNE-004" }, result.FinalState.PlayerZones["P2"].RuneDeck);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            new[] { "P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003" },
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "END_TURN" }, result.Prompts["P2"].Actions);
        Assert.False(result.Prompts["P1"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineAppliesTurnEndSpecialCleanup()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-end-turn-special-cleanup.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "CLEANUP_REPEATED", StringComparison.Ordinal));
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "UNTIL_END_OF_TURN_EXPIRED", StringComparison.Ordinal));
        Assert.Equal(4, result.FinalState.TurnNumber);
        Assert.Equal("P2", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P2"]);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-DAMAGED"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-UNIT-DAMAGED"].UntilEndOfTurnEffects);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-BUFFED"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-UNIT-BUFFED"].UntilEndOfTurnEffects);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-MAIN-001" }, result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineRepeatsCleanupUntilStable()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-cleanup-repeats-until-stable.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.EventKinds, eventKind => string.Equals(eventKind, "UNTIL_END_OF_TURN_EXPIRED", StringComparison.Ordinal));
        Assert.Single(result.EventKinds, eventKind => string.Equals(eventKind, "CLEANUP_REPEATED", StringComparison.Ordinal));
        Assert.Equal(6, result.FinalState.TurnNumber);
        Assert.Equal("P2", result.FinalState.TurnPlayerId);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-DAMAGED"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-UNIT-DAMAGED"].UntilEndOfTurnEffects);
        Assert.Equal(new[] { "P2-RUNE-001", "P2-RUNE-002" }, result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPunishmentThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-punishment-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPunishmentAgainstBaseUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineBanishesUnitWhenPunishmentDamageWouldDestroyIt()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-punishment-lethal-damage-banishes-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Banished);
    }

    [Fact]
    public async Task CoreRuleEngineBanishesPunishmentTargetDestroyedLaterThisTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-punishment-banishes-if-destroyed-later.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Banished);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineDrawsWhenShatteredFireDestroysTarget()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-shattered-fire-draws-after-lethal-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-MAIN-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineDoesNotDrawWhenShatteredFireTargetSurvives()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-shattered-fire-does-not-draw-without-destroy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStarfallAgainstTwoUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-starfall-damages-two-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsStarfallToDamageSameUnitTwice()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-starfall-can-damage-same-unit-twice.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDuelMutualPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-duel-mutual-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-DUEL-001"].Damage);
        Assert.DoesNotContain("P2-UNIT-DUEL-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGentlemanDuelPowerThenMutualDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-GENTLEMAN-001"].Damage);
        Assert.Equal(5, result.FinalState.CardObjects["P1-UNIT-GENTLEMAN-001"].Power);
        Assert.DoesNotContain("P2-UNIT-GENTLEMAN-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMarchingOrdersEchoMutualPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-marching-orders-echo-mutual-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(6, result.FinalState.CardObjects["P1-BASE-MARCHING-001"].Damage);
        Assert.DoesNotContain("P2-BATTLEFIELD-MARCHING-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMarchingOrdersAgainstEnemyBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MARCHING-ORDERS"],
                    Battlefields = ["P1-UNIT-MARCHING-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-MARCHING-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MARCHING-001"] = new("P1-UNIT-MARCHING-001", power: 4),
                ["P2-BASE-MARCHING-001"] = new("P2-BASE-MARCHING-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-marching-orders-enemy-base", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MARCHING-ORDERS",
                "SFD·114/221",
                ["P1-UNIT-MARCHING-001", "P2-BASE-MARCHING-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MARCHING-ORDERS"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDuelWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-DUEL"],
                    Battlefields = ["P1-UNIT-DUEL-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-DUEL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DUEL-001"] = new("P1-UNIT-DUEL-001", power: 4),
                ["P2-UNIT-DUEL-001"] = new("P2-UNIT-DUEL-001", power: 2)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-duel-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-DUEL",
                "OGN·128/298",
                ["P2-UNIT-DUEL-001", "P1-UNIT-DUEL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-DUEL"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysClashOfGiantsMutualPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-clash-of-giants-mutual-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-GIANT-001"].Damage);
        Assert.DoesNotContain("P2-BATTLEFIELD-GIANT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysIcathianRainAgainstSameUnitSixTimes()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-icathian-rain-can-hit-same-unit-six-times.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBladeWhirlwindAgainstAllBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blade-whirlwind-damage-all-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineDestroysUnitsAfterLethalBladeWhirlwindDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-blade-whirlwind-lethal-damage-destroys-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStayAwayStunAndDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stay-away-stun-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["STUNNED"], result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDisposalOrderDrawMode()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-disposal-order-draw-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-MAIN-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDisposalOrderRecycleMode()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-MAIN-001", "P2-GRAVE-003", "P2-GRAVE-001"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMeditationBaseDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-meditation-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-MAIN-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSalvageDrawWithoutDestroyingEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-salvage-draw-no-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Contains("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKingOfTheHillDrawsBaseCardOnly()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMeditationExhaustFriendlyUnitExtraDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-meditation-exhaust-friendly-extra-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.True(result.FinalState.CardObjects["P1-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMeditationWhenOptionalCostTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MEDITATION"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", isExhausted: false)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-meditation-enemy-optional-cost-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MEDITATION",
                "OGN·048/298",
                [],
                OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MEDITATION"], result.State.PlayerZones["P1"].Hand);
        Assert.False(result.State.CardObjects["P2-UNIT-001"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSacrificeDestroyFriendlyPowerfulDrawCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SACRIFICE-001", "P1-SPELL-SACRIFICE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-UNIT-SACRIFICE-001"));
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSacrificeWithoutAdditionalCost()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SACRIFICE"],
                    Battlefields = ["P1-UNIT-SACRIFICE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SACRIFICE-001"] = new("P1-UNIT-SACRIFICE-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sacrifice-missing-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-SACRIFICE", "UNL-173/219", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SACRIFICE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-SACRIFICE-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSacrificeWhenAdditionalCostUnitIsNotPowerful()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SACRIFICE"],
                    Battlefields = ["P1-UNIT-SACRIFICE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SACRIFICE-001"] = new("P1-UNIT-SACRIFICE-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sacrifice-weak-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SACRIFICE",
                "UNL-173/219",
                [],
                OptionalCosts: ["DESTROY_FRIENDLY_POWERFUL_UNIT:P1-UNIT-SACRIFICE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SACRIFICE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-SACRIFICE-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-UNIT-SACRIFICE-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSoulStrangleDestroyFriendlyPowerBuffDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SOUL-002"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-UNIT-SOUL-001", "P1-SPELL-SOUL-STRANGLE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-SOUL-002"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-SOUL-002"].UntilEndOfTurnPowerModifier);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-UNIT-SOUL-001"));
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSoulStrangleWhenSecondTargetIsEnemy()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SOUL-STRANGLE"],
                    Battlefields = ["P1-UNIT-SOUL-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-SOUL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SOUL-001"] = new("P1-UNIT-SOUL-001", power: 4),
                ["P2-UNIT-SOUL-001"] = new("P2-UNIT-SOUL-001", power: 2)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-soul-strangle-enemy-second-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SOUL-STRANGLE",
                "SFD·163/221",
                ["P1-UNIT-SOUL-001", "P2-UNIT-SOUL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SOUL-STRANGLE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-SOUL-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-UNIT-SOUL-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-UNIT-SOUL-001"].Power);
        Assert.Equal(2, result.State.CardObjects["P2-UNIT-SOUL-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCleaveOverwhelmAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cleave-overwhelm-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_3"], result.FinalState.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnEffects);
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-CLEAVE-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineCleaveDoesNotModifyPowerWhenTargetIsNotAttacking()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CLEAVE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-CLEAVE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-CLEAVE-001"] = new(
                    "P2-UNIT-CLEAVE-001",
                    power: 2,
                    isAttacking: false)
            }
        };
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleave-non-attacking-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CLEAVE",
                "OGN·004/298",
                ["P2-UNIT-CLEAVE-001"]),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-cleave-non-attacking-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent("intent-cleave-non-attacking-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Contains(p2PassResult.Events, gameEvent => gameEvent.Kind == "STATUS_EFFECT_APPLIED");
        Assert.DoesNotContain(p2PassResult.Events, gameEvent => gameEvent.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(["OVERWHELM_3"], p2PassResult.State.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, p2PassResult.State.CardObjects["P2-UNIT-CLEAVE-001"].Power);
        Assert.Equal(0, p2PassResult.State.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBloodRushEchoOverwhelmAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blood-rush-echo-overwhelm-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_2"], result.FinalState.CardObjects["P2-UNIT-BLOOD-RUSH-001"].UntilEndOfTurnEffects);
        Assert.Equal(6, result.FinalState.CardObjects["P2-UNIT-BLOOD-RUSH-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-BLOOD-RUSH-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPowerPunchOverwhelmRoamAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-power-punch-overwhelm-roam-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_2", "ROAM"], result.FinalState.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnEffects);
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-POWER-PUNCH-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePowerPunchDoesNotModifyPowerWhenTargetIsNotAttacking()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-POWER-PUNCH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-POWER-PUNCH-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-POWER-PUNCH-001"] = new(
                    "P2-UNIT-POWER-PUNCH-001",
                    power: 2,
                    isAttacking: false)
            }
        };
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-power-punch-non-attacking-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-POWER-PUNCH",
                "UNL-010/219",
                ["P2-UNIT-POWER-PUNCH-001"]),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-power-punch-non-attacking-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent("intent-power-punch-non-attacking-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Equal(2, p2PassResult.Events.Count(gameEvent => gameEvent.Kind == "STATUS_EFFECT_APPLIED"));
        Assert.DoesNotContain(p2PassResult.Events, gameEvent => gameEvent.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(["OVERWHELM_2", "ROAM"], p2PassResult.State.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, p2PassResult.State.CardObjects["P2-UNIT-POWER-PUNCH-001"].Power);
        Assert.Equal(0, p2PassResult.State.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysParrySteadfastBarrierDefendingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-parry-steadfast-barrier-defending-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["BARRIER", "STEADFAST_3"], result.FinalState.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnEffects);
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-PARRY-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineParryDoesNotModifyPowerWhenTargetIsNotDefending()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PARRY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-PARRY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-PARRY-001"] = new(
                    "P2-UNIT-PARRY-001",
                    power: 2,
                    isDefending: false)
            }
        };
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-parry-non-defending-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PARRY",
                "OGN·057/298",
                ["P2-UNIT-PARRY-001"]),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-parry-non-defending-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent("intent-parry-non-defending-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Equal(2, p2PassResult.Events.Count(gameEvent => gameEvent.Kind == "STATUS_EFFECT_APPLIED"));
        Assert.DoesNotContain(p2PassResult.Events, gameEvent => gameEvent.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(["BARRIER", "STEADFAST_3"], p2PassResult.State.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, p2PassResult.State.CardObjects["P2-UNIT-PARRY-001"].Power);
        Assert.Equal(0, p2PassResult.State.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRoaringReckoningDiscardEchoOverwhelmAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-roaring-reckoning-discard-echo-overwhelm-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_4"], result.FinalState.CardObjects["P2-UNIT-ROARING-RECKONING-001"].UntilEndOfTurnEffects);
        Assert.Equal(10, result.FinalState.CardObjects["P2-UNIT-ROARING-RECKONING-001"].Power);
        Assert.Equal(8, result.FinalState.CardObjects["P2-UNIT-ROARING-RECKONING-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(["P1-DISCARD-001", "P1-SPELL-ROARING-RECKONING"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRoaringReckoningWhenDiscardOptionalCostTargetsSource()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ROARING-RECKONING"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-ROARING-RECKONING-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-ROARING-RECKONING-001"] = new(
                    "P2-UNIT-ROARING-RECKONING-001",
                    power: 2,
                    isAttacking: true)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-roaring-reckoning-discard-source", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ROARING-RECKONING",
                "UNL-017/219",
                ["P2-UNIT-ROARING-RECKONING-001"],
                OptionalCosts: ["DISCARD_HAND_CARD:P1-SPELL-ROARING-RECKONING"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(["P1-SPELL-ROARING-RECKONING"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMoonsilverGiftDiscardDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-moonsilver-gift-discard-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DISCARD-001", "P1-SPELL-MOONSILVER-GIFT"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMoonsilverGiftWhenDiscardTargetIsSource()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001", "P1-DRAW-002"],
                    Hand = ["P1-SPELL-MOONSILVER-GIFT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-moonsilver-gift-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MOONSILVER-GIFT",
                "UNL-125/219",
                ["P1-SPELL-MOONSILVER-GIFT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MOONSILVER-GIFT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRewindTimelineDiscardHandsDrawFour()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rewind-timeline-discard-hands-draw-four.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003", "P1-DRAW-004"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DRAW-001", "P2-DRAW-002", "P2-DRAW-003", "P2-DRAW-004"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-HAND-001", "P1-HAND-002", "P1-SPELL-REWIND-TIMELINE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-HAND-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReviveReturnGraveyardUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-revive-return-graveyard-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-OTHER-001", "P1-SPELL-REVIVE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReviveWhenTargetIsOpponentGraveyard()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-REVIVE"],
                    Graveyard = ["P1-GRAVE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Graveyard = ["P2-GRAVE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-revive-opponent-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-REVIVE",
                "OGN·170/298",
                ["P2-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-REVIVE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-GRAVE-UNIT-001"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBorrowedHistoryDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-borrowed-history-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCenterYourMindBaseDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-center-your-mind-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpoilsOfWarFullCostDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-spoils-of-war-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineReducesSpoilsOfWarAfterEnemyUnitDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAbyssalHuntThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-abyssal-hunt-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineBoostsAbyssalHuntWhenControllerHasFaceDownCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-abyssal-hunt-face-down-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDancingGrenadeAgainstBaseUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dancing-grenade-base-unit-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysIncinerateThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-incinerate-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHextechRayThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hextech-ray-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineBoostsThunderingDropAgainstAttackingUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thundering-drop-attacking-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPiercingLightAgainstTwoBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-piercing-light-two-target-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-002"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineReducesThunderingSkyByHighestControlledUnitPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thundering-sky-cost-reduced-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineClearsHextechRayDamageAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCometStrikeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-comet-strike-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFinalSparkThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-final-spark-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSuperMegaDeathRocketThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-super-mega-death-rocket-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCenterStageDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-center-stage-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCenterStageEchoDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-center-stage-echo-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysProphetsOmenThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-prophets-omen-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMightMakesRightDrawPowerfulUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-might-makes-right-draw-powerful-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEvolutionDayThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-evolution-day-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMobilizeCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mobilize-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-002"], result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMobilizeDrawsIfRuneCallFails()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mobilize-draws-if-rune-call-fails.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].RuneDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCatalystOfAeonsCallTwoRunes()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-catalyst-of-aeons-call-two-runes.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-001", "P1-RUNE-002"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-003"], result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-002"].IsExhausted);
        Assert.DoesNotContain("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCatalystOfAeonsDrawsIfRuneCallIsShort()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMindAndBalanceReducedDrawThenCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mind-and-balance-reduced-draw-then-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var eventKinds = result.EventKinds.ToArray();
        Assert.True(Array.IndexOf(eventKinds, "CARD_DRAWN") < Array.IndexOf(eventKinds, "RUNES_CALLED"));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVengeanceThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-vengeance-destroy-unit-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysWellspringOfHatredDestroyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDetonationThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-detonation-destroy-battlefield-unit-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHuntTheWeakAgainstSmallBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDarkinBladeAndTargetControllerDraws()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-darkin-blade-destroy-target-controller-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-DRAW-001", "P2-DRAW-002"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRuinationAndDestroysAllUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ruination-destroy-all-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysQuicksandPitThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReprimandAndReturnsBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reprimand-return-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-HAND-001", "P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGustAndReturnsSmallBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-gust-return-small-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHappenstanceAndReturnsFriendlyThenEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-happenstance-return-friendly-and-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReconsiderAndCallsRuneForReturnedOwner()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reconsider-return-friendly-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysUndertowAndReturnsAllUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-undertow-return-all-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-BASE-UNIT-001", "P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBattleOrFlightAndMovesBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects.ContainsKey("P2-BATTLEFIELD-UNIT-001"));
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFlashAndMovesTwoFriendlyBattlefieldUnitsToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-UNIT-001", "P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-002"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-002"].UntilEndOfTurnEffects);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTheCurtainRisesEchoAndReadiesUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-the-curtain-rises-echo-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBeatdownAndReadiesUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-beatdown-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-BASE-UNIT-001"].IsExhausted);
        Assert.Single(result.EventKinds, kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHuntAndReadiesAllFriendlyUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hunt-ready-all-friendly-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-BASE-UNIT-001"].IsExhausted);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-BASE-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysOverchargedEnergyExhaustsFriendlyUnitsAndDamagesBattlefields()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.True(result.FinalState.CardObjects["P1-BASE-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(12, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_EXHAUSTED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCannonBarrageDamagesOnlyEnemyCombatUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cannon-barrage-damage-enemy-combat-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P1-FRIENDLY-DEFENDER"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-ENEMY-ATTACKER"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-ENEMY-DEFENDER"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-ENEMY-IDLE"].Damage);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysProductionSurgeCreatesRobotAndDraws()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-production-surge-create-robot-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-PRODUCTION-SURGE-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SPELL-PRODUCTION-SURGE-TOKEN-001"].Power);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Contains("UNIT_TOKEN_CREATED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineReducesProductionSurgeWhenControllerHasMechanicalUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-production-surge-reduced-by-mechanical.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MECHANICAL-UNIT-001", "P1-SPELL-PRODUCTION-SURGE-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["机械"], result.FinalState.CardObjects["P1-MECHANICAL-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCommonCauseCreatesFourMinionsInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-common-cause-create-four-minions-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-COMMON-CAUSE-TOKEN-001",
                "P1-SPELL-COMMON-CAUSE-TOKEN-002",
                "P1-SPELL-COMMON-CAUSE-TOKEN-003",
                "P1-SPELL-COMMON-CAUSE-TOKEN-004"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            4,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
            Assert.Equal(1, result.FinalState.CardObjects[objectId].Power));
    }

    [Fact]
    public async Task CoreRuleEngineRecallsHighlanderBloodlineTargetWhenDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-highlander-bloodline-recall-if-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT_RECALLED_TO_BASE", result.EventKinds);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRecallsTacticalRetreatTargetWhenDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tactical-retreat-recall-if-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT_RECALLED_TO_BASE", result.EventKinds);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLastStandFriendlyPowerPlus3()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-last-stand-friendly-power-plus-3.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIED_UNTIL_END_OF_TURN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAnimalFriendsPowerPerControlledTag()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-animal-friends-power-per-controlled-tag.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIED_UNTIL_END_OF_TURN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSandcraftWithEchoCreatesTwoSandSoldiersInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-SANDCRAFT-TOKEN-001",
                "P1-SPELL-SANDCRAFT-TOKEN-002"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
            Assert.Equal(2, result.FinalState.CardObjects[objectId].Power));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDangerTemperatureBuffsOnlyFriendlyMechanicalUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-danger-temperature-mechanical-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-MECHANICAL-BASE"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P1-MECHANICAL-BATTLEFIELD"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-NON-MECHANICAL-BASE"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-MECHANICAL-BASE"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysWellTrainedPowerDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-well-trained-power-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineExpiresWellTrainedPowerAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-well-trained-power-expires-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIER_EXPIRED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPracticalExperiencePowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-practical-experience-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDuelingStanceFriendlyPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dueling-stance-friendly-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-DUELING-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-DUELING-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineRepeatsSavageStrengthPowerWithEcho()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-savage-strength-echo-power-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(7, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRepeatsFreezePowerPenaltyWithEcho()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-freeze-echo-power-minus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDistanceBreakDanceSplitPowerModifiers()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-distance-break-dance-split-power-modifiers.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-BASE-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShootFirstPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shoot-first-power-plus-5-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(7, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGloryCallPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-glory-call-power-plus-3.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDecisiveStrikeAllFriendlyPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-decisive-strike-all-friendly-power-plus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTremendousStrengthPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tremendous-strength-power-plus-7.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(9, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(7, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMoonfallPowerPenalty()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-moonfall-power-minus-10.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-10, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGrandStrategyAllFriendlyPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-grand-strategy-all-friendly-power-plus-5.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(7, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(8, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBackToBackTwoFriendlyPowerBoosts()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-back-to-back-two-friendly-power-plus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPowerBindEchoTwoFriendlyPowerBoosts()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-power-bind-echo-two-friendly-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            4,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-smoke-bomb-power-floor-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(-2, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-smoke-bomb-power-floor-expires-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIER_EXPIRED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-extortion-power-floor-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEclipsePowerPenalty()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-eclipse-power-minus-4.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineDestroysBaseUnitWithVengeance()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-vengeance-base-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-vengeance-base-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-vengeance-base-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(new[] { "UNIT_DESTROYED" }, p2Pass.Events.TakeLast(1).Select(gameEvent => gameEvent.Kind));
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-BASE-UNIT-001" }, p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-BASE-UNIT-001", p2Pass.State.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStellarConvergenceAgainstTwoTargetsThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stellar-convergence-two-target-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRocketBarrageBaseUnitModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rocket-barrage-base-unit-mode-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleDrawModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-draw-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleBattlefieldDamageModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-battlefield-damage-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleBaseDamageModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-base-damage-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleBattlefieldPowerModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-battlefield-power-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVoidSeekerThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-void-seeker-damage-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineBurnsOutDuringVoidSeekerDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-void-seeker-draw-burnout-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRunePrisonThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rune-prison-stun-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKerplunkAgainstAttackingUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kerplunk-stun-attacking-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKerplunkEchoAgainstAttackingUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kerplunk-echo-stun-attacking-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysExistentialDreadEchoStunThenReturn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-existential-dread-echo-stun-then-return.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            ["STATUS_EFFECT_APPLIED", "UNIT_RETURNED_TO_HAND"],
            result.EventKinds.Where(kind =>
                string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)
                || string.Equals(kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRunePrisonAgainstBaseUnitThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rune-prison-base-unit-stun-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineExpiresRunePrisonStunAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-rune-prison-stun-expires-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPunishmentWhenManaIsInsufficient()
    {
        var state = PunishmentState(mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-no-mana", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(new[] { "P1-SPELL-PUNISHMENT" }, result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPunishmentWhenTargetIsInvalid()
    {
        var state = PunishmentState(mana: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-bad-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-HAND-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineUsesBaseThunderingDropDamageAgainstNonAttackingUnit()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-THUNDERING-DROP"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 5)
            }
        };

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-thundering-drop-base-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-THUNDERING-DROP", "SFD·017/221", ["P2-UNIT-001"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-thundering-drop-base-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-thundering-drop-base-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(2, p2Pass.State.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsKerplunkWhenTargetIsNotAttacking()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-KERPLUNK"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-kerplunk-not-attacking-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-KERPLUNK", "SFD·040/221", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsExistentialDreadWhenTargetIsFriendlyAttackingUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-EXISTENTIAL-DREAD"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001", isAttacking: true)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-existential-dread-friendly-attacking-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-EXISTENTIAL-DREAD",
                "UNL-134/219",
                ["P1-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsThunderingSkyWithoutEnoughReducedCost()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-THUNDERING-SKY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 6)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-thundering-sky-not-enough-reduced-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-THUNDERING-SKY", "OGN·014/298", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMindAndBalanceWithoutOpponentScoreReduction()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 4
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001"],
                    RuneDeck = ["P1-RUNE-001"],
                    Hand = ["P1-SPELL-MIND-AND-BALANCE"]
                },
                ["P2"] = PlayerZones.Empty
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-mind-and-balance-not-enough-reduced-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-MIND-AND-BALANCE", "OGN·047/298", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MIND-AND-BALANCE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHuntTheWeakWhenTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HUNT-THE-WEAK"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hunt-the-weak-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HUNT-THE-WEAK", "UNL-159/219", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsGustWhenTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-GUST"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gust-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-GUST", "OGN·169/298", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hextech-ray-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HEXTECH-RAY", "OGN·009/298", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFriendlyOnlySpellWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BACK-TO-BACK"],
                    Base = ["P1-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-back-to-back-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BACK-TO-BACK",
                "OGN·206/298",
                ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFlashWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FLASH"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-flash-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FLASH",
                "OGS·011/024",
                ["P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHappenstanceWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HAPPENSTANCE"],
                    Base = ["P1-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-happenstance-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HAPPENSTANCE",
                "UNL-128/219",
                ["P2-BATTLEFIELD-UNIT-001", "P1-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReconsiderWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-RECONSIDER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reconsider-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-RECONSIDER",
                "OGN·104/298",
                ["P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFlashWhenTargetIsFriendlyBaseUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FLASH"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-flash-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FLASH",
                "OGS·011/024",
                ["P1-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsUpToTwoTargetSpellWithOneTarget()
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
                    Hand = ["P1-SPELL-STELLAR-CONVERGENCE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-stellar-convergence-one-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-STELLAR-CONVERGENCE", "OGN·105/298", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Single(result.State.StackItems);
        Assert.Equal(new[] { "P2-UNIT-001" }, result.State.StackItems[0].TargetObjectIds);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsPiercingLightWithOneBattlefieldTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PIERCING-LIGHT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-piercing-light-one-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PIERCING-LIGHT", "SFD·023/221", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.Equal(new[] { "P2-UNIT-001" }, result.State.StackItems[0].TargetObjectIds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPiercingLightRepeatedTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PIERCING-LIGHT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-piercing-light-repeated-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PIERCING-LIGHT", "SFD·023/221", ["P2-UNIT-001", "P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsModalSpellWhenModeIsMissing()
    {
        var state = PunishmentState(mana: 4) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ROCKET-BARRAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-rocket-barrage-missing-mode", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-ROCKET-BARRAGE", "SFD·077/221", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEchoWhenManaIsInsufficient()
    {
        var state = CenterStageState(mana: 3);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-center-stage-echo-no-mana", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-CENTER-STAGE", "UNL-061/219", [], OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(new[] { "P1-SPELL-CENTER-STAGE" }, result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEchoOnNonEchoSpell()
    {
        var state = PunishmentState(mana: 4);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-illegal-echo", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-UNIT-001"], OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(new[] { "P1-SPELL-PUNISHMENT" }, result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPassPriorityOutsidePriorityWindow()
    {
        var state = new MatchState(
            "p2-pass-priority-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pass-priority", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal("MAIN", result.State.Phase);
        Assert.Equal(new[] { "END_TURN" }, result.Prompts["P1"].Actions);
    }

    [Fact]
    public async Task CoreRuleEngineTransfersPriorityWhenStackItemIsPending()
    {
        var state = new MatchState(
            "p2-fepr-priority-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralClosed,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            "P1",
            [],
            [new StackItemState("STACK-001", "P1", "P1-ABILITY-001", "TEST_RESOLVE")]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-priority", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new[] { "PRIORITY_PASSED" }, result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.PriorityPlayerId);
        Assert.Equal(new[] { "P1" }, result.State.PassedPriorityPlayerIds);
        Assert.Single(result.State.StackItems);
        Assert.Equal(new[] { "WAIT" }, result.Prompts["P1"].Actions);
        Assert.Equal(new[] { "PASS_PRIORITY" }, result.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task CoreRuleEnginePassPriorityDoesNotEndTurnInOrdinaryMainPhase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-pass-priority-does-not-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal(7, result.FinalState.TurnNumber);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_OPEN", result.FinalState.TimingState);
        Assert.Equal(new[] { "END_TURN" }, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineResolvesStackWhenAllPlayersPassPriority()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-fepr-priority-pass-resolves-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal(7, result.FinalState.TurnNumber);
        Assert.Equal("P1", result.FinalState.ActivePlayerId);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_OPEN", result.FinalState.TimingState);
        Assert.Null(result.FinalState.PriorityPlayerId);
        Assert.Empty(result.FinalState.PassedPriorityPlayerIds);
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(new[] { "END_TURN" }, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineReturnsPriorityToLatestRemainingStackController()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-fepr-resolves-latest-keeps-remaining-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("P1", result.FinalState.ActivePlayerId);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_CLOSED", result.FinalState.TimingState);
        Assert.Equal("P1", result.FinalState.PriorityPlayerId);
        Assert.Empty(result.FinalState.PassedPriorityPlayerIds);
        var remaining = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-OLDER", remaining.StackItemId);
        Assert.Equal("P1", remaining.ControllerId);
        Assert.Equal(new[] { "PASS_PRIORITY" }, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPassFocusOutsideSpellDuel()
    {
        var state = new MatchState(
            "p2-pass-focus-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pass-focus", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new[] { "END_TURN" }, result.Prompts["P1"].Actions);
    }

    [Fact]
    public async Task CoreRuleEngineTransfersFocusInSpellDuel()
    {
        var state = new MatchState(
            "p2-spell-duel-focus-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.SpellDuelOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            null,
            [],
            [],
            "P1",
            []);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p1-pass-focus", "P1", "PASS_FOCUS"),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new[] { "FOCUS_PASSED" }, result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Equal(new[] { "P1" }, result.State.PassedFocusPlayerIds);
        Assert.Equal(new[] { "WAIT" }, result.Prompts["P1"].Actions);
        Assert.Equal(new[] { "PASS_FOCUS" }, result.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task CoreRuleEngineClosesSpellDuelWhenAllPlayersPassFocus()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-spell-duel-pass-focus-closes-window.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(fixture.Expected.FinalTick, result.FinalTick);
        Assert.Equal(fixture.Expected.EventKinds, result.EventKinds);
        Assert.Equal("P1", result.FinalState.ActivePlayerId);
        Assert.Equal("P1", result.FinalState.TurnPlayerId);
        Assert.Equal("MAIN", result.FinalState.Phase);
        Assert.Equal("NEUTRAL_OPEN", result.FinalState.TimingState);
        Assert.Null(result.FinalState.FocusPlayerId);
        Assert.Empty(result.FinalState.PassedFocusPlayerIds);
        Assert.Equal(new[] { "END_TURN" }, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
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
        foreach (var (playerId, actions) in fixture.Expected.PromptActions)
        {
            Assert.Equal(actions, result.Prompts[playerId].Actions);
        }

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

    private static MatchState PunishmentState(int mana)
    {
        return new MatchState(
            "p2-punishment-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"],
                    Hand = ["P2-HAND-001"]
                }
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001")
            });
    }

    private static MatchState CenterStageState(int mana)
    {
        return new MatchState(
            "p2-center-stage-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001", "P1-DRAW-002"],
                    Hand = ["P1-SPELL-CENTER-STAGE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal));
    }

    private sealed class CapturingRuleEngine : IRuleEngine
    {
        public MatchState? CapturedState { get; private set; }

        public ValueTask<ResolutionResult> ResolveAsync(
            MatchState state,
            PlayerIntent intent,
            GameCommand command,
            CancellationToken cancellationToken)
        {
            CapturedState = state;
            var nextState = state with
            {
                Tick = state.Tick + 1
            };
            return ValueTask.FromResult(new ResolutionResult(
                true,
                null,
                nextState,
                [],
                ResolutionResult.BuildSnapshots(nextState),
                ResolutionResult.BuildPrompts(nextState)));
        }
    }
}
