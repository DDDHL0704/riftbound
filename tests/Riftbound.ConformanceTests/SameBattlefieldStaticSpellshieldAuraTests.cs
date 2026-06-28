using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SameBattlefieldStaticSpellshieldAuraTests
{
    private const string BattlefieldObjectId = "P2-AERIE-BATTLEFIELD";
    private const string SourceObjectId = "P2-AERIE-HEAD-FAN";
    private const string TargetObjectId = "P2-AERIE-ALLY";
    private const string OffsiteTargetObjectId = "P2-AERIE-OFFSITE-ALLY";
    private const string SpellObjectId = "P1-SPELL-INCINERATE";

    [Fact]
    public void AerieHeadFanProjectsSameBattlefieldOtherFriendlySpellshield()
    {
        var state = BuildState(mana: 3);

        var ruleTextAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal($"RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD:{SourceObjectId}:{TargetObjectId}:{CardResourceKeywordNames.Spellshield}", ruleTextAura.EffectId);
        Assert.Equal("OBJECT", ruleTextAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", ruleTextAura.Duration);
        Assert.Equal(TargetObjectId, ruleTextAura.TargetObjectId);
        Assert.Equal(0, ruleTextAura.PowerDelta);
        Assert.Equal(0, ruleTextAura.BasePower);
        Assert.Equal(0, ruleTextAura.EffectivePower);
        Assert.Empty(ruleTextAura.EffectKind);
        Assert.Null(ruleTextAura.SourceCardNo);
        Assert.Empty(ruleTextAura.SourcePath);
        Assert.Null(ruleTextAura.ParticipantObjectIds);
        Assert.Null(ruleTextAura.SourceDependencyObjectIds);
        Assert.Null(ruleTextAura.TargetDependencyObjectIds);
        Assert.Null(ruleTextAura.ParticipantDependencyObjectIds);
        Assert.Null(ruleTextAura.SourceOrder);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, OffsiteTargetObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AerieHeadFanSameBattlefieldSpellshieldAddsEnemySpellTargetTax()
    {
        var state = BuildState(mana: 3);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-aerie-head-fan-spellshield-tax", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                "OGS·003/024",
                [TargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(3, costPaidEvent.Payload["mana"]);
        Assert.Equal(2, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(3, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costPaidEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal(
            [TargetObjectId],
            Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task AerieHeadFanSameBattlefieldSpellshieldExpiresWhenTargetMovesAway()
    {
        var state = BuildState(mana: 2, targetAtSameBattlefield: false);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, TargetObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-aerie-head-fan-spellshield-expired-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                "OGS·003/024",
                [TargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(2, costPaidEvent.Payload["mana"]);
        Assert.Equal(0, costPaidEvent.Payload["spellshieldTaxMana"]);
        Assert.Empty(Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task AerieHeadFanSameBattlefieldSpellshieldDoesNotApplyFromFaceDownSource()
    {
        var state = BuildState(mana: 2, sourceFaceDown: true);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-aerie-head-fan-spellshield-face-down-source", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                "OGS·003/024",
                [TargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(2, costPaidEvent.Payload["mana"]);
        Assert.Equal(0, costPaidEvent.Payload["spellshieldTaxMana"]);
        Assert.Empty(Assert.IsType<string[]>(costPaidEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    private static MatchState BuildState(
        int mana,
        bool targetAtSameBattlefield = true,
        bool sourceFaceDown = false)
    {
        var targetBattlefieldObjectId = targetAtSameBattlefield
            ? BattlefieldObjectId
            : "P2-AERIE-OTHER-BATTLEFIELD";

        return new MatchState(
            "same-battlefield-static-spellshield-aura-room",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [SpellObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        BattlefieldObjectId,
                        "P2-AERIE-OTHER-BATTLEFIELD",
                        SourceObjectId,
                        TargetObjectId,
                        OffsiteTargetObjectId
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [SpellObjectId] = new(
                    SpellObjectId,
                    cardNo: "OGS·003/024",
                    ownerId: "P1",
                    controllerId: "P1"),
                [BattlefieldObjectId] = Battlefield(BattlefieldObjectId),
                ["P2-AERIE-OTHER-BATTLEFIELD"] = Battlefield("P2-AERIE-OTHER-BATTLEFIELD"),
                [SourceObjectId] = new(
                    SourceObjectId,
                    cardNo: "UNL-041/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Spellshield],
                    ownerId: "P2",
                    controllerId: "P2",
                    isFaceDown: sourceFaceDown),
                [TargetObjectId] = Unit(TargetObjectId, "P2"),
                [OffsiteTargetObjectId] = Unit(OffsiteTargetObjectId, "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                ["P2-AERIE-OTHER-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "P2-AERIE-OTHER-BATTLEFIELD"),
                [SourceObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [TargetObjectId] = new("P2", "BATTLEFIELD", targetBattlefieldObjectId),
                [OffsiteTargetObjectId] = new("P2", "BATTLEFIELD", "P2-AERIE-OTHER-BATTLEFIELD")
            });
    }

    private static CardObjectState Unit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: 2,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Battlefield(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·275/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P2",
            controllerId: "P2");
    }
}
