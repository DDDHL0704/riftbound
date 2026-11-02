using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadZiggsPartyTests
{
    [Theory]
    [InlineData("RAD·023/167", 39515L)]
    [InlineData("RAD·023a/167", 39516L)]
    public async Task EachPrintingPaysItsCostAndPlaysABombAtTheChosenBattlefield(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("炸欢天", card.SubTitle);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, ["OTHER"], Destination: "BASE"));
        await game.Top();
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        var bomb = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "OTHER"), game.State.ObjectLocations[bomb.ObjectId]);
        Assert.Contains(CardObjectTags.EquipmentCard, bomb.Tags); Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
        await game.Replay();
    }

    private static MatchState BombPosition()
    {
        var s = VenUnitEntryBasicTests.Position("RAD·142/167");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "RAD·023/167", "P1"),
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2"),
                ["BOMB"] = factory.CreateObject("BOMB", "P1", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Battlefields = ["BF", "BOMB"] },
                ["P2"] = s.PlayerZones["P2"] with { Base = [], Battlefields = ["OTHER", "ENEMY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["BOMB"] = new("P1", "BATTLEFIELD", "BF"), ["ENEMY"] = new("P2", "BATTLEFIELD", "BF") } };
        return s;
    }

    [Theory]
    [InlineData("friendly", 3)] [InlineData("enemy", 2)] [InlineData("gone", 2)]
    [InlineData("two", 4)] [InlineData("prevented", 0)]
    public async Task IndependentBombSkillUsesTheCurrentFriendlyEquipmentDamageBonuses(string branch, int expected)
    {
        var s = BombPosition();
        if (branch == "enemy") s = LocalDestructionRecallTests.At(s, "ALLY", "P2", "BASE");
        if (branch == "two") s = LastBreathDrawBoundaryTests.Add(s, "ZIGGS2", "RAD·023a/167", "P1", "BASE");
        if (branch == "gone") s = OfficialCopyIdentityTests.AddSpell(s, "RETURN", "OGN·104/298");
        if (branch == "prevented") s = s with { UntilEndOfTurnEffects = ["PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN"] };
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·142/167", ["BOMB"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("BOMB"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (branch == "gone") {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        }
        await game.Top(); Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task EquipmentSkillBonusDoesNotIncreaseSpellDamage()
    {
        var s = BombPosition();
        s = AttackDamageDistributionTests.Spell(s, "AXE", "RAD·148/167", "P1");
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("AXE", "RAD·148/167", ["ALLY", "ENEMY"])); await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage); await game.Replay();
    }
}
