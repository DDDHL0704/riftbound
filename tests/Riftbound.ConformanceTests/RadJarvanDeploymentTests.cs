using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadJarvanDeploymentTests
{
    private const string Hero = "RAD·091/167", Gear = "RAD·052/167";

    private static MatchState Position(string no = Hero)
    {
        var state = ConquestTokenTriggerTests.Position(no);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", no, "P1") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) } };
        state = RadGearTestPosition.Put(state, "GEAR", Gear, "P1", "HAND");
        return RadGearTestPosition.Put(state, "CALLED", "UNL-R01", "P1", "RUNE_DECK");
    }

    [Theory]
    [InlineData(Hero, 39592)] [InlineData("RAD·091a/167", 39593)]
    public async Task BothPrintingsKeepTheirOfficialCostAndShareDeploymentAbilities(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        var grant = Assert.Single(EquipmentKeywordGrantParser.Parse(card.CardEffect));
        Assert.Equal("部署", grant.RequiredKeyword); Assert.Equal("迅捷", grant.GrantedKeyword);
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitConquestCallRune);
        Assert.Equal("部署", trigger.RequiredFriendlyEquipmentKeywordHere); Assert.Equal(1, trigger.RuneCallCount);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(16, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Theory]
    [InlineData(Hero)] [InlineData("RAD·091a/167")]
    public async Task DeploymentEquipmentCanBePlayedDuringTheNaturalSpellDuel(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        await game.Act("P1", new PlayCardCommand("GEAR", Gear, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal("BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(29, game.State.RunePools["P1"].Mana); Assert.Equal(30, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData("enemy-source")] [InlineData("hidden-source")] [InlineData("absent-source")]
    [InlineData("ordinary-equipment")] [InlineData("foreign-owner")]
    public async Task SwiftRequiresAControlledFaceUpSourceAndOwnedDeploymentEquipment(string branch)
    {
        var moved = await new CoreRuleEngine().ResolveAsync(Position(), new("open-jarvan-duel", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []), default);
        Assert.True(moved.Accepted, moved.ErrorMessage); var state = moved.State;
        if (branch == "enemy-source") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "BATTLEFIELD", "BF");
        if (branch == "absent-source") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        if (branch == "hidden-source") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsFaceDown = true } } };
        var no = branch == "ordinary-equipment" ? "SFD·059/221" : Gear;
        if (branch == "ordinary-equipment") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", no, "P1") } };
        if (branch == "foreign-owner") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P2" } } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("GEAR", no, [],
            Destination: branch == "ordinary-equipment" ? "BASE" : "BATTLEFIELD:BF"));
    }

    [Fact]
    public async Task SwiftDoesNotPermitAReactionWhileAnotherSpellIsOnTheStack()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "SPELL", "OGN·048/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        Assert.NotEmpty(game.State.StackItems);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("GEAR", Gear, [], Destination: "BATTLEFIELD:BF"));
        await game.Replay();
    }

    [Theory]
    [InlineData("local", true)] [InlineData("base", false)] [InlineData("other", false)]
    [InlineData("enemy", false)] [InlineData("ordinary", false)]
    public async Task OnlyLocalFriendlyDeploymentEquipmentEarnsTheConquestRune(string branch, bool expected)
    {
        var state = Position();
        if (branch == "other") state = RadGearTestPosition.Put(state, "OTHER", "OGN·296/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "LOCAL", branch == "ordinary" ? "SFD·022/221" : Gear,
            branch == "enemy" ? "P2" : "P1", branch is "base" or "ordinary" ? "BASE" : "BATTLEFIELD", branch == "other" ? "OTHER" : "BF");
        var local = state.CardObjects["LOCAL"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LOCAL"] = branch == "ordinary"
                ? local with { Tags = [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], AttachedToObjectId = "UNIT" }
                : local with { Tags = [..local.Tags, CardEquipmentKeywordNames.Deploy] } } };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Contains("CALLED", game.State.PlayerZones["P1"].RuneDeck);
        if (expected) {
            var item = Assert.Single(game.State.StackItems, i => i.ConquestSource is not null);
            Assert.True(item.ConquestSource!.HadRequiredFriendlyEquipmentHere);
            var forged = item with { ConquestSource = item.ConquestSource with { HadRequiredFriendlyEquipmentHere = null } };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
            await game.Top();
            Assert.Contains("CALLED", game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects["CALLED"].IsExhausted);
        } else Assert.DoesNotContain(game.State.StackItems, i => i.ConquestSource is not null);
        await game.Replay();
    }
}
