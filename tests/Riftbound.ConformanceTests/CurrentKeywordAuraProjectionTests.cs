using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.CurrentKeywordAuraParticipantTests;

namespace Riftbound.ConformanceTests;

// Explicit aura metadata exercises the production projection seam without
// registering a consumer or treating this finite input as an accepted match.
public sealed class CurrentKeywordAuraProjectionTests
{
    [Theory]
    [InlineData("intrinsic")]
    [InlineData("temporary")]
    [InlineData("continuous")]
    public void AuthorityMembershipAndProjectionUseTheSameCurrentPossession(string mode)
    {
        var state = Position(mode);
        var aura = Aura(CurrentBulwark);
        var before = MatchStateHasher.Hash(state);
        var effect = Assert.Single(Effects(state, aura));
        Assert.Equal("P1-TARGET", effect.TargetObjectId);
        Assert.Equal(1, effect.PowerDelta);
        Assert.Equal(state.CardObjects["P1-TARGET"].Power + 1, effect.EffectivePower);
        Assert.Equal(["P1-TARGET"], effect.ParticipantObjectIds);
        Assert.Equal(["P1-TARGET"], effect.TargetDependencyObjectIds);
        Assert.Equal(["P1-TARGET"], effect.ParticipantDependencyObjectIds);
        Assert.Equal(mode == "continuous" ? new[] { "F", "SOURCE" } : ["F"], effect.SourceDependencyObjectIds);
        foreach (var id in new[] { "P1-TARGET", "P2-TARGET" })
            Assert.Equal(id == "P1-TARGET", AuthorityMatches(state, aura, state.CardObjects[id]));
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Fact]
    public void BattlefieldOwnershipAndControllerDoNotExcludeEitherPlayersBulwarkUnit()
    {
        var state = Position("intrinsic");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["P2-TARGET"] = Printed("P2-TARGET", "OGN·054/298", "P2") } };
        Assert.Equal(["P1-TARGET", "P2-TARGET"], Members(state, Aura(CurrentBulwark)));
        Assert.Equal(2, Effects(state, Aura(CurrentBulwark)).Count);
    }

    [Theory]
    [InlineData("ANY:CURRENT_KEYWORD:壁垒|TAG:ABSENT", false)]
    [InlineData("ANY:TAG:ABSENT|CARD_NAME:莽莽魄罗", true)]
    [InlineData("CARD_NAME:莽莽魄罗", true)]
    public void ExistingCompoundAndNameFiltersRetainTheirMeaning(string filter, bool matches)
    {
        var state = Position("temporary");
        Assert.Equal(matches ? new[] { "P1-TARGET" } : [], Members(state, Aura(filter)));
        Assert.Equal(matches, AuthorityMatches(state, Aura(filter), state.CardObjects["P1-TARGET"]));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void PossessionIsIndependentOfBackRowAndDamageImmunity(bool bulwark, int priority)
    {
        var state = Position("temporary");
        var target = Printed("P1-TARGET", "OGN·189/298", "P1");
        target = target with { MovementCountThisTurn = bulwark ? 2 : 0,
            Tags = bulwark ? target.Tags : [..target.Tags, CardCombatKeywordNames.BackRow],
            UntilEndOfTurnEffects = bulwark ? ["COMBAT_KEYWORD:壁垒:1"] : [] };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [target.ObjectId] = target } };
        Assert.Equal(bulwark, Inspect(state, target).Matches);
        var actualPriority = (int)typeof(CoreRuleEngine).GetMethod("CombatDamageTargetPriorityFor",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state, target.ObjectId])!;
        Assert.Equal(priority, actualPriority);
        Assert.Equal(bulwark ? new[] { target.ObjectId } : [], Members(state, Aura(CurrentBulwark)));
    }

    [Theory]
    [InlineData("face-down")]
    [InlineData("private-location")]
    [InlineData("other-controller")]
    public void AnInactiveContinuousSourceCannotQualifyOrLeakItsIdentity(string change)
    {
        var state = Position("continuous");
        var source = state.CardObjects["SOURCE"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SOURCE"] = source with { IsFaceDown = change == "face-down", ControllerId = change == "other-controller" ? "P2" : "P1" } } };
        if (change == "private-location") state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            { ["SOURCE"] = new("P1", "HAND") } };
        var query = Inspect(state, state.CardObjects["P1-TARGET"]);
        Assert.False(query.Matches);
        Assert.Empty(query.PublicKeywordSourceIds);
        Assert.DoesNotContain(Effects(state, Aura(CurrentBulwark)), e => e.TargetObjectId == "P1-TARGET");
    }

    [Fact]
    public void TemporaryPossessionExpiresWithoutRetainingTheHistoricSpellAsADependency()
    {
        var state = Position("temporary");
        var query = Inspect(state, state.CardObjects["P1-TARGET"]);
        Assert.True(query.Matches); Assert.Empty(query.PublicKeywordSourceIds);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["P1-TARGET"] = state.CardObjects["P1-TARGET"] with { UntilEndOfTurnEffects = [] } } };
        Assert.Empty(Effects(state, Aura(CurrentBulwark)));
        Assert.False(Inspect(state, state.CardObjects["P1-TARGET"]).Matches);
    }

    [Fact]
    public void AQueryConsumesItsSuppliedLayerWhileTheProjectionReevaluatesStaleDerivedKeywords()
    {
        var state = Position("temporary");
        var target = state.CardObjects["P1-TARGET"];
        target = target with { UntilEndOfTurnEffects = [], Tags = [..target.Tags, CardCombatKeywordNames.Bulwark],
            RuleDerivedTags = [CardCombatKeywordNames.Bulwark] };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [target.ObjectId] = target } };
        var before = MatchStateHasher.Hash(state);
        Assert.True(Inspect(state, target).Matches);
        Assert.Empty(Members(state, Aura(CurrentBulwark)));
        Assert.Empty(Effects(state, Aura(CurrentBulwark)));
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.False(Inspect(state, target with { ObjectGeneration = 0 }).Matches);
    }

    [Fact]
    public void ACurrentCopiedLilliaGrantIncludesOnlyItsLivePublicOrigin()
    {
        var state = Position("continuous");
        var gear = Printed("SONG", "SFD·059/221", "P1") with { AttachedToObjectId = "SOURCE", AttachmentRevision = 1,
            AttachedRuleText = new(new("SOURCE", 1), "UNL-058/219", 1, 0, 0) };
        // A retained printed-text copy and a changed current face are distinct
        // rule instances. This is a component frame, not a copy-command witness.
        var source = Printed("SOURCE", "OGN·210/298", "P1") with
            { CopyFaceTransitions = [new(1, 1, "UNL-058/219", "OGN·210/298")] };
        state = state with { Tick = 1,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["SOURCE"] = source, ["SONG"] = gear },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                { ["P1"] = state.PlayerZones["P1"] with { Base = ["SOURCE", "SONG"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SONG"] = new("P1", "BASE") } };
        var target = state.CardObjects["P1-TARGET"];
        Assert.True(Inspect(state, target).Matches);
        Assert.Equal(["SONG", "SOURCE"], Inspect(state, target).PublicKeywordSourceIds);
        Assert.Equal(["F", "SONG", "SOURCE"], Assert.Single(Effects(state, Aura(CurrentBulwark))).SourceDependencyObjectIds);
        var changed = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SOURCE"] = source with { ObjectGeneration = 2 } } };
        Assert.False(Inspect(changed, target).Matches); Assert.Empty(Effects(changed, Aura(CurrentBulwark)));
        var reattached = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SONG"] = gear with { AttachmentRevision = 2 } } };
        Assert.False(Inspect(reattached, target).Matches); Assert.Empty(Effects(reattached, Aura(CurrentBulwark)));
        var hidden = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            { ["SONG"] = new("P1", "HAND") } };
        Assert.False(Inspect(hidden, target).Matches); Assert.Empty(Inspect(hidden, target).PublicKeywordSourceIds);
    }

    internal static (bool Matches, IReadOnlyList<string> PublicKeywordSourceIds) Inspect(MatchState state, CardObjectState target)
        => ((bool, IReadOnlyList<string>))typeof(CoreRuleEngine).GetMethod("InspectCurrentKeywordFilter",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state, Aura(CurrentBulwark), target])!;

    internal static bool AuthorityMatches(MatchState state, StaticAuraSpec aura, CardObjectState target)
        => (bool)typeof(CoreRuleEngine).GetMethod("BattlefieldStaticAuraAppliesToParticipant",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state, aura, target])!;

    internal static IReadOnlyList<ContinuousEffectState> Effects(MatchState state, StaticAuraSpec aura)
        => (IReadOnlyList<ContinuousEffectState>)typeof(MatchState).GetMethod("BuildBattlefieldPowerStaticAuraEffects",
            BindingFlags.Static | BindingFlags.NonPublic, [typeof(MatchState), typeof(string), typeof(StaticAuraSpec)])!
            .Invoke(null, [state, "F", aura])!;
}
