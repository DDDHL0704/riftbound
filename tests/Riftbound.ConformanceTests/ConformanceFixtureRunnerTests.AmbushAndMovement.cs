using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P4AmbushPlayCardModeIsExplicitlyRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush]),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-premodel", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-premodel-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeWithTargetIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush]),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-BATTLEFIELD-UNIT-001"] = new(
                    "P2-BATTLEFIELD-UNIT-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-target-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                ["P2-BATTLEFIELD-UNIT-001"],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeWithTargetRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeWithBaseDestinationIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush]),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-base-destination-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BASE"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeWithBaseDestinationRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-base-destination-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeInPriorityWindowPlaysGloomyApothecaryToBattlefield()
    {
        var state = PunishmentState(mana: 3) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush]),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-0-P2-SPELL-PROBE",
                    "P2",
                    "P2-SPELL-PROBE",
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ]
        };

        state = AmbushPermissionTests.LegacyOccupiedField(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-priority-window-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "PERMANENT_CONFIRMED", "UNIT_PLAYED_TO_BATTLEFIELD", "TRIGGER_QUEUED"], result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.NotNull(result.State.PendingCardChoice);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001", "P1-MAIN", "P1-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(2, result.State.StackItems.Count);
        var probeStackItem = result.State.StackItems[0];
        Assert.Equal("STACK-0-P2-SPELL-PROBE", probeStackItem.StackItemId);
        Assert.Equal("P2", probeStackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", probeStackItem.EffectKind);
        var ambushStackItem = result.State.StackItems[1];
        Assert.Equal("STACK-1-P1-HAND-UNL-GLOOMY-APOTHECARY", ambushStackItem.StackItemId);
        Assert.Equal("P1", ambushStackItem.ControllerId);
        Assert.Equal("GLOOMY_APOTHECARY_PLAY_UNIT_OPTIONAL_RETURN_FRIENDLY_BATTLEFIELD", ambushStackItem.EffectKind);
        Assert.Equal("BATTLEFIELD:P1-MAIN", ambushStackItem.Destination);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeInPriorityWindowPlaysBehaviorSpecAmbushUnitToBattlefield()
    {
        var state = PunishmentState(mana: 5) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-VI"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-VI"] = new(
                    "P1-HAND-UNL-VI",
                    cardNo: "UNL-176/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush]),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-0-P2-SPELL-PROBE",
                    "P2",
                    "P2-SPELL-PROBE",
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ]
        };

        state = AmbushPermissionTests.LegacyOccupiedField(state);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(5, 1) } };
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-behavior-spec-unit", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-VI",
                "UNL-176/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001", "P1-MAIN", "P1-HAND-UNL-VI"], result.State.PlayerZones["P1"].Battlefields);
        // CN 359.2 / 337.4: only the older spell remains on the chain.
        Assert.Equal("STACK-0-P2-SPELL-PROBE", Assert.Single(result.State.StackItems).StackItemId);
        Assert.Equal("P2", result.State.PriorityPlayerId);
    }

    [Fact]
    public async Task P4AmbushPlayCardModePriorityWindowRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-priority-window-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal("NEUTRAL_CLOSED", result.FinalState.TimingState);
        Assert.Equal("P1", result.FinalState.PriorityPlayerId);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        var stackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeGloomyApothecaryBattlefieldFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-gloomy-apothecary-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-BATTLEFIELD-FRIENDLY-001", "P1-MAIN", "P1-HAND-UNL-GLOOMY-APOTHECARY"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        var source = result.FinalState.CardObjects["P1-HAND-UNL-GLOOMY-APOTHECARY"];
        Assert.Equal("UNL-021/219", source.CardNo);
        Assert.Equal(3, source.Power);
        Assert.Equal([CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush], source.Tags);
        var remainingStackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", remainingStackItem.StackItemId);
        Assert.Equal("P2", result.FinalState.PriorityPlayerId);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeSourceOutsideHandIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BASE-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-BASE-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 3,
                    cardNo: "UNL-021/219"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-source-outside-hand-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-BASE-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.CardNotInHand, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeSourceOutsideHandRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-source-outside-hand-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeUnknownSourceIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-unknown-source-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY-MISSING",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.CardNotInHand, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.False(result.State.CardObjects.ContainsKey("P1-HAND-UNL-GLOOMY-APOTHECARY-MISSING"));
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeUnknownSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-unknown-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-HAND-UNL-GLOOMY-APOTHECARY-MISSING"));
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeOpponentHandSourceIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNL-GLOOMY-APOTHECARY"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P2-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 3,
                    cardNo: "UNL-021/219"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-opponent-hand-source-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-HAND-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.CardNotInHand, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("UNL-021/219", result.State.CardObjects["P2-HAND-UNL-GLOOMY-APOTHECARY"].CardNo);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeOpponentHandSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-opponent-hand-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HAND-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal("UNL-021/219", result.FinalState.CardObjects["P2-HAND-UNL-GLOOMY-APOTHECARY"].CardNo);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeNonAmbushHandSourceIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-LEGION-REARGUARD"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-LEGION-REARGUARD"] = new(
                    "P1-HAND-OGN-LEGION-REARGUARD",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Haste],
                    manaCost: 2,
                    cardNo: "OGN·010/298"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-non-ambush-source-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-OGN-LEGION-REARGUARD",
                "OGN·010/298",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-LEGION-REARGUARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("OGN·010/298", result.State.CardObjects["P1-HAND-OGN-LEGION-REARGUARD"].CardNo);
        Assert.DoesNotContain(CardInteractionKeywordNames.Ambush, result.State.CardObjects["P1-HAND-OGN-LEGION-REARGUARD"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeNonAmbushHandSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-non-ambush-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-LEGION-REARGUARD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal("OGN·010/298", result.FinalState.CardObjects["P1-HAND-OGN-LEGION-REARGUARD"].CardNo);
        Assert.DoesNotContain(CardInteractionKeywordNames.Ambush, result.FinalState.CardObjects["P1-HAND-OGN-LEGION-REARGUARD"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeSourceCardNoMismatchIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-VI"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-VI"] = new(
                    "P1-HAND-UNL-VI",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 5,
                    cardNo: "UNL-176/219"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-cardno-mismatch-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-VI",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-VI"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("UNL-176/219", result.State.CardObjects["P1-HAND-UNL-VI"].CardNo);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeSourceCardNoMismatchRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-cardno-mismatch-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-VI"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal("UNL-176/219", result.FinalState.CardObjects["P1-HAND-UNL-VI"].CardNo);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeWithOptionalCostIsRejectedUntilBattlefieldReactionPlayExists()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 3,
                    cardNo: "UNL-021/219"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-ambush-play-card-optional-cost-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-HAND-UNL-GLOOMY-APOTHECARY",
                "UNL-021/219",
                [],
                Mode: "AMBUSH",
                OptionalCosts: ["ECHO"],
                Destination: "BATTLEFIELD:P1-MAIN"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AmbushPlayCardModeOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-ambush-play-card-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandMovesFriendlyBaseUnitToBattlefieldInCoarseModel()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-BASE-001"],
                    Battlefields = ["P1-MOVE-FIELD-KEEPER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-BASE-001"] = new(
                    "P1-MOVE-UNIT-BASE-001",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-MOVE-FIELD-KEEPER"] = new(
                    "P1-MOVE-FIELD-KEEPER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-coarse", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-BASE-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["UNIT_MOVED_TO_BATTLEFIELD"], result.Events.Select(evt => evt.Kind).ToArray());
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-FIELD-KEEPER", "P1-MOVE-UNIT-BASE-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-BASE-001"].Power);
        Assert.True(result.State.CardObjects["P1-MOVE-UNIT-BASE-001"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task MoveUnitDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string sourceObjectId = "P1-MOVE-UNIT-BASE-001";
        const string keeperObjectId = "P1-MOVE-FIELD-KEEPER";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [sourceObjectId],
                    Battlefields = [keeperObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                [keeperObjectId] = new(
                    keeperObjectId,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new MoveUnitCommand(
            sourceObjectId,
            "BASE",
            "BATTLEFIELD",
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.MoveUnit, prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, sourceObjectId, StringComparison.Ordinal))
            && (candidate.Destinations ?? []).Any(destination => string.Equals(destination.Id, "BATTLEFIELD", StringComparison.Ordinal)));
        var rawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.MoveUnit,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            sourceObjectId,
            origin = "BASE",
            destination = "BATTLEFIELD",
            optionalCosts = Array.Empty<string>()
        });
        const string clientIntentId = "intent-move-unit-base-to-battlefield-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["UNIT_MOVED_TO_BATTLEFIELD"], accepted.Events.Select(evt => evt.Kind).ToArray());
        Assert.Equal(1, accepted.State.Tick);
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Equal([keeperObjectId, sourceObjectId], accepted.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, accepted.State.CardObjects[sourceObjectId].Power);
        Assert.True(accepted.State.CardObjects[sourceObjectId].IsExhausted);
        Assert.Empty(accepted.State.StackItems);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.MoveUnit, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.MoveUnit, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(sourceObjectId, journalEntry.RawCommand.Value.GetProperty("sourceObjectId").GetString());
        Assert.Equal("BASE", journalEntry.RawCommand.Value.GetProperty("origin").GetString());
        Assert.Equal("BATTLEFIELD", journalEntry.RawCommand.Value.GetProperty("destination").GetString());
        Assert.Empty(journalEntry.RawCommand.Value.GetProperty("optionalCosts").EnumerateArray());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["UNIT_MOVED_TO_BATTLEFIELD"], replay.Events.Select(evt => evt.Kind).ToArray());
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Equal([keeperObjectId, sourceObjectId], replay.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, replay.State.CardObjects[sourceObjectId].Power);
        Assert.Empty(replay.State.StackItems);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.MoveUnit,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            sourceObjectId,
            origin = "BASE",
            destination = "BATTLEFIELD",
            optionalCosts = Array.Empty<string>(),
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Empty(conflict.State.PlayerZones["P1"].Base);
        Assert.Equal([keeperObjectId, sourceObjectId], conflict.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, conflict.State.CardObjects[sourceObjectId].Power);
        Assert.Empty(conflict.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P4MoveUnitCommandMovesFriendlyBattlefieldUnitToBaseInCoarseModel()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-BASE-KEEPER"],
                    Battlefields = ["P1-MOVE-UNIT-BATTLEFIELD-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-BASE-KEEPER"] = new(
                    "P1-MOVE-BASE-KEEPER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-MOVE-UNIT-BATTLEFIELD-001"] = new(
                    "P1-MOVE-UNIT-BATTLEFIELD-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-coarse-base", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-BATTLEFIELD-001",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["UNIT_MOVED_TO_BASE"], result.Events.Select(evt => evt.Kind).ToArray());
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-MOVE-BASE-KEEPER", "P1-MOVE-UNIT-BATTLEFIELD-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.State.CardObjects["P1-MOVE-UNIT-BATTLEFIELD-001"].Power);
        Assert.True(result.State.CardObjects["P1-MOVE-UNIT-BATTLEFIELD-001"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsExhaustedSourceWithoutMutation()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EXHAUSTED-MOVE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EXHAUSTED-MOVE-UNIT-001"] = new(
                    "P1-EXHAUSTED-MOVE-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard])
            }
        };
        var hash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-exhausted-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-EXHAUSTED-MOVE-UNIT-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsSourceWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNKNOWN-MOVE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNKNOWN-MOVE-UNIT-001"] = new(
                    "P1-UNKNOWN-MOVE-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-unknown-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-UNKNOWN-MOVE-UNIT-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("移动单位需要服务端已确认的单位牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-UNKNOWN-MOVE-UNIT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Null(result.State.CardObjects["P1-UNKNOWN-MOVE-UNIT-001"].CardNo);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsOpponentControlledSource()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-MOVE-UNIT-BATTLEFIELD-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-MOVE-UNIT-BATTLEFIELD-001"] = new(
                    "P2-MOVE-UNIT-BATTLEFIELD-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-opponent-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P2-MOVE-UNIT-BATTLEFIELD-001",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位来源不在提交的起点，或不由该玩家控制。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-MOVE-UNIT-BATTLEFIELD-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(3, result.State.CardObjects["P2-MOVE-UNIT-BATTLEFIELD-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsOpponentControlledSourceInPlayerZone()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-OPPONENT-CONTROLLED-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-OPPONENT-CONTROLLED-001"] = new(
                    "P1-MOVE-UNIT-OPPONENT-CONTROLLED-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-opponent-controlled-player-zone", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-OPPONENT-CONTROLLED-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位只能选择当前玩家控制的单位。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-MOVE-UNIT-OPPONENT-CONTROLLED-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.State.CardObjects["P1-MOVE-UNIT-OPPONENT-CONTROLLED-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsFaceDownSource()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-FACEDOWN-STANDBY-MOVE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FACEDOWN-STANDBY-MOVE-001"] = new(
                    "P1-FACEDOWN-STANDBY-MOVE-001",
                    isFaceDown: true,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-face-down-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-FACEDOWN-STANDBY-MOVE-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位需要选择正面单位。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-FACEDOWN-STANDBY-MOVE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.True(result.State.CardObjects["P1-FACEDOWN-STANDBY-MOVE-001"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-FACEDOWN-STANDBY-MOVE-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACEDOWN-STANDBY-MOVE-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsNonUnitSource()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-MOVE-SOURCE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-MOVE-SOURCE-001"] = new(
                    "P1-EQUIPMENT-MOVE-SOURCE-001",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-non-unit-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-EQUIPMENT-MOVE-SOURCE-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位需要选择正面单位。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-EQUIPMENT-MOVE-SOURCE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.False(result.State.CardObjects["P1-EQUIPMENT-MOVE-SOURCE-001"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, "武装", "灵便"],
            result.State.CardObjects["P1-EQUIPMENT-MOVE-SOURCE-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsSameZoneDestination()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-SAME-ZONE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-SAME-ZONE-001"] = new(
                    "P1-MOVE-UNIT-SAME-ZONE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-same-zone", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-SAME-ZONE-001",
                "BASE",
                "BASE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位需要选择不同的目的地。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-MOVE-UNIT-SAME-ZONE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-SAME-ZONE-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsOutsideActivePlayerOpenMainWindow()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-WINDOW-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-WINDOW-001"] = new(
                    "P1-MOVE-UNIT-WINDOW-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-window", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-WINDOW-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("移动单位只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal(["P1-MOVE-UNIT-WINDOW-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-WINDOW-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsPendingStackWindow()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-0-P2-SPELL-PROBE",
                    "P2",
                    "P2-SPELL-PROBE",
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-STACK-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-STACK-001"] = new(
                    "P1-MOVE-UNIT-STACK-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-stack-window", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-STACK-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("移动单位只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(["P1-MOVE-UNIT-STACK-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-STACK-001"].Power);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsOptionalCostsUntilRoamModelExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-MOVE-UNIT-OPTIONAL-COST-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-OPTIONAL-COST-001"] = new(
                    "P1-MOVE-UNIT-OPTIONAL-COST-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "游走"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-optional-cost", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-OPTIONAL-COST-001",
                "BATTLEFIELD",
                "BASE",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("移动单位的额外费用暂未开放。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-UNIT-OPTIONAL-COST-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-OPTIONAL-COST-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "游走"], result.State.CardObjects["P1-MOVE-UNIT-OPTIONAL-COST-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsUnsupportedCoarseZones()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-ZONE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-ZONE-001"] = new(
                    "P1-MOVE-UNIT-ZONE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-invalid-zone", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-ZONE-001",
                "HAND",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位需要以基地或战场作为起点和终点。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-MOVE-UNIT-ZONE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-ZONE-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsSourceOutsideRequestedOrigin()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-ORIGIN-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-ORIGIN-001"] = new(
                    "P1-MOVE-UNIT-ORIGIN-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-origin-mismatch", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-ORIGIN-001",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位来源不在提交的起点，或不由该玩家控制。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-MOVE-UNIT-ORIGIN-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-ORIGIN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.State.CardObjects["P1-MOVE-UNIT-ORIGIN-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsSourceStillInHand()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-MOVE-UNIT-HAND-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-HAND-001"] = new(
                    "P1-MOVE-UNIT-HAND-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-hand-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-HAND-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位来源不在提交的起点，或不由该玩家控制。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-MOVE-UNIT-HAND-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-HAND-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.State.CardObjects["P1-MOVE-UNIT-HAND-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsCombatantSourceUntilCombatMovementExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-MOVE-UNIT-COMBATANT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-MOVE-UNIT-DEFENDER-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-COMBATANT-001"] = new(
                    "P1-MOVE-UNIT-COMBATANT-001",
                    cardNo: "SFD·125/221",
                    isAttacking: true,
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-MOVE-UNIT-DEFENDER-001"] = new(
                    "P2-MOVE-UNIT-DEFENDER-001",
                    cardNo: "SFD·125/221",
                    isDefending: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "壁垒"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-combatant-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-COMBATANT-001",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("战斗中的单位不能移动。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-UNIT-COMBATANT-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-MOVE-UNIT-DEFENDER-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.True(result.State.CardObjects["P1-MOVE-UNIT-COMBATANT-001"].IsAttacking);
        Assert.True(result.State.CardObjects["P2-MOVE-UNIT-DEFENDER-001"].IsDefending);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-COMBATANT-001"].Power);
        Assert.Equal(3, result.State.CardObjects["P2-MOVE-UNIT-DEFENDER-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsAttachedEquipmentSourceUntilAttachmentMovementExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-ATTACHED-001"] = new(
                    "P1-MOVE-UNIT-ATTACHED-001",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-MOVE-EQUIPMENT-ATTACHED-001"] = new(
                    "P1-MOVE-EQUIPMENT-ATTACHED-001",
                    power: 0,
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    attachedToObjectId: "P1-MOVE-UNIT-ATTACHED-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-attached-equipment-source", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-ATTACHED-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("带有贴附装备的单位移动暂未开放。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(
            ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"],
            result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-ATTACHED-001"].Power);
        Assert.Equal(
            "P1-MOVE-UNIT-ATTACHED-001",
            result.State.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-ATTACHED-001"] = new(
                    "P1-MOVE-UNIT-ATTACHED-001",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MOVE-EQUIPMENT-ATTACHED-001"] = new(
                    "P1-MOVE-EQUIPMENT-ATTACHED-001",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    attachedToObjectId: "P1-MOVE-UNIT-ATTACHED-001",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p5-move-unit-attached-equipment-to-battlefield", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-ATTACHED-001",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["UNIT_MOVED_TO_BATTLEFIELD", "EQUIPMENT_MOVED_WITH_UNIT"], result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(
            "P1-MOVE-UNIT-ATTACHED-001",
            result.State.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].AttachedToObjectId);
        Assert.Equal("P1", result.State.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].OwnerId);
        Assert.Equal("P1", result.State.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].ControllerId);

        var followEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_MOVED_WITH_UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-MOVE-UNIT-ATTACHED-001", followEvent.Payload["unitObjectId"]);
        Assert.Equal("P1-MOVE-EQUIPMENT-ATTACHED-001", followEvent.Payload["equipmentObjectId"]);
        Assert.Equal("BASE", followEvent.Payload["originZone"]);
        Assert.Equal("BATTLEFIELD", followEvent.Payload["destinationZone"]);
    }

    [Fact]
    public async Task P5MoveUnitCommandMovesBaseUnitToLowercaseOfficialBattlefieldObjectId()
    {
        const string battlefieldObjectId = "P1-BATTLEFIELD-OGN-276a-298-01";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-LOWER-A-UNIT"],
                    Battlefields = [battlefieldObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-LOWER-A-UNIT"] = new(
                    "P1-MOVE-LOWER-A-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [battlefieldObjectId] = new(
                    battlefieldObjectId,
                    cardNo: "OGN·276a/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-LOWER-A-UNIT"] = new("P1", "BASE"),
                [battlefieldObjectId] = new("P1", "BATTLEFIELD", battlefieldObjectId)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p5-move-unit-lower-a-battlefield", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-LOWER-A-UNIT",
                "BASE",
                $"BATTLEFIELD:{battlefieldObjectId}",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal([battlefieldObjectId, "P1-MOVE-LOWER-A-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations["P1-MOVE-LOWER-A-UNIT"].Zone);
        Assert.Equal(
            battlefieldObjectId,
            result.State.ObjectLocations["P1-MOVE-LOWER-A-UNIT"].BattlefieldObjectId);
        var moveEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal($"BATTLEFIELD:{battlefieldObjectId}", moveEvent.Payload["destination"]);
        Assert.Equal(battlefieldObjectId, moveEvent.Payload["battlefieldObjectId"]);
    }

    [Fact]
    public async Task P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-ATTACHED-001"] = new(
                    "P1-MOVE-UNIT-ATTACHED-001",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MOVE-EQUIPMENT-ATTACHED-001"] = new(
                    "P1-MOVE-EQUIPMENT-ATTACHED-001",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    attachedToObjectId: "P1-MOVE-UNIT-ATTACHED-001",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p5-move-unit-attached-equipment-to-base", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-ATTACHED-001",
                "BATTLEFIELD",
                "BASE",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["UNIT_MOVED_TO_BASE", "EQUIPMENT_MOVED_WITH_UNIT"], result.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(
            ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"],
            result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(
            "P1-MOVE-UNIT-ATTACHED-001",
            result.State.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsPreciseBattlefieldLocationsWithoutRoamCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-SFD-YASUO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SFD-YASUO"] = new(
                    "P1-BATTLEFIELD-SFD-YASUO",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "游走"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-premodel", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-SFD-YASUO",
                "BATTLEFIELD:P1-LEFT",
                "BATTLEFIELD:P1-RIGHT",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("精确战场移动路径暂未开放。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-SFD-YASUO"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-BATTLEFIELD-SFD-YASUO"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandMovesRoamUnitBetweenPreciseBattlefields()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-SFD-YASUO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SFD-YASUO"] = new(
                    "P1-BATTLEFIELD-SFD-YASUO",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    cardNo: "SFD·235/221")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-roam-precise", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-SFD-YASUO",
                "BATTLEFIELD:P1-LEFT",
                "BATTLEFIELD:P1-RIGHT",
                ["ROAM"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        var evt = Assert.Single(result.Events);
        Assert.Equal("UNIT_MOVED_TO_BATTLEFIELD", evt.Kind);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-SFD-YASUO"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-BATTLEFIELD-SFD-YASUO"].Power);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-SFD-YASUO"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task BaronNestStaticMovesUnitBetweenPreciseBattlefieldsWithoutRoamCost()
    {
        var state = BaronNestMoveState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-baron-nest-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(
                "P1-BARON-MOVER",
                "BATTLEFIELD:P1-BATTLEFIELD-ORIGIN",
                "BATTLEFIELD:P1-BARON-NEST",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal("P1-BARON-NEST", result.State.ObjectLocations["P1-BARON-MOVER"].BattlefieldObjectId);
        var moveEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD:P1-BATTLEFIELD-ORIGIN", moveEvent.Payload["origin"]);
        Assert.Equal("BATTLEFIELD:P1-BARON-NEST", moveEvent.Payload["destination"]);
        Assert.Equal("P1-BARON-NEST", moveEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("BARON_NEST_MOVE_STATIC", moveEvent.Payload["movementPermission"]);
        Assert.False(moveEvent.Payload.ContainsKey("movementKeyword"));
        Assert.Empty(Assert.IsType<string[]>(moveEvent.Payload["optionalCosts"]));
    }

    [Theory]
    [InlineData("non-baron-destination")]
    [InlineData("opponent-controlled-baron")]
    [InlineData("origin-mismatch")]
    [InlineData("source-without-card-no")]
    [InlineData("face-down-source")]
    [InlineData("combatant-source")]
    [InlineData("opponent-controlled-source")]
    public async Task BaronNestStaticMoveRejectsInvalidCommandWithoutMutation(string caseName)
    {
        var state = caseName switch
        {
            "non-baron-destination" => BaronNestMoveState() with
            {
                CardObjects = ReplaceCardObject(
                    BaronNestMoveState().CardObjects,
                    "P1-BARON-NEST",
                    BaronNestMoveState().CardObjects["P1-BARON-NEST"] with { CardNo = "OGN·275/298" })
            },
            "opponent-controlled-baron" => BaronNestMoveState() with
            {
                CardObjects = ReplaceCardObject(
                    BaronNestMoveState().CardObjects,
                    "P1-BARON-NEST",
                    BaronNestMoveState().CardObjects["P1-BARON-NEST"] with
                    {
                        OwnerId = "P2",
                        ControllerId = "P2"
                    })
            },
            "origin-mismatch" => BaronNestMoveState() with
            {
                ObjectLocations = ReplaceObjectLocation(
                    BaronNestMoveState().ObjectLocations,
                    "P1-BARON-MOVER",
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-BARON-NEST"))
            },
            "source-without-card-no" => BaronNestMoveState() with
            {
                CardObjects = ReplaceCardObject(
                    BaronNestMoveState().CardObjects,
                    "P1-BARON-MOVER",
                    BaronNestMoveState().CardObjects["P1-BARON-MOVER"] with { CardNo = null })
            },
            "face-down-source" => BaronNestMoveState() with
            {
                CardObjects = ReplaceCardObject(
                    BaronNestMoveState().CardObjects,
                    "P1-BARON-MOVER",
                    BaronNestMoveState().CardObjects["P1-BARON-MOVER"] with { IsFaceDown = true })
            },
            "combatant-source" => BaronNestMoveState() with
            {
                CardObjects = ReplaceCardObject(
                    BaronNestMoveState().CardObjects,
                    "P1-BARON-MOVER",
                    BaronNestMoveState().CardObjects["P1-BARON-MOVER"] with { IsAttacking = true })
            },
            "opponent-controlled-source" => BaronNestMoveState() with
            {
                CardObjects = ReplaceCardObject(
                    BaronNestMoveState().CardObjects,
                    "P1-BARON-MOVER",
                    BaronNestMoveState().CardObjects["P1-BARON-MOVER"] with
                    {
                        OwnerId = "P2",
                        ControllerId = "P2"
                    })
            },
            _ => BaronNestMoveState()
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-baron-nest-move-reject-{caseName}", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(
                "P1-BARON-MOVER",
                "BATTLEFIELD:P1-BATTLEFIELD-ORIGIN",
                "BATTLEFIELD:P1-BARON-NEST",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Fact]
    public void BaronNestStaticMovePromptExposesDestinationWithoutRoamCost()
    {
        var prompt = ResolutionResult.BuildPrompts(BaronNestMoveState())["P1"];
        var moveCandidate = Assert.Single(prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(moveCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-BARON-MOVER", StringComparison.Ordinal));
        Assert.Contains(moveCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P1-BARON-NEST", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        var baronRequirement = Assert.Single(requirements, requirement =>
            string.Equals(requirement["sourceObjectId"] as string, "P1-BARON-MOVER", StringComparison.Ordinal)
            && string.Equals(requirement["mode"] as string, "BARON_NEST_MOVE_STATIC", StringComparison.Ordinal));

        var destinationChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(baronRequirement["destinationChoices"]);
        Assert.Equal(["BATTLEFIELD:P1-BARON-NEST"], destinationChoices.Select(choice => choice.Id).ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(baronRequirement["optionalCostChoices"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(baronRequirement["requiredOptionalCosts"]));
        Assert.Equal("BARON_NEST_MOVE_STATIC", baronRequirement["movementPermission"]);
        Assert.False(Assert.IsType<bool>(baronRequirement["requiresRoamOptionalCost"]));
    }

    [Fact]
    public void BaronNestStaticMovePromptHidesOpponentControlledDestination()
    {
        var baseState = BaronNestMoveState();
        var state = baseState with
        {
            CardObjects = ReplaceCardObject(
                baseState.CardObjects,
                "P1-BARON-NEST",
                baseState.CardObjects["P1-BARON-NEST"] with
                {
                    OwnerId = "P2",
                    ControllerId = "P2"
                })
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.DoesNotContain(moveCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P1-BARON-NEST", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.DoesNotContain(requirements, requirement =>
            string.Equals(requirement["mode"] as string, "BARON_NEST_MOVE_STATIC", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsOpponentControlledPreciseRoamSourceInPlayerZone()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-OPPONENT-CONTROLLED-ROAM"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-OPPONENT-CONTROLLED-ROAM"] = new(
                    "P1-BATTLEFIELD-OPPONENT-CONTROLLED-ROAM",
                    cardNo: "SFD·235/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-precise-opponent-controlled-player-zone", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-BATTLEFIELD-OPPONENT-CONTROLLED-ROAM",
                "BATTLEFIELD:P1-LEFT",
                "BATTLEFIELD:P1-RIGHT",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("移动单位只能选择当前玩家控制的单位。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-BATTLEFIELD-OPPONENT-CONTROLLED-ROAM"], result.State.PlayerZones["P1"].Battlefields);
        Assert.False(result.State.ObjectLocations.ContainsKey("P1-BATTLEFIELD-OPPONENT-CONTROLLED-ROAM"));
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsPreciseBattlefieldDestinationWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                        "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                        "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new(
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new(
                    "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                    cardNo: "SFD·096/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-unknown-cardno-battlefield", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                "BATTLEFIELD:P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                "BATTLEFIELD:P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal(
            "精确战场移动需要服务端已确认的战场牌信息。",
            result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal("P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN", result.State.ObjectLocations["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"].BattlefieldObjectId);
        Assert.Null(result.State.CardObjects["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"].CardNo);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsPreciseBattlefieldDestinationWithoutRoamCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-UNIT-PRECISE-DESTINATION-001"],
                    Battlefields = ["P1-MOVE-FIELD-KEEPER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-UNIT-PRECISE-DESTINATION-001"] = new(
                    "P1-MOVE-UNIT-PRECISE-DESTINATION-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-MOVE-FIELD-KEEPER"] = new(
                    "P1-MOVE-FIELD-KEEPER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-precise-destination", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-PRECISE-DESTINATION-001",
                "BASE",
                "BATTLEFIELD:P1-MAIN",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("精确战场移动路径暂未开放。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-MOVE-UNIT-PRECISE-DESTINATION-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-FIELD-KEEPER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-MOVE-UNIT-PRECISE-DESTINATION-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.State.CardObjects["P1-MOVE-UNIT-PRECISE-DESTINATION-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandMovesBaseUnitToConcreteBattlefield()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-CONCRETE-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-CONCRETE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-BASE-UNIT-001"] = new(
                    "P1-MOVE-BASE-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-CONCRETE-001"] = new(
                    "P1-BATTLEFIELD-CONCRETE-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-CONCRETE-001"] = new(
                    "P2-BATTLEFIELD-CONCRETE-001",
                    cardNo: "OGN·276/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-BASE-UNIT-001"] = new("P1", "BASE"),
                ["P1-BATTLEFIELD-CONCRETE-001"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-CONCRETE-001"),
                ["P2-BATTLEFIELD-CONCRETE-001"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-CONCRETE-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-base-to-concrete-battlefield", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-BASE-UNIT-001",
                "BASE",
                "BATTLEFIELD:P1-BATTLEFIELD-CONCRETE-001",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var moveEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD:P1-BATTLEFIELD-CONCRETE-001", moveEvent.Payload["destination"]);
        Assert.Equal("P1-BATTLEFIELD-CONCRETE-001", moveEvent.Payload["battlefieldObjectId"]);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-BATTLEFIELD-CONCRETE-001", "P1-MOVE-BASE-UNIT-001"],
            result.State.PlayerZones["P1"].Battlefields);
        var location = result.State.ObjectLocations["P1-MOVE-BASE-UNIT-001"];
        Assert.Equal("P1", location.PlayerId);
        Assert.Equal("BATTLEFIELD", location.Zone);
        Assert.Equal("P1-BATTLEFIELD-CONCRETE-001", location.BattlefieldObjectId);
    }

    [Fact]
    public async Task P4MoveUnitCommandStartsBattlefieldContestWhenBaseUnitMovesToOccupiedEnemyBattlefield()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TurnPlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-CONTEST-001", "P1-FIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-CONTESTER-001"],
                    Battlefields = ["P2-BATTLEFIELD-CONTEST-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CONTEST-001"] = new(
                    "P1-BATTLEFIELD-CONTEST-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-CONTEST-001"] = new(
                    "P2-BATTLEFIELD-CONTEST-001",
                    cardNo: "OGN·276/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-FIELD-UNIT-001"] = new(
                    "P1-FIELD-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BASE-CONTESTER-001"] = new(
                    "P2-BASE-CONTESTER-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CONTEST-001"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P2-BATTLEFIELD-CONTEST-001"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-CONTEST-001"),
                ["P1-FIELD-UNIT-001"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P2-BASE-CONTESTER-001"] = new("P2", "BASE")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-base-to-enemy-battlefield", "P2", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P2-BASE-CONTESTER-001",
                "BASE",
                "BATTLEFIELD:P1-BATTLEFIELD-CONTEST-001",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.True(result.State.BattlefieldStates["P1-BATTLEFIELD-CONTEST-001"].Contested);
        Assert.Equal(
            ["P1", "P2"],
            result.State.BattlefieldStates["P1-BATTLEFIELD-CONTEST-001"].OccupantControllerIds);
        Assert.Equal(
            "P1-BATTLEFIELD-CONTEST-001",
            result.State.ObjectLocations["P2-BASE-CONTESTER-001"].BattlefieldObjectId);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectsPreciseBattlefieldOriginWithoutRoamCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-MOVE-BASE-KEEPER"],
                    Battlefields = ["P1-MOVE-UNIT-PRECISE-ORIGIN-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MOVE-BASE-KEEPER"] = new(
                    "P1-MOVE-BASE-KEEPER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-MOVE-UNIT-PRECISE-ORIGIN-001"] = new(
                    "P1-MOVE-UNIT-PRECISE-ORIGIN-001",
                    power: 3,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-move-unit-precise-origin", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-MOVE-UNIT-PRECISE-ORIGIN-001",
                "BATTLEFIELD:P1-MAIN",
                "BASE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("精确战场移动路径暂未开放。", result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-MOVE-BASE-KEEPER"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-UNIT-PRECISE-ORIGIN-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.State.CardObjects["P1-MOVE-UNIT-PRECISE-ORIGIN-001"].Power);
        Assert.True(result.State.CardObjects["P1-MOVE-UNIT-PRECISE-ORIGIN-001"].IsExhausted);
        Assert.Equal([CardObjectTags.UnitCard], result.State.CardObjects["P1-MOVE-UNIT-PRECISE-ORIGIN-001"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-command-premodel-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-SFD-YASUO"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BATTLEFIELD-SFD-YASUO"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandRoamPreciseBattlefieldFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-roam-precise-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-SFD-YASUO"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BATTLEFIELD-SFD-YASUO"].Power);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-SFD-YASUO"].IsExhausted);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandPreciseDestinationRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-precise-destination-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MOVE-UNIT-PRECISE-DESTINATION-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-FIELD-KEEPER"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-PRECISE-DESTINATION-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard],
            result.FinalState.CardObjects["P1-MOVE-UNIT-PRECISE-DESTINATION-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandPreciseOriginRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-precise-origin-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MOVE-BASE-KEEPER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-UNIT-PRECISE-ORIGIN-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-MOVE-UNIT-PRECISE-ORIGIN-001"].Power);
        Assert.True(result.FinalState.CardObjects["P1-MOVE-UNIT-PRECISE-ORIGIN-001"].IsExhausted);
        Assert.Equal(
            [CardObjectTags.UnitCard],
            result.FinalState.CardObjects["P1-MOVE-UNIT-PRECISE-ORIGIN-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandBaseToBattlefieldFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-base-to-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-MOVE-FIELD-KEEPER", "P1-MOVE-UNIT-BASE-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-BASE-001"].Power);
        Assert.True(result.FinalState.CardObjects["P1-MOVE-UNIT-BASE-001"].IsExhausted);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandBattlefieldToBaseFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-battlefield-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(
            ["P1-MOVE-BASE-KEEPER", "P1-MOVE-UNIT-BATTLEFIELD-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-MOVE-UNIT-BATTLEFIELD-001"].Power);
        Assert.True(result.FinalState.CardObjects["P1-MOVE-UNIT-BATTLEFIELD-001"].IsExhausted);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandOpponentSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-opponent-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-MOVE-UNIT-BATTLEFIELD-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P2-MOVE-UNIT-BATTLEFIELD-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandFaceDownSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-face-down-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-FACEDOWN-STANDBY-MOVE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-FACEDOWN-STANDBY-MOVE-001"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-FACEDOWN-STANDBY-MOVE-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.FinalState.CardObjects["P1-FACEDOWN-STANDBY-MOVE-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandNonUnitSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-non-unit-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-EQUIPMENT-MOVE-SOURCE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-MOVE-SOURCE-001"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, "武装", "灵便"],
            result.FinalState.CardObjects["P1-EQUIPMENT-MOVE-SOURCE-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandSameZoneDestinationRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-same-zone-destination-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-MOVE-UNIT-SAME-ZONE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-SAME-ZONE-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MOVE-UNIT-SAME-ZONE-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandWindowRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-window-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal("P2", result.FinalState.ActivePlayerId);
        Assert.Equal(["P1-MOVE-UNIT-WINDOW-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-WINDOW-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MOVE-UNIT-WINDOW-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandPendingStackRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-pending-stack-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(TimingStates.NeutralClosed, result.FinalState.TimingState);
        Assert.Equal("P1", result.FinalState.PriorityPlayerId);
        Assert.Equal(["P1-MOVE-UNIT-STACK-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-STACK-001"].Power);
        var stackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task P4MoveUnitCommandOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-UNIT-OPTIONAL-COST-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-OPTIONAL-COST-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "游走"],
            result.FinalState.CardObjects["P1-MOVE-UNIT-OPTIONAL-COST-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandUnsupportedZoneRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-unsupported-zone-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-MOVE-UNIT-ZONE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-ZONE-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MOVE-UNIT-ZONE-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandOriginMismatchRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-origin-mismatch-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-MOVE-UNIT-ORIGIN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-ORIGIN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MOVE-UNIT-ORIGIN-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandHandSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-hand-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-MOVE-UNIT-HAND-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-HAND-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MOVE-UNIT-HAND-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandCombatantSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-combatant-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MOVE-UNIT-COMBATANT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-MOVE-UNIT-DEFENDER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-MOVE-UNIT-COMBATANT-001"].IsAttacking);
        Assert.True(result.FinalState.CardObjects["P2-MOVE-UNIT-DEFENDER-001"].IsDefending);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-COMBATANT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-MOVE-UNIT-DEFENDER-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MoveUnitCommandAttachedEquipmentSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-move-unit-attached-equipment-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(
            ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-MOVE-UNIT-ATTACHED-001"].Power);
        Assert.Equal(
            "P1-MOVE-UNIT-ATTACHED-001",
            result.FinalState.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].AttachedToObjectId);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P5MoveUnitCommandAttachedEquipmentFollowsHostFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-move-unit-attached-equipment-follows-host.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.Tick);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-MOVE-UNIT-ATTACHED-001", "P1-MOVE-EQUIPMENT-ATTACHED-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(
            "P1-MOVE-UNIT-ATTACHED-001",
            result.FinalState.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].AttachedToObjectId);
        Assert.Equal("P1", result.FinalState.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].OwnerId);
        Assert.Equal("P1", result.FinalState.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].ControllerId);
        Assert.True(result.FinalState.CardObjects["P1-MOVE-UNIT-ATTACHED-001"].IsExhausted);
        Assert.False(result.FinalState.CardObjects["P1-MOVE-EQUIPMENT-ATTACHED-001"].IsExhausted);
    }
}
