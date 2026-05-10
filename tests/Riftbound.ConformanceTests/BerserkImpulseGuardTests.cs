using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BerserkImpulseGuardTests
{
    [Fact]
    public async Task BerserkImpulsePlaysOpponentTopMainDeckUnitToControllerBaseAndResetsState()
    {
        var engine = new CoreRuleEngine();
        var state = BuildBerserkImpulseState("P2-TOP-UNIT");

        var played = await PlayBerserkImpulseAsync(engine, state, "P2-TOP-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-berserk-impulse-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-berserk-impulse-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-BASE-UNIT", "P2-TOP-UNIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SECOND-UNIT"], p2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-TOP-UNIT"].Damage);
        Assert.Equal(6, p2Pass.State.CardObjects["P2-TOP-UNIT"].Power);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-TOP-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Empty(p2Pass.State.CardObjects["P2-TOP-UNIT"].UntilEndOfTurnEffects);
        Assert.False(p2Pass.State.CardObjects["P2-TOP-UNIT"].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-BERSERK-IMPULSE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-TOP-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerPlayerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedByPlayerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceZone"] as string, "MAIN_DECK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-TOP-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-SECOND-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-TOP-SPELL", "P2-TOP-SPELL")]
    [InlineData("P2-TOP-EQUIPMENT", "P2-TOP-EQUIPMENT")]
    [InlineData("P2-TOP-RUNE", "P2-TOP-RUNE")]
    [InlineData("P2-TOP-FACE-DOWN-UNIT", "P2-TOP-FACE-DOWN-UNIT")]
    [InlineData("P2-HAND-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-BASE-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-BATTLEFIELD-UNIT", "P2-TOP-UNIT")]
    public async Task BerserkImpulseRejectsInvalidTargetsWithoutMutation(
        string targetObjectId,
        string opponentTopObjectId)
    {
        var state = BuildBerserkImpulseState(opponentTopObjectId);
        var initialP1Hand = state.PlayerZones["P1"].Hand;
        var initialP1MainDeck = state.PlayerZones["P1"].MainDeck;
        var initialP1Base = state.PlayerZones["P1"].Base;
        var initialP2Hand = state.PlayerZones["P2"].Hand;
        var initialP2MainDeck = state.PlayerZones["P2"].MainDeck;
        var initialP2Base = state.PlayerZones["P2"].Base;
        var initialP2Battlefields = state.PlayerZones["P2"].Battlefields;

        var result = await PlayBerserkImpulseAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(initialP1Hand, result.State.PlayerZones["P1"].Hand);
        Assert.Equal(initialP1MainDeck, result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(initialP1Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(initialP2Hand, result.State.PlayerZones["P2"].Hand);
        Assert.Equal(initialP2MainDeck, result.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(initialP2Base, result.State.PlayerZones["P2"].Base);
        Assert.Equal(initialP2Battlefields, result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-OTHER-TOP-UNIT", "P2-DIRTY-TARGET-UNIT")]
    [InlineData("P2-DIRTY-TARGET-UNIT", "P2-DIRTY-TARGET-UNIT-P1-CONTROLLED")]
    [InlineData("P2-DIRTY-TARGET-SPELL", "P2-DIRTY-TARGET-SPELL")]
    [InlineData("P2-DIRTY-FACE-DOWN-UNIT", "P2-DIRTY-FACE-DOWN-UNIT")]
    public async Task BerserkImpulseDirtyResolutionDoesNotMoveInvalidTopDeckTarget(
        string opponentTopObjectId,
        string targetObjectId)
    {
        var engine = new CoreRuleEngine();
        var state = BuildDirtyResolutionState(opponentTopObjectId, targetObjectId);
        var initialP2MainDeck = state.PlayerZones["P2"].MainDeck;

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-berserk-impulse-dirty-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-berserk-impulse-dirty-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(initialP2MainDeck, p2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayBerserkImpulseAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-berserk-impulse-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-BERSERK-IMPULSE",
                "OGN·025/298",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildBerserkImpulseState(string opponentTopObjectId)
    {
        return new MatchState(
            roomId: "berserk-impulse-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-UNIT"],
                    Hand = ["P1-SPELL-BERSERK-IMPULSE"],
                    Base = ["P1-BASE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNIT"],
                    MainDeck = [opponentTopObjectId, "P2-SECOND-UNIT"],
                    Base = ["P2-BASE-UNIT"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: BaseCardObjects());
    }

    private static MatchState BuildDirtyResolutionState(
        string opponentTopObjectId,
        string targetObjectId)
    {
        var cardObjects = BaseCardObjects();
        cardObjects["P2-OTHER-TOP-UNIT"] = Unit("P2-OTHER-TOP-UNIT", power: 2);
        cardObjects["P2-DIRTY-TARGET-UNIT"] = Unit("P2-DIRTY-TARGET-UNIT", power: 3);
        cardObjects["P2-DIRTY-TARGET-UNIT-P1-CONTROLLED"] = new(
            "P2-DIRTY-TARGET-UNIT-P1-CONTROLLED",
            cardNo: "SFD·125/221",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-DIRTY-TARGET-SPELL"] = Spell("P2-DIRTY-TARGET-SPELL");
        cardObjects["P2-DIRTY-FACE-DOWN-UNIT"] = Unit(
            "P2-DIRTY-FACE-DOWN-UNIT",
            power: 3,
            isFaceDown: true);

        return new MatchState(
            roomId: "berserk-impulse-dirty-resolution-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = [opponentTopObjectId, "P2-DECK-KEEP"]
                }
            },
            cardObjects: cardObjects,
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-BERSERK-IMPULSE-DIRTY",
                    "P1",
                    "P1-SPELL-BERSERK-IMPULSE",
                    "BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT",
                    "OGN·025/298",
                    [targetObjectId])
            ]);
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, CardObjectState> BaseCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-BERSERK-IMPULSE"] = new(
                "P1-SPELL-BERSERK-IMPULSE",
                cardNo: "OGN·025/298",
                manaCost: 4,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", power: 2),
            ["P1-TOP-UNIT"] = Unit("P1-TOP-UNIT", power: 2),
            ["P2-TOP-UNIT"] = new(
                "P2-TOP-UNIT",
                damage: 1,
                untilEndOfTurnEffects: ["STUNNED"],
                power: 8,
                untilEndOfTurnPowerModifier: 2,
                isExhausted: true,
                tags: [CardObjectTags.UnitCard],
                manaCost: 6),
            ["P2-SECOND-UNIT"] = Unit("P2-SECOND-UNIT", power: 3),
            ["P2-TOP-SPELL"] = Spell("P2-TOP-SPELL"),
            ["P2-TOP-EQUIPMENT"] = Equipment("P2-TOP-EQUIPMENT"),
            ["P2-TOP-RUNE"] = Rune("P2-TOP-RUNE"),
            ["P2-TOP-FACE-DOWN-UNIT"] = Unit("P2-TOP-FACE-DOWN-UNIT", power: 3, isFaceDown: true),
            ["P2-HAND-UNIT"] = Unit("P2-HAND-UNIT", power: 3),
            ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", power: 3),
            ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", power: 3),
            ["P2-DECK-KEEP"] = Spell("P2-DECK-KEEP")
        };
    }

    private static CardObjectState Unit(string objectId, int power, bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·125/221",
            power: power,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.UnitCard]);
    }

    private static CardObjectState Spell(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·169/298",
            manaCost: 1,
            tags: [CardObjectTags.SpellCard]);
    }

    private static CardObjectState Equipment(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·139/221",
            manaCost: 1,
            tags: [CardObjectTags.EquipmentCard]);
    }

    private static CardObjectState Rune(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "RUNES·001",
            tags: [CardObjectTags.RuneCard]);
    }
}
