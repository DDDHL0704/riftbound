using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LayerEngineTimestampDependencyTests
{
    private const string OrnnObjectId = "P1-UNIT-ORNN-LAYER";
    private const string OrnnCardNo = "SFD·085/221";
    private const string PublicEquipmentObjectId = "P1-EQUIPMENT-PUBLIC";
    private const string SecondPublicEquipmentObjectId = "P1-EQUIPMENT-PUBLIC-2";
    private const string HiddenEquipmentObjectId = "P1-EQUIPMENT-HIDDEN-FACE-DOWN";
    private const string BattlefieldSourceObjectId = "P1-BATTLEFIELD-POWER-PLUS";
    private const string BattlefieldAttackerObjectId = "P1-BATTLEFIELD-STATIC-ATTACKER";
    private const string BattlefieldDefenderObjectId = "P2-BATTLEFIELD-STATIC-DEFENDER";
    private const string FieldFirstBattlefieldSourceObjectId = "P1-BATTLEFIELD-Z-SOURCE";
    private const string FieldLaterBattlefieldSourceObjectId = "P1-BATTLEFIELD-A-SOURCE";
    private const string BattlefieldSharedUnitObjectId = "P1-BATTLEFIELD-SOURCE-ORDER-UNIT";

    [Fact]
    public void LayerEngineContinuousEffectSequenceIsStableForMixedPowerAndStaticAuraState()
    {
        var state = BuildMixedLayerState();

        var firstViews = ContinuousEffectViews(ResolutionResult.BuildSnapshots(state)["P1"]);
        var secondViews = ContinuousEffectViews(ResolutionResult.BuildSnapshots(state)["P1"]);

        Assert.Equal(
            Enumerable.Range(1, firstViews.Count).ToArray(),
            firstViews.Select(effect => Assert.IsType<int>(effect["sequence"])).ToArray());
        Assert.Equal(
            firstViews.Select(EffectSignature).ToArray(),
            secondViews.Select(EffectSignature).ToArray());

        var mixedViews = firstViews
            .Where(effect => string.Equals(effect["targetObjectId"] as string, OrnnObjectId, StringComparison.Ordinal)
                && (string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                    || string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)))
            .OrderBy(effect => Assert.IsType<int>(effect["sequence"]))
            .ToArray();
        Assert.Equal(
            ["DIRECT_POWER_PLUS_TWO", "MINIMUM_POWER_FLOOR_MIN_SIX", "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER"],
            mixedViews.Select(effect => Assert.IsType<string>(effect["effectKind"])).ToArray());
        Assert.Equal([1, 2, 3], mixedViews.Select(effect => Assert.IsType<int>(effect["sequence"])).ToArray());

        var powerEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, OrnnObjectId, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal([1, 2], powerEffects.Select(effect => effect.AppliedOrder.GetValueOrDefault()).ToArray());
    }

    [Fact]
    public void LayerEngineStaticAuraDependenciesUseOnlyPublicRelationshipObjectIds()
    {
        var state = BuildMixedLayerState();

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal([OrnnObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([PublicEquipmentObjectId], aura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantDependencyObjectIds ?? []);

        var snapshot = ResolutionResult.BuildSnapshots(state)["P2"];
        var auraView = Assert.Single(
            ContinuousEffectViews(snapshot),
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal([OrnnObjectId], StringList(auraView, "sourceDependencyObjectIds"));
        Assert.Equal([OrnnObjectId], StringList(auraView, "targetDependencyObjectIds"));
        Assert.Equal([PublicEquipmentObjectId], StringList(auraView, "participantDependencyObjectIds"));
        AssertDoesNotExposeDependencyObjectId(snapshot, HiddenEquipmentObjectId);
        Assert.DoesNotContain(
            auraView.Keys,
            key => key.Contains("task", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LayerEngineStaticAuraDependencyMetadataDisappearsWhenSourceLeavesPublicField()
    {
        var state = BuildOrnnState(
            p1Base: [PublicEquipmentObjectId, HiddenEquipmentObjectId],
            p1Graveyard: [OrnnObjectId],
            cardObjects: BuildOrnnCardObjects(includeSecondPublicEquipment: false));

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));

        var snapshot = ResolutionResult.BuildSnapshots(state)["P1"];
        Assert.DoesNotContain(
            ContinuousEffectViews(snapshot),
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public void LayerEngineStaticAuraParticipantDependenciesRecomputeWhenParticipantLeavesPublicField()
    {
        var before = BuildOrnnState(
            p1Base: [OrnnObjectId, PublicEquipmentObjectId, SecondPublicEquipmentObjectId, HiddenEquipmentObjectId],
            cardObjects: BuildOrnnCardObjects(includeSecondPublicEquipment: true));
        var beforeAura = Assert.Single(
            before.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal(
            [PublicEquipmentObjectId, SecondPublicEquipmentObjectId],
            beforeAura.ParticipantDependencyObjectIds);

        var after = BuildOrnnState(
            p1Base: [OrnnObjectId, PublicEquipmentObjectId, HiddenEquipmentObjectId],
            p1Graveyard: [SecondPublicEquipmentObjectId],
            cardObjects: BuildOrnnCardObjects(includeSecondPublicEquipment: true));
        var afterAura = Assert.Single(
            after.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));

        Assert.Equal([PublicEquipmentObjectId], afterAura.ParticipantDependencyObjectIds);
        var snapshot = ResolutionResult.BuildSnapshots(after)["P1"];
        var auraView = Assert.Single(
            ContinuousEffectViews(snapshot),
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal([PublicEquipmentObjectId], StringList(auraView, "participantDependencyObjectIds"));
        AssertDoesNotExposeDependencyObjectId(snapshot, SecondPublicEquipmentObjectId);
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraTargetDependenciesDisappearWhenParticipantLeavesBattlefield()
    {
        var before = BuildBattlefieldStaticAuraState(includeDefender: true);
        var beforeEffects = BattlefieldStaticAuraEffects(before);
        Assert.Equal(2, beforeEffects.Length);
        Assert.All(
            beforeEffects,
            effect =>
            {
                var targetObjectId = Assert.IsType<string>(effect.TargetObjectId);
                Assert.Equal([BattlefieldSourceObjectId], effect.SourceDependencyObjectIds);
                Assert.Equal([targetObjectId], effect.TargetDependencyObjectIds);
                Assert.Equal(
                    [BattlefieldAttackerObjectId, BattlefieldDefenderObjectId],
                    effect.ParticipantDependencyObjectIds);
            });

        var after = BuildBattlefieldStaticAuraState(includeDefender: false);
        var afterEffect = Assert.Single(BattlefieldStaticAuraEffects(after));
        Assert.Equal(BattlefieldAttackerObjectId, afterEffect.TargetObjectId);
        Assert.Equal([BattlefieldSourceObjectId], afterEffect.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], afterEffect.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], afterEffect.ParticipantDependencyObjectIds);

        var snapshot = ResolutionResult.BuildSnapshots(after)["P1"];
        var effectView = Assert.Single(
            ContinuousEffectViews(snapshot),
            effect => string.Equals(effect["effectKind"] as string, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal));
        Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "targetDependencyObjectIds"));
        Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "participantDependencyObjectIds"));
        AssertDoesNotExposeDependencyObjectId(snapshot, BattlefieldDefenderObjectId);
    }

    [Fact]
    public void LayerEngineStaticAuraSourceOrderUsesPublicFieldOrderBeforeEffectId()
    {
        Assert.True(
            string.CompareOrdinal(FieldLaterBattlefieldSourceObjectId, FieldFirstBattlefieldSourceObjectId) < 0,
            "The fixture keeps lexical object id order opposite to public field order.");

        var state = BuildBattlefieldSourceOrderState();

        var snapshot = ResolutionResult.BuildSnapshots(state)["P1"];
        var sourceOrderViews = ContinuousEffectViews(snapshot)
            .Where(effect => string.Equals(effect["effectKind"] as string, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => Assert.IsType<int>(effect["sequence"]))
            .ToArray();

        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            sourceOrderViews.Select(effect => Assert.IsType<string>(effect["sourceObjectId"])).ToArray());
        Assert.Equal([1, 3], sourceOrderViews.Select(effect => Assert.IsType<int>(effect["sourceOrder"])).ToArray());
        Assert.Equal(
            sourceOrderViews.Select(effect => Assert.IsType<int>(effect["sourceOrder"])).ToArray(),
            state.ContinuousEffects
                .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                    && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
                .OrderBy(effect => effect.Sequence)
                .Select(effect => effect.SourceOrder.GetValueOrDefault())
                .ToArray());
    }

    private static MatchState BuildMixedLayerState()
    {
        var cardObjects = BuildOrnnCardObjects(includeSecondPublicEquipment: false);
        cardObjects[OrnnObjectId] = new CardObjectState(
            OrnnObjectId,
            cardNo: OrnnCardNo,
            power: 6,
            untilEndOfTurnPowerModifier: 1,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1",
            untilEndOfTurnPowerModifiers:
            [
                new PowerModifierLedgerEntry(
                    "POWER:P1-UNIT-ORNN-LAYER:DIRECT_PLUS_TWO",
                    "DIRECT_POWER_PLUS_TWO",
                    "UNTIL_END_OF_TURN",
                    OrnnObjectId,
                    "P1-SPELL-DIRECT",
                    "TEST-DIRECT",
                    2,
                    5,
                    7,
                    "LayerEngineTimestampDependencyTests.Direct",
                    2,
                    0,
                    7,
                    1),
                new PowerModifierLedgerEntry(
                    "POWER:P1-UNIT-ORNN-LAYER:MINIMUM_FLOOR",
                    "MINIMUM_POWER_FLOOR_MIN_SIX",
                    "UNTIL_END_OF_TURN",
                    OrnnObjectId,
                    "P1-SPELL-FLOOR",
                    "TEST-FLOOR",
                    -1,
                    7,
                    6,
                    "LayerEngineTimestampDependencyTests.MinimumFloor",
                    -3,
                    6,
                    6,
                    2)
            ]);

        return BuildOrnnState(
            p1Base: [OrnnObjectId, PublicEquipmentObjectId, HiddenEquipmentObjectId],
            cardObjects: cardObjects);
    }

    private static MatchState BuildOrnnState(
        IReadOnlyList<string> p1Base,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string>? p1Graveyard = null)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Base = p1Base,
                Graveyard = p1Graveyard ?? []
            },
            ["P2"] = PlayerZones.Empty
        };

        return BaseState("layer-engine-ornn-dependencies") with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = ObjectLocationsForZones(playerZones)
        };
    }

    private static Dictionary<string, CardObjectState> BuildOrnnCardObjects(bool includeSecondPublicEquipment)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [OrnnObjectId] = new(
                OrnnObjectId,
                cardNo: OrnnCardNo,
                power: 5,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            [PublicEquipmentObjectId] = Equipment(PublicEquipmentObjectId, isFaceDown: false),
            [HiddenEquipmentObjectId] = Equipment(HiddenEquipmentObjectId, isFaceDown: true)
        };
        if (includeSecondPublicEquipment)
        {
            cardObjects[SecondPublicEquipmentObjectId] = Equipment(SecondPublicEquipmentObjectId, isFaceDown: false);
        }

        return cardObjects;
    }

    private static MatchState BuildBattlefieldStaticAuraState(bool includeDefender)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Battlefields = [BattlefieldSourceObjectId, BattlefieldAttackerObjectId]
            },
            ["P2"] = PlayerZones.Empty with
            {
                Battlefields = includeDefender ? [BattlefieldDefenderObjectId] : [],
                Graveyard = includeDefender ? [] : [BattlefieldDefenderObjectId]
            }
        };

        return BaseState("layer-engine-battlefield-dependencies") with
        {
            PlayerZones = playerZones,
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BattlefieldSourceObjectId] = new(
                    BattlefieldSourceObjectId,
                    cardNo: "OGN·294/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                [BattlefieldAttackerObjectId] = Unit(BattlefieldAttackerObjectId, "P1", power: 2),
                [BattlefieldDefenderObjectId] = Unit(BattlefieldDefenderObjectId, "P2", power: 3)
            },
            ObjectLocations = BattlefieldObjectLocations(includeDefender)
        };
    }

    private static MatchState BuildBattlefieldSourceOrderState()
    {
        return BaseState("layer-engine-battlefield-source-order") with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        FieldFirstBattlefieldSourceObjectId,
                        BattlefieldSharedUnitObjectId,
                        FieldLaterBattlefieldSourceObjectId
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = BattlefieldPowerSource(FieldFirstBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = BattlefieldPowerSource(FieldLaterBattlefieldSourceObjectId),
                [BattlefieldSharedUnitObjectId] = Unit(BattlefieldSharedUnitObjectId, "P1", power: 2)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        };
    }

    private static MatchState BaseState(string roomId)
    {
        return new MatchState(
            roomId,
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
            TimingState = TimingStates.NeutralOpen
        };
    }

    private static CardObjectState Unit(string objectId, string playerId, int power)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState BattlefieldPowerSource(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·294/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Equipment(string objectId, bool isFaceDown)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·022/221",
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ObjectLocationsForZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones)
    {
        return playerZones
            .SelectMany(player => new[]
            {
                ("BASE", player.Value.Base),
                ("GRAVEYARD", player.Value.Graveyard)
            }.SelectMany(zone => zone.Item2.Select(objectId =>
                new KeyValuePair<string, ObjectLocationState>(
                    objectId,
                    new ObjectLocationState(player.Key, zone.Item1)))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> BattlefieldObjectLocations(bool includeDefender)
    {
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", BattlefieldSourceObjectId),
            [BattlefieldAttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldSourceObjectId),
            [BattlefieldDefenderObjectId] = includeDefender
                ? new ObjectLocationState("P2", "BATTLEFIELD", BattlefieldSourceObjectId)
                : new ObjectLocationState("P2", "GRAVEYARD")
        };

        return locations;
    }

    private static ContinuousEffectState[] BattlefieldStaticAuraEffects(MatchState state)
    {
        return state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, BattlefieldSourceObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<Dictionary<string, object?>> ContinuousEffectViews(SnapshotDto snapshot)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
    }

    private static IReadOnlyList<string> StringList(Dictionary<string, object?> view, string key)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(view[key]);
    }

    private static string EffectSignature(Dictionary<string, object?> view)
    {
        return string.Join(
            "|",
            Assert.IsType<int>(view["sequence"]),
            view["effectId"] as string ?? string.Empty,
            view["scope"] as string ?? string.Empty,
            view["layer"] as string ?? string.Empty,
            view["targetObjectId"] as string ?? string.Empty,
            view["sourceObjectId"] as string ?? string.Empty,
            view.TryGetValue("appliedOrder", out var appliedOrder) ? appliedOrder?.ToString() ?? string.Empty : string.Empty);
    }

    private static void AssertDoesNotExposeDependencyObjectId(
        SnapshotDto snapshot,
        string objectId)
    {
        var dependencyIds = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
                snapshot.Timing["continuousEffects"])
            .SelectMany(DependencyObjectIds)
            .ToArray();

        Assert.DoesNotContain(objectId, dependencyIds);
        Assert.DoesNotContain(
            dependencyIds,
            dependencyId => dependencyId.Contains("TASK", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> DependencyObjectIds(Dictionary<string, object?> view)
    {
        foreach (var key in new[]
        {
            "sourceDependencyObjectIds",
            "targetDependencyObjectIds",
            "participantDependencyObjectIds"
        })
        {
            if (view.TryGetValue(key, out var value)
                && value is IReadOnlyList<string> objectIds)
            {
                foreach (var objectId in objectIds)
                {
                    yield return objectId;
                }
            }
        }
    }
}
