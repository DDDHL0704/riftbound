using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadFollowupSpellTests
{
    [Theory]
    [InlineData("RAD·070/167", 39569L, "先打再跟", 3,
        "{{迅捷}}（可在你的回合或法术对决中打出。）\n如果你已在本回合内将单位或装备打出过到战场，则此牌的费用减少{{2}}。\n给予一名单位在本回合内{{S}}+3。")]
    [InlineData("RAD·101/167", 39603L, "无法拒绝的提议", 2,
        "{{迅捷}}（可在你的回合或法术对决中打出。）\n摧毁一件装备。其控制者打出一个休眠的“金币”装备指示物。（其具有“{{反应>}} 摧毁此牌，{{T}}：{{获得}}{{A}}。”）")]
    [InlineData("RAD·121/167", 39626L, "组合拳", 3,
        "{{迅捷}}（可在你的回合或法术对决中打出。）\n给予一名友方单位{{增益}}并{{眩晕}}一名敌方单位。（如果该友方单位未拥有增益，则其获得一个{{S}}+1增益。使该敌方单位在本回合内无法造成战斗伤害。）")]
    public async Task ExactOfficialPrintingsAndSharedBehaviorAreAdmitted(
        string no, long id, string name, int mana, string text)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal(text, card.CardEffect);
        Assert.Equal(mana, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal("法术", card.CardCategoryName);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(mana, behavior.ManaCost);
        Assert.Equal(no == "RAD·121/167" ? 2 : 1, behavior.RequiredTargetCount);
        Assert.True(behavior.CanPlayDuringSpellDuel);
        Assert.Equal(BehaviorImplementationStatuses.Implemented,
            Assert.Single(BehaviorSpecCatalogBuilder.Build(
                await OfficialCardCatalog.LoadRuntimeAsync(),
                FunctionalUnitBuilder.Build((await OfficialCardCatalog.LoadRuntimeAsync()).Cards),
                OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
                    (await OfficialCardCatalog.LoadRuntimeAsync()).Cards,
                    CardBehaviorRegistry.GetAll().Select(definition => new ImplementedCardBehavior(
                        definition.CardNo, definition.EffectKind, definition.DisplayName,
                        CardBehaviorRegistry.TriggerEffectKinds(definition))).ToArray())), s => s.CardNo == no).Status);
    }

    [Fact]
    public async Task PlayThenFollowReducesOnlyAfterAUnitOrEquipmentWasPlayedAndBuffExpires()
    {
        var initial = AddFirstUnit(VenUnitEntryBasicTests.Position("RAD·070/167"));
        var before = new PlayCardCommand("UNIT", "RAD·070/167", ["ALLY"]);
        var fullQuote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, before));
        Assert.True(fullQuote.CanPay, fullQuote.Message); Assert.Equal(3, fullQuote.Cost!.Mana);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "VEN·002", [], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        var reducedQuote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, before));
        Assert.True(reducedQuote.CanPay, reducedQuote.Message); Assert.Equal(1, reducedQuote.Cost!.Mana);
        await game.Act("P1", before); await game.Top();
        Assert.Equal(10, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task PlayThenFollowDoesNotTreatAnEarlierSpellAsAUnitOrEquipment()
    {
        var state = VenUnitEntryBasicTests.Position("RAD·070/167") with {
            PlayerCardsPlayedThisTurn = new Dictionary<string, int> { ["P1"] = 1, ["P2"] = 0 },
            UntilEndOfTurnEffects = ["PLAYED_SPELL_THIS_TURN:P1"]
        };
        var command = new PlayCardCommand("UNIT", "RAD·070/167", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        var game = new Driver(state);
        await game.Act("P1", command); await game.Top();
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        Assert.Equal(state.CardObjects["ALLY"].Power + 3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task IrresistibleOfferDestroysOpponentsEquipmentAndGivesItsControllerDormantGold()
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·101/167");
        var zones = initial.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Base = ["ALLY"] };
        zones["P2"] = zones["P2"] with { Base = ["ENEMY", "GEAR"] };
        var cards = initial.CardObjects.ToDictionary();
        cards["GEAR"] = cards["GEAR"] with { OwnerId = "P2", ControllerId = "P2" };
        var locations = initial.ObjectLocations.ToDictionary(); locations["GEAR"] = new("P2", "BASE");
        var game = new Driver(initial with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations });
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·101/167", ["GEAR"]));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var tokenId = Assert.Single(game.Events().Where(e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED")
            .Select(e => Assert.IsType<string>(e.Event.Payload["tokenObjectId"])));
        Assert.Contains(tokenId, game.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(tokenId, game.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", game.State.CardObjects[tokenId].OwnerId);
        Assert.Equal("P2", game.State.CardObjects[tokenId].ControllerId);
        Assert.True(game.State.CardObjects[tokenId].IsExhausted);
        Assert.Contains(CardObjectTags.EquipmentCard, game.State.CardObjects[tokenId].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")]
    [InlineData("ENEMY")]
    [InlineData("BF")]
    [InlineData("GRAVE")]
    public async Task IrresistibleOfferRejectsAnythingThatIsNotAnEquipment(string target)
        => await Reject(VenUnitEntryBasicTests.Position("RAD·101/167"),
            new PlayCardCommand("UNIT", "RAD·101/167", [target]));

    [Fact]
    public async Task ComboGrantsOnlyTheFriendlyUnitAPlusOneBoonAndStunsTheEnemy()
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·121/167");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·121/167", ["BF", "OTHER"]));
        await game.Top();
        Assert.Equal(initial.CardObjects["BF"].Power + 1, game.State.CardObjects["BF"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["BF"].Tags);
        Assert.Contains("STUNNED", game.State.CardObjects["OTHER"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["OTHER"].Tags);
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["OTHER"].UntilEndOfTurnEffects);
        Assert.Equal(initial.CardObjects["BF"].Power, game.State.CardObjects["BF"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task ComboDoesNotAddAnotherPowerWhenTheFriendlyUnitAlreadyHasABoon()
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·121/167");
        var cards = initial.CardObjects.ToDictionary();
        cards["BF"] = cards["BF"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Boon] };
        var game = new Driver(initial with { CardObjects = cards });
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·121/167", ["BF", "OTHER"])); await game.Top();
        Assert.Equal(initial.CardObjects["BF"].Power, game.State.CardObjects["BF"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["BF"].Tags);
        Assert.Contains("STUNNED", game.State.CardObjects["OTHER"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("OTHER", "BF")]
    [InlineData("BF", "BF")]
    [InlineData("BF", "GEAR")]
    public async Task ComboRejectsWrongTargetOrderTypesAndControllers(string first, string second)
        => await Reject(VenUnitEntryBasicTests.Position("RAD·121/167"),
            new PlayCardCommand("UNIT", "RAD·121/167", [first, second]));

    private static MatchState AddFirstUnit(MatchState state)
    {
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards["FIRST"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIRST", "VEN·002", "P1");
        locations["FIRST"] = new("P1", "HAND");
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Hand = ["UNIT", "FIRST"] };
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-followup", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
