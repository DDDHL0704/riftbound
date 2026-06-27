using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MoltenDrakeOtherFriendlyActiveEntryTests
{
    private const string MoltenDrakeCardNo = "OGN·011/298";
    private const string MoltenDrakeObjectId = "P1-MOLTEN-DRAKE";
    private const string LegionRearguardCardNo = "OGN·010/298";
    private const string LegionRearguardObjectId = "P1-LEGION-REARGUARD";

    [Fact]
    public async Task MoltenDrakeMakesOtherFriendlyUnpaidHasteUnitEnterReadyFromStaticAbilitySpec()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLegionRearguardStateWithMoltenDrakeOnBase();

        var played = await PlayLegionRearguardAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(LegionRearguardObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[LegionRearguardObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsLegionRearguardUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.OtherFriendlyUnitsEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(MoltenDrakeObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(MoltenDrakeCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
        Assert.False(unitEvent.Payload.ContainsKey("hasteReadyOptionalCostPaid"));
    }

    [Fact]
    public async Task FaceDownMoltenDrakeDoesNotMakeOtherFriendlyUnitEnterReady()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLegionRearguardStateWithMoltenDrakeOnBase(faceDownMoltenDrake: true);

        var played = await PlayLegionRearguardAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(LegionRearguardObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[LegionRearguardObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsLegionRearguardUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayLegionRearguardAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-molten-drake-static-entry-play-legion", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                LegionRearguardObjectId,
                LegionRearguardCardNo,
                []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-molten-drake-static-entry-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-molten-drake-static-entry-p2-pass", "P2", CommandTypes.PassPriority),
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

    private static MatchState BuildLegionRearguardStateWithMoltenDrakeOnBase(bool faceDownMoltenDrake = false)
    {
        var moltenDrakeTags = new[] { CardObjectTags.UnitCard }
            .Concat(faceDownMoltenDrake ? [CardObjectTags.Standby] : Array.Empty<string>())
            .ToArray();

        return new MatchState(
            "molten-drake-other-friendly-active-entry",
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
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [LegionRearguardObjectId],
                    Base = [MoltenDrakeObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [MoltenDrakeObjectId] = new(
                    MoltenDrakeObjectId,
                    isFaceDown: faceDownMoltenDrake,
                    power: 8,
                    tags: moltenDrakeTags,
                    cardNo: MoltenDrakeCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                [LegionRearguardObjectId] = new(
                    LegionRearguardObjectId,
                    cardNo: LegionRearguardCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [MoltenDrakeObjectId] = new("P1", "BASE"),
                [LegionRearguardObjectId] = new("P1", "HAND")
            }
        };
    }
}
