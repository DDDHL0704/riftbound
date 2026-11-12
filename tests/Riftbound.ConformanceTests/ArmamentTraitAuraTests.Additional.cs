using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class ArmamentTraitAuraTests
{
    private static MatchState DeclaredAttachment(string no, string hostNo = "OGN·219/298")
    {
        var state = RadUnitEffectTestFixture.Place(Position(no, hostNo), "UNIT", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { AttachedToObjectId = "ALLY" } } };
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }

    [Fact]
    public void TheOfficialCatalogSeparatesHostTraitsLocalAurasAndOrdinarySelfKeywords()
    {
        var mechanical = Assert.IsType<OfficialArmamentDefinition>(OfficialArmamentCatalog.Find(MechanicalGear));
        var roam = Assert.IsType<OfficialArmamentDefinition>(OfficialArmamentCatalog.Find(RoamGear));
        Assert.Equal(new[] { "机械" }, mechanical.GrantedTraits);
        Assert.Empty(mechanical.GrantedKeywords ?? []);
        Assert.Empty(mechanical.FriendlyUnitsHereKeywords ?? []);
        Assert.Equal(new[] { "游走" }, roam.FriendlyUnitsHereKeywords);
        Assert.Empty(roam.GrantedKeywords ?? []);
        Assert.Empty(roam.GrantedTraits ?? []);
        var selfRoam = Assert.IsType<OfficialArmamentDefinition>(OfficialArmamentCatalog.Find("SFD·133/221"));
        Assert.Contains("游走", selfRoam.GrantedKeywords ?? []);
        Assert.Empty(selfRoam.FriendlyUnitsHereKeywords ?? []);
    }

    public static IEnumerable<object[]> InactiveLowerSources() => new[] { MechanicalGear, RoamGear }
        .SelectMany(no => new[] { "detached", "gear-hidden", "host-hidden", "gear-hand", "host-hand", "different-location", "not-equipment" }
            .Select(branch => new object[] { no, branch }));

    [Theory]
    [MemberData(nameof(InactiveLowerSources))]
    public void RemovingTheCurrentAttachmentOrPublicLocationRemovesItsOwnDerivedLayer(string no, string branch)
    {
        // These are direct evaluator boundaries, not a claim that a forged
        // attachment was accepted by an actual gameplay command or recovery.
        var state = DeclaredAttachment(no);
        var tag = no == MechanicalGear ? "机械" : "游走";
        Assert.Contains(tag, state.CardObjects["ALLY"].Tags);
        var id = branch.StartsWith("host-", StringComparison.Ordinal) ? "ALLY" : "UNIT";
        if (branch is "gear-hand" or "host-hand")
            state = RadUnitEffectTestFixture.Place(state, id, "P1", "HAND");
        else if (branch == "different-location")
            state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        else
        {
            var card = state.CardObjects[id];
            card = branch switch {
                "detached" => card with { AttachedToObjectId = null },
                "gear-hidden" or "host-hidden" => card with { IsFaceDown = true },
                _ => card with { Tags = [CardObjectTags.SpellCard] }
            };
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = card } };
        }
        var before = MatchStateHasher.Hash(state);
        var evaluated = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.DoesNotContain(tag, evaluated.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain(tag, evaluated.CardObjects["FRIEND"].Tags);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public void AForeignAuraAttachmentUsesTheHostControllerRatherThanEquipmentControlOrOwnership(string controller)
    {
        var state = DeclaredAttachment(RoamGear);
        state = RadUnitEffectTestFixture.Place(state, "ALLY", controller, "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "FRIEND", "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2" } } };
        var evaluated = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        Assert.Contains("游走", evaluated.CardObjects["ALLY"].Tags);
        Assert.Equal(controller == "P1", evaluated.CardObjects["FRIEND"].Tags.Contains("游走"));
        Assert.Equal(controller == "P2", evaluated.CardObjects["ENEMY"].Tags.Contains("游走"));
        Assert.Equal("P1", evaluated.CardObjects["UNIT"].ControllerId);
    }

    [Theory]
    [InlineData(MechanicalGear)] [InlineData(RoamGear)]
    public async Task AFaceUpHostWithThePrintedStandbyKeywordStillReceivesItsLowerBox(string no)
    {
        const string host = "UNL-145/219";
        var initial = RadUnitEffectTestFixture.Place(Position(no, host), "ALLY", "P1", "HAND");
        var game = new Driver(initial);
        Assert.True(StandbyRules.HasPrintedKeyword(host));
        await game.Act("P1", new PlayCardCommand("ALLY", host, []));
        if (game.State.StackItems.Count > 0) await game.Top();
        await Attach(game, no);
        Assert.False(game.State.CardObjects["ALLY"].IsFaceDown);
        Assert.True(StandbyRules.HasPrintedKeyword(game.State.CardObjects["ALLY"].CardNo));
        Assert.Contains(no == MechanicalGear ? "机械" : "游走", game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·T01")] [InlineData("VEN·T03")] [InlineData("OGN·088/298")]
    public async Task IntrinsicMechanicalTraitsOfRealTokensAndCopiedPrintedFacesSurviveDetachment(string face)
    {
        var state = AttackDamageDistributionTests.Spell(Position(MechanicalGear), "DETACH", "SFD·011/221", "P1");
        var isToken = P6TokenFactoryCatalog.TryGetByCardNo(face, out var token);
        var source = isToken ? token!.CreateObject("ALLY", "P1", "P1")
            : OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", face, "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ALLY"] = source } };
        if (!isToken) state = AttackDamageDistributionTests.Spell(state, "COPY", "UNL-200/219", "P1");
        var game = new Driver(state);
        var hostId = "ALLY";
        if (!isToken)
        {
            await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ALLY"]));
            await game.Top();
            var copy = Assert.IsType<ReflexiveCopyContext>(Assert.Single(game.State.StackItems).ReflexiveCopy);
            hostId = Assert.Single(copy.Recipients).ObjectId;
            await game.Top();
            Assert.Equal(face, game.State.CardObjects[hostId].CardNo);
            Assert.NotEmpty(game.State.CardObjects[hostId].CopyFaceTransitions!);
        }
        Assert.Contains("机械", game.State.CardObjects[hostId].Tags);
        await game.Act("P1", new PlayCardCommand("UNIT", MechanicalGear, []));
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", hostId, ["ASSEMBLE_BLUE"]));
        await game.Top();
        Assert.Equal(hostId, game.State.CardObjects["UNIT"].AttachedToObjectId);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", [hostId, "UNIT"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Contains("机械", game.State.CardObjects[hostId].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData(MechanicalGear)] [InlineData(RoamGear)]
    public async Task ActuallyDestroyingTheEquipmentEndsTheHostGrantAndLocalAura(string no)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no), "DESTROY-GEAR", "SFD·005/221", "P1");
        var game = new Driver(state); await Attach(game, no);
        await game.Act("P1", new PlayCardCommand("DESTROY-GEAR", "SFD·005/221", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var tag = no == MechanicalGear ? "机械" : "游走";
        Assert.DoesNotContain(tag, game.State.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain(tag, game.State.CardObjects["FRIEND"].Tags);
        await game.Replay();
    }

    [Fact]
    public void RecoveryRejectsAnErasedOrInventedDerivedTraitAndLocalAura()
    {
        foreach (var no in new[] { MechanicalGear, RoamGear })
        {
            var state = DeclaredAttachment(no);
            var id = no == MechanicalGear ? "ALLY" : "FRIEND";
            var tag = no == MechanicalGear ? "机械" : "游走";
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
            var source = state.CardObjects[id];
            var erased = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = source with { Tags = source.Tags.Where(value => value != tag).ToArray(), RuleDerivedTags = null } } };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(erased));
            var extra = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = state.CardObjects["ENEMY"] with {
                    Tags = [..state.CardObjects["ENEMY"].Tags, tag], RuleDerivedTags = [tag] } } };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(extra));
        }
    }
}
