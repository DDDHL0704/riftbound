using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class WiseElderSourceObjectFilteredPowerTests
{
    private const string WiseElderObjectId = "P1-WISE-ELDER";
    private const string WiseElderCardNo = "OGN·065/298";

    [Fact]
    public void WiseElderBoonStaticPowerProjectsSourceObjectFilteredAura()
    {
        var state = BuildState(hasBoon: true);

        var staticAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, WiseElderObjectId, StringComparison.Ordinal));

        Assert.Equal("STATIC_AURA:SOURCE_OBJECT_FILTERED_POWER:P1-WISE-ELDER", staticAura.EffectId);
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", staticAura.Duration);
        Assert.Equal(WiseElderObjectId, staticAura.TargetObjectId);
        Assert.Equal(WiseElderObjectId, staticAura.SourceObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal(StaticAuraKinds.SourceObjectFilteredPower, staticAura.EffectKind);
        Assert.Equal(WiseElderCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSourceObjectFilteredPowerBonus", staticAura.SourcePath);
        Assert.True(staticAura.IsLayerEngineFoundationOnly);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_MATCHES_FILTER", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SOURCE_OBJECT_TAGS", staticAura.Lifecycle);
        Assert.Equal([WiseElderObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([WiseElderObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([WiseElderObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([WiseElderObjectId], staticAura.ParticipantDependencyObjectIds);
    }

    [Fact]
    public void WiseElderWithoutBoonDoesNotProjectSourceObjectFilteredAura()
    {
        var state = BuildState(hasBoon: false);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceObjectFilteredPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, WiseElderObjectId, StringComparison.Ordinal));
    }

    private static MatchState BuildState(bool hasBoon)
    {
        string[] wiseTags = hasBoon
            ? [CardObjectTags.UnitCard, CardObjectTags.Boon]
            : [CardObjectTags.UnitCard];

        return new MatchState(
            "wise-elder-source-object-filtered-power-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [WiseElderObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [WiseElderObjectId] = new(
                    WiseElderObjectId,
                    cardNo: WiseElderCardNo,
                    power: 4,
                    tags: wiseTags,
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }
}
