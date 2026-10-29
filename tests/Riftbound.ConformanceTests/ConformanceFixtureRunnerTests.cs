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
