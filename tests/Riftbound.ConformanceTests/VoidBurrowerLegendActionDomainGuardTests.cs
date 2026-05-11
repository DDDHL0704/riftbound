using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VoidBurrowerLegendActionDomainGuardTests
{
    [Fact]
    public async Task VoidBurrowerRepresentativeConquerRevealsTopTwoPlaysFirstUnitAndRecyclesRest()
    {
        var state = VoidBurrowerConquerState("SFD·187/221", "P1-LEGEND-VOID-BURROWER", legendExhausted: false, topUnit: true);

        var result = await DeclareBattleAsync(state, "intent-void-burrower-legend-action-domain-play-unit");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-LEGEND-VOID-BURROWER"].IsExhausted);
        Assert.Equal(["P1-VOID-PLAY-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-VOID-KEEP", "P1-VOID-RECYCLE"], result.State.PlayerZones["P1"].MainDeck);

        var playedUnit = result.State.CardObjects["P1-VOID-PLAY-UNIT"];
        Assert.False(playedUnit.IsExhausted);
        Assert.Equal(0, playedUnit.Damage);
        Assert.Equal(0, playedUnit.UntilEndOfTurnPowerModifier);
        Assert.Empty(playedUnit.UntilEndOfTurnEffects);
        Assert.Equal("P1", playedUnit.OwnerId);
        Assert.Equal("P1", playedUnit.ControllerId);

        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_PLAY_ONE_RECYCLE_REST", StringComparison.Ordinal));
        Assert.Equal("SFD·187/221", triggerEvent.Payload["legendCardNo"]);
        Assert.Equal("P1-LEGEND-VOID-BURROWER", triggerEvent.Payload["legendObjectId"]);
        Assert.Equal("P1-VOID-ATTACKER", triggerEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-VOID-PLAY-UNIT", triggerEvent.Payload["playedObjectId"]);
        Assert.Equal(["P1-VOID-PLAY-UNIT", "P1-VOID-RECYCLE"], (string[])triggerEvent.Payload["revealedObjectIds"]!);
        Assert.Equal(["P1-VOID-RECYCLE"], (IReadOnlyList<string>)triggerEvent.Payload["recycledObjectIds"]!);

        var exhaustedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-VOID-BURROWER", exhaustedEvent.Payload["sourceObjectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_PLAY_ONE_RECYCLE_REST", exhaustedEvent.Payload["reason"]);

        var revealEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-VOID-BURROWER", revealEvent.Payload["sourceObjectId"]);
        Assert.Equal(["P1-VOID-PLAY-UNIT", "P1-VOID-RECYCLE"], (string[])revealEvent.Payload["cardIds"]!);
        Assert.Equal(2, revealEvent.Payload["count"]);

        var playEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-VOID-BURROWER", playEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-VOID-PLAY-UNIT", playEvent.Payload["targetObjectId"]);
        Assert.Equal("MAIN_DECK", playEvent.Payload["sourceZone"]);
        Assert.Equal("BASE", playEvent.Payload["destinationZone"]);

        var recycleEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-VOID-BURROWER", recycleEvent.Payload["sourceObjectId"]);
        Assert.Equal(["P1-VOID-RECYCLE"], (IReadOnlyList<string>)recycleEvent.Payload["cardIds"]!);
        Assert.Equal(1, recycleEvent.Payload["count"]);
    }

    [Fact]
    public async Task VoidBurrowerReprintRepresentativeConquerWithNoRevealedUnitRecyclesBothCards()
    {
        var state = VoidBurrowerConquerState("SFD·243/221", "P1-LEGEND-VOID-BURROWER-REPRINT", legendExhausted: false, topUnit: false);

        var result = await DeclareBattleAsync(state, "intent-void-burrower-legend-action-domain-reprint-no-unit");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-LEGEND-VOID-BURROWER-REPRINT"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-VOID-KEEP", result.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Contains("P1-VOID-RECYCLE", result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-VOID-SECOND-RECYCLE", result.State.PlayerZones["P1"].MainDeck);

        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_PLAY_ONE_RECYCLE_REST", StringComparison.Ordinal));
        Assert.Equal("SFD·243/221", triggerEvent.Payload["legendCardNo"]);
        Assert.Equal("P1-LEGEND-VOID-BURROWER-REPRINT", triggerEvent.Payload["legendObjectId"]);
        Assert.Equal(string.Empty, triggerEvent.Payload["playedObjectId"]);
        Assert.Equal(["P1-VOID-RECYCLE", "P1-VOID-SECOND-RECYCLE"], (string[])triggerEvent.Payload["revealedObjectIds"]!);

        Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        var revealEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.Equal(2, revealEvent.Payload["count"]);
        Assert.Equal(["P1-VOID-RECYCLE", "P1-VOID-SECOND-RECYCLE"], (string[])revealEvent.Payload["cardIds"]!);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));

        var recycleEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(2, recycleEvent.Payload["count"]);
        var recycledCardIds = Assert.IsAssignableFrom<IEnumerable<string>>(recycleEvent.Payload["cardIds"]);
        Assert.Contains("P1-VOID-RECYCLE", recycledCardIds);
        Assert.Contains("P1-VOID-SECOND-RECYCLE", recycledCardIds);
    }

    [Fact]
    public async Task VoidBurrowerRepresentativeConquerRequiresActiveLegend()
    {
        var state = VoidBurrowerConquerState("SFD·187/221", "P1-LEGEND-VOID-BURROWER", legendExhausted: true, topUnit: true);

        var result = await DeclareBattleAsync(state, "intent-void-burrower-legend-action-domain-exhausted");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-LEGEND-VOID-BURROWER"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-VOID-PLAY-UNIT", "P1-VOID-RECYCLE", "P1-VOID-KEEP"], result.State.PlayerZones["P1"].MainDeck);

        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_PLAY_ONE_RECYCLE_REST", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> DeclareBattleAsync(MatchState state, string intentId)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent(intentId, "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-VOID-ATTACKER"],
                ["P2-VOID-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static MatchState VoidBurrowerConquerState(
        string legendCardNo,
        string legendObjectId,
        bool legendExhausted,
        bool topUnit)
    {
        var firstRevealedCardObjectId = topUnit ? "P1-VOID-PLAY-UNIT" : "P1-VOID-RECYCLE";
        var secondRevealedCardObjectId = topUnit ? "P1-VOID-RECYCLE" : "P1-VOID-SECOND-RECYCLE";
        return BaseState() with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-VOID-ATTACKER"],
                    LegendZone = [legendObjectId],
                    MainDeck = [firstRevealedCardObjectId, secondRevealedCardObjectId, "P1-VOID-KEEP"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-VOID-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-VOID-ATTACKER"] = new(
                    "P1-VOID-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                [legendObjectId] = new(
                    legendObjectId,
                    cardNo: legendCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-VOID-DEFENDER"] = new(
                    "P2-VOID-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-VOID-PLAY-UNIT"] = new(
                    "P1-VOID-PLAY-UNIT",
                    cardNo: "SFD·029/221",
                    power: 3,
                    damage: 2,
                    isExhausted: true,
                    untilEndOfTurnPowerModifier: 2,
                    untilEndOfTurnEffects: ["TEMP_REPRESENTATIVE"],
                    tags: [CardObjectTags.UnitCard, "强攻"],
                    ownerId: "P1"),
                ["P1-VOID-RECYCLE"] = new(
                    "P1-VOID-RECYCLE",
                    cardNo: "UNL-159/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1"),
                ["P1-VOID-SECOND-RECYCLE"] = new(
                    "P1-VOID-SECOND-RECYCLE",
                    cardNo: "UNL-160/219",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1"),
                ["P1-VOID-KEEP"] = new(
                    "P1-VOID-KEEP",
                    cardNo: "UNL-001/219",
                    ownerId: "P1")
            }
        };
    }

    private static MatchState BaseState()
    {
        return new MatchState(
            roomId: "void-burrower-legend-action-domain-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal));
    }
}
