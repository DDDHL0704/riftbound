using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadDeployedGearTests
{
    private const string Banner = "RAD·002/167", Hide = "RAD·044/167", Weapons = "RAD·073/167",
        Portal = "RAD·087/167", Poster = "RAD·116/167";

    [Theory]
    [InlineData(Banner, 39492, 1)] [InlineData(Hide, 39539, 4)] [InlineData(Weapons, 39572, 1)]
    [InlineData(Portal, 39587, 2)] [InlineData(Poster, 39621, 1)]
    public async Task OfficialPrintingDeploysToOpposingBattlefieldAndCapturesItsOwnAbility(string no, long id, int mana)
    {
        var official = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, official.Id); Assert.Equal(mana, official.Energy);
        var game = new Driver(Hand(no));
        await game.Act("P1", new PlayCardCommand("CARD", no, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Equal("OTHER", game.State.ObjectLocations["CARD"].BattlefieldObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["CARD"].Tags);
        Assert.Equal(30 - mana, game.State.RunePools["P1"].Mana);
        if (no is Hide or Weapons or Poster) Assert.Single(game.State.StackItems, i => i.SourceObjectId == "CARD" && i.UnitEvent is not null);
        await game.Replay();
    }

    [Theory]
    [InlineData(Banner)] [InlineData(Hide)] [InlineData(Weapons)] [InlineData(Portal)] [InlineData(Poster)]
    public async Task BaseAndMissingBattlefieldAreRejectedWithoutSpendingResources(string no)
    {
        foreach (var destination in new[] { "BASE", "BATTLEFIELD:missing" })
            await Reject(Hand(no), "P1", new PlayCardCommand("CARD", no, [], Destination: destination));
    }

    [Theory]
    [InlineData(Banner, "强攻")] [InlineData(Hide, "坚守2")] [InlineData(Portal, "游走")]
    public void FriendlyKeywordAuraTracksLocationControlAndAUnitSourceItself(string no, string keyword)
    {
        var state = Put(Create(), "CARD", no, "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        var aura = Assert.Single(state.ContinuousEffects, e => e.SourceObjectId == "CARD" && e.TargetObjectId == "ALLY");
        Assert.EndsWith(":" + keyword, aura.EffectId);
        Assert.DoesNotContain(state.ContinuousEffects, e => e.SourceObjectId == "CARD" && e.TargetObjectId == "ENEMY");
        var converted = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CARD"] = state.CardObjects["CARD"] with { Power = 3, Tags = [..state.CardObjects["CARD"].Tags, CardObjectTags.UnitCard] } } };
        Assert.Contains(converted.ContinuousEffects, e => e.SourceObjectId == "CARD" && e.TargetObjectId == "CARD");
        state = RadUnitEffectTestFixture.Place(state, "CARD", "P2", "BATTLEFIELD", "BF");
        Assert.DoesNotContain(state.ContinuousEffects, e => e.SourceObjectId == "CARD" && e.TargetObjectId == "ALLY");
        Assert.Contains(state.ContinuousEffects, e => e.SourceObjectId == "CARD" && e.TargetObjectId == "ENEMY");
        state = RadUnitEffectTestFixture.Place(state, "CARD", "P2", "BASE");
        Assert.DoesNotContain(state.ContinuousEffects, e => e.SourceObjectId == "CARD");
    }

    [Fact]
    public async Task PortalAuraPermitsBattlefieldMovementOnlyForFriendlyUnitsHere()
    {
        var state = Put(Create(), "CARD", Portal, "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task HideOffersAnEnemyMoveAfterEntryAndAllowsDeclining(bool accept)
    {
        var game = new Driver(Hand(Hide));
        await game.Act("P1", new PlayCardCommand("CARD", Hide, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(new[] { "ENEMY" }, game.State.PendingCardChoice!.LegalObjectIds);
        await Reject(game.State, "P1", Choice(game.State, ["ALLY"]));
        await Choose(game, accept ? ["ENEMY"] : []);
        if (accept) { Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone); await game.Top(); }
        Assert.Equal(accept ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ENEMY"].Zone);
        await game.Replay();
    }

    [Fact]
    public async Task WeaponsCanGiveABoonToAFriendlyUnitOutsideItsBattlefield()
    {
        var initial = Hand(Weapons);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("CARD", Weapons, [], Destination: "BATTLEFIELD:BF"));
        Assert.Contains("ALLY", game.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("ENEMY", game.State.PendingCardChoice!.LegalObjectIds);
        await Choose(game, ["ALLY"]); await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal(initial.CardObjects["ALLY"].Power + 1, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PortalCanPayOrangePowerToReadyAnEnemyHereOrDecline(bool pay)
    {
        var state = RadUnitEffectTestFixture.Place(Hand(Portal), "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { IsExhausted = true } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(30, 0,
                new Dictionary<string, int> { ["orange"] = 2 }) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", Portal, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(new[] { "ENEMY" }, game.State.PendingCardChoice!.LegalObjectIds);
        await Choose(game, ["ENEMY"]);
        var pool = game.State.RunePools["P1"].TotalPower;
        await Pay(game, pay);
        if (pay) { Assert.True(game.State.CardObjects["ENEMY"].IsExhausted); await game.Top(); }
        Assert.Equal(!pay, game.State.CardObjects["ENEMY"].IsExhausted);
        Assert.Equal(pool - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PosterCreatesASleepingOnePowerMinionOnlyAfterPayingAndResponses(bool pay)
    {
        var game = new Driver(Hand(Poster));
        await game.Act("P1", new PlayCardCommand("CARD", Poster, [], Destination: "BATTLEFIELD:BF"));
        Assert.DoesNotContain(game.State.CardObjects.Values, TokenObjectRules.IsToken);
        var mana = game.State.RunePools["P1"].Mana;
        await Pay(game, pay);
        if (pay) { Assert.DoesNotContain(game.State.CardObjects.Values, TokenObjectRules.IsToken); await game.Top(); }
        var tokens = game.State.CardObjects.Values.Where(TokenObjectRules.IsToken).ToArray();
        Assert.Equal(pay ? 1 : 0, tokens.Length);
        if (pay) { var token = Assert.Single(tokens); Assert.Equal(1, token.Power); Assert.True(token.IsExhausted);
            Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base); }
        Assert.Equal(mana - (pay ? 1 : 0), game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData(Weapons)] [InlineData(Poster)]
    public async Task FriendlyEquipmentHereObservesConquestWithoutBeingAConqueringUnit(string no)
    {
        var state = Put(ConquestLifecycleRegressionTests.State("OGN·278/298", 1), "CARD", no, "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "CARD" && i.UnitEvent is not null);
        if (no == Weapons) { await Choose(game, ["UNIT"]); await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags); }
        else { await Pay(game, true); await game.Top(); Assert.Contains(game.State.CardObjects.Values, TokenObjectRules.IsToken); }
        await game.Replay();
    }

    [Theory]
    [InlineData(Banner, true, "强攻", 2)]
    [InlineData(Hide, false, "坚守", 4)]
    public void MultipleSourcesAddToPrintedCombatKeywordsAndStopAfterMoving(string no, bool attacking, string keyword, int bonus)
    {
        var state = Put(Create(), "CARD", no, "P1", "BATTLEFIELD", "BF");
        state = Put(state, "SECOND", no, "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsAttacking = attacking, IsDefending = !attacking,
                Tags = [CardObjectTags.UnitCard, keyword] } } };
        var battle = new BattleState(true, "battle:BF", "BF", attacking ? ["ALLY"] : [], attacking ? [] : ["ALLY"],
            new Dictionary<string, string> { ["ALLY"] = "P1" });
        var basePower = state.CardObjects["ALLY"].Power;
        Assert.Equal(basePower + 1 + bonus, CoreRuleEngine.ResolveAssignmentBattleCombatPower(state, battle, "ALLY", out _, out _));
        state = RadUnitEffectTestFixture.Place(state, "SECOND", "P1", "BATTLEFIELD", "OTHER");
        Assert.Equal(basePower + 1 + bonus / 2, CoreRuleEngine.ResolveAssignmentBattleCombatPower(state, battle, "ALLY", out _, out _));
        state = RadUnitEffectTestFixture.Place(state, "CARD", "P2", "BATTLEFIELD", "BF");
        Assert.Equal(basePower + 1, CoreRuleEngine.ResolveAssignmentBattleCombatPower(state, battle, "ALLY", out _, out _));
    }

    private static MatchState Put(MatchState state, string id, string no, string owner, string zone, string? field = null)
    {
        state = RadGearTestPosition.Put(state, id, no, owner, zone, field);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { Tags = [..state.CardObjects[id].Tags, CardEquipmentKeywordNames.Deploy] } } };
    }

    private static MatchState Hand(string no) => Put(Create(), "CARD", no, "P1", "HAND");
    private static ChooseCardsCommand Choice(MatchState state, IReadOnlyList<string> selected)
        => new(state.PendingCardChoice!.ChoiceId, state.PendingCardChoice.ChoiceWindow, selected);
    private static Task Choose(Driver game, IReadOnlyList<string> selected) => game.Act("P1", Choice(game.State, selected));
    private static Task Pay(Driver game, bool accept)
        => game.Act("P1", new PayCostCommand(game.State.PendingPayment!.PaymentId,
            game.State.PendingPayment.PaymentWindow, [accept ? "PAY" : "DECLINE"]));
}
