using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;

public sealed class LucianLegendArmamentKeywordTests
{
    private static MatchState Board(string legend, bool attached = false)
    {
        var state = ArmamentMightTests.Position("SFD·064/221", attached: attached);
        Assert.True(PrintedCardFactory.TryCreate("LUCIAN", "P1", legend, out var source));
        Assert.True(PrintedCardFactory.TryCreate("GEAR", "P1", "SFD·064/221", out var gear));
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LUCIAN"] = source,
                ["GEAR"] = gear with { AttachedToObjectId = attached ? "HOST" : null } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LUCIAN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LUCIAN"] = new("P1", "LEGEND") } };
    }

    private static int Assault(MatchState state)
    {
        var evaluated = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        return CardCombatKeywordRules.KeywordAmount(evaluated.CardObjects["HOST"].Tags, CardCombatKeywordNames.Assault);
    }

    [Theory, InlineData("SFD·183/221"), InlineData("SFD·241/221")]
    public async Task RealAssemblyAndDetachmentAddAndRemoveTheActualAssaultKeyword(string legend)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Board(legend), "DETACH", "SFD·011/221");
        var game = new Driver(initial);
        Assert.Equal(0, Assault(game.State));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_BLUE"]));
        Assert.Equal(0, Assault(game.State));
        await game.Top();
        Assert.Equal(1, Assault(game.State));
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(0, Assault(game.State));
        await game.Replay();
    }

    [Theory, InlineData("SFD·183/221"), InlineData("SFD·241/221")]
    public async Task PrintedLowerKeywordsAndEachSeparateWeaponGrantAddTogether(string legend)
    {
        var initial = VenConditionalPlayCostTests.Place(Board(legend, attached: true), "DAGGER", "SFD·009/221", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["DAGGER"] = initial.CardObjects["DAGGER"] with { AttachedToObjectId = "HOST" } } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "PLAIN", "OGN·219/298");
        var game = new Driver(initial);
        Assert.Equal(4, Assault(game.State)); // Printed dagger 2 plus one per weapon.
        await game.Act("P1", new PlayCardCommand("PLAIN", "OGN·219/298", []));
        Assert.Equal(4, Assault(game.State));
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·183/221", "P1", 1)] [InlineData("SFD·241/221", "P1", 1)]
    [InlineData("SFD·183/221", "P2", 0)] [InlineData("SFD·241/221", "P2", 0)]
    public async Task ForeignAttachmentsUseTheWeaponsControllerRatherThanTheHostsLegend(
        string legend, string weaponController, int expected)
    {
        // Declared initial control/ownership boundary; this is not a control-transfer action.
        var initial = At(At(Board(legend, attached: true), "HOST", "P2", "BATTLEFIELD", "F"),
            "GEAR", weaponController, "BATTLEFIELD", "F");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = weaponController == "P1" ? "P2" : "P1" } } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "PLAIN", "OGN·219/298");
        var game = new Driver(initial);
        Assert.Equal(expected, Assault(game.State));
        await game.Act("P1", new PlayCardCommand("PLAIN", "OGN·219/298", []));
        Assert.Equal(expected, Assault(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("legend-hidden")] [InlineData("legend-hand")]
    [InlineData("gear-hidden")] [InlineData("gear-hand")] [InlineData("detached")]
    public void InactiveProvidersCannotGrantAHostKeyword(string inactive)
    {
        var state = Board("SFD·183/221", attached: true);
        if (inactive is "legend-hidden" or "gear-hidden") {
            var id = inactive == "legend-hidden" ? "LUCIAN" : "GEAR";
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { IsFaceDown = true } } };
        } else if (inactive is "legend-hand" or "gear-hand") {
            var id = inactive == "legend-hand" ? "LUCIAN" : "GEAR";
            var zones = state.PlayerZones["P1"];
            state = state with {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = zones with { Base = zones.Base.Where(x => x != id).ToArray(),
                        LegendZone = zones.LegendZone.Where(x => x != id).ToArray(), Hand = [..zones.Hand, id] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                    [id] = new("P1", "HAND") } };
        } else state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = null } } };
        Assert.Equal(0, Assault(state));
    }

    [Theory]
    [InlineData("你的每件武装都会提供{{强攻}}。", true)]
    [InlineData("你的每件武装都会提供{{强攻}}。（如果配装的单位是进攻方，则{{S}}+1。）", true)]
    [InlineData("你的每件武装都会提供{{强攻}}，如果你拥有增益。", false)]
    [InlineData("敌方的每件武装都会提供{{强攻}}。", false)]
    [InlineData("你的装备获得{{强攻}}。", false)]
    public void ProvidingAHostKeywordHasItsOwnExactScope(string text, bool expected)
        => Assert.Equal(expected, StaticAuraParser.Parse(text).Any(a => a.Kind == StaticAuraKinds.FriendlyArmamentProvidedKeyword));
    [Theory]
    [InlineData("SFD·183/221", "SFD·028/221", false)]
    [InlineData("SFD·241/221", "SFD·028a/221", false)]
    [InlineData("SFD·183/221", "SFD·028/221", true)]
    [InlineData("SFD·241/221", "SFD·028a/221", true)]
    public async Task ARealLucianAttackReadsBothWeaponGrantsAndRecomputesAfterAWeaponIsDetached(
        string legend, string unitNo, bool detachDagger)
    {
        var initial = PrintedAttackClauseTests.Position(unitNo);
        initial = VenConditionalPlayCostTests.Place(initial, "GEAR", "SFD·064/221", "P1", "BASE");
        initial = VenConditionalPlayCostTests.Place(initial, "DAGGER", "SFD·009/221", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "REMOVE", "SFD·011/221", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LUCIAN"] = OfficialGarenConquestBattlefieldCountTests.Printed("LUCIAN", legend, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LUCIAN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LUCIAN"] = new("P1", "LEGEND") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", unitNo, []));
        foreach (var (id, cost) in new[] { ("GEAR", "ASSEMBLE_BLUE"), ("DAGGER", "ASSEMBLE_RED") }) {
            await game.Act("P1", new AssembleEquipmentCommand(id, "UNIT", [cost]));
            await game.Top();
            Assert.Equal("UNIT", game.State.CardObjects[id].AttachedToObjectId);
        }
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"]));
        await game.Top();
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        await PrintedAttackClauseTests.SelectEnemy(game);
        Assert.Single(game.State.StackItems);
        if (detachDagger) {
            await game.Act("P1", new PlayCardCommand("REMOVE", "SFD·011/221", ["UNIT", "DAGGER"]));
            await game.Top();
            Assert.Null(game.State.CardObjects["DAGGER"].AttachedToObjectId);
        }
        await game.Top();
        // Printed Lucian 1 + dagger 2 + legend 1 per remaining weapon.
        Assert.Equal(detachDagger ? 2 : 5, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·009/221", "强攻2")]
    [InlineData("SFD·064/221", "坚守2")]
    [InlineData("SFD·033/221", "壁垒")]
    [InlineData("SFD·133/221", "游走")]
    [InlineData("SFD·042/221", null)]
    [InlineData("SFD·090/221", null)]
    public void OnlyStandaloneLowerBoxKeywordsAreUnconditionalGrants(string cardNo, string? keyword)
    {
        var definition = Assert.IsType<OfficialArmamentDefinition>(OfficialArmamentCatalog.Find(cardNo));
        Assert.Equal(keyword is null ? [] : new[] { keyword }, definition.GrantedKeywords ?? []);
    }

    [Fact]
    public void APlainEquipmentWithoutWeaponRulesTextDoesNotProvideTheLegendKeyword()
    {
        var state = Board("SFD·183/221", attached: true);
        var plain = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·046/221", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = plain with { AttachedToObjectId = "HOST" } } };
        Assert.DoesNotContain(CardEquipmentKeywordNames.Weapon, plain.Tags);
        Assert.Equal(0, Assault(state));
    }

}
