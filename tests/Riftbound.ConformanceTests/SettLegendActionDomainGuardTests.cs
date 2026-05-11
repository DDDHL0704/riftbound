using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SettLegendActionDomainGuardTests
{
    [Fact]
    public async Task SettLegendActivePaysOneAndRecallsBoonUnitInsteadOfDestroyingIt()
    {
        var state = SettDestroyReplacementState("OGN·269/298", "P1-LEGEND-SETT", mana: 1, legendExhausted: false, boonAttacker: true);

        var result = await DeclareSettBattleAsync(state, "intent-sett-legend-replacement-positive");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-LEGEND-SETT"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-SETT-BOON-ATTACKER"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);

        var recalledUnit = result.State.CardObjects["P1-SETT-BOON-ATTACKER"];
        Assert.True(recalledUnit.IsExhausted);
        Assert.Equal(1, recalledUnit.Power);
        Assert.Equal(0, recalledUnit.Damage);
        Assert.DoesNotContain(CardObjectTags.Boon, recalledUnit.Tags);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SETT-BOON-ATTACKER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["mana"]) == 1
            && string.Equals(gameEvent.Payload["reason"] as string, "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["replacementEffectId"] as string, "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SETT-BOON-ATTACKER", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("OGN·269/298", "P1-LEGEND-SETT-NO-MANA", 0, false, true)]
    [InlineData("OGN·269/298", "P1-LEGEND-SETT-EXHAUSTED", 1, true, true)]
    [InlineData("OGN·269/298", "P1-LEGEND-SETT-NON-BOON", 1, false, false)]
    [InlineData("OGN·310*/298", "P1-LEGEND-SETT-ALT", 0, false, true)]
    public async Task SettLegendReplacementSkipsInvalidRepresentativeCases(
        string legendCardNo,
        string legendObjectId,
        int mana,
        bool legendExhausted,
        bool boonAttacker)
    {
        var state = SettDestroyReplacementState(legendCardNo, legendObjectId, mana, legendExhausted, boonAttacker);

        var result = await DeclareSettBattleAsync(state, $"intent-sett-legend-replacement-skip-{legendObjectId}");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(legendExhausted, result.State.CardObjects[legendObjectId].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-SETT-BOON-ATTACKER"], result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_CONSUMED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["replacementEffectId"] as string, "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-SETT-BOON-ATTACKER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SettLegendExhaustedReprintReadiesOnConquer()
    {
        var state = SettConquerState("OGN·310/298", "P1-LEGEND-SETT-REPRINT", legendExhausted: true);

        var result = await DeclareSettBattleAsync(state, "intent-sett-legend-conquer-ready");

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.False(result.State.CardObjects["P1-LEGEND-SETT-REPRINT"].IsExhausted);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_READY_LEGEND", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["legendObjectId"] as string, "P1-LEGEND-SETT-REPRINT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LEGEND-SETT-REPRINT", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> DeclareSettBattleAsync(MatchState state, string intentId)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent(intentId, "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                state.PlayerZones["P1"].Battlefields,
                state.PlayerZones["P2"].Battlefields,
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static MatchState SettDestroyReplacementState(
        string legendCardNo,
        string legendObjectId,
        int mana,
        bool legendExhausted,
        bool boonAttacker)
    {
        return BaseState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-SETT-BOON-ATTACKER"],
                    LegendZone = [legendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SETT-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SETT-BOON-ATTACKER"] = new(
                    "P1-SETT-BOON-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: boonAttacker ? [CardObjectTags.UnitCard, CardObjectTags.Boon] : [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [legendObjectId] = new(
                    legendObjectId,
                    cardNo: legendCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SETT-DEFENDER"] = new(
                    "P2-SETT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState SettConquerState(
        string legendCardNo,
        string legendObjectId,
        bool legendExhausted)
    {
        return BaseState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-SETT-ATTACKER"],
                    LegendZone = [legendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SETT-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SETT-ATTACKER"] = new(
                    "P1-SETT-ATTACKER",
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
                ["P2-SETT-DEFENDER"] = new(
                    "P2-SETT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BaseState(int mana)
    {
        return new MatchState(
            roomId: "sett-legend-action-domain-guard-test",
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
                ["P1"] = new(mana, 0),
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
