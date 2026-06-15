using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LegionRearguardHasteReadyEntryTests
{
    private const string LegionRearguardCardNo = "OGN·010/298";
    private const string LegionRearguardObjectId = "P1-LEGION-REARGUARD";

    [Fact]
    public async Task LegionRearguardNoOptionalHasteReadyResolvesExhaustedToBase()
    {
        var engine = new CoreRuleEngine();

        var played = await PlayLegionRearguardAsync(engine, BuildLegionRearguardState(new RunePool(2, 0)));

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Empty(resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains(LegionRearguardObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", resolved.State.ObjectLocations[LegionRearguardObjectId].Zone);
        Assert.True(resolved.State.CardObjects[LegionRearguardObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsLegionRearguardUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("hasteReadyOptionalCostPaid"));
    }

    [Fact]
    public async Task LegionRearguardPaidHasteReadyResolvesActiveToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLegionRearguardState(new RunePool(
            3,
            0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Red] = 1
            }));

        var played = await PlayLegionRearguardAsync(
            engine,
            state,
            optionalCosts: [HasteOptionalCostNames.HasteReady]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Empty(resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains(LegionRearguardObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", resolved.State.ObjectLocations[LegionRearguardObjectId].Zone);
        Assert.False(resolved.State.CardObjects[LegionRearguardObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsLegionRearguardUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(true, unitEvent.Payload["hasteReadyOptionalCostPaid"]);
    }

    private static async Task<ResolutionResult> PlayLegionRearguardAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-legion-rearguard-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                LegionRearguardObjectId,
                LegionRearguardCardNo,
                [],
                OptionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-legion-rearguard-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-legion-rearguard-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static bool IsLegionRearguardUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, LegionRearguardObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, LegionRearguardObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "军团后卫", StringComparison.Ordinal);
    }

    private static MatchState BuildLegionRearguardState(RunePool p1RunePool)
    {
        return new MatchState(
            "legion-rearguard-haste-ready-entry",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "s1",
                ["P2"] = "s2"
            }) with
        {
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = p1RunePool,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [LegionRearguardObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [LegionRearguardObjectId] = new(
                    LegionRearguardObjectId,
                    cardNo: LegionRearguardCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [LegionRearguardObjectId] = new("P1", "HAND")
            }
        };
    }
}
