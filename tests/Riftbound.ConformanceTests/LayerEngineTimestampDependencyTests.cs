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
    private const string OtherBattlefieldObjectId = "P1-BATTLEFIELD-OTHER";
    private const string OtherBattlefieldUnitObjectId = "P1-BATTLEFIELD-OTHER-UNIT";
    private const string FieldFirstBattlefieldSourceObjectId = "P1-BATTLEFIELD-Z-SOURCE";
    private const string FieldLaterBattlefieldSourceObjectId = "P1-BATTLEFIELD-A-SOURCE";
    private const string BattlefieldSharedUnitObjectId = "P1-BATTLEFIELD-SOURCE-ORDER-UNIT";
    private const string PowerLedgerLegacyTargetObjectId = "P1-UNIT-POWER-LEDGER-LEGACY-REMAINDER";

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
    public void LayerEnginePowerModifierLedgerLegacyRemainderSnapshotsAreDeterministicAcrossPlayersAndBuilds()
    {
        var state = BuildPowerModifierLedgerLegacyRemainderState();
        var authoritativePowerEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, PowerLedgerLegacyTargetObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(3, authoritativePowerEffects.Length);
        Assert.Equal(
            [
                "LEDGER_REMAINDER_FIRST_PLUS_THREE",
                "LEDGER_REMAINDER_SECOND_MINUS_TWO",
                "LEGACY_UNTRACKED_POWER_MODIFIER"
            ],
            authoritativePowerEffects.Select(effect => effect.EffectKind).ToArray());
        Assert.Equal(new int?[] { 1, 2, null }, authoritativePowerEffects.Select(effect => effect.AppliedOrder).ToArray());

        var firstSnapshots = ResolutionResult.BuildSnapshots(state);
        var secondSnapshots = ResolutionResult.BuildSnapshots(state);
        var firstP1Signatures = PowerModifierSnapshotSignatures(firstSnapshots["P1"]);

        Assert.Equal(firstP1Signatures, PowerModifierSnapshotSignatures(firstSnapshots["P2"]));
        Assert.Equal(firstP1Signatures, PowerModifierSnapshotSignatures(secondSnapshots["P1"]));
        Assert.Equal(firstP1Signatures, PowerModifierSnapshotSignatures(secondSnapshots["P2"]));

        var p1Views = PowerModifierSnapshotViews(firstSnapshots["P1"]);
        Assert.Equal(
            [
                "LEDGER_REMAINDER_FIRST_PLUS_THREE",
                "LEDGER_REMAINDER_SECOND_MINUS_TWO",
                "LEGACY_UNTRACKED_POWER_MODIFIER"
            ],
            p1Views.Select(effect => Assert.IsType<string>(effect["effectKind"])).ToArray());

        var trackedViews = p1Views.Take(2).ToArray();
        Assert.Equal([3, -2], trackedViews.Select(effect => Assert.IsType<int>(effect["requestedPowerDelta"])).ToArray());
        Assert.Equal([3, -2], trackedViews.Select(effect => Assert.IsType<int>(effect["appliedPowerDelta"])).ToArray());
        Assert.Equal([0, 1], trackedViews.Select(effect => Assert.IsType<int>(effect["minimumPower"])).ToArray());
        Assert.Equal([7, 5], trackedViews.Select(effect => Assert.IsType<int>(effect["resultingPower"])).ToArray());
        Assert.Equal([1, 2], trackedViews.Select(effect => Assert.IsType<int>(effect["appliedOrder"])).ToArray());

        var legacyRemainderView = p1Views[2];
        Assert.Equal("FOUNDATION_ONLY", Assert.IsType<string>(legacyRemainderView["layerEngineStatus"]));
        Assert.Equal("LEGACY_UNTRACKED_POWER_MODIFIER", Assert.IsType<string>(legacyRemainderView["effectKind"]));
        Assert.Equal(
            "MatchState.ContinuousEffects.LegacyRemainder",
            Assert.IsType<string>(legacyRemainderView["sourcePath"]));
        Assert.NotEmpty(StringList(legacyRemainderView, "deferredLayerEngineResiduals"));
        Assert.Null(legacyRemainderView["sourceObjectId"]);
        Assert.False(legacyRemainderView.ContainsKey("sourceCardNo"));
        Assert.False(legacyRemainderView.ContainsKey("requestedPowerDelta"));
        Assert.False(legacyRemainderView.ContainsKey("appliedPowerDelta"));
        Assert.False(legacyRemainderView.ContainsKey("minimumPower"));
        Assert.False(legacyRemainderView.ContainsKey("resultingPower"));
        Assert.False(legacyRemainderView.ContainsKey("appliedOrder"));

        static Dictionary<string, object?>[] PowerModifierSnapshotViews(SnapshotDto snapshot)
        {
            var views = ContinuousEffectViews(snapshot)
                .Where(effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                    && string.Equals(effect["targetObjectId"] as string, PowerLedgerLegacyTargetObjectId, StringComparison.Ordinal))
                .OrderBy(effect => Assert.IsType<int>(effect["sequence"]))
                .ToArray();

            Assert.Equal(3, views.Length);
            Assert.Equal(
                Enumerable.Range(1, views.Length).ToArray(),
                views.Select(effect => Assert.IsType<int>(effect["sequence"])).ToArray());

            return views;
        }

        static string[] PowerModifierSnapshotSignatures(SnapshotDto snapshot)
        {
            return PowerModifierSnapshotViews(snapshot)
                .Select(effect => string.Join(
                    "|",
                    Assert.IsType<int>(effect["sequence"]).ToString(),
                    Assert.IsType<string>(effect["effectId"]),
                    Assert.IsType<string>(effect["scope"]),
                    Assert.IsType<string>(effect["layer"]),
                    Assert.IsType<string>(effect["duration"]),
                    Assert.IsType<string>(effect["targetObjectId"]),
                    effect["sourceObjectId"] as string ?? string.Empty,
                    Assert.IsType<int>(effect["powerDelta"]).ToString(),
                    Assert.IsType<int>(effect["basePower"]).ToString(),
                    Assert.IsType<int>(effect["effectivePower"]).ToString(),
                    OptionalString(effect, "effectKind"),
                    OptionalString(effect, "sourceCardNo"),
                    OptionalString(effect, "sourcePath"),
                    OptionalString(effect, "layerEngineStatus"),
                    OptionalInt(effect, "requestedPowerDelta"),
                    OptionalInt(effect, "appliedPowerDelta"),
                    OptionalInt(effect, "minimumPower"),
                    OptionalInt(effect, "resultingPower"),
                    OptionalInt(effect, "appliedOrder"),
                    OptionalStringList(effect, "deferredLayerEngineResiduals")))
                .ToArray();
        }

        static string OptionalString(Dictionary<string, object?> view, string key)
        {
            return view.TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;
        }

        static string OptionalInt(Dictionary<string, object?> view, string key)
        {
            return view.TryGetValue(key, out var value) ? Assert.IsType<int>(value).ToString() : string.Empty;
        }

        static string OptionalStringList(Dictionary<string, object?> view, string key)
        {
            return view.TryGetValue(key, out var value)
                ? string.Join(",", Assert.IsAssignableFrom<IReadOnlyList<string>>(value))
                : string.Empty;
        }
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
    public void LayerEngineStaticAuraDependencyMetadataDisappearsWhenSourceLeavesPublicFieldAcrossPlayerViews()
    {
        var state = BuildOrnnState(
            p1Base: [PublicEquipmentObjectId, HiddenEquipmentObjectId],
            p1Graveyard: [OrnnObjectId],
            cardObjects: BuildOrnnCardObjects(includeSecondPublicEquipment: false));

        var snapshots = ResolutionResult.BuildSnapshots(state);
        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            Assert.DoesNotContain(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                    && string.Equals(effect["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal));
            AssertDoesNotExposeDependencyObjectId(snapshot, OrnnObjectId);
            AssertDoesNotExposeDependencyObjectId(snapshot, PublicEquipmentObjectId);
            AssertDoesNotExposeDependencyObjectId(snapshot, HiddenEquipmentObjectId);
        }
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
    public void LayerEngineObjectStaticAuraParticipantMetadataRecomputesAcrossPlayerViewsWhenEquipmentParticipantLeavesPublicField()
    {
        var state = BuildOrnnState(
            p1Base: [OrnnObjectId, PublicEquipmentObjectId, HiddenEquipmentObjectId],
            p1Graveyard: [SecondPublicEquipmentObjectId],
            cardObjects: BuildOrnnCardObjects(includeSecondPublicEquipment: true));

        Assert.Contains(PublicEquipmentObjectId, state.PlayerZones["P1"].Base);
        Assert.DoesNotContain(SecondPublicEquipmentObjectId, state.PlayerZones["P1"].Base);
        Assert.Contains(SecondPublicEquipmentObjectId, state.PlayerZones["P1"].Graveyard);

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal($"STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{OrnnObjectId}", aura.EffectId);
        Assert.Equal(OrnnObjectId, aura.TargetObjectId);
        Assert.Equal(OrnnObjectId, aura.SourceObjectId);
        Assert.Equal(1, aura.PowerDelta);
        Assert.Equal(4, aura.BasePower);
        Assert.Equal(5, aura.EffectivePower);
        Assert.Equal("FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER", aura.EffectKind);
        Assert.Equal([PublicEquipmentObjectId], aura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([PublicEquipmentObjectId], aura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(SecondPublicEquipmentObjectId, aura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(SecondPublicEquipmentObjectId, aura.ParticipantDependencyObjectIds ?? []);
        Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantDependencyObjectIds ?? []);

        var snapshots = ResolutionResult.BuildSnapshots(state);
        var p1View = AssertStaticAuraSnapshotView(snapshots["P1"], aura);
        var p2View = AssertStaticAuraSnapshotView(snapshots["P2"], aura);
        Assert.Equal(StaticAuraSnapshotSignature(p1View), StaticAuraSnapshotSignature(p2View));

        foreach (var view in new[] { p1View, p2View })
        {
            Assert.Equal(1, Assert.IsType<int>(view["powerDelta"]));
            Assert.Equal(4, Assert.IsType<int>(view["basePower"]));
            Assert.Equal(5, Assert.IsType<int>(view["effectivePower"]));
            Assert.Equal([PublicEquipmentObjectId], StringList(view, "participantObjectIds"));
            Assert.Equal([PublicEquipmentObjectId], StringList(view, "participantDependencyObjectIds"));
            Assert.DoesNotContain(SecondPublicEquipmentObjectId, StringList(view, "participantObjectIds"));
            Assert.DoesNotContain(SecondPublicEquipmentObjectId, StringList(view, "participantDependencyObjectIds"));
            Assert.DoesNotContain(HiddenEquipmentObjectId, StringList(view, "participantObjectIds"));
            Assert.DoesNotContain(HiddenEquipmentObjectId, StringList(view, "participantDependencyObjectIds"));
        }

        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, SecondPublicEquipmentObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, HiddenEquipmentObjectId);
        }
    }

    [Fact]
    public void LayerEngineObjectStaticAuraPowerScalarsMatchAuthoritativeStateAcrossPlayerViews()
    {
        var cardObjects = BuildOrnnCardObjects(includeSecondPublicEquipment: true);
        cardObjects[OrnnObjectId] = new CardObjectState(
            OrnnObjectId,
            cardNo: OrnnCardNo,
            power: 6,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        var state = BuildOrnnState(
            p1Base: [OrnnObjectId, PublicEquipmentObjectId, SecondPublicEquipmentObjectId, HiddenEquipmentObjectId],
            cardObjects: cardObjects);

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal($"STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{OrnnObjectId}", aura.EffectId);
        Assert.Equal("OBJECT", aura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", aura.Duration);
        Assert.Equal(OrnnObjectId, aura.TargetObjectId);
        Assert.Equal(OrnnObjectId, aura.SourceObjectId);
        Assert.Equal(2, aura.PowerDelta);
        Assert.Equal(4, aura.BasePower);
        Assert.Equal(6, aura.EffectivePower);
        Assert.Equal("FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER", aura.EffectKind);
        Assert.Equal(OrnnCardNo, aura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute", aura.SourcePath);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT", aura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE", aura.Lifecycle);
        Assert.True(aura.IsLayerEngineFoundationOnly);
        Assert.Contains("multiple equipment/static aura interactions", aura.DeferredLayerEngineResiduals ?? []);
        Assert.Equal([PublicEquipmentObjectId, SecondPublicEquipmentObjectId], aura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([PublicEquipmentObjectId, SecondPublicEquipmentObjectId], aura.ParticipantDependencyObjectIds);
        Assert.Equal(1, aura.Sequence);
        Assert.Equal(1, aura.SourceOrder.GetValueOrDefault());
        Assert.Null(aura.RequestedPowerDelta);
        Assert.Null(aura.AppliedPowerDelta);
        Assert.Null(aura.MinimumPower);
        Assert.Null(aura.ResultingPower);
        Assert.Null(aura.AppliedOrder);

        var snapshots = ResolutionResult.BuildSnapshots(state);
        var p1View = AssertStaticAuraSnapshotView(snapshots["P1"], aura);
        var p2View = AssertStaticAuraSnapshotView(snapshots["P2"], aura);
        Assert.Equal(StaticAuraSnapshotSignature(p1View), StaticAuraSnapshotSignature(p2View));
    }

    [Fact]
    public void LayerEngineObjectStaticAuraParticipantMetadataUsesCanonicalOrderWhenPublicFieldOrderDiffersAcrossPlayerViews()
    {
        Assert.True(
            string.CompareOrdinal(PublicEquipmentObjectId, SecondPublicEquipmentObjectId) < 0,
            "The fixture keeps canonical object id order opposite to the public field order.");

        var cardObjects = BuildOrnnCardObjects(includeSecondPublicEquipment: true);
        cardObjects[OrnnObjectId] = new CardObjectState(
            OrnnObjectId,
            cardNo: OrnnCardNo,
            power: 6,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        var state = BuildOrnnState(
            p1Base: [OrnnObjectId, SecondPublicEquipmentObjectId, PublicEquipmentObjectId, HiddenEquipmentObjectId],
            cardObjects: cardObjects);

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal));
        Assert.Equal($"STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{OrnnObjectId}", aura.EffectId);
        Assert.Equal(OrnnObjectId, aura.SourceObjectId);
        Assert.Equal(OrnnObjectId, aura.TargetObjectId);
        Assert.Equal("FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER", aura.EffectKind);
        Assert.Equal(2, aura.PowerDelta);
        Assert.Equal(4, aura.BasePower);
        Assert.Equal(6, aura.EffectivePower);
        Assert.Equal(1, aura.SourceOrder.GetValueOrDefault());
        Assert.Equal([PublicEquipmentObjectId, SecondPublicEquipmentObjectId], aura.ParticipantObjectIds);
        Assert.Equal([OrnnObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([PublicEquipmentObjectId, SecondPublicEquipmentObjectId], aura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantDependencyObjectIds ?? []);

        var snapshots = ResolutionResult.BuildSnapshots(state);
        var p1View = AssertStaticAuraSnapshotView(snapshots["P1"], aura);
        var p2View = AssertStaticAuraSnapshotView(snapshots["P2"], aura);
        Assert.Equal(StaticAuraSnapshotSignature(p1View), StaticAuraSnapshotSignature(p2View));

        foreach (var view in new[] { p1View, p2View })
        {
            Assert.Equal([PublicEquipmentObjectId, SecondPublicEquipmentObjectId], StringList(view, "participantObjectIds"));
            Assert.Equal([PublicEquipmentObjectId, SecondPublicEquipmentObjectId], StringList(view, "participantDependencyObjectIds"));
            Assert.DoesNotContain(HiddenEquipmentObjectId, StringList(view, "participantObjectIds"));
            Assert.DoesNotContain(HiddenEquipmentObjectId, StringList(view, "participantDependencyObjectIds"));
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraPowerScalarsMatchAuthoritativeStateAcrossPlayerViews()
    {
        var state = BuildBattlefieldStaticAuraState(includeDefender: true);
        var effects = BattlefieldStaticAuraEffects(state);
        Assert.Equal(
            [BattlefieldAttackerObjectId, BattlefieldDefenderObjectId],
            effects.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());

        var snapshots = ResolutionResult.BuildSnapshots(state);
        AssertBattlefieldStaticAuraMatchesSnapshots(
            snapshots,
            effects[0],
            BattlefieldAttackerObjectId,
            basePower: 2,
            effectivePower: 3,
            sequence: 1);
        AssertBattlefieldStaticAuraMatchesSnapshots(
            snapshots,
            effects[1],
            BattlefieldDefenderObjectId,
            basePower: 3,
            effectivePower: 4,
            sequence: 2);
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraUsesObjectLocationsToExcludeOtherBattlefields()
    {
        var state = BuildBattlefieldStaticAuraState(includeDefender: true);
        state = state with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = state.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        BattlefieldSourceObjectId,
                        BattlefieldAttackerObjectId,
                        OtherBattlefieldObjectId,
                        OtherBattlefieldUnitObjectId
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
            {
                [OtherBattlefieldObjectId] = new(
                    OtherBattlefieldObjectId,
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                [OtherBattlefieldUnitObjectId] = Unit(OtherBattlefieldUnitObjectId, "P1", power: 4)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
            {
                [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId),
                [OtherBattlefieldUnitObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId)
            }
        };

        var effects = BattlefieldStaticAuraEffects(state);
        Assert.Equal(
            [BattlefieldAttackerObjectId, BattlefieldDefenderObjectId],
            effects.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.DoesNotContain(
            OtherBattlefieldUnitObjectId,
            effects.Select(effect => Assert.IsType<string>(effect.TargetObjectId)));
        Assert.All(
            effects,
            effect =>
            {
                var targetObjectId = Assert.IsType<string>(effect.TargetObjectId);

                Assert.Equal([targetObjectId], effect.TargetDependencyObjectIds);
                Assert.Equal([BattlefieldAttackerObjectId, BattlefieldDefenderObjectId], effect.ParticipantObjectIds);
                Assert.Equal(
                    [BattlefieldAttackerObjectId, BattlefieldDefenderObjectId],
                    effect.ParticipantDependencyObjectIds);
                Assert.DoesNotContain(OtherBattlefieldUnitObjectId, effect.TargetDependencyObjectIds ?? []);
                Assert.DoesNotContain(OtherBattlefieldUnitObjectId, effect.ParticipantObjectIds ?? []);
                Assert.DoesNotContain(OtherBattlefieldUnitObjectId, effect.ParticipantDependencyObjectIds ?? []);
            });
        Assert.DoesNotContain(
            effects.Select(AuthoritativeBattlefieldStaticAuraSignature),
            signature => signature.Contains(OtherBattlefieldUnitObjectId, StringComparison.Ordinal));

        var snapshots = ResolutionResult.BuildSnapshots(state);
        var p1Signatures = AssertBattlefieldStaticAuraSnapshotExcludesOtherBattlefieldUnit(snapshots["P1"]);
        var p2Signatures = AssertBattlefieldStaticAuraSnapshotExcludesOtherBattlefieldUnit(snapshots["P2"]);
        Assert.Equal(p1Signatures, p2Signatures);

        static string AuthoritativeBattlefieldStaticAuraSignature(ContinuousEffectState effect)
        {
            return string.Join(
                "|",
                effect.EffectId ?? string.Empty,
                effect.TargetObjectId ?? string.Empty,
                string.Join(",", effect.TargetDependencyObjectIds ?? []),
                string.Join(",", effect.ParticipantObjectIds ?? []),
                string.Join(",", effect.ParticipantDependencyObjectIds ?? []));
        }

        static string[] AssertBattlefieldStaticAuraSnapshotExcludesOtherBattlefieldUnit(SnapshotDto snapshot)
        {
            var effectViews = ContinuousEffectViews(snapshot)
                .Where(effect => string.Equals(
                    effect["effectKind"] as string,
                    "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
                    StringComparison.Ordinal))
                .OrderBy(effect => Assert.IsType<int>(effect["sequence"]))
                .ToArray();
            Assert.Equal(2, effectViews.Length);
            Assert.DoesNotContain(
                effectViews,
                effect => string.Equals(
                    effect["targetObjectId"] as string,
                    OtherBattlefieldUnitObjectId,
                    StringComparison.Ordinal));
            Assert.All(
                effectViews,
                effect =>
                {
                    Assert.DoesNotContain(OtherBattlefieldUnitObjectId, StringList(effect, "targetDependencyObjectIds"));
                    Assert.DoesNotContain(OtherBattlefieldUnitObjectId, StringList(effect, "participantObjectIds"));
                    Assert.DoesNotContain(OtherBattlefieldUnitObjectId, StringList(effect, "participantDependencyObjectIds"));
                });
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldUnitObjectId);

            var signatures = effectViews.Select(StaticAuraSnapshotSignature).ToArray();
            Assert.DoesNotContain(
                signatures,
                signature => signature.Contains(OtherBattlefieldUnitObjectId, StringComparison.Ordinal));

            return signatures;
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraParticipantOrderIsCanonicalWhenPublicBattlefieldZoneOrderDiffers()
    {
        Assert.True(
            string.CompareOrdinal(BattlefieldAttackerObjectId, BattlefieldDefenderObjectId) < 0,
            "The fixture keeps canonical object id order opposite to this public battlefield order.");

        var state = BuildBattlefieldStaticAuraState(includeDefender: true);
        state = state with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = state.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        BattlefieldSourceObjectId,
                        BattlefieldDefenderObjectId,
                        HiddenEquipmentObjectId,
                        BattlefieldAttackerObjectId,
                        OtherBattlefieldObjectId,
                        OtherBattlefieldUnitObjectId
                    ]
                },
                ["P2"] = state.PlayerZones["P2"] with
                {
                    Battlefields = []
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
            {
                [HiddenEquipmentObjectId] = Equipment(HiddenEquipmentObjectId, isFaceDown: true),
                [OtherBattlefieldObjectId] = new(
                    OtherBattlefieldObjectId,
                    cardNo: "UNL-T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                [OtherBattlefieldUnitObjectId] = Unit(OtherBattlefieldUnitObjectId, "P1", power: 4)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", BattlefieldSourceObjectId),
                [BattlefieldDefenderObjectId] = new("P1", "BATTLEFIELD", BattlefieldSourceObjectId),
                [HiddenEquipmentObjectId] = new("P1", "BATTLEFIELD", BattlefieldSourceObjectId),
                [BattlefieldAttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldSourceObjectId),
                [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId),
                [OtherBattlefieldUnitObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId)
            }
        };

        Assert.Equal(
            [BattlefieldDefenderObjectId, BattlefieldAttackerObjectId],
            state.PlayerZones["P1"].Battlefields
                .Where(objectId => string.Equals(objectId, BattlefieldAttackerObjectId, StringComparison.Ordinal)
                    || string.Equals(objectId, BattlefieldDefenderObjectId, StringComparison.Ordinal))
                .ToArray());

        var expectedParticipantObjectIds = new[] { BattlefieldAttackerObjectId, BattlefieldDefenderObjectId };
        var effects = BattlefieldStaticAuraEffects(state);
        Assert.Equal(
            expectedParticipantObjectIds,
            effects.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());

        foreach (var aura in effects)
        {
            var targetObjectId = Assert.IsType<string>(aura.TargetObjectId);
            Assert.Equal([BattlefieldSourceObjectId], aura.SourceDependencyObjectIds);
            Assert.Equal([targetObjectId], aura.TargetDependencyObjectIds);
            Assert.Equal(expectedParticipantObjectIds, aura.ParticipantObjectIds);
            Assert.Equal(expectedParticipantObjectIds, aura.ParticipantDependencyObjectIds);
            Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(HiddenEquipmentObjectId, aura.ParticipantDependencyObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldObjectId, aura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldObjectId, aura.ParticipantDependencyObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldUnitObjectId, aura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldUnitObjectId, aura.ParticipantDependencyObjectIds ?? []);
        }

        var firstSnapshots = ResolutionResult.BuildSnapshots(state);
        var secondSnapshots = ResolutionResult.BuildSnapshots(state);
        var firstP1Signatures = SnapshotBattlefieldStaticAuraSignatures(
            firstSnapshots["P1"],
            effects,
            expectedParticipantObjectIds);

        Assert.Equal(
            firstP1Signatures,
            SnapshotBattlefieldStaticAuraSignatures(firstSnapshots["P2"], effects, expectedParticipantObjectIds));
        Assert.Equal(
            firstP1Signatures,
            SnapshotBattlefieldStaticAuraSignatures(secondSnapshots["P1"], effects, expectedParticipantObjectIds));
        Assert.Equal(
            firstP1Signatures,
            SnapshotBattlefieldStaticAuraSignatures(secondSnapshots["P2"], effects, expectedParticipantObjectIds));

        static string[] SnapshotBattlefieldStaticAuraSignatures(
            SnapshotDto snapshot,
            ContinuousEffectState[] authoritativeEffects,
            string[] expectedParticipantObjectIds)
        {
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, HiddenEquipmentObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldUnitObjectId);

            var signatures = authoritativeEffects
                .Select(effect =>
                {
                    var view = AssertStaticAuraSnapshotView(snapshot, effect);
                    Assert.Equal(expectedParticipantObjectIds, StringList(view, "participantObjectIds"));
                    Assert.Equal(expectedParticipantObjectIds, StringList(view, "participantDependencyObjectIds"));
                    Assert.DoesNotContain(HiddenEquipmentObjectId, StringList(view, "participantObjectIds"));
                    Assert.DoesNotContain(HiddenEquipmentObjectId, StringList(view, "participantDependencyObjectIds"));
                    Assert.DoesNotContain(OtherBattlefieldObjectId, StringList(view, "participantObjectIds"));
                    Assert.DoesNotContain(OtherBattlefieldObjectId, StringList(view, "participantDependencyObjectIds"));
                    Assert.DoesNotContain(OtherBattlefieldUnitObjectId, StringList(view, "participantObjectIds"));
                    Assert.DoesNotContain(OtherBattlefieldUnitObjectId, StringList(view, "participantDependencyObjectIds"));

                    return StaticAuraSnapshotSignature(view);
                })
                .ToArray();
            Assert.DoesNotContain(
                signatures,
                signature => signature.Contains(HiddenEquipmentObjectId, StringComparison.Ordinal)
                    || signature.Contains(OtherBattlefieldObjectId, StringComparison.Ordinal)
                    || signature.Contains(OtherBattlefieldUnitObjectId, StringComparison.Ordinal));

            return signatures;
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraParticipantMetadataUsesObjectLocationToIgnoreStaleBattlefieldZoneParticipantAcrossPlayerViews()
    {
        var state = BuildBattlefieldStaticAuraState(includeDefender: true);
        state = state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
            {
                [BattlefieldDefenderObjectId] = new("P2", "GRAVEYARD")
            }
        };

        Assert.Contains(BattlefieldDefenderObjectId, state.PlayerZones["P2"].Battlefields);
        Assert.Equal("GRAVEYARD", state.ObjectLocations[BattlefieldDefenderObjectId].Zone);

        var aura = Assert.Single(BattlefieldStaticAuraEffects(state));
        Assert.Equal(BattlefieldAttackerObjectId, aura.TargetObjectId);
        Assert.Equal([BattlefieldSourceObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], aura.ParticipantObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], aura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(BattlefieldDefenderObjectId, aura.SourceDependencyObjectIds ?? []);
        Assert.DoesNotContain(BattlefieldDefenderObjectId, aura.TargetDependencyObjectIds ?? []);
        Assert.DoesNotContain(BattlefieldDefenderObjectId, aura.ParticipantObjectIds ?? []);
        Assert.DoesNotContain(BattlefieldDefenderObjectId, aura.ParticipantDependencyObjectIds ?? []);

        var snapshots = ResolutionResult.BuildSnapshots(state);
        var p1View = AssertBattlefieldStaticAuraSnapshotIgnoresStaleBattlefieldParticipant(snapshots["P1"]);
        var p2View = AssertBattlefieldStaticAuraSnapshotIgnoresStaleBattlefieldParticipant(snapshots["P2"]);
        Assert.Equal(StaticAuraSnapshotSignature(p1View), StaticAuraSnapshotSignature(p2View));

        static Dictionary<string, object?> AssertBattlefieldStaticAuraSnapshotIgnoresStaleBattlefieldParticipant(
            SnapshotDto snapshot)
        {
            var effectView = Assert.Single(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(
                    effect["effectKind"] as string,
                    "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
                    StringComparison.Ordinal));
            Assert.Equal(BattlefieldAttackerObjectId, Assert.IsType<string>(effectView["targetObjectId"]));
            Assert.Equal([BattlefieldSourceObjectId], StringList(effectView, "sourceDependencyObjectIds"));
            Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "targetDependencyObjectIds"));
            Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "participantObjectIds"));
            Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "participantDependencyObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "sourceDependencyObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "targetDependencyObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "participantObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "participantDependencyObjectIds"));
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, BattlefieldDefenderObjectId);

            return effectView;
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraTargetDependenciesDisappearWhenParticipantLeavesBattlefieldAcrossPlayerViews()
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
                    effect.ParticipantObjectIds);
                Assert.Equal(
                    [BattlefieldAttackerObjectId, BattlefieldDefenderObjectId],
                    effect.ParticipantDependencyObjectIds);
            });

        var after = BuildBattlefieldStaticAuraState(includeDefender: false);
        var afterEffect = Assert.Single(BattlefieldStaticAuraEffects(after));
        Assert.Equal(BattlefieldAttackerObjectId, afterEffect.TargetObjectId);
        Assert.Equal([BattlefieldSourceObjectId], afterEffect.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], afterEffect.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], afterEffect.ParticipantObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId], afterEffect.ParticipantDependencyObjectIds);

        var snapshots = ResolutionResult.BuildSnapshots(after);
        var p1View = AssertBattlefieldStaticAuraSnapshotViewAfterParticipantLeaves(snapshots["P1"]);
        var p2View = AssertBattlefieldStaticAuraSnapshotViewAfterParticipantLeaves(snapshots["P2"]);
        Assert.Equal(StaticAuraSnapshotSignature(p1View), StaticAuraSnapshotSignature(p2View));

        static Dictionary<string, object?> AssertBattlefieldStaticAuraSnapshotViewAfterParticipantLeaves(
            SnapshotDto snapshot)
        {
            var effectView = Assert.Single(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(
                    effect["effectKind"] as string,
                    "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
                    StringComparison.Ordinal));
            Assert.Equal(BattlefieldAttackerObjectId, Assert.IsType<string>(effectView["targetObjectId"]));
            Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "targetDependencyObjectIds"));
            Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "participantObjectIds"));
            Assert.Equal([BattlefieldAttackerObjectId], StringList(effectView, "participantDependencyObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "targetDependencyObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "participantObjectIds"));
            Assert.DoesNotContain(BattlefieldDefenderObjectId, StringList(effectView, "participantDependencyObjectIds"));
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, BattlefieldDefenderObjectId);

            return effectView;
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraMetadataDisappearsWhenSourceLeavesBattlefieldAcrossPlayerViews()
    {
        var before = BuildBattlefieldStaticAuraState(includeDefender: true);
        Assert.Equal(2, BattlefieldStaticAuraEffects(before).Length);

        var after = before with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(before.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = before.PlayerZones["P1"] with
                {
                    Battlefields = [BattlefieldAttackerObjectId],
                    Graveyard = [BattlefieldSourceObjectId]
                }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(before.ObjectLocations, StringComparer.Ordinal)
            {
                [BattlefieldSourceObjectId] = new("P1", "GRAVEYARD")
            }
        };

        Assert.Empty(BattlefieldStaticAuraEffects(after));
        Assert.DoesNotContain(
            after.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, BattlefieldSourceObjectId, StringComparison.Ordinal));

        var snapshots = ResolutionResult.BuildSnapshots(after);
        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            Assert.DoesNotContain(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(effect["effectKind"] as string, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                    && string.Equals(effect["sourceObjectId"] as string, BattlefieldSourceObjectId, StringComparison.Ordinal));
            AssertDoesNotExposeDependencyObjectId(snapshot, BattlefieldSourceObjectId);
            AssertDoesNotExposeDependencyObjectId(snapshot, BattlefieldAttackerObjectId);
            AssertDoesNotExposeDependencyObjectId(snapshot, BattlefieldDefenderObjectId);
        }
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

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderMetadataMatchesAuthoritativeStateAcrossPlayerViews()
    {
        var state = BuildBattlefieldSourceOrderState();
        var authoritativeEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();
        Assert.Equal(2, authoritativeEffects.Length);
        Assert.Equal([1, 2], authoritativeEffects.Select(effect => effect.Sequence).ToArray());
        Assert.Equal([1, 3], authoritativeEffects.Select(effect => effect.SourceOrder.GetValueOrDefault()).ToArray());
        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            authoritativeEffects.Select(effect => Assert.IsType<string>(effect.SourceObjectId)).ToArray());

        var authoritativeSignatures = authoritativeEffects.Select(AuthoritativeOrderSignature).ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(state);

        Assert.Equal(authoritativeSignatures, SnapshotOrderSignatures(snapshots["P1"]));
        Assert.Equal(authoritativeSignatures, SnapshotOrderSignatures(snapshots["P2"]));

        static string AuthoritativeOrderSignature(ContinuousEffectState effect)
        {
            return string.Join(
                "|",
                effect.Sequence.ToString(),
                effect.SourceOrder.GetValueOrDefault().ToString(),
                Assert.IsType<string>(effect.SourceObjectId),
                Assert.IsType<string>(effect.TargetObjectId));
        }

        static string SnapshotOrderSignature(Dictionary<string, object?> effect)
        {
            return string.Join(
                "|",
                Assert.IsType<int>(effect["sequence"]).ToString(),
                Assert.IsType<int>(effect["sourceOrder"]).ToString(),
                Assert.IsType<string>(effect["sourceObjectId"]),
                Assert.IsType<string>(effect["targetObjectId"]));
        }

        static string[] SnapshotOrderSignatures(SnapshotDto snapshot)
        {
            return ContinuousEffectViews(snapshot)
                .Where(effect => string.Equals(effect["effectKind"] as string, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                    && string.Equals(effect["targetObjectId"] as string, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
                .OrderBy(effect => Assert.IsType<int>(effect["sequence"]))
                .Select(SnapshotOrderSignature)
                .ToArray();
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataMatchesAuthoritativeStateAcrossPlayerViews()
    {
        var state = BuildBattlefieldSourceOrderState();
        var authoritativeEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(2, authoritativeEffects.Length);

        var authoritativeDependencySignatures = authoritativeEffects
            .Select(StaticAuraAuthoritativeDependencySignature)
            .ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var p1DependencySignatures = new List<string>();
        var p2DependencySignatures = new List<string>();

        foreach (var aura in authoritativeEffects)
        {
            var sourceObjectId = Assert.IsType<string>(aura.SourceObjectId);
            Assert.Equal([sourceObjectId], aura.SourceDependencyObjectIds);
            Assert.Equal([BattlefieldSharedUnitObjectId], aura.TargetDependencyObjectIds);
            Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantObjectIds);
            Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantDependencyObjectIds);
            Assert.True(aura.IsLayerEngineFoundationOnly);
            Assert.True(aura.SourceOrder.HasValue);

            var p1View = AssertStaticAuraSnapshotView(snapshots["P1"], aura);
            var p2View = AssertStaticAuraSnapshotView(snapshots["P2"], aura);
            p1DependencySignatures.Add(StaticAuraSnapshotDependencySignature(p1View));
            p2DependencySignatures.Add(StaticAuraSnapshotDependencySignature(p2View));
        }

        Assert.Equal(authoritativeDependencySignatures, p1DependencySignatures);
        Assert.Equal(authoritativeDependencySignatures, p2DependencySignatures);
    }

    [Fact]
    public void LayerEngineMixedObjectAndBattlefieldStaticAuraDependencySnapshotsAreDeterministicAcrossPlayersAndBuilds()
    {
        var objectState = BuildMixedLayerState();
        var sourceOrderState = BuildBattlefieldSourceOrderState();
        var cardObjects = new Dictionary<string, CardObjectState>(objectState.CardObjects, StringComparer.Ordinal);
        foreach (var cardObject in sourceOrderState.CardObjects)
        {
            cardObjects[cardObject.Key] = cardObject.Value;
        }

        var state = objectState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(objectState.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = objectState.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        FieldFirstBattlefieldSourceObjectId,
                        BattlefieldSharedUnitObjectId,
                        FieldLaterBattlefieldSourceObjectId,
                        OtherBattlefieldObjectId,
                        OtherBattlefieldUnitObjectId
                    ]
                },
                ["P2"] = objectState.PlayerZones["P2"] with
                {
                    Battlefields = [BattlefieldDefenderObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(cardObjects, StringComparer.Ordinal)
            {
                [OtherBattlefieldObjectId] = new(
                    OtherBattlefieldObjectId,
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                [OtherBattlefieldUnitObjectId] = Unit(OtherBattlefieldUnitObjectId, "P1", power: 4),
                [BattlefieldDefenderObjectId] = Unit(BattlefieldDefenderObjectId, "P2", power: 3)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(objectState.ObjectLocations, StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [BattlefieldSharedUnitObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [BattlefieldDefenderObjectId] = new("P2", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId),
                [OtherBattlefieldUnitObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId)
            }
        };

        var authoritativeStaticAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && (string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal)
                    || string.Equals(effect.SourceObjectId, FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal)
                    || string.Equals(effect.SourceObjectId, FieldLaterBattlefieldSourceObjectId, StringComparison.Ordinal)))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(3, authoritativeStaticAuras.Length);
        var ornnAura = Assert.Single(
            authoritativeStaticAuras,
            effect => string.Equals(effect.SourceObjectId, OrnnObjectId, StringComparison.Ordinal));
        Assert.Equal([OrnnObjectId], ornnAura.SourceDependencyObjectIds);
        Assert.Equal([OrnnObjectId], ornnAura.TargetDependencyObjectIds);
        Assert.Equal([PublicEquipmentObjectId], ornnAura.ParticipantObjectIds);
        Assert.Equal([PublicEquipmentObjectId], ornnAura.ParticipantDependencyObjectIds);

        var fieldFirstAura = Assert.Single(
            authoritativeStaticAuras,
            effect => string.Equals(effect.SourceObjectId, FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal));
        Assert.Equal(BattlefieldSharedUnitObjectId, fieldFirstAura.TargetObjectId);
        Assert.Equal(3, fieldFirstAura.SourceOrder.GetValueOrDefault());
        Assert.Equal([FieldFirstBattlefieldSourceObjectId], fieldFirstAura.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], fieldFirstAura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], fieldFirstAura.ParticipantObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], fieldFirstAura.ParticipantDependencyObjectIds);

        var fieldLaterAura = Assert.Single(
            authoritativeStaticAuras,
            effect => string.Equals(effect.SourceObjectId, FieldLaterBattlefieldSourceObjectId, StringComparison.Ordinal));
        Assert.Equal(BattlefieldDefenderObjectId, fieldLaterAura.TargetObjectId);
        Assert.Equal(5, fieldLaterAura.SourceOrder.GetValueOrDefault());
        Assert.Equal([FieldLaterBattlefieldSourceObjectId], fieldLaterAura.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldDefenderObjectId], fieldLaterAura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldDefenderObjectId], fieldLaterAura.ParticipantObjectIds);
        Assert.Equal([BattlefieldDefenderObjectId], fieldLaterAura.ParticipantDependencyObjectIds);

        foreach (var aura in authoritativeStaticAuras)
        {
            AssertDoesNotMentionObjectId(aura, HiddenEquipmentObjectId);
            AssertDoesNotMentionObjectId(aura, OtherBattlefieldObjectId);
            AssertDoesNotMentionObjectId(aura, OtherBattlefieldUnitObjectId);
        }

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.TargetObjectId, OtherBattlefieldUnitObjectId, StringComparison.Ordinal)
                || string.Equals(effect.SourceObjectId, OtherBattlefieldObjectId, StringComparison.Ordinal));

        var firstSnapshots = ResolutionResult.BuildSnapshots(state);
        var secondSnapshots = ResolutionResult.BuildSnapshots(state);
        var firstP1ContinuousEffectSignatures = SnapshotContinuousEffectSignatures(firstSnapshots["P1"]);
        var firstP1StaticAuraSignatures = SnapshotStaticAuraSignatures(firstSnapshots["P1"], authoritativeStaticAuras);

        Assert.Equal(firstP1ContinuousEffectSignatures, SnapshotContinuousEffectSignatures(firstSnapshots["P2"]));
        Assert.Equal(firstP1ContinuousEffectSignatures, SnapshotContinuousEffectSignatures(secondSnapshots["P1"]));
        Assert.Equal(firstP1ContinuousEffectSignatures, SnapshotContinuousEffectSignatures(secondSnapshots["P2"]));
        Assert.Equal(firstP1StaticAuraSignatures, SnapshotStaticAuraSignatures(firstSnapshots["P2"], authoritativeStaticAuras));
        Assert.Equal(firstP1StaticAuraSignatures, SnapshotStaticAuraSignatures(secondSnapshots["P1"], authoritativeStaticAuras));
        Assert.Equal(firstP1StaticAuraSignatures, SnapshotStaticAuraSignatures(secondSnapshots["P2"], authoritativeStaticAuras));

        static void AssertDoesNotMentionObjectId(ContinuousEffectState aura, string objectId)
        {
            Assert.DoesNotContain(objectId, aura.SourceDependencyObjectIds ?? []);
            Assert.DoesNotContain(objectId, aura.TargetDependencyObjectIds ?? []);
            Assert.DoesNotContain(objectId, aura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(objectId, aura.ParticipantDependencyObjectIds ?? []);
        }

        static string[] SnapshotContinuousEffectSignatures(SnapshotDto snapshot)
        {
            var views = ContinuousEffectViews(snapshot);
            Assert.Equal(
                Enumerable.Range(1, views.Count).ToArray(),
                views.Select(effect => Assert.IsType<int>(effect["sequence"])).ToArray());

            return views.Select(EffectSignature).ToArray();
        }

        static string[] SnapshotStaticAuraSignatures(
            SnapshotDto snapshot,
            ContinuousEffectState[] authoritativeStaticAuras)
        {
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, HiddenEquipmentObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldUnitObjectId);

            return authoritativeStaticAuras
                .Select(effect => StaticAuraSnapshotSignature(AssertStaticAuraSnapshotView(snapshot, effect)))
                .ToArray();
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataTracksReorderedPublicFieldOrderAcrossPlayerViews()
    {
        var lexicalSourceObjectIds = new[] { FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId }
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([FieldLaterBattlefieldSourceObjectId, FieldFirstBattlefieldSourceObjectId], lexicalSourceObjectIds);

        var before = BuildBattlefieldSourceOrderState();
        var state = before with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(before.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = before.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        BattlefieldSharedUnitObjectId,
                        FieldFirstBattlefieldSourceObjectId,
                        FieldLaterBattlefieldSourceObjectId
                    ]
                }
            }
        };

        var authoritativeEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();
        var authoritativeSourceObjectIds = authoritativeEffects
            .Select(effect => Assert.IsType<string>(effect.SourceObjectId))
            .ToArray();

        Assert.Equal(2, authoritativeEffects.Length);
        Assert.Equal([1, 2], authoritativeEffects.Select(effect => effect.Sequence).ToArray());
        Assert.Equal([2, 3], authoritativeEffects.Select(effect => effect.SourceOrder.GetValueOrDefault()).ToArray());
        Assert.Equal([FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId], authoritativeSourceObjectIds);
        Assert.NotEqual(lexicalSourceObjectIds, authoritativeSourceObjectIds);

        foreach (var aura in authoritativeEffects)
        {
            var sourceObjectId = Assert.IsType<string>(aura.SourceObjectId);
            Assert.Equal([sourceObjectId], aura.SourceDependencyObjectIds);
            Assert.Equal([BattlefieldSharedUnitObjectId], aura.TargetDependencyObjectIds);
            Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantObjectIds);
            Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantDependencyObjectIds);
            Assert.True(aura.IsLayerEngineFoundationOnly);
            Assert.True(aura.SourceOrder.HasValue);
        }

        var authoritativeDependencySignatures = authoritativeEffects
            .Select(StaticAuraAuthoritativeDependencySignature)
            .ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(state);

        Assert.Equal(authoritativeDependencySignatures, SnapshotDependencySignatures(snapshots["P1"], authoritativeEffects));
        Assert.Equal(authoritativeDependencySignatures, SnapshotDependencySignatures(snapshots["P2"], authoritativeEffects));

        static string[] SnapshotDependencySignatures(
            SnapshotDto snapshot,
            ContinuousEffectState[] authoritativeEffects)
        {
            return authoritativeEffects
                .Select(effect => StaticAuraSnapshotDependencySignature(AssertStaticAuraSnapshotView(snapshot, effect)))
                .ToArray();
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataRecomputesWhenParticipantMovesAroundSourcesAcrossPlayerViews()
    {
        var before = BuildBattlefieldSourceOrderState() with
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
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldDefenderObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = BattlefieldPowerSource(FieldFirstBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = BattlefieldPowerSource(FieldLaterBattlefieldSourceObjectId),
                [BattlefieldSharedUnitObjectId] = Unit(BattlefieldSharedUnitObjectId, "P1", power: 2),
                [BattlefieldDefenderObjectId] = Unit(BattlefieldDefenderObjectId, "P2", power: 3)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [BattlefieldSharedUnitObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [BattlefieldDefenderObjectId] = new("P2", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId)
            }
        };
        var beforeEffects = BattlefieldSourceOrderParticipantEffects(before);
        Assert.Equal([1, 3], beforeEffects.Select(effect => effect.SourceOrder.GetValueOrDefault()).ToArray());

        var beforeDependencyIdentities = beforeEffects
            .Select(StaticAuraAuthoritativeDependencyIdentity)
            .ToArray();
        var after = before with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(before.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = before.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        FieldFirstBattlefieldSourceObjectId,
                        FieldLaterBattlefieldSourceObjectId,
                        BattlefieldSharedUnitObjectId
                    ]
                }
            }
        };
        var authoritativeEffects = BattlefieldSourceOrderParticipantEffects(after);

        Assert.Equal([1, 2], authoritativeEffects.Select(effect => effect.SourceOrder.GetValueOrDefault()).ToArray());
        Assert.Equal(beforeDependencyIdentities, authoritativeEffects.Select(StaticAuraAuthoritativeDependencyIdentity).ToArray());

        var authoritativeDependencySignatures = authoritativeEffects
            .Select(StaticAuraAuthoritativeDependencySignature)
            .ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(after);

        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            var snapshotDependencySignatures = authoritativeEffects
                .Select(effect => StaticAuraSnapshotDependencySignature(AssertStaticAuraSnapshotView(snapshot, effect)))
                .ToArray();
            Assert.Equal(authoritativeDependencySignatures, snapshotDependencySignatures);
        }

        static ContinuousEffectState[] BattlefieldSourceOrderParticipantEffects(MatchState state)
        {
            return state.ContinuousEffects
                .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                    && (string.Equals(effect.SourceObjectId, FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal)
                        || string.Equals(effect.SourceObjectId, FieldLaterBattlefieldSourceObjectId, StringComparison.Ordinal)))
                .OrderBy(effect => effect.Sequence)
                .ToArray();
        }

        static string StaticAuraAuthoritativeDependencyIdentity(ContinuousEffectState effect)
        {
            return string.Join(
                "|",
                Assert.IsType<string>(effect.EffectId),
                Assert.IsType<string>(effect.SourceObjectId),
                Assert.IsType<string>(effect.TargetObjectId),
                string.Join(",", effect.SourceDependencyObjectIds ?? Array.Empty<string>()),
                string.Join(",", effect.TargetDependencyObjectIds ?? Array.Empty<string>()),
                string.Join(",", effect.ParticipantObjectIds ?? Array.Empty<string>()),
                string.Join(",", effect.ParticipantDependencyObjectIds ?? Array.Empty<string>()),
                string.Join(",", effect.DeferredLayerEngineResiduals ?? Array.Empty<string>()));
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataRecomputesWhenSourceLeavesBattlefieldAcrossPlayerViews()
    {
        var before = BuildBattlefieldSourceOrderState();
        var beforeEffects = before.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();
        Assert.Equal(2, beforeEffects.Length);
        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            beforeEffects.Select(effect => Assert.IsType<string>(effect.SourceObjectId)).ToArray());
        Assert.Equal([1, 3], beforeEffects.Select(effect => effect.SourceOrder.GetValueOrDefault()).ToArray());

        var after = before with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(before.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = before.PlayerZones["P1"] with
                {
                    Battlefields = [BattlefieldSharedUnitObjectId, FieldLaterBattlefieldSourceObjectId],
                    Graveyard = [FieldFirstBattlefieldSourceObjectId]
                }
            }
        };
        var authoritativeEffects = after.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();
        var aura = Assert.Single(authoritativeEffects);

        Assert.Equal(1, aura.Sequence);
        Assert.Equal(2, aura.SourceOrder.GetValueOrDefault());
        Assert.Equal(FieldLaterBattlefieldSourceObjectId, aura.SourceObjectId);
        Assert.Equal(BattlefieldSharedUnitObjectId, aura.TargetObjectId);
        Assert.Equal([FieldLaterBattlefieldSourceObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(
            after.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal));

        var authoritativeDependencySignature = StaticAuraAuthoritativeDependencySignature(aura);
        Assert.DoesNotContain(FieldFirstBattlefieldSourceObjectId, authoritativeDependencySignature);

        var snapshots = ResolutionResult.BuildSnapshots(after);
        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            Assert.DoesNotContain(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(
                    effect["sourceObjectId"] as string,
                    FieldFirstBattlefieldSourceObjectId,
                    StringComparison.Ordinal));
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, FieldFirstBattlefieldSourceObjectId);

            var view = AssertStaticAuraSnapshotView(snapshot, aura);
            var snapshotDependencySignature = StaticAuraSnapshotDependencySignature(view);
            Assert.Equal(authoritativeDependencySignature, snapshotDependencySignature);
            Assert.DoesNotContain(FieldFirstBattlefieldSourceObjectId, snapshotDependencySignature);
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataUsesObjectLocationToIgnoreStaleBattlefieldZoneSourceAcrossPlayerViews()
    {
        var before = BuildBattlefieldSourceOrderState();
        var beforeEffects = before.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(2, beforeEffects.Length);
        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            beforeEffects.Select(effect => Assert.IsType<string>(effect.SourceObjectId)).ToArray());

        var after = before with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(before.ObjectLocations, StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = new("P1", "GRAVEYARD"),
                [BattlefieldSharedUnitObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId)
            }
        };

        Assert.Contains(FieldFirstBattlefieldSourceObjectId, after.PlayerZones["P1"].Battlefields);
        Assert.Equal("GRAVEYARD", after.ObjectLocations[FieldFirstBattlefieldSourceObjectId].Zone);
        Assert.Equal(
            FieldLaterBattlefieldSourceObjectId,
            after.ObjectLocations[BattlefieldSharedUnitObjectId].BattlefieldObjectId);
        Assert.Equal(
            FieldLaterBattlefieldSourceObjectId,
            after.ObjectLocations[FieldLaterBattlefieldSourceObjectId].BattlefieldObjectId);

        var authoritativeEffects = after.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();
        var authoritativeEffectSignatures = authoritativeEffects
            .Select(effect => string.Join(
                "|",
                Assert.IsType<string>(effect.EffectId),
                StaticAuraAuthoritativeDependencySignature(effect)))
            .ToArray();
        Assert.DoesNotContain(
            authoritativeEffectSignatures,
            signature => signature.Contains(FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal));

        var aura = Assert.Single(authoritativeEffects);

        Assert.Equal(1, aura.Sequence);
        Assert.Equal(2, aura.SourceOrder.GetValueOrDefault());
        Assert.Equal(FieldLaterBattlefieldSourceObjectId, aura.SourceObjectId);
        Assert.Equal(BattlefieldSharedUnitObjectId, aura.TargetObjectId);
        Assert.Equal([FieldLaterBattlefieldSourceObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantDependencyObjectIds);

        var authoritativeDependencySignature = StaticAuraAuthoritativeDependencySignature(aura);
        Assert.DoesNotContain(FieldFirstBattlefieldSourceObjectId, authoritativeDependencySignature);

        var snapshots = ResolutionResult.BuildSnapshots(after);
        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            Assert.DoesNotContain(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(
                        effect["sourceObjectId"] as string,
                        FieldFirstBattlefieldSourceObjectId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        effect["targetObjectId"] as string,
                        FieldFirstBattlefieldSourceObjectId,
                        StringComparison.Ordinal)
                    || (effect["effectId"] as string)?.Contains(FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal) == true);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, FieldFirstBattlefieldSourceObjectId);

            var view = AssertStaticAuraSnapshotView(snapshot, aura);
            var snapshotDependencySignature = StaticAuraSnapshotDependencySignature(view);
            Assert.Equal(authoritativeDependencySignature, snapshotDependencySignature);
            Assert.DoesNotContain(FieldFirstBattlefieldSourceObjectId, snapshotDependencySignature);
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataUsesObjectLocationToIgnoreStaleBattlefieldZoneParticipantAcrossPlayerViews()
    {
        var before = BuildBattlefieldSourceOrderState();
        var beforeEffects = before.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(2, beforeEffects.Length);
        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            beforeEffects.Select(effect => Assert.IsType<string>(effect.SourceObjectId)).ToArray());

        var after = before with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(before.ObjectLocations, StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [BattlefieldSharedUnitObjectId] = new("P1", "GRAVEYARD"),
                [FieldLaterBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId)
            }
        };

        Assert.Contains(BattlefieldSharedUnitObjectId, after.PlayerZones["P1"].Battlefields);
        Assert.Equal("GRAVEYARD", after.ObjectLocations[BattlefieldSharedUnitObjectId].Zone);
        Assert.Equal(
            FieldFirstBattlefieldSourceObjectId,
            after.ObjectLocations[FieldFirstBattlefieldSourceObjectId].BattlefieldObjectId);
        Assert.Equal(
            FieldLaterBattlefieldSourceObjectId,
            after.ObjectLocations[FieldLaterBattlefieldSourceObjectId].BattlefieldObjectId);

        var authoritativeEffects = after.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();
        var authoritativeEffectSignatures = authoritativeEffects
            .Select(effect => string.Join(
                "|",
                effect.EffectId ?? string.Empty,
                effect.SourceObjectId ?? string.Empty,
                effect.TargetObjectId ?? string.Empty,
                string.Join(",", effect.SourceDependencyObjectIds ?? []),
                string.Join(",", effect.TargetDependencyObjectIds ?? []),
                string.Join(",", effect.ParticipantObjectIds ?? []),
                string.Join(",", effect.ParticipantDependencyObjectIds ?? []),
                string.Join(",", effect.DeferredLayerEngineResiduals ?? [])))
            .ToArray();

        Assert.Empty(authoritativeEffects);
        Assert.DoesNotContain(
            authoritativeEffectSignatures,
            signature => signature.Contains(BattlefieldSharedUnitObjectId, StringComparison.Ordinal));

        var snapshots = ResolutionResult.BuildSnapshots(after);
        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            var effectViews = ContinuousEffectViews(snapshot)
                .Where(effect => string.Equals(
                    effect["effectKind"] as string,
                    "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
                    StringComparison.Ordinal))
                .OrderBy(effect => Assert.IsType<int>(effect["sequence"]))
                .ToArray();
            var snapshotEffectSignatures = effectViews
                .Select(effect => string.Join(
                    "|",
                    effect["effectId"] as string ?? string.Empty,
                    effect["sourceObjectId"] as string ?? string.Empty,
                    effect["targetObjectId"] as string ?? string.Empty,
                    string.Join(",", StringList(effect, "sourceDependencyObjectIds")),
                    string.Join(",", StringList(effect, "targetDependencyObjectIds")),
                    string.Join(",", StringList(effect, "participantObjectIds")),
                    string.Join(",", StringList(effect, "participantDependencyObjectIds")),
                    string.Join(",", StringList(effect, "deferredLayerEngineResiduals"))))
                .ToArray();

            Assert.Empty(effectViews);
            Assert.DoesNotContain(
                snapshotEffectSignatures,
                signature => signature.Contains(BattlefieldSharedUnitObjectId, StringComparison.Ordinal));
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, BattlefieldSharedUnitObjectId);
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataExcludesRemovedSourceAndOtherBattlefieldAcrossPlayerViews()
    {
        var before = BuildBattlefieldSourceOrderState();
        var beforeEffects = before.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, BattlefieldSharedUnitObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(2, beforeEffects.Length);
        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            beforeEffects.Select(effect => Assert.IsType<string>(effect.SourceObjectId)).ToArray());

        var after = before with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(before.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = before.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        BattlefieldSharedUnitObjectId,
                        FieldLaterBattlefieldSourceObjectId,
                        OtherBattlefieldObjectId
                    ],
                    Graveyard = [FieldFirstBattlefieldSourceObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects, StringComparer.Ordinal)
            {
                [OtherBattlefieldObjectId] = new(
                    OtherBattlefieldObjectId,
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(before.ObjectLocations, StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = new("P1", "GRAVEYARD"),
                [BattlefieldSharedUnitObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId)
            }
        };
        var aura = Assert.Single(
            after.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal));

        Assert.Equal(1, aura.Sequence);
        Assert.Equal(2, aura.SourceOrder.GetValueOrDefault());
        Assert.Equal(FieldLaterBattlefieldSourceObjectId, aura.SourceObjectId);
        Assert.Equal(BattlefieldSharedUnitObjectId, aura.TargetObjectId);
        Assert.Equal([FieldLaterBattlefieldSourceObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantObjectIds);
        Assert.Equal([BattlefieldSharedUnitObjectId], aura.ParticipantDependencyObjectIds);
        Assert.DoesNotContain(
            after.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, FieldFirstBattlefieldSourceObjectId, StringComparison.Ordinal)
                || string.Equals(effect.SourceObjectId, OtherBattlefieldObjectId, StringComparison.Ordinal)
                || string.Equals(effect.TargetObjectId, OtherBattlefieldObjectId, StringComparison.Ordinal));

        var authoritativeDependencySignature = StaticAuraAuthoritativeDependencySignature(aura);
        Assert.DoesNotContain(FieldFirstBattlefieldSourceObjectId, authoritativeDependencySignature);
        Assert.DoesNotContain(OtherBattlefieldObjectId, authoritativeDependencySignature);

        var snapshots = ResolutionResult.BuildSnapshots(after);
        foreach (var snapshot in new[] { snapshots["P1"], snapshots["P2"] })
        {
            Assert.DoesNotContain(
                ContinuousEffectViews(snapshot),
                effect => string.Equals(
                        effect["sourceObjectId"] as string,
                        FieldFirstBattlefieldSourceObjectId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        effect["sourceObjectId"] as string,
                        OtherBattlefieldObjectId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        effect["targetObjectId"] as string,
                        OtherBattlefieldObjectId,
                        StringComparison.Ordinal));
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, FieldFirstBattlefieldSourceObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldObjectId);

            var view = AssertStaticAuraSnapshotView(snapshot, aura);
            var snapshotDependencySignature = StaticAuraSnapshotDependencySignature(view);
            Assert.Equal(authoritativeDependencySignature, snapshotDependencySignature);
            Assert.DoesNotContain(FieldFirstBattlefieldSourceObjectId, snapshotDependencySignature);
            Assert.DoesNotContain(OtherBattlefieldObjectId, snapshotDependencySignature);
        }
    }

    [Fact]
    public void LayerEngineBattlefieldStaticAuraSourceOrderExcludesOtherBattlefieldParticipantsAcrossPlayerViews()
    {
        var state = BuildBattlefieldSourceOrderState();
        state = state with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = state.PlayerZones["P1"] with
                {
                    Battlefields =
                    [
                        FieldFirstBattlefieldSourceObjectId,
                        BattlefieldSharedUnitObjectId,
                        FieldLaterBattlefieldSourceObjectId,
                        OtherBattlefieldObjectId,
                        OtherBattlefieldUnitObjectId
                    ]
                },
                ["P2"] = state.PlayerZones["P2"] with
                {
                    Battlefields = [BattlefieldDefenderObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
            {
                [OtherBattlefieldObjectId] = new(
                    OtherBattlefieldObjectId,
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                [OtherBattlefieldUnitObjectId] = Unit(OtherBattlefieldUnitObjectId, "P1", power: 4),
                [BattlefieldDefenderObjectId] = Unit(BattlefieldDefenderObjectId, "P2", power: 3)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
            {
                [FieldFirstBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [BattlefieldSharedUnitObjectId] = new("P1", "BATTLEFIELD", FieldFirstBattlefieldSourceObjectId),
                [FieldLaterBattlefieldSourceObjectId] = new("P1", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [BattlefieldDefenderObjectId] = new("P2", "BATTLEFIELD", FieldLaterBattlefieldSourceObjectId),
                [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId),
                [OtherBattlefieldUnitObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId)
            }
        };

        var expectedParticipantObjectIdsBySource = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [FieldFirstBattlefieldSourceObjectId] = [BattlefieldSharedUnitObjectId],
            [FieldLaterBattlefieldSourceObjectId] = [BattlefieldDefenderObjectId]
        };
        var authoritativeEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && effect.SourceObjectId is not null
                && expectedParticipantObjectIdsBySource.ContainsKey(effect.SourceObjectId))
            .OrderBy(effect => effect.Sequence)
            .ToArray();

        Assert.Equal(2, authoritativeEffects.Length);
        Assert.Equal([1, 2], authoritativeEffects.Select(effect => effect.Sequence).ToArray());
        Assert.Equal([1, 3], authoritativeEffects.Select(effect => effect.SourceOrder.GetValueOrDefault()).ToArray());
        Assert.Equal(
            [FieldFirstBattlefieldSourceObjectId, FieldLaterBattlefieldSourceObjectId],
            authoritativeEffects.Select(effect => Assert.IsType<string>(effect.SourceObjectId)).ToArray());
        Assert.Equal(
            [BattlefieldSharedUnitObjectId, BattlefieldDefenderObjectId],
            authoritativeEffects.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, OtherBattlefieldUnitObjectId, StringComparison.Ordinal));

        foreach (var aura in authoritativeEffects)
        {
            var sourceObjectId = Assert.IsType<string>(aura.SourceObjectId);
            var expectedParticipantObjectIds = expectedParticipantObjectIdsBySource[sourceObjectId];
            Assert.Equal([sourceObjectId], aura.SourceDependencyObjectIds);
            Assert.Equal(expectedParticipantObjectIds, aura.TargetDependencyObjectIds);
            Assert.Equal(expectedParticipantObjectIds, aura.ParticipantObjectIds);
            Assert.Equal(expectedParticipantObjectIds, aura.ParticipantDependencyObjectIds);
            Assert.DoesNotContain(OtherBattlefieldObjectId, aura.TargetDependencyObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldObjectId, aura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldObjectId, aura.ParticipantDependencyObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldUnitObjectId, aura.TargetDependencyObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldUnitObjectId, aura.ParticipantObjectIds ?? []);
            Assert.DoesNotContain(OtherBattlefieldUnitObjectId, aura.ParticipantDependencyObjectIds ?? []);
        }

        var authoritativeDependencySignatures = authoritativeEffects
            .Select(StaticAuraAuthoritativeDependencySignature)
            .ToArray();
        Assert.DoesNotContain(
            authoritativeDependencySignatures,
            signature => signature.Contains(OtherBattlefieldObjectId, StringComparison.Ordinal)
                || signature.Contains(OtherBattlefieldUnitObjectId, StringComparison.Ordinal));

        var snapshots = ResolutionResult.BuildSnapshots(state);
        Assert.Equal(authoritativeDependencySignatures, SnapshotDependencySignatures(snapshots["P1"], authoritativeEffects));
        Assert.Equal(authoritativeDependencySignatures, SnapshotDependencySignatures(snapshots["P2"], authoritativeEffects));

        static string[] SnapshotDependencySignatures(
            SnapshotDto snapshot,
            ContinuousEffectState[] authoritativeEffects)
        {
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldObjectId);
            AssertDoesNotExposeDependencyOrParticipantObjectId(snapshot, OtherBattlefieldUnitObjectId);

            var dependencySignatures = authoritativeEffects
                .Select(effect => StaticAuraSnapshotDependencySignature(AssertStaticAuraSnapshotView(snapshot, effect)))
                .ToArray();
            Assert.DoesNotContain(
                dependencySignatures,
                signature => signature.Contains(OtherBattlefieldObjectId, StringComparison.Ordinal)
                    || signature.Contains(OtherBattlefieldUnitObjectId, StringComparison.Ordinal));

            return dependencySignatures;
        }
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

    private static MatchState BuildPowerModifierLedgerLegacyRemainderState()
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Base = [PowerLedgerLegacyTargetObjectId]
            },
            ["P2"] = PlayerZones.Empty
        };

        return BaseState("layer-engine-power-ledger-legacy-remainder") with
        {
            PlayerZones = playerZones,
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [PowerLedgerLegacyTargetObjectId] = new(
                    PowerLedgerLegacyTargetObjectId,
                    cardNo: "TEST-18VT-UNIT",
                    power: 10,
                    untilEndOfTurnPowerModifier: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    untilEndOfTurnPowerModifiers:
                    [
                        new PowerModifierLedgerEntry(
                            "POWER:P1-UNIT-POWER-LEDGER-LEGACY-REMAINDER:SECOND_MINUS_TWO",
                            "LEDGER_REMAINDER_SECOND_MINUS_TWO",
                            "UNTIL_END_OF_TURN",
                            PowerLedgerLegacyTargetObjectId,
                            "P1-SPELL-LEDGER-SECOND",
                            "TEST-18VT-SECOND",
                            -2,
                            7,
                            5,
                            "LayerEngineTimestampDependencyTests.LegacyRemainder.Second",
                            -2,
                            1,
                            5,
                            2),
                        new PowerModifierLedgerEntry(
                            "POWER:P1-UNIT-POWER-LEDGER-LEGACY-REMAINDER:FIRST_PLUS_THREE",
                            "LEDGER_REMAINDER_FIRST_PLUS_THREE",
                            "UNTIL_END_OF_TURN",
                            PowerLedgerLegacyTargetObjectId,
                            "P1-SPELL-LEDGER-FIRST",
                            "TEST-18VT-FIRST",
                            3,
                            4,
                            7,
                            "LayerEngineTimestampDependencyTests.LegacyRemainder.First",
                            3,
                            0,
                            7,
                            1)
                    ])
            },
            ObjectLocations = ObjectLocationsForZones(playerZones)
        };
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

    private static void AssertBattlefieldStaticAuraMatchesSnapshots(
        IReadOnlyDictionary<string, SnapshotDto> snapshots,
        ContinuousEffectState aura,
        string targetObjectId,
        int basePower,
        int effectivePower,
        int sequence)
    {
        Assert.Equal($"STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:{BattlefieldSourceObjectId}:{targetObjectId}", aura.EffectId);
        Assert.Equal("BATTLEFIELD", aura.Scope);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", aura.Duration);
        Assert.Equal(targetObjectId, aura.TargetObjectId);
        Assert.Equal(BattlefieldSourceObjectId, aura.SourceObjectId);
        Assert.Equal(1, aura.PowerDelta);
        Assert.Equal(basePower, aura.BasePower);
        Assert.Equal(effectivePower, aura.EffectivePower);
        Assert.Equal("BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", aura.EffectKind);
        Assert.Equal("OGN·294/298", aura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus", aura.SourcePath);
        Assert.Equal("SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD", aura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS", aura.Lifecycle);
        Assert.True(aura.IsLayerEngineFoundationOnly);
        Assert.Contains("full official LayerEngine coverage", aura.DeferredLayerEngineResiduals ?? []);
        Assert.Equal([BattlefieldAttackerObjectId, BattlefieldDefenderObjectId], aura.ParticipantObjectIds);
        Assert.Equal([BattlefieldSourceObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([targetObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal([BattlefieldAttackerObjectId, BattlefieldDefenderObjectId], aura.ParticipantDependencyObjectIds);
        Assert.Equal(sequence, aura.Sequence);
        Assert.Equal(1, aura.SourceOrder.GetValueOrDefault());
        Assert.Null(aura.RequestedPowerDelta);
        Assert.Null(aura.AppliedPowerDelta);
        Assert.Null(aura.MinimumPower);
        Assert.Null(aura.ResultingPower);
        Assert.Null(aura.AppliedOrder);

        var p1View = AssertStaticAuraSnapshotView(snapshots["P1"], aura);
        var p2View = AssertStaticAuraSnapshotView(snapshots["P2"], aura);
        Assert.Equal(StaticAuraSnapshotSignature(p1View), StaticAuraSnapshotSignature(p2View));
    }

    private static Dictionary<string, object?> AssertStaticAuraSnapshotView(
        SnapshotDto snapshot,
        ContinuousEffectState aura)
    {
        var view = Assert.Single(
            ContinuousEffectViews(snapshot),
            effect => string.Equals(effect["effectId"] as string, aura.EffectId, StringComparison.Ordinal));

        Assert.Equal(aura.EffectId, Assert.IsType<string>(view["effectId"]));
        Assert.Equal(aura.Scope, Assert.IsType<string>(view["scope"]));
        Assert.Equal(aura.Layer, Assert.IsType<string>(view["layer"]));
        Assert.Equal(aura.Duration, Assert.IsType<string>(view["duration"]));
        Assert.Equal(aura.TargetObjectId, Assert.IsType<string>(view["targetObjectId"]));
        Assert.Equal(aura.SourceObjectId, Assert.IsType<string>(view["sourceObjectId"]));
        Assert.Equal(aura.PowerDelta, Assert.IsType<int>(view["powerDelta"]));
        Assert.Equal(aura.BasePower, Assert.IsType<int>(view["basePower"]));
        Assert.Equal(aura.EffectivePower, Assert.IsType<int>(view["effectivePower"]));
        Assert.Equal(aura.Sequence, Assert.IsType<int>(view["sequence"]));
        Assert.Equal(aura.EffectKind, Assert.IsType<string>(view["effectKind"]));
        Assert.Equal(aura.SourceCardNo, Assert.IsType<string>(view["sourceCardNo"]));
        Assert.Equal(aura.SourcePath, Assert.IsType<string>(view["sourcePath"]));
        Assert.Equal(aura.Condition, Assert.IsType<string>(view["condition"]));
        Assert.Equal(aura.Lifecycle, Assert.IsType<string>(view["lifecycle"]));
        Assert.Equal("FOUNDATION_ONLY", Assert.IsType<string>(view["layerEngineStatus"]));
        Assert.Equal(aura.SourceOrder.GetValueOrDefault(), Assert.IsType<int>(view["sourceOrder"]));
        Assert.Equal(aura.ParticipantObjectIds, StringList(view, "participantObjectIds"));
        Assert.Equal(aura.SourceDependencyObjectIds, StringList(view, "sourceDependencyObjectIds"));
        Assert.Equal(aura.TargetDependencyObjectIds, StringList(view, "targetDependencyObjectIds"));
        Assert.Equal(aura.ParticipantDependencyObjectIds, StringList(view, "participantDependencyObjectIds"));
        Assert.Equal(aura.DeferredLayerEngineResiduals, StringList(view, "deferredLayerEngineResiduals"));
        Assert.False(view.ContainsKey("requestedPowerDelta"));
        Assert.False(view.ContainsKey("appliedPowerDelta"));
        Assert.False(view.ContainsKey("minimumPower"));
        Assert.False(view.ContainsKey("resultingPower"));
        Assert.False(view.ContainsKey("appliedOrder"));

        return view;
    }

    private static string StaticAuraSnapshotSignature(Dictionary<string, object?> view)
    {
        return string.Join(
            "|",
            Assert.IsType<string>(view["effectId"]),
            Assert.IsType<string>(view["scope"]),
            Assert.IsType<string>(view["layer"]),
            Assert.IsType<string>(view["duration"]),
            Assert.IsType<string>(view["targetObjectId"]),
            Assert.IsType<string>(view["sourceObjectId"]),
            Assert.IsType<int>(view["powerDelta"]).ToString(),
            Assert.IsType<int>(view["basePower"]).ToString(),
            Assert.IsType<int>(view["effectivePower"]).ToString(),
            Assert.IsType<int>(view["sequence"]).ToString(),
            Assert.IsType<int>(view["sourceOrder"]).ToString(),
            Assert.IsType<string>(view["effectKind"]),
            Assert.IsType<string>(view["sourceCardNo"]),
            Assert.IsType<string>(view["sourcePath"]),
            Assert.IsType<string>(view["condition"]),
            Assert.IsType<string>(view["lifecycle"]),
            Assert.IsType<string>(view["layerEngineStatus"]),
            string.Join(",", StringList(view, "participantObjectIds")),
            string.Join(",", StringList(view, "sourceDependencyObjectIds")),
            string.Join(",", StringList(view, "targetDependencyObjectIds")),
            string.Join(",", StringList(view, "participantDependencyObjectIds")),
            string.Join(",", StringList(view, "deferredLayerEngineResiduals")));
    }

    private static string StaticAuraAuthoritativeDependencySignature(ContinuousEffectState effect)
    {
        return string.Join(
            "|",
            effect.Sequence.ToString(),
            effect.SourceOrder.GetValueOrDefault().ToString(),
            Assert.IsType<string>(effect.SourceObjectId),
            Assert.IsType<string>(effect.TargetObjectId),
            string.Join(",", effect.SourceDependencyObjectIds ?? Array.Empty<string>()),
            string.Join(",", effect.TargetDependencyObjectIds ?? Array.Empty<string>()),
            string.Join(",", effect.ParticipantObjectIds ?? Array.Empty<string>()),
            string.Join(",", effect.ParticipantDependencyObjectIds ?? Array.Empty<string>()),
            string.Join(",", effect.DeferredLayerEngineResiduals ?? Array.Empty<string>()));
    }

    private static string StaticAuraSnapshotDependencySignature(Dictionary<string, object?> view)
    {
        return string.Join(
            "|",
            Assert.IsType<int>(view["sequence"]).ToString(),
            Assert.IsType<int>(view["sourceOrder"]).ToString(),
            Assert.IsType<string>(view["sourceObjectId"]),
            Assert.IsType<string>(view["targetObjectId"]),
            string.Join(",", StringList(view, "sourceDependencyObjectIds")),
            string.Join(",", StringList(view, "targetDependencyObjectIds")),
            string.Join(",", StringList(view, "participantObjectIds")),
            string.Join(",", StringList(view, "participantDependencyObjectIds")),
            string.Join(",", StringList(view, "deferredLayerEngineResiduals")));
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

    private static void AssertDoesNotExposeDependencyOrParticipantObjectId(
        SnapshotDto snapshot,
        string objectId)
    {
        var objectIds = ContinuousEffectViews(snapshot)
            .SelectMany(DependencyOrParticipantObjectIds)
            .ToArray();

        Assert.DoesNotContain(objectId, objectIds);
        Assert.DoesNotContain(
            objectIds,
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

    private static IEnumerable<string> DependencyOrParticipantObjectIds(Dictionary<string, object?> view)
    {
        if (view.TryGetValue("targetObjectId", out var targetObjectId)
            && targetObjectId is string target)
        {
            yield return target;
        }

        foreach (var key in new[]
        {
            "sourceDependencyObjectIds",
            "targetDependencyObjectIds",
            "participantObjectIds",
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
