using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LuxHighCostPaidCostTriggerTests
{
    private const string LuxUnitObjectId = "P1-LUX";
    private const string LuxUnitCardNo = "OGS·006/024";
    private const string LuxLegendObjectId = "P1-LEGEND-LUX";
    private const string LuxLegendCardNo = "OGS·021/024";
    private const string HiddenDrawObjectId = "P1-DRAW-HIDDEN";
    private const string HiddenDrawCardNo = "SFD·106/221";
    private const string HighPrintedSpellObjectId = "P1-SPELL-EVOLUTION-DAY";
    private const string HighPrintedSpellCardNo = "OGN·114/298";
    private const string LowerPrintedSpellObjectId = "P1-SPELL-CRESCENT-STRIKE";
    private const string LowerPrintedSpellCardNo = "UNL-072/219";
    private const string SpellshieldTargetObjectId = "P2-ORNN-SPELLSHIELD-2";
    private const string LuxUnitHighCostEffectKind = "OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3";
    private const string LuxLegendHighCostTrigger = "HIGH_COST_SPELL_DRAW_ONE";
    private const string RagingDrakeReductionEffectId =
        "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:P1-UNIT-RAGING-DRAKE";

    [Fact]
    public async Task LuxPaidCostHighPrintedSpellReducedBelowThresholdDoesNotTriggerUnitOrLegend()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxPaidCostState(
            HighPrintedSpellObjectId,
            HighPrintedSpellCardNo,
            mana: 1,
            untilEndOfTurnEffects: [RagingDrakeReductionEffectId],
            includeDeck: true);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-paid-cost-reduced-high-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(HighPrintedSpellObjectId, HighPrintedSpellCardNo, []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaid = AssertSingleCostPaid(result);
        Assert.Equal(6, Assert.IsType<int>(costPaid.Payload["baseMana"]));
        Assert.Equal(1, Assert.IsType<int>(costPaid.Payload["mana"]));
        Assert.Equal(5, Assert.IsType<int>(costPaid.Payload["nextSpellCostReductionMana"]));
        AssertLuxUnitDidNotTrigger(result);
        AssertLuxLegendDidNotTrigger(result);
        AssertLuxPowerUnchanged(result);
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LuxPaidCostLowerPrintedSpellRaisedBySpellshieldTaxTriggersUnitAndLegend()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxPaidCostState(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            mana: 5,
            includeSpellshieldTarget: true,
            includeDeck: true);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-paid-cost-taxed-low-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(LowerPrintedSpellObjectId, LowerPrintedSpellCardNo, [SpellshieldTargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaid = AssertSingleCostPaid(result);
        Assert.Equal(3, Assert.IsType<int>(costPaid.Payload["baseMana"]));
        Assert.Equal(5, Assert.IsType<int>(costPaid.Payload["mana"]));
        Assert.Equal(2, Assert.IsType<int>(costPaid.Payload["spellshieldTaxMana"]));
        Assert.Equal(
            [SpellshieldTargetObjectId],
            Assert.IsType<string[]>(costPaid.Payload["spellshieldTaxTargetObjectIds"]));

        AssertLuxUnitTriggered(result);
        var lux = result.State.CardObjects[LuxUnitObjectId];
        Assert.Equal(8, lux.Power);
        Assert.Equal(3, lux.UntilEndOfTurnPowerModifier);

        var legendTrigger = Assert.Single(result.Events, IsLuxLegendTriggerEvent);
        Assert.Equal(
            ["legendCardNo", "playedCardManaCost", "playedCardNo", "playerId", "trigger"],
            legendTrigger.Payload.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(LuxLegendCardNo, Assert.IsType<string>(legendTrigger.Payload["legendCardNo"]));
        Assert.Equal(3, Assert.IsType<int>(legendTrigger.Payload["playedCardManaCost"]));

        var drawEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(["count", "playerId"], drawEvent.Payload.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(1, Assert.IsType<int>(drawEvent.Payload["count"]));
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);

        var opponentSnapshot = JsonSerializer.Serialize(result.Snapshots["P2"]);
        Assert.DoesNotContain(HiddenDrawCardNo, opponentSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenDrawObjectId, opponentSnapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LuxPaidCostRejectedSpellshieldTaxPathLeavesStateUnmutated()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLuxPaidCostState(
            LowerPrintedSpellObjectId,
            LowerPrintedSpellCardNo,
            mana: 4,
            includeSpellshieldTarget: true,
            includeDeck: true);

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-lux-paid-cost-tax-rejected", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(LowerPrintedSpellObjectId, LowerPrintedSpellCardNo, [SpellshieldTargetObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Equal([LowerPrintedSpellObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Equal([HiddenDrawObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(4, result.State.RunePools["P1"].Mana);
        Assert.Empty(result.State.StackItems);
        AssertLuxPowerUnchanged(result);
        AssertLuxUnitDidNotTrigger(result);
        AssertLuxLegendDidNotTrigger(result);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    private static MatchState BuildLuxPaidCostState(
        string spellObjectId,
        string spellCardNo,
        int mana,
        IReadOnlyList<string>? untilEndOfTurnEffects = null,
        bool includeSpellshieldTarget = false,
        bool includeDeck = false)
    {
        var p1MainDeck = includeDeck ? new[] { HiddenDrawObjectId } : [];
        var p2Battlefields = includeSpellshieldTarget ? new[] { SpellshieldTargetObjectId } : [];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [LuxUnitObjectId] = new(
                LuxUnitObjectId,
                power: 5,
                tags: [CardObjectTags.UnitCard],
                manaCost: 6,
                cardNo: LuxUnitCardNo,
                ownerId: "P1",
                controllerId: "P1"),
            [LuxLegendObjectId] = new(
                LuxLegendObjectId,
                cardNo: LuxLegendCardNo,
                ownerId: "P1",
                controllerId: "P1"),
            [spellObjectId] = new(
                spellObjectId,
                cardNo: spellCardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        if (includeDeck)
        {
            cardObjects[HiddenDrawObjectId] = new(
                HiddenDrawObjectId,
                cardNo: HiddenDrawCardNo,
                ownerId: "P1",
                controllerId: "P1");
        }

        if (includeSpellshieldTarget)
        {
            cardObjects[SpellshieldTargetObjectId] = new(
                SpellshieldTargetObjectId,
                power: 6,
                tags: [CardObjectTags.UnitCard, "法盾2"],
                manaCost: 6,
                cardNo: "SFD·085/221",
                ownerId: "P2",
                controllerId: "P2");
        }

        return new MatchState(
            "lux-paid-cost-room",
            81,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
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
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = p1MainDeck,
                    Hand = [spellObjectId],
                    Base = [LuxUnitObjectId],
                    LegendZone = [LuxLegendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            seed: 404,
            untilEndOfTurnEffects: untilEndOfTurnEffects ?? [],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }

    private static GameEvent AssertSingleCostPaid(ResolutionResult result)
    {
        return Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    private static void AssertLuxUnitTriggered(ResolutionResult result)
    {
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("effectKind", out var effectKind) ? effectKind as string : null, LuxUnitHighCostEffectKind, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("targetObjectId", out var targetObjectId) ? targetObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["appliedPowerDelta"], 3));
    }

    private static void AssertLuxUnitDidNotTrigger(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Payload.TryGetValue("effectKind", out var effectKind) ? effectKind as string : null, LuxUnitHighCostEffectKind, StringComparison.Ordinal)
            || string.Equals(gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null, LuxUnitHighCostEffectKind, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId) ? sourceObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("targetObjectId", out var targetObjectId) ? targetObjectId as string : null, LuxUnitObjectId, StringComparison.Ordinal));
    }

    private static void AssertLuxLegendDidNotTrigger(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, IsLuxLegendTriggerEvent);
    }

    private static bool IsLuxLegendTriggerEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(
                gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null,
                LuxLegendHighCostTrigger,
                StringComparison.Ordinal);
    }

    private static void AssertLuxPowerUnchanged(ResolutionResult result)
    {
        var lux = result.State.CardObjects[LuxUnitObjectId];
        Assert.Equal(5, lux.Power);
        Assert.Equal(0, lux.UntilEndOfTurnPowerModifier);
    }
}
