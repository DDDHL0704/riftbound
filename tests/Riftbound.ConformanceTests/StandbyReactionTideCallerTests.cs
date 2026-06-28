using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyReactionTideCallerTests
{
    private const string BattlefieldObjectId = "P1-TIDE-CALLER-BATTLEFIELD";
    private const string TideCallerObjectId = "P1-FACEDOWN-BASE-OGN-199-TIDE-CALLER";
    private const string FriendlyTargetObjectId = "P1-TIDE-CALLER-SWAP-TARGET";
    private const string FriendlyEquipmentObjectId = "P1-TIDE-CALLER-FRIENDLY-EQUIPMENT";
    private const string EnemyUnitObjectId = "P2-TIDE-CALLER-ENEMY-UNIT";
    private const string PendingSpellObjectId = "P2-TIDE-CALLER-PENDING-SPELL";

    [Fact]
    public void PromptMetadataExposesFriendlyUnitTargetsForStandbyReactionSwap()
    {
        var prompt = ResolutionResult.BuildPrompts(BuildClosedPriorityState())["P1"];

        Assert.Contains(CommandTypes.RevealCard, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.True(candidate.Enabled);
        Assert.Equal([TideCallerObjectId], (candidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal([FriendlyTargetObjectId], (candidate.Targets ?? []).Select(target => target.Id).ToArray());

        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        Assert.Equal(TideCallerObjectId, Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("OGN·199/298", Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal("STANDBY_REACTION", Assert.IsType<string>(sourceRequirement["mode"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.Equal(CardTargetScopes.FriendlyUnit, Assert.IsType<string>(sourceRequirement["targetScope"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal([FriendlyTargetObjectId], targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public async Task BaseStandbyReactionTideCallerSwapsWithFriendlyUnitOnResolution()
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildClosedPriorityState(),
            new PlayerIntent("intent-tide-caller-standby-reaction", "P1", CommandTypes.RevealCard),
            new RevealCardCommand(
                TideCallerObjectId,
                "OGN·199/298",
                [FriendlyTargetObjectId],
                Mode: "STANDBY_REACTION",
                OptionalCosts: ["STANDBY_REVEAL_0"],
                Destination: "STACK"),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.DoesNotContain(TideCallerObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyEquipmentObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["STACK-0-P2-PENDING-SPELL", "STACK-32-P1-FACEDOWN-BASE-OGN-199-TIDE-CALLER"], result.State.StackItems.Select(item => item.StackItemId));
        var standbyStackItem = result.State.StackItems[1];
        Assert.Equal("TIDE_CALLER_STANDBY_SWAP_PLAY_UNIT", standbyStackItem.EffectKind);
        Assert.Equal([FriendlyTargetObjectId], standbyStackItem.TargetObjectIds);
        Assert.Equal(string.Empty, standbyStackItem.Destination);
        Assert.Equal(TimingStates.NeutralClosed, standbyStackItem.TimingContext);
        Assert.Equal("STACK", result.State.ObjectLocations[TideCallerObjectId].Zone);
        Assert.Equal(["CARD_REVEALED", "CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind));

        var p1Pass = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-tide-caller-standby-reaction-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-tide-caller-standby-reaction-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["STACK-0-P2-PENDING-SPELL"], p2Pass.State.StackItems.Select(item => item.StackItemId));
        Assert.Contains(FriendlyTargetObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyEquipmentObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(TideCallerObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(TideCallerObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(FriendlyTargetObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", BattlefieldObjectId), p2Pass.State.ObjectLocations[TideCallerObjectId]);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), p2Pass.State.ObjectLocations[FriendlyTargetObjectId]);
        Assert.False(p2Pass.State.CardObjects[TideCallerObjectId].IsFaceDown);
        Assert.Equal(2, p2Pass.State.CardObjects[TideCallerObjectId].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], p2Pass.State.CardObjects[TideCallerObjectId].Tags);
        Assert.Equal(["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_PLAYED_TO_BASE", "UNIT_LOCATIONS_SWAPPED"], p2Pass.Events.Select(gameEvent => gameEvent.Kind));

        var swapEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal));
        Assert.Equal(TideCallerObjectId, swapEvent.Payload["sourceObjectId"]);
        Assert.Equal(TideCallerObjectId, swapEvent.Payload["firstTargetObjectId"]);
        Assert.Equal(FriendlyTargetObjectId, swapEvent.Payload["secondTargetObjectId"]);
        Assert.Equal("BATTLEFIELD", swapEvent.Payload["firstDestinationZone"]);
        Assert.Equal("BASE", swapEvent.Payload["secondDestinationZone"]);
    }

    private static MatchState BuildClosedPriorityState()
    {
        return new MatchState(
            "standby-reaction-tide-caller-room",
            31,
            5,
            "P1",
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
            priorityPlayerId: "P1",
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [TideCallerObjectId, FriendlyEquipmentObjectId],
                    Battlefields = [BattlefieldObjectId, FriendlyTargetObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [EnemyUnitObjectId]
                }
            },
            cardObjects: BuildCardObjects(),
            stackItems:
            [
                new StackItemState(
                    "STACK-0-P2-PENDING-SPELL",
                    "P2",
                    PendingSpellObjectId,
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [TideCallerObjectId] = new("P1", "BASE"),
                [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [FriendlyTargetObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [FriendlyEquipmentObjectId] = new("P1", "BASE"),
                [EnemyUnitObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [PendingSpellObjectId] = new("P2", "STACK")
            });
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [TideCallerObjectId] = new(
                TideCallerObjectId,
                isFaceDown: true,
                cardNo: "OGN·199/298",
                power: 2,
                manaCost: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: "P1",
                controllerId: "P1"),
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "OGN·278/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [FriendlyTargetObjectId] = Unit(FriendlyTargetObjectId, "P1", "SFD·125/221", 3),
            [FriendlyEquipmentObjectId] = new(
                FriendlyEquipmentObjectId,
                cardNo: "UNL-030/219",
                tags: [CardObjectTags.EquipmentCard],
                ownerId: "P1",
                controllerId: "P1"),
            [EnemyUnitObjectId] = Unit(EnemyUnitObjectId, "P2", "UNL-057/219", 7),
            [PendingSpellObjectId] = new(
                PendingSpellObjectId,
                cardNo: "OGN·007/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: "P2",
                controllerId: "P2")
        };
    }

    private static CardObjectState Unit(string objectId, string playerId, string cardNo, int power)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }
}
