using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OrnnFriendlyEquipmentStaticPowerTests
{
    private const string OrnnObjectId = "P1-UNIT-ORNN-STATIC";
    private const string OrnnCardNo = "SFD·085/221";
    private const string OrnnAltCardNo = "SFD·085a/221";
    private const string FriendlyBaseEquipmentObjectId = "P1-EQUIPMENT-BASE";
    private const string SecondFriendlyBaseEquipmentObjectId = "P1-EQUIPMENT-BASE-2";
    private const string FriendlyPlayedEquipmentObjectId = "P1-EQUIPMENT-PLAYED";
    private const string HandEquipmentObjectId = "P1-EQUIPMENT-HAND";
    private const string FaceDownEquipmentObjectId = "P1-EQUIPMENT-FACE-DOWN";
    private const string DirtyControllerEquipmentObjectId = "P1-EQUIPMENT-DIRTY-CONTROLLER";
    private const string EnemyEquipmentObjectId = "P2-EQUIPMENT-ENEMY";
    private const string FriendlyUnitObjectId = "P1-UNIT-FRIENDLY";
    private const string FirstRuneObjectId = "P1-RUNE-1";
    private const string SecondRuneObjectId = "P1-RUNE-2";

    [Theory]
    [InlineData(OrnnCardNo)]
    [InlineData(OrnnAltCardNo)]
    public async Task OrnnCountsOnlyFriendlyPublicFieldEquipmentWhenPlayed(string cardNo)
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(cardNo, includeFriendlyFieldEquipment: true);

        var played = await PlayOrnnAsync(engine, state, cardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Contains(OrnnObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(SecondFriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyEquipmentObjectId, resolved.State.PlayerZones["P2"].Base);

        var ornn = resolved.State.CardObjects[OrnnObjectId];
        Assert.Equal(6, ornn.Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾2", CardEquipmentKeywordNames.Tempered], ornn.Tags);
        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(OrnnObjectId, staticAura.TargetObjectId);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(6, staticAura.EffectivePower);
        Assert.Equal(
            [FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId],
            staticAura.ParticipantObjectIds);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE", staticAura.Lifecycle);

        var unitPlayed = Assert.Single(resolved.Events, IsOrnnUnitPlayedEvent);
        Assert.Equal(6, Assert.IsType<int>(unitPlayed.Payload["power"]));
        Assert.Equal(2, Assert.IsType<int>(unitPlayed.Payload["friendlyEquipmentPowerBonus"]));
    }

    [Fact]
    public async Task OrnnKeepsBasePowerWhenNoFriendlyPublicFieldEquipmentExists()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(OrnnCardNo, includeFriendlyFieldEquipment: false);

        var played = await PlayOrnnAsync(engine, state, OrnnCardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(4, resolved.State.CardObjects[OrnnObjectId].Power);
        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Empty(staticAura.ParticipantObjectIds ?? []);
        Assert.Equal(0, staticAura.PowerDelta);
        var unitPlayed = Assert.Single(resolved.Events, IsOrnnUnitPlayedEvent);
        Assert.Equal(4, Assert.IsType<int>(unitPlayed.Payload["power"]));
        Assert.False(unitPlayed.Payload.ContainsKey("friendlyEquipmentPowerBonus"));
    }

    [Fact]
    public async Task OrnnRecomputesUpWhenFriendlyPublicEquipmentResolvesAfterOrnnIsInField()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 4,
            p1Hand: [FriendlyPlayedEquipmentObjectId],
            p1Base: [OrnnObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 4),
                [FriendlyPlayedEquipmentObjectId] = Equipment(
                    FriendlyPlayedEquipmentObjectId,
                    "P1",
                    "P1",
                    cardNo: "SFD·046/221")
            });

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-play-equipment", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(FriendlyPlayedEquipmentObjectId, "SFD·046/221", []),
            CancellationToken.None);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(FriendlyPlayedEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal(5, resolved.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(resolved.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotStaticAura(
            resolved.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [FriendlyPlayedEquipmentObjectId],
            powerDelta: 1,
            basePower: 4,
            effectivePower: 5);
    }

    [Fact]
    public async Task OrnnRecomputesDownFromStableBaseAndDoesNotDriftAcrossRepeatedAcceptedCommands()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 6,
            p1Base: [OrnnObjectId, FriendlyBaseEquipmentObjectId, SecondFriendlyBaseEquipmentObjectId, FirstRuneObjectId, SecondRuneObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 6),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId),
                [SecondRuneObjectId] = Rune(SecondRuneObjectId)
            });
        state = state with
        {
            PlayerZones = state.PlayerZones.ToDictionary(
                entry => entry.Key,
                entry => string.Equals(entry.Key, "P1", StringComparison.Ordinal)
                    ? entry.Value with
                    {
                        Base = [OrnnObjectId, FriendlyBaseEquipmentObjectId, FirstRuneObjectId, SecondRuneObjectId],
                        Graveyard = [SecondFriendlyBaseEquipmentObjectId]
                    }
                    : entry.Value,
                StringComparer.Ordinal),
            ObjectLocations = state.ObjectLocations
                .Where(entry => !string.Equals(entry.Key, SecondFriendlyBaseEquipmentObjectId, StringComparison.Ordinal))
                .Append(new KeyValuePair<string, ObjectLocationState>(
                    SecondFriendlyBaseEquipmentObjectId,
                    new ObjectLocationState("P1", "GRAVEYARD")))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
        };

        var firstTap = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-tap-rune-1", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);
        var secondTap = await engine.ResolveAsync(
            firstTap.State,
            new PlayerIntent("intent-ornn-dynamic-tap-rune-2", "P1", CommandTypes.TapRune),
            new TapRuneCommand(SecondRuneObjectId),
            CancellationToken.None);

        Assert.True(firstTap.Accepted, firstTap.ErrorMessage);
        Assert.True(secondTap.Accepted, secondTap.ErrorMessage);
        Assert.Equal(5, firstTap.State.CardObjects[OrnnObjectId].Power);
        Assert.Equal(5, secondTap.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(secondTap.Snapshots["P1"], OrnnObjectId, basePower: 5, effectivePower: 5);
        AssertSnapshotStaticAura(
            secondTap.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [FriendlyBaseEquipmentObjectId],
            powerDelta: 1,
            basePower: 4,
            effectivePower: 5);
    }

    [Fact]
    public async Task OrnnRecomputeExcludesEnemyHandFaceDownDirtyControllerAndNonEquipmentObjects()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnFieldState(
            ornnPower: 7,
            p1Hand: [HandEquipmentObjectId],
            p1Base:
            [
                OrnnObjectId,
                FriendlyBaseEquipmentObjectId,
                FaceDownEquipmentObjectId,
                DirtyControllerEquipmentObjectId,
                FriendlyUnitObjectId,
                FirstRuneObjectId
            ],
            p2Base: [EnemyEquipmentObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 7),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FriendlyBaseEquipmentObjectId] = Unit(FriendlyBaseEquipmentObjectId, "SFD·022/221", "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1", power: 3),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2"),
                [FirstRuneObjectId] = Rune(FirstRuneObjectId)
            });

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-dynamic-exclusions", "P1", CommandTypes.TapRune),
            new TapRuneCommand(FirstRuneObjectId),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.Equal(4, tapped.State.CardObjects[OrnnObjectId].Power);
        AssertSnapshotPower(tapped.Snapshots["P1"], OrnnObjectId, basePower: 4, effectivePower: 4);
        AssertSnapshotStaticAura(
            tapped.Snapshots["P1"],
            OrnnObjectId,
            OrnnObjectId,
            [],
            powerDelta: 0,
            basePower: 4,
            effectivePower: 4);
    }

    [Fact]
    public void OrnnStaticAuraMetadataDisappearsWhenSourceLeavesField()
    {
        var state = BuildOrnnFieldState(
            ornnPower: 5,
            p1Base: [FriendlyBaseEquipmentObjectId],
            p1Graveyard: [OrnnObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: 5),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1")
            });

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayOrnnAsync(
        CoreRuleEngine engine,
        MatchState state,
        string cardNo)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(OrnnObjectId, cardNo, []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ornn-static-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static MatchState BuildOrnnState(
        string cardNo,
        bool includeFriendlyFieldEquipment)
    {
        var p1Base = new List<string>
        {
            FriendlyUnitObjectId,
            FaceDownEquipmentObjectId,
            DirtyControllerEquipmentObjectId
        };
        var p1Battlefields = new List<string>();
        if (includeFriendlyFieldEquipment)
        {
            p1Base.Insert(0, FriendlyBaseEquipmentObjectId);
            p1Base.Insert(1, SecondFriendlyBaseEquipmentObjectId);
        }

        var p1Hand = new[] { OrnnObjectId, HandEquipmentObjectId };
        var p2Base = new[] { EnemyEquipmentObjectId };
        var objectLocations = p1Hand
            .Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "HAND")))
            .Concat(p1Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BASE"))))
            .Concat(p1Battlefields.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BATTLEFIELD"))))
            .Concat(p2Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P2", "BASE"))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new MatchState(
            "ornn-friendly-equipment-static-power",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            }) with
        {
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = ["P1", "P2"],
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = p1Base,
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, cardNo, "P1", "P1"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1"),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2")
            },
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildOrnnFieldState(
        int ornnPower,
        IReadOnlyList<string>? p1Hand = null,
        IReadOnlyList<string>? p1Base = null,
        IReadOnlyList<string>? p1Graveyard = null,
        IReadOnlyList<string>? p2Base = null,
        Dictionary<string, CardObjectState>? cardObjects = null)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Hand = p1Hand ?? [],
                Base = p1Base ?? [OrnnObjectId],
                Graveyard = p1Graveyard ?? []
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = p2Base ?? []
            }
        };
        var objectLocations = playerZones
            .SelectMany(player => new[]
                {
                    ("HAND", player.Value.Hand),
                    ("BASE", player.Value.Base),
                    ("GRAVEYARD", player.Value.Graveyard)
                }
                .SelectMany(zone => zone.Item2.Select(objectId =>
                    new KeyValuePair<string, ObjectLocationState>(
                        objectId,
                        new ObjectLocationState(player.Key, zone.Item1)))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new MatchState(
            "ornn-friendly-equipment-dynamic-static-power",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            }) with
        {
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = ["P1", "P2"],
            TurnPlayerId = "P1",
            ActivePlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, OrnnCardNo, "P1", "P1", power: ornnPower)
            },
            ObjectLocations = objectLocations
        };
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string ownerId,
        string controllerId,
        int power = 0)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Equipment(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false,
        string cardNo = "SFD·022/221")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Rune(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "UNL-R01",
            tags: [CardObjectTags.RuneCard, "COLOR:red"],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static bool IsOrnnUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal);
    }

    private static void AssertSnapshotPower(
        SnapshotDto snapshot,
        string objectId,
        int basePower,
        int effectivePower)
    {
        var p1View = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
        var objects = Assert.IsType<Dictionary<string, object?>>(p1View["objects"]);
        var objectView = Assert.IsType<Dictionary<string, object?>>(objects[objectId]);

        Assert.Equal(basePower, Assert.IsType<int>(objectView["basePower"]));
        Assert.Equal(effectivePower, Assert.IsType<int>(objectView["effectivePower"]));
        Assert.Equal(effectivePower, Assert.IsType<int>(objectView["power"]));
    }

    private static void AssertSnapshotStaticAura(
        SnapshotDto snapshot,
        string sourceObjectId,
        string targetObjectId,
        IReadOnlyList<string> participantObjectIds,
        int powerDelta,
        int basePower,
        int effectivePower)
    {
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var effect = Assert.Single(
            continuousEffects,
            effect => string.Equals(Assert.IsType<string>(effect["layer"]), ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, sourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, targetObjectId, StringComparison.Ordinal));

        Assert.Equal(powerDelta, Assert.IsType<int>(effect["powerDelta"]));
        Assert.Equal(basePower, Assert.IsType<int>(effect["basePower"]));
        Assert.Equal(effectivePower, Assert.IsType<int>(effect["effectivePower"]));
        Assert.Equal(
            "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER",
            Assert.IsType<string>(effect["effectKind"]));
        Assert.Equal(
            "SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT",
            Assert.IsType<string>(effect["condition"]));
        Assert.Equal(
            "RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE",
            Assert.IsType<string>(effect["lifecycle"]));
        if (participantObjectIds.Count == 0)
        {
            Assert.False(effect.ContainsKey("participantObjectIds"));
            return;
        }

        Assert.Equal(
            participantObjectIds,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["participantObjectIds"]));
    }
}
