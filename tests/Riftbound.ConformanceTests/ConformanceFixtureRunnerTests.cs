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
    public async Task CoreRuleEnginePlaysAssembleTheRanksDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-assemble-the-ranks-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-ASSEMBLE-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCallToActionDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-call-to-action-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-CALL-ACTION-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVoidRushDrawNoFreePlay()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-void-rush-draw-no-free-play.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-VOID-RUSH-DRAW-001", "P1-VOID-RUSH-DRAW-002"],
            result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReflectionsSwapDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reflections-swap-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-REFLECTIONS-BATTLEFIELD-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-REFLECTIONS-BASE-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReflectionsWithoutEphemeralTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-REFLECTIONS"],
                    Base = ["P1-REFLECTIONS-BASE-001"],
                    Battlefields = ["P1-REFLECTIONS-BATTLEFIELD-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REFLECTIONS-BASE-001"] = new("P1-REFLECTIONS-BASE-001", tags: [CardObjectTags.UnitCard]),
                ["P1-REFLECTIONS-BATTLEFIELD-001"] = new("P1-REFLECTIONS-BATTLEFIELD-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reflections-no-ephemeral", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-REFLECTIONS",
                "UNL-083/219",
                ["P1-REFLECTIONS-BASE-001", "P1-REFLECTIONS-BATTLEFIELD-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REFLECTIONS-BASE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-REFLECTIONS-BATTLEFIELD-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysThunderingDropBasePowerDamageMove()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thundering-drop-base-power-damage-move.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-THUNDERING-DROP-BASE-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P2-THUNDERING-DROP-UNIT-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P2-THUNDERING-DROP-UNIT-002"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBattleCommandMoveFriendlyAndOpponentUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-battle-command-move-friendly-and-opponent-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-BATTLE-COMMAND-FIELD-KEEPER", "P1-BATTLE-COMMAND-BASE-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BATTLE-COMMAND-BASE-001"], result.FinalState.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBattleCommandWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BATTLE-COMMAND"],
                    Base = ["P1-BATTLE-COMMAND-BASE-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BATTLE-COMMAND-BASE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-COMMAND-BASE-001"] = new("P1-BATTLE-COMMAND-BASE-001", power: 4),
                ["P2-BATTLE-COMMAND-BASE-001"] = new("P2-BATTLE-COMMAND-BASE-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-battle-command-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BATTLE-COMMAND",
                "UNL-101/219",
                ["P2-BATTLE-COMMAND-BASE-001", "P1-BATTLE-COMMAND-BASE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BATTLE-COMMAND"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLE-COMMAND-BASE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLE-COMMAND-BASE-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVoidAssaultMoveFriendlyAndEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-void-assault-move-friendly-and-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-VOID-ASSAULT-FIELD-KEEPER", "P1-VOID-ASSAULT-FRIENDLY-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-VOID-ASSAULT-ENEMY-001"], result.FinalState.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsVoidAssaultWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VOID-ASSAULT"],
                    Base = ["P1-VOID-ASSAULT-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-VOID-ASSAULT-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-VOID-ASSAULT-FRIENDLY-001"] = new(
                    "P1-VOID-ASSAULT-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-VOID-ASSAULT-ENEMY-001"] = new(
                    "P2-VOID-ASSAULT-ENEMY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-void-assault-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-VOID-ASSAULT",
                "UNL-202/219",
                ["P2-VOID-ASSAULT-ENEMY-001", "P1-VOID-ASSAULT-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-VOID-ASSAULT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-VOID-ASSAULT-FRIENDLY-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-VOID-ASSAULT-ENEMY-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBulletTimePowerDamageEnemyBattlefield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bullet-time-power-damage-enemy-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BULLET-TIME-UNIT-002"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BULLET-TIME-BASE-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBulletTimeWhenPowerCostIsInsufficient()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 2),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bullet-time-insufficient-power", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [],
                OptionalCosts: ["SPEND_POWER:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 2), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BULLET-TIME"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPortalpalRescueBanishPlayBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-portalpal-rescue-banish-play-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-PORTALPAL-BASE-KEEPER", "P1-PORTALPAL-TARGET-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal(0, result.FinalState.CardObjects["P1-PORTALPAL-TARGET-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-PORTALPAL-TARGET-001"].Power);
        Assert.Empty(result.FinalState.CardObjects["P1-PORTALPAL-TARGET-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPortalpalRescueAgainstEnemyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PORTALPAL-RESCUE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-PORTALPAL-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-PORTALPAL-ENEMY-001"] = new(
                    "P2-PORTALPAL-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-portalpal-rescue-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PORTALPAL-RESCUE", "OGN·102/298", ["P2-PORTALPAL-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PORTALPAL-RESCUE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-PORTALPAL-ENEMY-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHuntingRhythmBanishPlayBattlefield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hunting-rhythm-banish-play-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-HUNTING-RHYTHM-FIELD-KEEPER", "P1-HUNTING-RHYTHM-TARGET-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HUNTING-RHYTHM-TARGET-001"].Damage);
        Assert.Equal(5, result.FinalState.CardObjects["P1-HUNTING-RHYTHM-TARGET-001"].Power);
        Assert.Empty(result.FinalState.CardObjects["P1-HUNTING-RHYTHM-TARGET-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHuntingRhythmAgainstEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HUNTING-RHYTHM"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-HUNTING-RHYTHM-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HUNTING-RHYTHM-ENEMY-001"] = new(
                    "P2-HUNTING-RHYTHM-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hunting-rhythm-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HUNTING-RHYTHM", "UNL-184/219", ["P2-HUNTING-RHYTHM-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HUNTING-RHYTHM"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HUNTING-RHYTHM-ENEMY-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
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
    public async Task CoreRuleEnginePlaysPortalpaloozaOtherChoosesCards()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-portalpalooza-other-chooses-cards.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-PORTAL-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-PORTAL-DRAW-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPortalpaloozaOtherChoosesRunes()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-portalpalooza-other-chooses-runes.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-PORTAL-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-PORTAL-RUNE-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.True(result.FinalState.CardObjects["P1-PORTAL-RUNE-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-PORTAL-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCovertSabotageRecycleOpponentNonUnitHandCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-covert-sabotage-recycle-opponent-non-unit-hand-card.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-COVERT-SABOTAGE-UNIT-CARD"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            ["P2-MAIN-KEEPER", "P2-COVERT-SABOTAGE-SPELL-CARD"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCovertSabotageUnitHandCardTarget()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-COVERT-SABOTAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-CARD", "P2-SPELL-CARD"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-CARD"] = new("P2-UNIT-CARD", tags: [CardObjectTags.UnitCard]),
                ["P2-SPELL-CARD"] = new("P2-SPELL-CARD", tags: ["CARD_TYPE:SPELL"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-covert-sabotage-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-COVERT-SABOTAGE", "OGN·156/298", ["P2-UNIT-CARD"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-COVERT-SABOTAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-CARD", "P2-SPELL-CARD"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPredictiveOffensiveDrawOneRecycleOther()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-predictive-offensive-draw-one-recycle-other.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-PREDICTIVE-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-PREDICTIVE-RECYCLE-001"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPredictiveOffensiveTargetOutsideTopTwo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-001", "P1-TOP-002", "P1-NOT-VIEWED-001"],
                    Hand = ["P1-SPELL-PREDICTIVE-OFFENSIVE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-predictive-offensive-outside-top-two", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PREDICTIVE-OFFENSIVE", "SFD·122/221", ["P1-NOT-VIEWED-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-PREDICTIVE-OFFENSIVE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-001", "P1-TOP-002", "P1-NOT-VIEWED-001"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCardTrickDrawOneRecycleRest()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-card-trick-draw-one-recycle-rest.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-CARD-TRICK-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-CARD-TRICK-RECYCLE-A", "P1-CARD-TRICK-RECYCLE-B"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCardTrickTargetOutsideTopThree()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-001", "P1-TOP-002", "P1-TOP-003", "P1-NOT-VIEWED-001"],
                    Hand = ["P1-SPELL-CARD-TRICK"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-card-trick-outside-top-three", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-CARD-TRICK", "OGN·183/298", ["P1-NOT-VIEWED-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-CARD-TRICK"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-001", "P1-TOP-002", "P1-TOP-003", "P1-NOT-VIEWED-001"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonTigerDrawUnitRecycleRest()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragon-tiger-draw-unit-recycle-rest.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAGON-TIGER-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-DRAGON-TIGER-RECYCLE-B", "P1-DRAGON-TIGER-RECYCLE-A"],
            result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DRAGON-TIGER-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonTigerNoSelectionRecycleAll()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragon-tiger-no-unit-selection-recycle-all.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-DRAGON-TIGER-RECYCLE-B", "P1-DRAGON-TIGER-RECYCLE-C", "P1-DRAGON-TIGER-RECYCLE-A"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReinforcementsNoSelectionRecycleTopFive()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reinforcements-no-selection-recycle-top-five.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            [
                "P1-MAIN-KEEPER",
                "P1-REINFORCEMENTS-RECYCLE-E",
                "P1-REINFORCEMENTS-RECYCLE-D",
                "P1-REINFORCEMENTS-RECYCLE-A",
                "P1-REINFORCEMENTS-RECYCLE-C",
                "P1-REINFORCEMENTS-RECYCLE-B"
            ],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonTigerNonUnitTopDeckTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-SPELL-001", "P1-TOP-UNIT-001", "P1-TOP-UNIT-002"],
                    Hand = ["P1-SPELL-DRAGON-TIGER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TOP-SPELL-001"] = new("P1-TOP-SPELL-001", tags: ["CARD_TYPE:SPELL"]),
                ["P1-TOP-UNIT-001"] = new("P1-TOP-UNIT-001", tags: [CardObjectTags.UnitCard]),
                ["P1-TOP-UNIT-002"] = new("P1-TOP-UNIT-002", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-tiger-non-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-DRAGON-TIGER", "UNL-032/219", ["P1-TOP-SPELL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-DRAGON-TIGER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-SPELL-001", "P1-TOP-UNIT-001", "P1-TOP-UNIT-002"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonTigerTargetOutsideTopThree()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-UNIT-001", "P1-TOP-UNIT-002", "P1-TOP-UNIT-003", "P1-NOT-VIEWED-UNIT-001"],
                    Hand = ["P1-SPELL-DRAGON-TIGER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TOP-UNIT-001"] = new("P1-TOP-UNIT-001", tags: [CardObjectTags.UnitCard]),
                ["P1-TOP-UNIT-002"] = new("P1-TOP-UNIT-002", tags: [CardObjectTags.UnitCard]),
                ["P1-TOP-UNIT-003"] = new("P1-TOP-UNIT-003", tags: [CardObjectTags.UnitCard]),
                ["P1-NOT-VIEWED-UNIT-001"] = new("P1-NOT-VIEWED-UNIT-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-tiger-outside-top-three", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-DRAGON-TIGER", "UNL-032/219", ["P1-NOT-VIEWED-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-DRAGON-TIGER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-UNIT-001", "P1-TOP-UNIT-002", "P1-TOP-UNIT-003", "P1-NOT-VIEWED-UNIT-001"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
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
    public async Task CoreRuleEnginePlaysSalvageDestroyEquipmentThenDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-salvage-destroy-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-SALVAGE-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-SALVAGE-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-EQUIPMENT-SALVAGE-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSalvageAgainstUnitTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-SALVAGE-DRAW-001"],
                    Hand = ["P1-SPELL-SALVAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-SALVAGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-UNIT-SALVAGE-001"] = new(
                    "P2-BATTLEFIELD-UNIT-SALVAGE-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-salvage-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-SALVAGE", "OGN·224/298", ["P2-BATTLEFIELD-UNIT-SALVAGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SALVAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SALVAGE-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-SALVAGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
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
    public async Task CoreRuleEnginePlaysSinfulPleasureDiscardDamageByManaCost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sinful-pleasure-discard-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DISCARD-SINFUL-001", "P1-SPELL-SINFUL-PLEASURE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BATTLEFIELD-SINFUL-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-DISCARD-SINFUL-001"].ManaCost);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSinfulPleasureWhenDiscardTargetIsOpponentHand()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SINFUL-PLEASURE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-DISCARD-SINFUL-001"],
                    Battlefields = ["P2-BATTLEFIELD-SINFUL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DISCARD-SINFUL-001"] = new("P2-DISCARD-SINFUL-001", manaCost: 4),
                ["P2-BATTLEFIELD-SINFUL-001"] = new("P2-BATTLEFIELD-SINFUL-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sinful-pleasure-opponent-hand-discard", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SINFUL-PLEASURE",
                "OGN·008/298",
                ["P2-DISCARD-SINFUL-001", "P2-BATTLEFIELD-SINFUL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SINFUL-PLEASURE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DISCARD-SINFUL-001"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-SINFUL-001"].Damage);
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
    public async Task CoreRuleEnginePlaysHarrowingPlayGraveyardUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-harrowing-play-graveyard-unit-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-HARROWING-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-OTHER-001", "P1-SPELL-HARROWING"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects["P1-HARROWING-GRAVE-UNIT-001"].Power);
        Assert.False(result.FinalState.CardObjects["P1-HARROWING-GRAVE-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHarrowingWhenTargetIsNonUnitGraveyardCard()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HARROWING"],
                    Graveyard = ["P1-GRAVE-EQUIPMENT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-EQUIPMENT-001"] = new(
                    "P1-GRAVE-EQUIPMENT-001",
                    power: 0,
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-harrowing-non-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HARROWING",
                "OGN·198/298",
                ["P1-GRAVE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HARROWING"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSteadfastLoyaltyPlayLowCostGraveyardUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-steadfast-loyalty-graveyard-unit-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-STEADFAST-LOYALTY-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-OTHER-001", "P1-SPELL-STEADFAST-LOYALTY"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-STEADFAST-LOYALTY-GRAVE-UNIT-001"].ManaCost);
        Assert.False(result.FinalState.CardObjects["P1-STEADFAST-LOYALTY-GRAVE-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSteadfastLoyaltyWhenTargetIsNonUnitGraveyardCard()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STEADFAST-LOYALTY"],
                    Graveyard = ["P1-GRAVE-EQUIPMENT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-EQUIPMENT-001"] = new(
                    "P1-GRAVE-EQUIPMENT-001",
                    power: 0,
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-steadfast-loyalty-non-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-STEADFAST-LOYALTY",
                "UNL-168/219",
                ["P1-GRAVE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-STEADFAST-LOYALTY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSteadfastLoyaltyWhenTargetCostsTooMuch()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STEADFAST-LOYALTY"],
                    Graveyard = ["P1-GRAVE-UNIT-EXPENSIVE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-EXPENSIVE"] = new(
                    "P1-GRAVE-UNIT-EXPENSIVE",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-steadfast-loyalty-expensive-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-STEADFAST-LOYALTY",
                "UNL-168/219",
                ["P1-GRAVE-UNIT-EXPENSIVE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-STEADFAST-LOYALTY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-EXPENSIVE"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGuerrillaWarfareReturnStandbyGraveyardCards()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-guerrilla-warfare-return-standby-graveyard.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-GRAVE-STANDBY-001", "P1-GRAVE-STANDBY-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-NON-STANDBY-001", "P1-SPELL-GUERRILLA-WARFARE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.Standby], result.FinalState.CardObjects["P1-GRAVE-STANDBY-001"].Tags);
        Assert.Equal([CardObjectTags.Standby], result.FinalState.CardObjects["P1-GRAVE-STANDBY-002"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsGuerrillaWarfareWhenTargetIsNotStandby()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-GUERRILLA-WARFARE"],
                    Graveyard = ["P1-GRAVE-NON-STANDBY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-NON-STANDBY-001"] = new("P1-GRAVE-NON-STANDBY-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-guerrilla-warfare-non-standby-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-GUERRILLA-WARFARE",
                "OGN·264/298",
                ["P1-GRAVE-NON-STANDBY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-GUERRILLA-WARFARE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-NON-STANDBY-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCallOfTheShadowsGiveEphemeralDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-call-of-the-shadows-give-ephemeral-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SPELL-CALL-OF-THE-SHADOWS"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCallOfTheShadowsWhenTargetAlreadyHasEphemeral()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CALL-OF-THE-SHADOWS"],
                    Base = ["P1-UNIT-EPHEMERAL"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-EPHEMERAL"] = new("P1-UNIT-EPHEMERAL", tags: [CardObjectTags.Ephemeral])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-call-of-the-shadows-ephemeral-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CALL-OF-THE-SHADOWS",
                "UNL-165/219",
                ["P1-UNIT-EPHEMERAL"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CALL-OF-THE-SHADOWS"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-EPHEMERAL"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFlowingTimeMirrorBattlefieldUnitEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-flowing-time-mirror-battlefield-unit-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P2-BATTLEFIELD-FLOWING-TIME-MIRROR-001"].Tags);
        Assert.Equal(["P1-SPELL-FLOWING-TIME-MIRROR"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFlowingTimeMirrorEquipmentEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-flowing-time-mirror-equipment-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P2-BASE-EQUIPMENT-FLOWING-TIME-MIRROR-001"].Tags);
        Assert.Equal(["P1-SPELL-FLOWING-TIME-MIRROR"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFlowingTimeMirrorAgainstBaseUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FLOWING-TIME-MIRROR"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-FLOWING-TIME-MIRROR-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-FLOWING-TIME-MIRROR-001"] = new("P2-BASE-FLOWING-TIME-MIRROR-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-flowing-time-mirror-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FLOWING-TIME-MIRROR",
                "OGN·180/298",
                ["P2-BASE-FLOWING-TIME-MIRROR-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FLOWING-TIME-MIRROR"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-FLOWING-TIME-MIRROR-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAshesToAshesEquipmentEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ashes-to-ashes-equipment-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P2-BASE-EQUIPMENT-ASHES-TO-ASHES-001"].Tags);
        Assert.Equal(["P1-SPELL-ASHES-TO-ASHES"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAshesToAshesAgainstUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ASHES-TO-ASHES"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ASHES-TO-ASHES-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ASHES-TO-ASHES-UNIT-001"] = new("P2-BASE-ASHES-TO-ASHES-UNIT-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ashes-to-ashes-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ASHES-TO-ASHES",
                "UNL-070/219",
                ["P2-BASE-ASHES-TO-ASHES-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ASHES-TO-ASHES"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ASHES-TO-ASHES-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSigilBurstDestroyEquipmentAndTargetControllerDraws()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sigil-burst-destroy-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-SIGIL-BURST-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-DRAW-SIGIL-BURST-001", "P2-DRAW-SIGIL-BURST-002"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BASE-EQUIPMENT-SIGIL-BURST-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSigilBurstAgainstUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SIGIL-BURST"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-SIGIL-BURST-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-SIGIL-BURST-UNIT-001"] = new("P2-BASE-SIGIL-BURST-UNIT-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sigil-burst-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SIGIL-BURST",
                "SFD·005/221",
                ["P2-BASE-SIGIL-BURST-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SIGIL-BURST"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-SIGIL-BURST-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBackAgainstWallDoublesFriendlyPowerAndEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-back-against-wall-double-power-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-BACK-AGAINST-WALL-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-BACK-AGAINST-WALL-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-BATTLEFIELD-BACK-AGAINST-WALL-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBackAgainstWallAgainstEnemyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BACK-AGAINST-WALL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"] = new("P2-BATTLEFIELD-BACK-AGAINST-WALL-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-back-against-wall-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BACK-AGAINST-WALL",
                "OGN·069/298",
                ["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BACK-AGAINST-WALL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"], result.State.PlayerZones["P2"].Battlefields);
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
    public async Task CoreRuleEnginePlaysPoroSnaxEquipmentDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-poro-snax-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-PORO-SNAX"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-DRAW-PORO-SNAX-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-PORO-SNAX"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPoroSnaxWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-PORO-SNAX"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-PORO-SNAX-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-PORO-SNAX-TARGET-001"] = new(
                    "P2-PORO-SNAX-TARGET-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-poro-snax-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-PORO-SNAX",
                "SFD·046/221",
                ["P2-PORO-SNAX-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-PORO-SNAX"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShurelyasRequiemEquipmentReadyAll()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shurelyas-requiem-equipment-ready-all.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-SHURELYA-BASE-UNIT-001", "P1-EQUIPMENT-SHURELYAS-REQUIEM"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-SHURELYA-BASE-UNIT-001"].IsExhausted);
        Assert.False(result.FinalState.CardObjects["P1-SHURELYA-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-SHURELYA-BASE-UNIT-001"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SHURELYAS-REQUIEM"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsShurelyasRequiemWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SHURELYAS-REQUIEM"],
                    Base = ["P1-SHURELYA-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SHURELYA-BASE-UNIT-001"] = new(
                    "P1-SHURELYA-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-shurelyas-requiem-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SHURELYAS-REQUIEM",
                "SFD·192/221",
                ["P1-SHURELYA-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SHURELYAS-REQUIEM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFutureForgeEquipmentCreateMinion()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-future-forge-equipment-create-minion.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-EQUIPMENT-FUTURE-FORGE", "P1-EQUIPMENT-FUTURE-FORGE-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-FUTURE-FORGE"].Tags);
        Assert.Equal(1, result.FinalState.CardObjects["P1-EQUIPMENT-FUTURE-FORGE-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-EQUIPMENT-FUTURE-FORGE-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFutureForgeWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-FUTURE-FORGE"],
                    Base = ["P1-FUTURE-FORGE-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FUTURE-FORGE-BASE-UNIT-001"] = new(
                    "P1-FUTURE-FORGE-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-future-forge-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-FUTURE-FORGE",
                "OGN·212/298",
                ["P1-FUTURE-FORGE-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-FUTURE-FORGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScrapHeapEquipmentDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scrap-heap-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SCRAP-HEAP"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-DRAW-SCRAP-HEAP-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SCRAP-HEAP"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsScrapHeapWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SCRAP-HEAP"],
                    Base = ["P1-SCRAP-HEAP-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SCRAP-HEAP-BASE-UNIT-001"] = new(
                    "P1-SCRAP-HEAP-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-scrap-heap-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SCRAP-HEAP",
                "OGN·182/298",
                ["P1-SCRAP-HEAP-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRAP-HEAP"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteLanternEquipmentCreateSprite()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-lantern-equipment-create-sprite.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-EQUIPMENT-SPRITE-LANTERN", "P1-EQUIPMENT-SPRITE-LANTERN-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P1-EQUIPMENT-SPRITE-LANTERN"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-EQUIPMENT-SPRITE-LANTERN-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-EQUIPMENT-SPRITE-LANTERN-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSpriteLanternWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SPRITE-LANTERN"],
                    Base = ["P1-SPRITE-LANTERN-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPRITE-LANTERN-BASE-UNIT-001"] = new(
                    "P1-SPRITE-LANTERN-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sprite-lantern-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SPRITE-LANTERN",
                "UNL-078/219",
                ["P1-SPRITE-LANTERN-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SPRITE-LANTERN"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSumpworksMapEquipmentEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sumpworks-map-equipment-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SUMPWORKS-MAP"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P1-EQUIPMENT-SUMPWORKS-MAP"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSumpworksMapWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SUMPWORKS-MAP"],
                    Base = ["P1-SUMPWORKS-MAP-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SUMPWORKS-MAP-BASE-UNIT-001"] = new(
                    "P1-SUMPWORKS-MAP-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sumpworks-map-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SUMPWORKS-MAP",
                "UNL-085/219",
                ["P1-SUMPWORKS-MAP-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SUMPWORKS-MAP"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScryingBlossomEquipmentExhausted()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scrying-blossom-equipment-exhausted.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SCRYING-BLOSSOM"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SCRYING-BLOSSOM"].Tags);
        Assert.True(result.FinalState.CardObjects["P1-EQUIPMENT-SCRYING-BLOSSOM"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsScryingBlossomWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SCRYING-BLOSSOM"],
                    Base = ["P1-SCRYING-BLOSSOM-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SCRYING-BLOSSOM-BASE-UNIT-001"] = new(
                    "P1-SCRYING-BLOSSOM-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-scrying-blossom-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SCRYING-BLOSSOM",
                "UNL-136/219",
                ["P1-SCRYING-BLOSSOM-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRYING-BLOSSOM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMagicBeansEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-magic-beans-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-MAGIC-BEANS"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-MAGIC-BEANS"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMagicBeansWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-MAGIC-BEANS"],
                    Base = ["P1-MAGIC-BEANS-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MAGIC-BEANS-BASE-UNIT-001"] = new(
                    "P1-MAGIC-BEANS-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-magic-beans-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-MAGIC-BEANS",
                "UNL-011/219",
                ["P1-MAGIC-BEANS-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-MAGIC-BEANS"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSteelBallistaEquipmentExhausted()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-steel-ballista-equipment-exhausted.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-STEEL-BALLISTA"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-STEEL-BALLISTA"].Tags);
        Assert.True(result.FinalState.CardObjects["P1-EQUIPMENT-STEEL-BALLISTA"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSteelBallistaWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-STEEL-BALLISTA"],
                    Base = ["P1-STEEL-BALLISTA-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-STEEL-BALLISTA-BASE-UNIT-001"] = new(
                    "P1-STEEL-BALLISTA-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-steel-ballista-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-STEEL-BALLISTA",
                "OGN·017/298",
                ["P1-STEEL-BALLISTA-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-STEEL-BALLISTA"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHeartOfIceEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-heart-of-ice-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-HEART-OF-ICE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-HEART-OF-ICE"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHeartOfIceWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-HEART-OF-ICE"],
                    Base = ["P1-HEART-OF-ICE-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEART-OF-ICE-BASE-UNIT-001"] = new(
                    "P1-HEART-OF-ICE-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-heart-of-ice-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-HEART-OF-ICE",
                "SFD·052/221",
                ["P1-HEART-OF-ICE-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-HEART-OF-ICE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRemorseOrbEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-remorse-orb-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-REMORSE-ORB"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-REMORSE-ORB"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRemorseOrbWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-REMORSE-ORB"],
                    Base = ["P1-REMORSE-ORB-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REMORSE-ORB-BASE-UNIT-001"] = new(
                    "P1-REMORSE-ORB-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-remorse-orb-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-REMORSE-ORB",
                "OGN·090/298",
                ["P1-REMORSE-ORB-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-REMORSE-ORB"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSoulSwordEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-soul-sword-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SOUL-SWORD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SOUL-SWORD"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-SOUL-SWORD"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSoulSwordWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SOUL-SWORD"],
                    Base = ["P1-SOUL-SWORD-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SOUL-SWORD-BASE-UNIT-001"] = new(
                    "P1-SOUL-SWORD-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-soul-sword-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SOUL-SWORD",
                "UNL-039/219",
                ["P1-SOUL-SWORD-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SOUL-SWORD"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysJaggedDirkEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-jagged-dirk-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-JAGGED-DIRK"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-JAGGED-DIRK"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-JAGGED-DIRK"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsJaggedDirkWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-JAGGED-DIRK"],
                    Base = ["P1-JAGGED-DIRK-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-JAGGED-DIRK-BASE-UNIT-001"] = new(
                    "P1-JAGGED-DIRK-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jagged-dirk-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-JAGGED-DIRK",
                "SFD·009/221",
                ["P1-JAGGED-DIRK-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-JAGGED-DIRK"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDoransShieldEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dorans-shield-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-DORANS-SHIELD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-SHIELD"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-SHIELD"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDoransShieldWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-DORANS-SHIELD"],
                    Base = ["P1-DORANS-SHIELD-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-SHIELD-BASE-UNIT-001"] = new(
                    "P1-DORANS-SHIELD-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dorans-shield-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-DORANS-SHIELD",
                "SFD·033/221",
                ["P1-DORANS-SHIELD-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-DORANS-SHIELD"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHextechInfusedBulwarkEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hextech-infused-bulwark-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHextechInfusedBulwarkWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"],
                    Base = ["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"] = new(
                    "P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hextech-infused-bulwark-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK",
                "SFD·073/221",
                ["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDoransBladeEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dorans-blade-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-DORANS-BLADE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-BLADE"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-BLADE"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDoransBladeWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-DORANS-BLADE"],
                    Base = ["P1-DORANS-BLADE-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-BLADE-BASE-UNIT-001"] = new(
                    "P1-DORANS-BLADE-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dorans-blade-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-DORANS-BLADE",
                "SFD·095/221",
                ["P1-DORANS-BLADE-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-DORANS-BLADE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysDoransRingEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-dorans-ring-equipment.fixture.json",
            "P1-EQUIPMENT-DORANS-RING");

    [Fact]
    public Task CoreRuleEngineRejectsDoransRingWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-DORANS-RING",
            "SFD·124/221",
            "P1-DORANS-RING-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysVanguardsEyeEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-vanguards-eye-equipment.fixture.json",
            "P1-EQUIPMENT-VANGUARDS-EYE");

    [Fact]
    public Task CoreRuleEngineRejectsVanguardsEyeWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-VANGUARDS-EYE",
            "SFD·153/221",
            "P1-VANGUARDS-EYE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysRecurveBowEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-recurve-bow-equipment.fixture.json",
            "P1-EQUIPMENT-RECURVE-BOW");

    [Fact]
    public Task CoreRuleEngineRejectsRecurveBowWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-RECURVE-BOW",
            "SFD·016/221",
            "P1-RECURVE-BOW-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysBrutalizerEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-brutalizer-equipment.fixture.json",
            "P1-EQUIPMENT-BRUTALIZER");

    [Fact]
    public Task CoreRuleEngineRejectsBrutalizerWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-BRUTALIZER",
            "SFD·042/221",
            "P1-BRUTALIZER-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysGuardianAngelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-guardian-angel-equipment.fixture.json",
            "P1-EQUIPMENT-GUARDIAN-ANGEL");

    [Fact]
    public Task CoreRuleEngineRejectsGuardianAngelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-GUARDIAN-ANGEL",
            "SFD·051/221",
            "P1-GUARDIAN-ANGEL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysHexdrinkerEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hexdrinker-equipment.fixture.json",
            "P1-EQUIPMENT-HEXDRINKER");

    [Fact]
    public Task CoreRuleEngineRejectsHexdrinkerWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-HEXDRINKER",
            "SFD·102/221",
            "P1-HEXDRINKER-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysWarmogsArmorEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-warmogs-armor-equipment.fixture.json",
            "P1-EQUIPMENT-WARMOGS-ARMOR");

    [Fact]
    public Task CoreRuleEngineRejectsWarmogsArmorWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-WARMOGS-ARMOR",
            "SFD·108/221",
            "P1-WARMOGS-ARMOR-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysTrinityForceEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-trinity-force-equipment.fixture.json",
            "P1-EQUIPMENT-TRINITY-FORCE");

    [Fact]
    public Task CoreRuleEngineRejectsTrinityForceWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-TRINITY-FORCE",
            "SFD·115/221",
            "P1-TRINITY-FORCE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysBootsOfSwiftnessEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-boots-of-swiftness-equipment.fixture.json",
            "P1-EQUIPMENT-BOOTS-OF-SWIFTNESS");

    [Fact]
    public Task CoreRuleEngineRejectsBootsOfSwiftnessWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BOOTS-OF-SWIFTNESS",
            "SFD·133/221",
            "P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysCullEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-cull-equipment.fixture.json",
            "P1-EQUIPMENT-CULL");

    [Fact]
    public Task CoreRuleEngineRejectsCullWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-CULL",
            "SFD·134/221",
            "P1-CULL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSacredShearsEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sacred-shears-equipment.fixture.json",
            "P1-EQUIPMENT-SACRED-SHEARS");

    [Fact]
    public Task CoreRuleEngineRejectsSacredShearsWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SACRED-SHEARS",
            "SFD·172/221",
            "P1-SACRED-SHEARS-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysBfSwordEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-bf-sword-equipment.fixture.json",
            "P1-EQUIPMENT-BF-SWORD");

    [Fact]
    public Task CoreRuleEngineRejectsBfSwordWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-BF-SWORD",
            "SFD·161/221",
            "P1-BF-SWORD-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysWanderersGuidebookEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-wanderers-guidebook-equipment.fixture.json",
            "P1-EQUIPMENT-WANDERERS-GUIDEBOOK");

    [Fact]
    public Task CoreRuleEngineRejectsWanderersGuidebookWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-WANDERERS-GUIDEBOOK",
            "SFD·086/221",
            "P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysArionsFallEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-arions-fall-equipment.fixture.json",
            "P1-EQUIPMENT-ARIONS-FALL");

    [Fact]
    public Task CoreRuleEngineRejectsArionsFallWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-ARIONS-FALL",
            "SFD·030/221",
            "P1-ARIONS-FALL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysHuntersMacheteEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hunters-machete-equipment.fixture.json",
            "P1-EQUIPMENT-HUNTERS-MACHETE");

    [Fact]
    public Task CoreRuleEngineRejectsHuntersMacheteWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-HUNTERS-MACHETE",
            "UNL-096/219",
            "P1-HUNTERS-MACHETE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysWitheredBattleaxeEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-withered-battleaxe-equipment.fixture.json",
            "P1-EQUIPMENT-WITHERED-BATTLEAXE");

    [Fact]
    public Task CoreRuleEngineRejectsWitheredBattleaxeWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-WITHERED-BATTLEAXE",
            "UNL-019/219",
            "P1-WITHERED-BATTLEAXE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysBoneClubEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-bone-club-equipment.fixture.json",
            "P1-EQUIPMENT-BONE-CLUB");

    [Fact]
    public Task CoreRuleEngineRejectsBoneClubWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BONE-CLUB",
            "SFD·118/221",
            "P1-BONE-CLUB-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysAncientSteleEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ancient-stele-equipment.fixture.json",
            "P1-EQUIPMENT-ANCIENT-STELE");

    [Fact]
    public Task CoreRuleEngineRejectsAncientSteleWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-ANCIENT-STELE",
            "SFD·117/221",
            "P1-ANCIENT-STELE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysHextechAnomalyEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hextech-anomaly-equipment.fixture.json",
            "P1-EQUIPMENT-HEXTECH-ANOMALY");

    [Fact]
    public Task CoreRuleEngineRejectsHextechAnomalyWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-HEXTECH-ANOMALY",
            "SFD·083/221",
            "P1-HEXTECH-ANOMALY-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysEnergyChannelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-energy-channel-equipment.fixture.json",
            "P1-EQUIPMENT-ENERGY-CHANNEL");

    [Fact]
    public Task CoreRuleEngineRejectsEnergyChannelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-ENERGY-CHANNEL",
            "OGN·098/298",
            "P1-ENERGY-CHANNEL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysTimeGateEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-time-gate-equipment.fixture.json",
            "P1-EQUIPMENT-TIME-GATE");

    [Fact]
    public Task CoreRuleEngineRejectsTimeGateWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-TIME-GATE",
            "SFD·078/221",
            "P1-TIME-GATE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysRavenTomeEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-raven-tome-equipment.fixture.json",
            "P1-EQUIPMENT-RAVEN-TOME");

    [Fact]
    public Task CoreRuleEngineRejectsRavenTomeWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-RAVEN-TOME",
            "OGN·032/298",
            "P1-RAVEN-TOME-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSunDiscEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sun-disc-equipment.fixture.json",
            "P1-EQUIPMENT-SUN-DISC");

    [Fact]
    public Task CoreRuleEngineRejectsSunDiscWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SUN-DISC",
            "OGN·021/298",
            "P1-SUN-DISC-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysForesightMaskEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-foresight-mask-equipment.fixture.json",
            "P1-EQUIPMENT-FORESIGHT-MASK");

    [Fact]
    public Task CoreRuleEngineRejectsForesightMaskWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-FORESIGHT-MASK",
            "OGN·060/298",
            "P1-FORESIGHT-MASK-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSolariAltarEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-solari-altar-equipment.fixture.json",
            "P1-EQUIPMENT-SOLARI-ALTAR");

    [Fact]
    public Task CoreRuleEngineRejectsSolariAltarWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-SOLARI-ALTAR",
            "OGN·072/298",
            "P1-SOLARI-ALTAR-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysChemtechBarrelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-chemtech-barrel-equipment.fixture.json",
            "P1-EQUIPMENT-CHEMTECH-BARREL");

    [Fact]
    public Task CoreRuleEngineRejectsChemtechBarrelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-CHEMTECH-BARREL",
            "SFD·063/221",
            "P1-CHEMTECH-BARREL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSoulWheelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-soul-wheel-equipment.fixture.json",
            "P1-EQUIPMENT-SOUL-WHEEL");

    [Fact]
    public Task CoreRuleEngineRejectsSoulWheelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SOUL-WHEEL",
            "SFD·144/221",
            "P1-SOUL-WHEEL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysMushroomBagEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-mushroom-bag-equipment.fixture.json",
            "P1-EQUIPMENT-MUSHROOM-BAG");

    [Fact]
    public Task CoreRuleEngineRejectsMushroomBagWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-MUSHROOM-BAG",
            "OGN·101/298",
            "P1-MUSHROOM-BAG-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysArenaBarEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-arena-bar-equipment.fixture.json",
            "P1-EQUIPMENT-ARENA-BAR");

    [Fact]
    public Task CoreRuleEngineRejectsArenaBarWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-ARENA-BAR",
            "OGN·124/298",
            "P1-ARENA-BAR-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysPirateHideoutEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-pirate-hideout-equipment.fixture.json",
            "P1-EQUIPMENT-PIRATE-HIDEOUT");

    [Fact]
    public Task CoreRuleEngineRejectsPirateHideoutWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-PIRATE-HIDEOUT",
            "OGN·143/298",
            "P1-PIRATE-HIDEOUT-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysForgottenSignpostEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-forgotten-signpost-equipment.fixture.json",
            "P1-EQUIPMENT-FORGOTTEN-SIGNPOST");

    [Fact]
    public Task CoreRuleEngineRejectsForgottenSignpostWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-FORGOTTEN-SIGNPOST",
            "UNL-045/219",
            "P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysFrozenGemEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-frozen-gem-equipment.fixture.json",
            "P1-EQUIPMENT-FROZEN-GEM");

    [Fact]
    public Task CoreRuleEngineRejectsFrozenGemWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-FROZEN-GEM",
            "UNL-074/219",
            "P1-FROZEN-GEM-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysCrumblingPalaceEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-crumbling-palace-equipment.fixture.json",
            "P1-EQUIPMENT-CRUMBLING-PALACE");

    [Fact]
    public Task CoreRuleEngineRejectsCrumblingPalaceWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-CRUMBLING-PALACE",
            "UNL-088/219",
            "P1-CRUMBLING-PALACE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysScarletRoseEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-scarlet-rose-equipment.fixture.json",
            "P1-EQUIPMENT-SCARLET-ROSE");

    [Fact]
    public Task CoreRuleEngineRejectsScarletRoseWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-SCARLET-ROSE",
            "UNL-109/219",
            "P1-SCARLET-ROSE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysReversalShardEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-reversal-shard-equipment.fixture.json",
            "P1-EQUIPMENT-REVERSAL-SHARD");

    [Fact]
    public Task CoreRuleEngineRejectsReversalShardWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            6,
            "P1-EQUIPMENT-REVERSAL-SHARD",
            "UNL-174/219",
            "P1-REVERSAL-SHARD-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysAssemblyRackEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-assembly-rack-equipment.fixture.json",
            "P1-EQUIPMENT-ASSEMBLY-RACK");

    [Fact]
    public Task CoreRuleEngineRejectsAssemblyRackWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-ASSEMBLY-RACK",
            "SFD·019/221",
            "P1-ASSEMBLY-RACK-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSfurSongEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sfur-song-equipment.fixture.json",
            "P1-EQUIPMENT-SFUR-SONG");

    [Fact]
    public Task CoreRuleEngineRejectsSfurSongWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-SFUR-SONG",
            "SFD·059/221",
            "P1-SFUR-SONG-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysZDriveEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-z-drive-equipment.fixture.json",
            "P1-EQUIPMENT-Z-DRIVE");

    [Fact]
    public Task CoreRuleEngineRejectsZDriveWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-Z-DRIVE",
            "SFD·090/221",
            "P1-Z-DRIVE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysVanguardArmoryEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-vanguard-armory-equipment.fixture.json",
            "P1-EQUIPMENT-VANGUARD-ARMORY");

    [Fact]
    public Task CoreRuleEngineRejectsVanguardArmoryWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            7,
            "P1-EQUIPMENT-VANGUARD-ARMORY",
            "SFD·168/221",
            "P1-VANGUARD-ARMORY-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysRemembranceAltarEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-remembrance-altar-equipment.fixture.json",
            "P1-EQUIPMENT-REMEMBRANCE-ALTAR");

    [Fact]
    public Task CoreRuleEngineRejectsRemembranceAltarWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-REMEMBRANCE-ALTAR",
            "SFD·169/221",
            "P1-REMEMBRANCE-ALTAR-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysRageSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-rage-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-RAGE-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsRageSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-RAGE-SIGIL",
            "SFD·222/221",
            "P1-RAGE-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysFocusSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-focus-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-FOCUS-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsFocusSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-FOCUS-SIGIL",
            "SFD·226/221",
            "P1-FOCUS-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysInsightSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-insight-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-INSIGHT-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsInsightSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-INSIGHT-SIGIL",
            "SFD·229/221",
            "P1-INSIGHT-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysPowerSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-power-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-POWER-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsPowerSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-POWER-SIGIL",
            "SFD·231/221",
            "P1-POWER-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysDiscordSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-discord-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-DISCORD-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsDiscordSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-DISCORD-SIGIL",
            "SFD·234/221",
            "P1-DISCORD-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysUnitySigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-unity-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-UNITY-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsUnitySigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-UNITY-SIGIL",
            "SFD·238/221",
            "P1-UNITY-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOgnRageSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-rage-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-RAGE-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnRageSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-RAGE-SIGIL",
            "OGN·040/298",
            "P1-OGN-RAGE-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOgnFocusSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-focus-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-FOCUS-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnFocusSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-FOCUS-SIGIL",
            "OGN·081/298",
            "P1-OGN-FOCUS-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOgnInsightSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-insight-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-INSIGHT-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnInsightSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-INSIGHT-SIGIL",
            "OGN·120/298",
            "P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOgnPowerSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-power-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-POWER-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnPowerSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-POWER-SIGIL",
            "OGN·163/298",
            "P1-OGN-POWER-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOgnDiscordSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-discord-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-DISCORD-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnDiscordSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-DISCORD-SIGIL",
            "OGN·204/298",
            "P1-OGN-DISCORD-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOgnUnitySigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-unity-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-UNITY-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnUnitySigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-UNITY-SIGIL",
            "OGN·245/298",
            "P1-OGN-UNITY-SIGIL-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysWondrousPackEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-wondrous-pack-equipment.fixture.json",
            "P1-EQUIPMENT-WONDROUS-PACK");

    [Fact]
    public Task CoreRuleEngineRejectsWondrousPackWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-WONDROUS-PACK",
            "OGN·181/298",
            "P1-WONDROUS-PACK-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSirenEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-siren-equipment.fixture.json",
            "P1-EQUIPMENT-SIREN");

    [Fact]
    public Task CoreRuleEngineRejectsSirenWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SIREN",
            "OGN·184/298",
            "P1-SIREN-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysOwnerlessTreasureEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ownerless-treasure-equipment.fixture.json",
            "P1-EQUIPMENT-OWNERLESS-TREASURE");

    [Fact]
    public Task CoreRuleEngineRejectsOwnerlessTreasureWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-OWNERLESS-TREASURE",
            "OGN·186/298",
            "P1-OWNERLESS-TREASURE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysScavengingWhizEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-scavenging-whiz-equipment.fixture.json",
            "P1-EQUIPMENT-SCAVENGING-WHIZ");

    [Fact]
    public Task CoreRuleEngineRejectsScavengingWhizWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SCAVENGING-WHIZ",
            "OGN·099/298",
            "P1-SCAVENGING-WHIZ-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysMistfallBladeyardEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-mistfall-bladeyard-equipment.fixture.json",
            "P1-EQUIPMENT-MISTFALL-BLADEYARD");

    [Fact]
    public Task CoreRuleEngineRejectsMistfallBladeyardWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-MISTFALL-BLADEYARD",
            "OGN·152/298",
            "P1-MISTFALL-BLADEYARD-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysShimmeringAuroraEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-shimmering-aurora-equipment.fixture.json",
            "P1-EQUIPMENT-SHIMMERING-AURORA");

    [Fact]
    public Task CoreRuleEngineRejectsShimmeringAuroraWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            9,
            "P1-EQUIPMENT-SHIMMERING-AURORA",
            "OGN·160/298",
            "P1-SHIMMERING-AURORA-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSolariEmblemEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-solari-emblem-equipment.fixture.json",
            "P1-EQUIPMENT-SOLARI-EMBLEM");

    [Fact]
    public Task CoreRuleEngineRejectsSolariEmblemWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-SOLARI-EMBLEM",
            "OGN·227/298",
            "P1-SOLARI-EMBLEM-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysVanguardHelmEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-vanguard-helm-equipment.fixture.json",
            "P1-EQUIPMENT-VANGUARD-HELM");

    [Fact]
    public Task CoreRuleEngineRejectsVanguardHelmWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-VANGUARD-HELM",
            "OGN·228/298",
            "P1-VANGUARD-HELM-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysHoneyfruitEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-honeyfruit-equipment.fixture.json",
            "P1-EQUIPMENT-HONEYFRUIT",
            expectedIsExhausted: true);

    [Fact]
    public Task CoreRuleEngineRejectsHoneyfruitWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-HONEYFRUIT",
            "UNL-049/219",
            "P1-HONEYFRUIT-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysLastRitesEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-last-rites-equipment.fixture.json",
            "P1-EQUIPMENT-LAST-RITES");

    [Fact]
    public Task CoreRuleEngineRejectsLastRitesWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-LAST-RITES",
            "SFD·150/221",
            "P1-LAST-RITES-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysBladeOfRuinedKingEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-blade-of-ruined-king-equipment.fixture.json",
            "P1-EQUIPMENT-BLADE-OF-RUINED-KING");

    [Fact]
    public Task CoreRuleEngineRejectsBladeOfRuinedKingWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BLADE-OF-RUINED-KING",
            "SFD·178/221",
            "P1-BLADE-OF-RUINED-KING-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysMysteriousWeaponEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-mysterious-weapon-equipment.fixture.json",
            "P1-EQUIPMENT-MYSTERIOUS-WEAPON");

    [Fact]
    public Task CoreRuleEngineRejectsMysteriousWeaponWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-MYSTERIOUS-WEAPON",
            "OGN·023/298",
            "P1-MYSTERIOUS-WEAPON-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysSeaMonsterHookEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sea-monster-hook-equipment.fixture.json",
            "P1-EQUIPMENT-SEA-MONSTER-HOOK");

    [Fact]
    public Task CoreRuleEngineRejectsSeaMonsterHookWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-SEA-MONSTER-HOOK",
            "OGN·242/298",
            "P1-SEA-MONSTER-HOOK-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysPetriciteMonumentEquipmentEphemeral() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-petricite-monument-equipment-ephemeral.fixture.json",
            "P1-EQUIPMENT-PETRICITE-MONUMENT",
            expectedTags: [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral]);

    [Fact]
    public Task CoreRuleEngineRejectsPetriciteMonumentWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-PETRICITE-MONUMENT",
            "SFD·104/221",
            "P1-PETRICITE-MONUMENT-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysZhonyasHourglassEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-zhonyas-hourglass-equipment.fixture.json",
            "P1-EQUIPMENT-ZHONYAS-HOURGLASS");

    [Fact]
    public Task CoreRuleEngineRejectsZhonyasHourglassWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-ZHONYAS-HOURGLASS",
            "OGN·077/298",
            "P1-ZHONYAS-HOURGLASS-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysEdgeOfNightEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-edge-of-night-equipment.fixture.json",
            "P1-EQUIPMENT-EDGE-OF-NIGHT");

    [Fact]
    public Task CoreRuleEngineRejectsEdgeOfNightWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-EDGE-OF-NIGHT",
            "SFD·139/221",
            "P1-EDGE-OF-NIGHT-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysHearthfireCloakEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hearthfire-cloak-equipment.fixture.json",
            "P1-EQUIPMENT-HEARTHFIRE-CLOAK");

    [Fact]
    public Task CoreRuleEngineRejectsHearthfireCloakWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-HEARTHFIRE-CLOAK",
            "SFD·190/221",
            "P1-HEARTHFIRE-CLOAK-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysRabadonsDeathcapEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-rabadons-deathcap-equipment.fixture.json",
            "P1-EQUIPMENT-RABADONS-DEATHCAP");

    [Fact]
    public Task CoreRuleEngineRejectsRabadonsDeathcapWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-RABADONS-DEATHCAP",
            "SFD·191/221",
            "P1-RABADONS-DEATHCAP-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysBlastConeEquipmentNoMove() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-blast-cone-equipment-no-move.fixture.json",
            "P1-EQUIPMENT-BLAST-CONE");

    [Fact]
    public Task CoreRuleEngineRejectsBlastConeWhenTargetsAreProvidedInNoMovePath() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-BLAST-CONE",
            "UNL-133/219",
            "P1-BLAST-CONE-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysDeathListEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-death-list-equipment.fixture.json",
            "P1-EQUIPMENT-DEATH-LIST");

    [Fact]
    public Task CoreRuleEngineRejectsDeathListWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-DEATH-LIST",
            "UNL-138/219",
            "P1-DEATH-LIST-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysCursedSarcophagusEquipmentBanishesGraveyardUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cursed-sarcophagus-equipment-banish-graveyard-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-CURSED-SARCOPHAGUS"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-SPELL-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-GRAVE-UNIT-001", "P1-GRAVE-UNIT-002"], result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-CURSED-SARCOPHAGUS"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsCursedSarcophagusWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-CURSED-SARCOPHAGUS",
            "UNL-148/219",
            "P1-CURSED-SARCOPHAGUS-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysScryingShellEquipmentPredictRecycleTopCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scrying-shell-equipment-predict-recycle.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SCRYING-SHELL"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SCRYING-SHELL-KEEP-001", "P1-SCRYING-SHELL-RECYCLE-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SCRYING-SHELL"].Tags);
    }

    [Fact]
    public Task CoreRuleEnginePlaysScryingShellEquipmentPredictNoRecycle() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-scrying-shell-equipment-predict-no-recycle.fixture.json",
            "P1-EQUIPMENT-SCRYING-SHELL");

    [Fact]
    public async Task CoreRuleEngineRejectsScryingShellWhenPredictTargetIsOutsideTopCard()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SCRYING-SHELL"],
                    MainDeck = ["P1-SCRYING-SHELL-TOP-001", "P1-SCRYING-SHELL-SECOND-001"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-scrying-shell-second-card", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SCRYING-SHELL",
                "UNL-161/219",
                ["P1-SCRYING-SHELL-SECOND-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRYING-SHELL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SCRYING-SHELL-TOP-001", "P1-SCRYING-SHELL-SECOND-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysForcedConscriptionControlSmallEnemyRecall()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-forced-conscription-control-small-enemy-recall.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-FORCED-CONSCRIPTION-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-FORCED-CONSCRIPTION-OTHER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P2-FORCED-CONSCRIPTION-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsForcedConscriptionWhenTargetPowerAboveThree()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FORCED-CONSCRIPTION"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-FORCED-CONSCRIPTION-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-FORCED-CONSCRIPTION-LARGE-001"] = new(
                    "P2-FORCED-CONSCRIPTION-LARGE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-forced-conscription-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FORCED-CONSCRIPTION",
                "UNL-140/219",
                ["P2-FORCED-CONSCRIPTION-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FORCED-CONSCRIPTION"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-FORCED-CONSCRIPTION-LARGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTakenForARideControlEnemyRecall()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-taken-for-a-ride-control-enemy-recall.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-TAKEN-FOR-A-RIDE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P2-TAKEN-FOR-A-RIDE-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsTakenForARideWhenTargetIsNotUnit()
    {
        var state = PunishmentState(mana: 8) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-TAKEN-FOR-A-RIDE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"] = new(
                    "P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-taken-for-a-ride-equipment-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-TAKEN-FOR-A-RIDE",
                "OGN·203/298",
                ["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(8, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-TAKEN-FOR-A-RIDE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBoneclubPromoEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-boneclub-promo-equipment.fixture.json",
            "P1-EQUIPMENT-BONECLUB-PROMO");

    [Fact]
    public Task CoreRuleEngineRejectsBoneclubPromoWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BONECLUB-PROMO",
            "SFD·118a/221·P",
            "P1-BONECLUB-PROMO-BASE-UNIT-001");

    [Fact]
    public Task CoreRuleEnginePlaysHextechGauntletEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hextech-gauntlet-equipment.fixture.json",
            "P1-EQUIPMENT-HEXTECH-GAUNTLET");

    [Fact]
    public Task CoreRuleEngineRejectsHextechGauntletWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-HEXTECH-GAUNTLET",
            "UNL-188/219",
            "P1-HEXTECH-GAUNTLET-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysTreasureGolemCreateFourGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-treasure-golem-create-four-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-UNIT-TREASURE-GOLEM",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-001",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-002",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-003",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-004"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(9, result.FinalState.CardObjects["P1-UNIT-TREASURE-GOLEM"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-TREASURE-GOLEM"].Tags);
        foreach (var tokenObjectId in result.FinalState.PlayerZones["P1"].Base.Skip(1))
        {
            Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects[tokenObjectId].Tags);
            Assert.True(result.FinalState.CardObjects[tokenObjectId].IsExhausted);
        }
    }

    [Fact]
    public Task CoreRuleEngineRejectsTreasureGolemWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            8,
            "P1-UNIT-TREASURE-GOLEM",
            "SFD·174/221",
            "P1-TREASURE-GOLEM-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysFaithfulCraftsmanCreateMinion()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-faithful-craftsman-create-minion.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-FAITHFUL-CRAFTSMAN", "P1-UNIT-FAITHFUL-CRAFTSMAN-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            3,
            "P1-UNIT-FAITHFUL-CRAFTSMAN",
            "OGN·211/298",
            "P1-FAITHFUL-CRAFTSMAN-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysRoyalGuardCreateSandSoldier()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-royal-guard-create-sand-soldier.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-ROYAL-GUARD", "P1-UNIT-ROYAL-GUARD-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ROYAL-GUARD"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ROYAL-GUARD-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.SandSoldier],
            result.FinalState.CardObjects["P1-UNIT-ROYAL-GUARD-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsRoyalGuardWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            4,
            "P1-UNIT-ROYAL-GUARD",
            "SFD·157/221",
            "P1-ROYAL-GUARD-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysBlueflameGuardianPowerPlusEight()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blueflame-guardian-power-plus-8.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BLUEFLAME-TARGET-001", "P1-UNIT-BLUEFLAME-GUARDIAN"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(8, result.FinalState.CardObjects["P1-UNIT-BLUEFLAME-GUARDIAN"].Power);
        Assert.Equal(10, result.FinalState.CardObjects["P1-BLUEFLAME-TARGET-001"].Power);
        Assert.Equal(8, result.FinalState.CardObjects["P1-BLUEFLAME-TARGET-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public Task CoreRuleEngineRejectsBlueflameGuardianWhenTargetIsNotUnit() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            8,
            "P1-UNIT-BLUEFLAME-GUARDIAN",
            "OGN·082/298",
            "P1-BLUEFLAME-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blastcone-sprout-power-minus-2-floor.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-BLASTCONE-SPROUT"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-BLASTCONE-SPROUT"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BLASTCONE-TARGET-001"].Power);
        Assert.Equal(-1, result.FinalState.CardObjects["P2-BLASTCONE-TARGET-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public Task CoreRuleEngineRejectsBlastconeSproutWhenTargetIsNotUnit() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            2,
            "P1-UNIT-BLASTCONE-SPROUT",
            "OGN·097/298",
            "P1-BLASTCONE-SPROUT-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysProwlingHunterCreateWarhawk()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-prowling-hunter-create-warhawk.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-PROWLING-HUNTER", "P1-UNIT-PROWLING-HUNTER-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-PROWLING-HUNTER"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-PROWLING-HUNTER-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
            result.FinalState.CardObjects["P1-UNIT-PROWLING-HUNTER-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsProwlingHunterWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            4,
            "P1-UNIT-PROWLING-HUNTER",
            "UNL-033/219",
            "P1-PROWLING-HUNTER-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysApprenticeEngineerReturnGraveyardEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-apprentice-engineer-return-graveyard-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-APPRENTICE-ENGINEER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-APPRENTICE-ENGINEER"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-APPRENTICE-ENGINEER"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsApprenticeEngineerWhenGraveyardTargetIsNotEquipment()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-APPRENTICE-ENGINEER"],
                    Graveyard = ["P1-GRAVE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-001"] = new(
                    "P1-GRAVE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-apprentice-engineer-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-APPRENTICE-ENGINEER",
                "SFD·061/221",
                ["P1-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-APPRENTICE-ENGINEER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDarkenedLurkerDiscardDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-darkened-lurker-discard-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-DARKENED-LURKER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-DARKENED-LURKER-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DARKENED-LURKER-DISCARD-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-DARKENED-LURKER"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-DARKENED-LURKER"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDarkenedLurkerWhenDiscardTargetIsSource()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-DARKENED-LURKER"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-darkened-lurker-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-DARKENED-LURKER",
                "UNL-123/219",
                ["P1-UNIT-DARKENED-LURKER"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-DARKENED-LURKER"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShepherdDogReturnGraveyardUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shepherd-dog-return-graveyard-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SHEPHERD-DOG"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SHEPHERD-DOG"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-SHEPHERD-DOG"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsShepherdDogWhenGraveyardTargetIsNotUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SHEPHERD-DOG"],
                    Graveyard = ["P1-GRAVE-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-EQUIPMENT-001"] = new(
                    "P1-GRAVE-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-shepherd-dog-equipment-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SHEPHERD-DOG",
                "OGN·165/298",
                ["P1-GRAVE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SHEPHERD-DOG"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAnnieReturnGraveyardSpell()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-annie-return-graveyard-spell.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-ANNIE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-SPELL-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ANNIE"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-ANNIE"].Tags);
        Assert.Equal([CardObjectTags.SpellCard], result.FinalState.CardObjects["P1-GRAVE-SPELL-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAnnieWhenGraveyardTargetIsNotSpell()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ANNIE"],
                    Graveyard = ["P1-GRAVE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-001"] = new(
                    "P1-GRAVE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-annie-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ANNIE",
                "OGS·010/024",
                ["P1-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ANNIE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScuttleCrabDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scuttle-crab-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SCUTTLE-CRAB"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SCUTTLE-CRAB-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-SCUTTLE-CRAB"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-SCUTTLE-CRAB"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsScuttleCrabWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            2,
            "P1-UNIT-SCUTTLE-CRAB",
            "UNL-053/219",
            "P1-SCUTTLE-CRAB-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysYordleInstructorDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-yordle-instructor-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-YORDLE-INSTRUCTOR"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-YORDLE-INSTRUCTOR-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-YORDLE-INSTRUCTOR"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Bulwark],
            result.FinalState.CardObjects["P1-UNIT-YORDLE-INSTRUCTOR"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsYordleInstructorWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            3,
            "P1-UNIT-YORDLE-INSTRUCTOR",
            "OGN·087/298",
            "P1-YORDLE-INSTRUCTOR-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteMotherCreateSprite()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-mother-create-sprite.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-SPRITE-MOTHER", "P1-UNIT-SPRITE-MOTHER-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsSpriteMotherWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            4,
            "P1-UNIT-SPRITE-MOTHER",
            "OGN·106/298",
            "P1-SPRITE-MOTHER-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysMegasharkCannonDamageEnemyBattlefield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-megashark-cannon-damage-enemy-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-MEGASHARK-CANNON"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-MEGASHARK-TARGET-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-MEGASHARK-CANNON"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P2-BATTLEFIELD-MEGASHARK-TARGET-001"].Damage);
        Assert.Equal(7, result.FinalState.CardObjects["P2-BATTLEFIELD-MEGASHARK-TARGET-001"].Power);
        Assert.Contains("DAMAGE_APPLIED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMegasharkCannonWhenTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-MEGASHARK-CANNON"],
                    Battlefields = ["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-megashark-cannon-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-MEGASHARK-CANNON",
                "OGN·092/298",
                ["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-MEGASHARK-CANNON"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysQuicksandMageDestroySmallEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-quicksand-mage-destroy-small-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-QUICKSAND-MAGE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BASE-QUICKSAND-TARGET-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-QUICKSAND-MAGE"].Power);
        Assert.DoesNotContain("P2-BASE-QUICKSAND-TARGET-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsQuicksandMageWhenEnemyPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-QUICKSAND-MAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-QUICKSAND-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-QUICKSAND-LARGE-001"] = new(
                    "P2-BASE-QUICKSAND-LARGE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-quicksand-mage-large-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-QUICKSAND-MAGE",
                "SFD·158/221",
                ["P2-BASE-QUICKSAND-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-QUICKSAND-MAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-QUICKSAND-LARGE-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysZaunBodyguardReturnBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-zaun-bodyguard-return-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-ZAUN-BODYGUARD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P2-HAND-001", "P2-BATTLEFIELD-ZAUN-TARGET-001"],
            result.FinalState.PlayerZones["P2"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ZAUN-BODYGUARD"].Power);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsZaunBodyguardWhenTargetIsInBase()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ZAUN-BODYGUARD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ZAUN-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ZAUN-TARGET-001"] = new(
                    "P2-BASE-ZAUN-TARGET-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-zaun-bodyguard-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ZAUN-BODYGUARD",
                "OGN·188/298",
                ["P2-BASE-ZAUN-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ZAUN-BODYGUARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ZAUN-TARGET-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSoulguardEquipmentGrantBoon()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-soulguard-equipment-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-SOULGUARD-BASE-UNIT-001", "P1-EQUIPMENT-SOULGUARD"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SOULGUARD"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SOULGUARD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-SOULGUARD-BASE-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSoulguardWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SOULGUARD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-SOULGUARD-ENEMY-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SOULGUARD-ENEMY-UNIT-001"] = new(
                    "P2-SOULGUARD-ENEMY-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-soulguard-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SOULGUARD",
                "OGN·063/298",
                ["P2-SOULGUARD-ENEMY-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SOULGUARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-SOULGUARD-ENEMY-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
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
    public async Task CoreRuleEnginePlaysLotusTrapAndDoublesNextDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-lotus-trap-doubles-next-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-LOTUS-001"].Damage);
        Assert.Contains(
            "DAMAGE_RECEIVED_DOUBLED_THIS_TURN",
            result.FinalState.CardObjects["P2-UNIT-LOTUS-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysNoxianGuillotineAndNextDamageDestroysUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-noxian-guillotine-next-damage-destroys.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-NOXIAN-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-UNIT-NOXIAN-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysImperialDecreeAndDamageDestroysUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-imperial-decree-damage-destroys-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains(
            "DESTROY_ON_NEXT_DAMAGE_THIS_TURN",
            result.FinalState.CardObjects["P1-IMPERIAL-ALLY-001"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("P2-IMPERIAL-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-IMPERIAL-ENEMY-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCounterstormAndPreventsNextDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-counterstorm-prevent-next-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-COUNTERSTORM-001"].Damage);
        Assert.DoesNotContain(
            "PREVENT_NEXT_DAMAGE_THIS_TURN",
            result.FinalState.CardObjects["P2-UNIT-COUNTERSTORM-001"].UntilEndOfTurnEffects);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
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
    public async Task CoreRuleEnginePlaysBellowsBreathAgainstUpToThreeUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bellows-breath-up-to-three-units-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-002"].Damage);
        Assert.Equal(
            3,
            result.EventKinds.Count(kind => string.Equals(kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFirestormAgainstEnemyBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-firestorm-damage-enemy-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P1-FRIENDLY-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-ENEMY-BASE-UNIT"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-ENEMY-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-ENEMY-BATTLEFIELD-UNIT-002"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCrescentStrikeTargetPlusSplash()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-crescent-strike-target-plus-splash.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P1-CRESCENT-FRIENDLY-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-CRESCENT-BASE-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P2-CRESCENT-PRIMARY-001"].Damage);
        Assert.Equal(1, result.FinalState.CardObjects["P2-CRESCENT-SPLASH-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSwitcherooSwappingBattlefieldUnitPowers()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-switcheroo-swap-battlefield-unit-powers.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-SWITCHEROO-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-SWITCHEROO-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-SWITCHEROO-BASE-UNIT"].Power);
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
    public async Task CoreRuleEnginePlaysHousecleaningDestroyEachPlayerUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-housecleaning-destroy-each-player-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-HOUSECLEANING-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-HOUSECLEANING-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-HOUSECLEANING-KEEPER-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-HOUSECLEANING-KEEPER-001"], result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKingsEdictDestroyingEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kings-edict-destroy-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("P1-KINGS-EDICT-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.Contains("P2-KINGS-EDICT-KEEPER-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-KINGS-EDICT-CHOSEN-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-KINGS-EDICT-CHOSEN-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpiritFireDestroyingBattlefieldUnitsTotalPowerFour()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-spirit-fire-destroy-total-power-four.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-002", result.FinalState.CardObjects.Keys);
        Assert.Contains("P2-SPIRIT-FIRE-KEEPER-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"], result.FinalState.PlayerZones["P2"].Graveyard);
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
    public async Task CoreRuleEnginePlaysHurricaneSweepEachPlayerReturnUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hurricane-sweep-each-player-return-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-HURRICANE-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-HURRICANE-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-HURRICANE-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HURRICANE-ENEMY-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCustodianJudgmentUnitToDeckTop()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-custodian-judgment-unit-to-deck-top.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-CUSTODIAN-TARGET-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P2-CUSTODIAN-TARGET-001", "P2-MAIN-001", "P2-MAIN-002"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCustodianJudgmentUnitToDeckBottom()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-custodian-judgment-unit-to-deck-bottom.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-CUSTODIAN-TARGET-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P2-MAIN-001", "P2-MAIN-002", "P2-CUSTODIAN-TARGET-001"],
            result.FinalState.PlayerZones["P2"].MainDeck);
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
    public async Task CoreRuleEnginePlaysUndertowAndReturnsAllUnitsAndEquipment()
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
        Assert.DoesNotContain("P1-BASE-EQUIPMENT-UNDERTOW-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-UNDERTOW-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P1-BASE-UNIT-001", "P1-BASE-EQUIPMENT-UNDERTOW-001", "P1-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P2-BASE-EQUIPMENT-UNDERTOW-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBrokenBladesRematchDestroyEachPlayerEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-broken-blades-rematch-destroy-each-player-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-EQUIPMENT-BROKEN-BLADES-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-BROKEN-BLADES-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBrokenBladesRematchAgainstUnitTarget()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BROKEN-BLADES-REMATCH"],
                    Base = ["P1-BASE-EQUIPMENT-BROKEN-BLADES-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-EQUIPMENT-BROKEN-BLADES-001"] = new("P1-BASE-EQUIPMENT-BROKEN-BLADES-001", tags: [CardObjectTags.EquipmentCard]),
                ["P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"] = new("P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-broken-blades-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BROKEN-BLADES-REMATCH",
                "OGN·179/298",
                ["P1-BASE-EQUIPMENT-BROKEN-BLADES-001", "P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BROKEN-BLADES-REMATCH"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-EQUIPMENT-BROKEN-BLADES-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
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
    public async Task CoreRuleEnginePlaysRideTheWindAndMovesFriendlyBattlefieldUnitToBaseReady()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ride-the-wind-move-friendly-battlefield-unit-to-base-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRuthlessPursuitMoveFriendlyUnitRecallMark()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ruthless-pursuit-move-friendly-unit-recall-mark.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUTHLESS-PURSUIT-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Contains(
            "MAY_RETURN_TO_BASE_ON_CONQUER_THIS_TURN",
            result.FinalState.CardObjects["P1-RUTHLESS-PURSUIT-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysIsolateAndMovesEnemyBattlefieldUnitToBaseWithoutDrawing()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-isolate-move-enemy-battlefield-unit-to-base.fixture.json"),
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
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.DoesNotContain("CARD_DRAWN", result.EventKinds);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCharmAndMovesEnemyBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-charm-move-enemy-battlefield-unit-to-base.fixture.json"),
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
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRisingDragonKickAndMovesEnemyBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rising-dragon-kick-move-enemy-battlefield-unit-to-base.fixture.json"),
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
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.DoesNotContain("STATUS_EFFECT_APPLIED", result.EventKinds);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
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
    public async Task CoreRuleEnginePlaysShieldWallAndMovesSelectedFriendlyBattlefieldUnitsToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-BASE-UNIT-001",
                "P1-SHIELD-WALL-UNIT-001",
                "P1-SHIELD-WALL-UNIT-002",
                "P1-SHIELD-WALL-UNIT-003"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(
            3,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPlayfulTentaclesAndMovesEnemyBattlefieldUnitsTotalPowerEight()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-playful-tentacles-move-total-power-eight.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P2-BASE-UNIT-001",
                "P2-PLAYFUL-TENTACLES-UNIT-001",
                "P2-PLAYFUL-TENTACLES-UNIT-002"
            ],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-PLAYFUL-TENTACLES-KEEPER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P2-PLAYFUL-TENTACLES-UNIT-001"].Damage);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P2-PLAYFUL-TENTACLES-UNIT-002"].UntilEndOfTurnEffects);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBaitAndMovesEnemyUnitToAnotherEnemyUnitLocation()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bait-move-enemy-unit-to-another-location.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-BAIT-DESTINATION-001", "P2-BAIT-MOVED-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BAIT-KEEPER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P2-BAIT-MOVED-001"].UntilEndOfTurnEffects);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_UNIT_LOCATION", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonsRageMoveThenMutualDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragons-rage-move-then-mutual-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-DRAGONS-RAGE-MOVED-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-DRAGONS-RAGE-DESTINATION-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P2-DRAGONS-RAGE-MOVED-001"].Damage);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_UNIT_LOCATION", StringComparison.Ordinal)));
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
    public async Task CoreRuleEnginePlaysTheCurtainRisesBaseReadyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-the-curtain-rises-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Single(result.EventKinds, kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal));
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
    public async Task CoreRuleEnginePlaysFeatherstormCreatesFourWarhawksWithSpellshield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-featherstorm-create-warhawks.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-FEATHERSTORM-TOKEN-001",
                "P1-SPELL-FEATHERSTORM-TOKEN-002",
                "P1-SPELL-FEATHERSTORM-TOKEN-003",
                "P1-SPELL-FEATHERSTORM-TOKEN-004"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            4,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
        {
            Assert.Equal(1, result.FinalState.CardObjects[objectId].Power);
            Assert.Equal([CardObjectTags.Spellshield], result.FinalState.CardObjects[objectId].Tags);
        });
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
    public async Task CoreRuleEnginePlaysStandDefiantPowerPerEnemyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stand-defiant-power-per-enemy-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-STAND-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BATTLEFIELD-STAND-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
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
    public async Task CoreRuleEnginePlaysSandcraftWithoutEchoCreatesOneSandSoldierInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sandcraft-create-one-sand-soldier-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-SANDCRAFT-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SPELL-SANDCRAFT-TOKEN-001"].Power);
        Assert.Single(result.EventKinds, kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysProtectTheEmperorCreatesSandSoldierInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-protect-the-emperor-create-sand-soldier.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-PROTECT-EMPEROR-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SPELL-PROTECT-EMPEROR-TOKEN-001"].Power);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSandSoldiersRiseReadiesTwoSandSoldiers()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sand-soldiers-rise-ready-two.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
        Assert.False(result.FinalState.CardObjects["P1-SAND-SOLDIER-001"].IsExhausted);
        Assert.False(result.FinalState.CardObjects["P1-SAND-SOLDIER-002"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteSummonCreatesSpriteInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-summon-create-sprite-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-SPRITE-SUMMON-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SPELL-SPRITE-SUMMON-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-SPELL-SPRITE-SUMMON-TOKEN-001"].Tags);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteBurstCreatesTwoSpritesInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-burst-create-two-sprites-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-SPRITE-BURST-TOKEN-001",
                "P1-SPELL-SPRITE-BURST-TOKEN-002"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
        {
            Assert.Equal(3, result.FinalState.CardObjects[objectId].Power);
            Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects[objectId].Tags);
        });
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMirrorImageCreatesEphemeralCopyInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mirror-image-copy-ephemeral-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-MIRROR-IMAGE-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(4, result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].Power);
        Assert.False(result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].IsExhausted);
        Assert.Equal(["机械", CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMirrorImageAgainstHandCard()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MIRROR-IMAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-MIRROR-IMAGE-TARGET"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HAND-MIRROR-IMAGE-TARGET"] = new("P2-HAND-MIRROR-IMAGE-TARGET", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-mirror-image-bad-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-MIRROR-IMAGE", "UNL-200/219", ["P2-HAND-MIRROR-IMAGE-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSkullcrackStunsFriendlyAndEnemyBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHeroicChargePowerPlusAndStunsEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-heroic-charge-power-plus-stun.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-HEROIC-FRIENDLY-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-HEROIC-FRIENDLY-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-HEROIC-ENEMY-001"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", result.FinalState.CardObjects["P1-HEROIC-FRIENDLY-001"].UntilEndOfTurnEffects);
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
    public async Task CoreRuleEnginePlaysSiphonEnergyBattlefieldPowerSplit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-siphon-energy-battlefield-power-split.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-002"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-002"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Power);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMoonRiseEnemyBattlefieldPowerMinus2()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-002"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Power);
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
    public async Task CoreRuleEnginePlaysSecretArtMercyGrantBoon()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-secret-art-mercy-grant-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-BOON-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-BOON-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStunningDisplayBoonMoveBaseUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stunning-display-boon-move-base-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BASE-BOON-UNIT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-BOON-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-BASE-BOON-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysOpenActionGrantAllBoons()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-open-action-grant-all-boons.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P1-OPEN-ACTION-BASE-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-OPEN-ACTION-BATTLEFIELD-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-OPEN-ACTION-BASE-001"].Tags);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-OPEN-ACTION-BATTLEFIELD-001"].Tags);
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
    public async Task CoreRuleEnginePlaysEclipsePowerPenaltyAndInsightRecycle()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-eclipse-power-minus-4-insight-recycle.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-INSIGHT-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-INSIGHT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-ECLIPSE-INSIGHT-RECYCLE-001"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEclipseInsightTargetOutsideTopCard()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-001", "P1-NOT-VIEWED-001"],
                    Hand = ["P1-SPELL-ECLIPSE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-ECLIPSE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-ECLIPSE-001"] = new("P2-UNIT-ECLIPSE-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-eclipse-insight-outside-top-card", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-ECLIPSE", "UNL-063/219", ["P2-UNIT-ECLIPSE-001", "P1-NOT-VIEWED-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-ECLIPSE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TOP-001", "P1-NOT-VIEWED-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
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
    public async Task CoreRuleEnginePlaysRocketBarrageDestroyEquipmentModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rocket-barrage-destroy-equipment-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-ROCKET-BARRAGE-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-BASE-EQUIPMENT-ROCKET-BARRAGE-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEmergencyRecallReturnEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-emergency-recall-return-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-EMERGENCY-RECALL-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-BASE-EQUIPMENT-EMERGENCY-RECALL-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-SPELL-EMERGENCY-RECALL"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEmergencyRecallAgainstUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-EMERGENCY-RECALL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-EMERGENCY-RECALL-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-EMERGENCY-RECALL-UNIT-001"] = new("P2-BASE-EMERGENCY-RECALL-UNIT-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-emergency-recall-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-EMERGENCY-RECALL",
                "SFD·135/221",
                ["P2-BASE-EMERGENCY-RECALL-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-EMERGENCY-RECALL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-EMERGENCY-RECALL-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysThermogenicBeamDestroyAllEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thermogenic-beam-destroy-all-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-EQUIPMENT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.Contains("P1-BATTLEFIELD-UNIT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.Contains("P2-BATTLEFIELD-UNIT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
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
    public async Task CoreRuleEnginePlaysZenithBladeAgainstEnemyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-UNIT-ZENITH-001"].UntilEndOfTurnEffects);
        Assert.Equal(["P1-UNIT-ZENITH-ALLY-001"], result.FinalState.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHighwayRobberyEnemyUnitDamageBranch()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-highway-robbery-enemy-unit-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P2-BASE-UNIT-HIGHWAY-ROBBERY-001"].Damage);
        Assert.Equal(["P2-BASE-UNIT-HIGHWAY-ROBBERY-001"], result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDeadlyFlourishEnemyUnitDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-deadly-flourish-enemy-unit-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-DEADLY-FLOURISH-001"].Damage);
        Assert.Equal(["P1-SPELL-DEADLY-FLOURISH"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPainfulPayoffDamageAndGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-painful-payoff-damage-create-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-PAINFUL-PAYOFF-001"].Damage);
        Assert.Equal(["P1-SPELL-PAINFUL-PAYOFF-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-PAINFUL-PAYOFF-TOKEN-001"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-SPELL-PAINFUL-PAYOFF-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysJungleAmbushCreatesGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-jungle-ambush-create-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-JUNGLE-AMBUSH-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-JUNGLE-AMBUSH-TOKEN-001"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-SPELL-JUNGLE-AMBUSH-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPainfulPayoffAgainstBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PAINFUL-PAYOFF"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-PAINFUL-PAYOFF-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-PAINFUL-PAYOFF-001"] = new("P2-BASE-PAINFUL-PAYOFF-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-painful-payoff-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PAINFUL-PAYOFF",
                "SFD·070/221",
                ["P2-BASE-PAINFUL-PAYOFF-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PAINFUL-PAYOFF"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-PAINFUL-PAYOFF-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBloodMoneyDestroyEnemySmallUnitAndGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blood-money-destroy-enemy-small-unit-create-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BATTLEFIELD-BLOOD-MONEY-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-SPELL-BLOOD-MONEY-ENEMY-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-BLOOD-MONEY-ENEMY-TOKEN-001"].IsExhausted);
        Assert.Equal(["P2"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBloodMoneyDestroyFriendlySmallUnitAndTwoGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blood-money-destroy-friendly-small-unit-create-two-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BATTLEFIELD-BLOOD-MONEY-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-001", "P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-002"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-002"].IsExhausted);
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBloodMoneyAgainstLargeBattlefieldUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BLOOD-MONEY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"] = new("P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001", power: 3, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-blood-money-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BLOOD-MONEY",
                "SFD·162/221",
                ["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BLOOD-MONEY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHighwayRobberyTargetControllerDrawChoice()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-highway-robbery-target-controller-draw-choice.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-HIGHWAY-ROBBERY-DRAW-001", "P1-HIGHWAY-ROBBERY-DRAW-002"],
            result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-UNIT-HIGHWAY-ROBBERY-DRAW-CHOICE-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLastBreathReadyThenPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-last-breath-ready-damage-enemy-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-LAST-BREATH-FRIENDLY-001"].IsExhausted);
        Assert.Equal(4, result.FinalState.CardObjects["P2-LAST-BREATH-ENEMY-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysConvergentMutationMatchFriendlyPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-convergent-mutation-match-friendly-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-CONVERGENT-LOW-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-CONVERGENT-LOW-001"].UntilEndOfTurnPowerModifier);
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
    public async Task CoreRuleEngineRejectsZenithBladeAgainstFriendlyOrBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ZENITH-BLADE"],
                    Battlefields = ["P1-FRIENDLY-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-UNIT-001"] = new("P1-FRIENDLY-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        foreach (var invalidTargetObjectId in new[] { "P1-FRIENDLY-UNIT-001", "P2-BASE-UNIT-001" })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-zenith-blade-invalid-{invalidTargetObjectId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-ZENITH-BLADE",
                    "OGN·262/298",
                    [invalidTargetObjectId]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHighwayRobberyAgainstFriendlyOrOffFieldUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HIGHWAY-ROBBERY"],
                    Base = ["P1-FRIENDLY-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Graveyard = ["P2-GRAVEYARD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-BASE-UNIT-001"] = new("P1-FRIENDLY-BASE-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-GRAVEYARD-UNIT-001"] = new("P2-GRAVEYARD-UNIT-001")
            }
        };

        foreach (var invalidTargetObjectId in new[] { "P1-FRIENDLY-BASE-UNIT-001", "P2-GRAVEYARD-UNIT-001" })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-highway-robbery-invalid-{invalidTargetObjectId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-HIGHWAY-ROBBERY",
                    "OGN·033/298",
                    [invalidTargetObjectId]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDeadlyFlourishAgainstFriendlyUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-DEADLY-FLOURISH"],
                    Base = ["P1-FRIENDLY-DEADLY-FLOURISH-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-DEADLY-FLOURISH-001"] = new("P1-FRIENDLY-DEADLY-FLOURISH-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-deadly-flourish-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-DEADLY-FLOURISH",
                "UNL-073/219",
                ["P1-FRIENDLY-DEADLY-FLOURISH-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-DEADLY-FLOURISH"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-DEADLY-FLOURISH-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLastBreathAgainstWrongTargetOrderOrEnemyBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-LAST-BREATH"],
                    Base = ["P1-FRIENDLY-LAST-BREATH-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-LAST-BREATH-001"],
                    Battlefields = ["P2-BATTLEFIELD-LAST-BREATH-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-LAST-BREATH-001"] = new("P1-FRIENDLY-LAST-BREATH-001", power: 4, isExhausted: true),
                ["P2-BASE-LAST-BREATH-001"] = new("P2-BASE-LAST-BREATH-001", power: 5),
                ["P2-BATTLEFIELD-LAST-BREATH-001"] = new("P2-BATTLEFIELD-LAST-BREATH-001", power: 5)
            }
        };

        foreach (var invalidTargetObjectIds in new[]
        {
            new[] { "P2-BATTLEFIELD-LAST-BREATH-001", "P1-FRIENDLY-LAST-BREATH-001" },
            new[] { "P1-FRIENDLY-LAST-BREATH-001", "P2-BASE-LAST-BREATH-001" }
        })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-last-breath-invalid-{string.Join("-", invalidTargetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-LAST-BREATH",
                    "OGN·260/298",
                    invalidTargetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsConvergentMutationAgainstEnemyOrDuplicateTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CONVERGENT-MUTATION"],
                    Base = ["P1-CONVERGENT-FRIENDLY-001"],
                    Battlefields = ["P1-CONVERGENT-HIGH-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONVERGENT-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CONVERGENT-FRIENDLY-001"] = new("P1-CONVERGENT-FRIENDLY-001", power: 2),
                ["P1-CONVERGENT-HIGH-001"] = new("P1-CONVERGENT-HIGH-001", power: 5),
                ["P2-CONVERGENT-ENEMY-001"] = new("P2-CONVERGENT-ENEMY-001", power: 5)
            }
        };

        foreach (var invalidTargetObjectIds in new[]
        {
            new[] { "P1-CONVERGENT-FRIENDLY-001", "P2-CONVERGENT-ENEMY-001" },
            new[] { "P1-CONVERGENT-FRIENDLY-001", "P1-CONVERGENT-FRIENDLY-001" }
        })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-convergent-mutation-invalid-{string.Join("-", invalidTargetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-CONVERGENT-MUTATION",
                    "OGN·108/298",
                    invalidTargetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
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
    public async Task CoreRuleEngineRejectsSpiritFireWhenTotalTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SPIRIT-FIRE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPIRIT-FIRE-UNIT-001"] = new("P2-SPIRIT-FIRE-UNIT-001", power: 2),
                ["P2-SPIRIT-FIRE-UNIT-002"] = new("P2-SPIRIT-FIRE-UNIT-002", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-spirit-fire-total-power-too-high", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-SPIRIT-FIRE", "OGN·256/298", ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPlayfulTentaclesWhenTotalTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PLAYFUL-TENTACLES"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-PLAYFUL-TENTACLES-UNIT-001", "P2-PLAYFUL-TENTACLES-UNIT-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-PLAYFUL-TENTACLES-UNIT-001"] = new("P2-PLAYFUL-TENTACLES-UNIT-001", power: 4),
                ["P2-PLAYFUL-TENTACLES-UNIT-002"] = new("P2-PLAYFUL-TENTACLES-UNIT-002", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-playful-tentacles-total-power-too-high", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PLAYFUL-TENTACLES",
                "UNL-054/219",
                ["P2-PLAYFUL-TENTACLES-UNIT-001", "P2-PLAYFUL-TENTACLES-UNIT-002"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-PLAYFUL-TENTACLES"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBaitAgainstFriendlyOrRepeatedTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BAIT"],
                    Base = ["P1-BAIT-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BAIT-DESTINATION-001"],
                    Battlefields = ["P2-BAIT-MOVED-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BAIT-FRIENDLY-001"] = new("P1-BAIT-FRIENDLY-001", power: 2),
                ["P2-BAIT-DESTINATION-001"] = new("P2-BAIT-DESTINATION-001", power: 3),
                ["P2-BAIT-MOVED-001"] = new("P2-BAIT-MOVED-001", power: 4)
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BAIT-MOVED-001", "P1-BAIT-FRIENDLY-001"],
            ["P1-BAIT-FRIENDLY-001", "P2-BAIT-DESTINATION-001"],
            ["P2-BAIT-MOVED-001", "P2-BAIT-MOVED-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-bait-invalid-{string.Join("-", targetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-BAIT",
                    "SFD·129/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(["P1-SPELL-BAIT"], result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonsRageAgainstFriendlyOrRepeatedTarget()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-DRAGONS-RAGE"],
                    Base = ["P1-DRAGONS-RAGE-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-DRAGONS-RAGE-MOVED-001"],
                    Battlefields = ["P2-DRAGONS-RAGE-DESTINATION-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DRAGONS-RAGE-FRIENDLY-001"] = new(
                    "P1-DRAGONS-RAGE-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-DRAGONS-RAGE-MOVED-001"] = new(
                    "P2-DRAGONS-RAGE-MOVED-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-DRAGONS-RAGE-DESTINATION-001"] = new(
                    "P2-DRAGONS-RAGE-DESTINATION-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-DRAGONS-RAGE-MOVED-001", "P1-DRAGONS-RAGE-FRIENDLY-001"],
            ["P1-DRAGONS-RAGE-FRIENDLY-001", "P2-DRAGONS-RAGE-DESTINATION-001"],
            ["P2-DRAGONS-RAGE-MOVED-001", "P2-DRAGONS-RAGE-MOVED-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-dragons-rage-invalid-{string.Join("-", targetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-DRAGONS-RAGE",
                    "OGN·258/298",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(["P1-SPELL-DRAGONS-RAGE"], result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHousecleaningAgainstWrongTargetOrder()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOUSECLEANING"],
                    Base = ["P1-HOUSECLEANING-FRIENDLY-001"],
                    Battlefields = ["P1-HOUSECLEANING-FRIENDLY-002"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-HOUSECLEANING-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HOUSECLEANING-FRIENDLY-001"] = new("P1-HOUSECLEANING-FRIENDLY-001", power: 2),
                ["P1-HOUSECLEANING-FRIENDLY-002"] = new("P1-HOUSECLEANING-FRIENDLY-002", power: 3),
                ["P2-HOUSECLEANING-ENEMY-001"] = new("P2-HOUSECLEANING-ENEMY-001", power: 4)
            }
        };

        foreach (var invalidTargetObjectIds in new[]
        {
            new[] { "P2-HOUSECLEANING-ENEMY-001", "P1-HOUSECLEANING-FRIENDLY-001" },
            new[] { "P1-HOUSECLEANING-FRIENDLY-001", "P1-HOUSECLEANING-FRIENDLY-002" }
        })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-housecleaning-invalid-{string.Join("-", invalidTargetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-HOUSECLEANING",
                    "OGN·209/298",
                    invalidTargetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsKingsEdictAgainstFriendlyUnit()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-KINGS-EDICT"],
                    Base = ["P1-FRIENDLY-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-UNIT-001"] = new("P1-FRIENDLY-UNIT-001"),
                ["P2-ENEMY-UNIT-001"] = new("P2-ENEMY-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-kings-edict-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-KINGS-EDICT", "OGN·237/298", ["P1-FRIENDLY-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHurricaneSweepDuplicateChosenUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HURRICANE-SWEEP"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-HURRICANE-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HURRICANE-ENEMY-001"] = new("P2-HURRICANE-ENEMY-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hurricane-sweep-duplicate-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HURRICANE-SWEEP",
                "OGN·187/298",
                ["P2-HURRICANE-ENEMY-001", "P2-HURRICANE-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCustodianJudgmentWithoutOwnerDeckChoiceMode()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CUSTODIAN-JUDGMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CUSTODIAN-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-CUSTODIAN-TARGET-001"] = new("P2-CUSTODIAN-TARGET-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-custodian-judgment-missing-mode", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-CUSTODIAN-JUDGMENT", "UNL-204/219", ["P2-CUSTODIAN-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCustodianJudgmentAgainstFriendlyOrBaseUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CUSTODIAN-JUDGMENT"],
                    Battlefields = ["P1-CUSTODIAN-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-CUSTODIAN-BASE-001"],
                    Battlefields = ["P2-CUSTODIAN-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CUSTODIAN-FRIENDLY-001"] = new("P1-CUSTODIAN-FRIENDLY-001"),
                ["P2-CUSTODIAN-BASE-001"] = new("P2-CUSTODIAN-BASE-001"),
                ["P2-CUSTODIAN-TARGET-001"] = new("P2-CUSTODIAN-TARGET-001")
            }
        };

        foreach (var invalidTargetObjectId in new[] { "P1-CUSTODIAN-FRIENDLY-001", "P2-CUSTODIAN-BASE-001" })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-custodian-judgment-invalid-{invalidTargetObjectId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-CUSTODIAN-JUDGMENT",
                    "UNL-204/219",
                    [invalidTargetObjectId],
                    "OWNER_MAIN_DECK_TOP"),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
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
    public async Task CoreRuleEngineRejectsRuthlessPursuitAgainstEnemyOrNonUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-RUTHLESS-PURSUIT"],
                    Base = ["P1-RUTHLESS-PURSUIT-EQUIPMENT-001"],
                    Battlefields = ["P1-RUTHLESS-PURSUIT-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-RUTHLESS-PURSUIT-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RUTHLESS-PURSUIT-UNIT-001"] = new(
                    "P1-RUTHLESS-PURSUIT-UNIT-001",
                    tags: [CardObjectTags.UnitCard]),
                ["P1-RUTHLESS-PURSUIT-EQUIPMENT-001"] = new(
                    "P1-RUTHLESS-PURSUIT-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard]),
                ["P2-RUTHLESS-PURSUIT-ENEMY-001"] = new(
                    "P2-RUTHLESS-PURSUIT-ENEMY-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-RUTHLESS-PURSUIT-ENEMY-001"],
            ["P1-RUTHLESS-PURSUIT-EQUIPMENT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-ruthless-pursuit-invalid-{string.Join("-", targetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-RUTHLESS-PURSUIT",
                    "SFD·184/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
            Assert.Equal(["P1-SPELL-RUTHLESS-PURSUIT"], result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }
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
    public async Task CoreRuleEngineRejectsShieldWallAgainstEnemyBaseOrRepeatedTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SHIELD-WALL"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-shield-wall-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-SHIELD-WALL",
                    "SFD·043/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SKULLCRACK"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P2-BASE-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-skullcrack-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-SKULLCRACK",
                    "OGN·220/298",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHeroicChargeAgainstWrongOrderOrBaseUnits()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEROIC-CHARGE"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P2-BASE-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-heroic-charge-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-HEROIC-CHARGE",
                    "UNL-155/219",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsStandDefiantAgainstNonFriendlyBattlefieldTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STAND-DEFIANT"],
                    Base = ["P1-BASE-STAND-001"],
                    Battlefields = ["P1-BATTLEFIELD-STAND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-STAND-001"],
                    Battlefields = ["P2-BATTLEFIELD-STAND-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-STAND-001"] = new("P1-BASE-STAND-001"),
                ["P1-BATTLEFIELD-STAND-001"] = new("P1-BATTLEFIELD-STAND-001"),
                ["P2-BASE-STAND-001"] = new("P2-BASE-STAND-001"),
                ["P2-BATTLEFIELD-STAND-001"] = new("P2-BATTLEFIELD-STAND-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BASE-STAND-001"],
            ["P2-BATTLEFIELD-STAND-001"],
            ["P2-BASE-STAND-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-stand-defiant-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-STAND-DEFIANT",
                    "SFD·001/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsIsolateAgainstNonEnemyBattlefieldTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ISOLATE"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001"],
            ["P2-BASE-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-isolate-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-ISOLATE",
                    "UNL-124/219",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
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
    public async Task CoreRuleEngineRejectsBellowsBreathRepeatedOrFourthTarget()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BELLOWS-BREATH"],
                    Battlefields = ["P1-UNIT-001", "P1-UNIT-002"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001", "P2-UNIT-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-001"] = new("P1-UNIT-001"),
                ["P1-UNIT-002"] = new("P1-UNIT-002"),
                ["P2-UNIT-001"] = new("P2-UNIT-001"),
                ["P2-UNIT-002"] = new("P2-UNIT-002")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-UNIT-001", "P1-UNIT-001"],
            ["P1-UNIT-001", "P1-UNIT-002", "P2-UNIT-001", "P2-UNIT-002"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-bellows-breath-invalid-targets", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-BELLOWS-BREATH",
                    "SFD·080/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFirestormUnitTarget()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FIRESTORM"]
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
            new PlayerIntent("intent-firestorm-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-FIRESTORM", "OGS·002/024", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCrescentStrikeAgainstFriendlyOrBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CRESCENT-STRIKE"],
                    Battlefields = ["P1-FRIENDLY-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-UNIT-001"] = new("P1-FRIENDLY-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-FRIENDLY-UNIT-001"],
            ["P2-BASE-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-crescent-strike-invalid-targets", "P1", "PLAY_CARD"),
                new PlayCardCommand("P1-SPELL-CRESCENT-STRIKE", "UNL-072/219", targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSwitcherooAgainstDuplicateOrBaseTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SWITCHEROO"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-switcheroo-invalid-targets", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-SWITCHEROO",
                    "SFD·145/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
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
    public async Task CoreRuleEngineRejectsRocketBarrageDestroyEquipmentModeAgainstUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ROCKET-BARRAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ROCKET-BARRAGE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ROCKET-BARRAGE-UNIT-001"] = new(
                    "P2-BASE-ROCKET-BARRAGE-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-rocket-barrage-destroy-equipment-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ROCKET-BARRAGE",
                "SFD·077/221",
                ["P2-BASE-ROCKET-BARRAGE-UNIT-001"],
                Mode: "DESTROY_EQUIPMENT"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ROCKET-BARRAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ROCKET-BARRAGE-UNIT-001"], result.State.PlayerZones["P2"].Base);
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

    private static async Task AssertSimpleEquipmentFixtureAsync(
        string fixtureFileName,
        string equipmentObjectId,
        bool expectedIsExhausted = false,
        string[]? expectedTags = null)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([equipmentObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        var expectedEquipmentTags = expectedTags ?? [CardObjectTags.EquipmentCard];
        Assert.Equal(expectedEquipmentTags, result.FinalState.CardObjects[equipmentObjectId].Tags);
        Assert.Equal(expectedIsExhausted, result.FinalState.CardObjects[equipmentObjectId].IsExhausted);
    }

    private static async Task AssertEquipmentWithTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId)
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base = [targetObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [targetObjectId] = new(
                    targetObjectId,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceObjectId.ToLowerInvariant()}-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [targetObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    private static async Task AssertSourceUnitWithTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId)
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base = [targetObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [targetObjectId] = new(
                    targetObjectId,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceObjectId.ToLowerInvariant()}-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [targetObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    private static async Task AssertSourceUnitNonUnitTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId)
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base = [targetObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [targetObjectId] = new(
                    targetObjectId,
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceObjectId.ToLowerInvariant()}-non-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [targetObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
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
