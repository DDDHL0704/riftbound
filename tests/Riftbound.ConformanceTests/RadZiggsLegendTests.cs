using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadZiggsLegendTests
{
    private const string Ability = LegendActionAbilityCatalog.ZiggsLegendAbilityId;

    private static MatchState Position(string cardNo = "RAD·141/167")
    {
        var state = VenUnitEntryBasicTests.Position("RAD·142/167");
        return LocalDestructionRecallTests.At(state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", cardNo, "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND")
            }
        }, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
    }

    private static LegendActCommand Activate(string target = "OTHER")
        => new("LEGEND", Ability, [target], ["SPEND_MANA:1"]);

    [Theory]
    [InlineData("RAD·141/167", 39649L, "BF")]
    [InlineData("RAD·141/167", 39649L, "OTHER")]
    [InlineData("RAD·169*/167", 39678L, "BF")]
    [InlineData("RAD·169*/167", 39678L, "OTHER")]
    [InlineData("RAD·169/167", 39679L, "BF")]
    [InlineData("RAD·169/167", 39679L, "OTHER")]
    public async Task EveryPrintingPaysThenPlaysABombToEitherBattlefieldAfterResponses(string no, long id, string battlefield)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(id, card.Id);
        Assert.Equal("爆破鬼才", card.CardName);
        Assert.Contains(no, LegendActionAbilityCatalog.SourceCardNosForAbility(Ability));
        var game = new Driver(Position(no));
        await game.Act("P1", Activate(battlefield));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendUnitEffect);
        await game.Top();
        var bomb = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", battlefield), game.State.ObjectLocations[bomb.ObjectId]);
        Assert.Contains(CardObjectTags.EquipmentCard, bomb.Tags);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
        Assert.False(bomb.IsExhausted);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_TOKEN_CREATED"
            && Equals(entry.Event.Payload.GetValueOrDefault("battlefieldObjectId"), battlefield));
        await game.Replay();
    }

    [Fact]
    public async Task CreatedBombUsesTheSharedDestructionDamageAndTokenRemoval()
    {
        var game = new Driver(Position());
        await game.Act("P1", Activate());
        await game.Top();
        var bomb = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·142/167", [bomb.ObjectId]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(bomb.ObjectId));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("duplicate")]
    [InlineData("enemy")]
    [InlineData("exhausted")]
    [InlineData("mana")]
    [InlineData("power")]
    public async Task IllegalActivationLeavesResourcesAndLegendUnchanged(string scenario)
    {
        var state = Position();
        if (scenario == "exhausted") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true }
            }
        };
        if (scenario is "mana" or "power") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = scenario == "mana" ? new(0, 20) : new(20, 0)
            }
        };
        var command = Activate(scenario == "unit" ? "ALLY" : "OTHER");
        if (scenario == "duplicate") command = command with { TargetObjectIds = ["OTHER", "OTHER"] };
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-bomb-activation", scenario == "enemy" ? "P2" : "P1", CommandTypes.LegendAct), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("generation")]
    [InlineData("target")]
    public async Task RecoveryRejectsForgedCapturedActivation(string scenario)
    {
        var game = new Driver(Position());
        await game.Act("P1", Activate());
        var item = Assert.Single(game.State.StackItems);
        var forged = scenario == "source" ? item with { CardNo = "RAD·139/167" }
            : scenario == "generation" ? item with { LegendUnitEffect = item.LegendUnitEffect! with { SourceGeneration = 99 } }
            : item with { TargetObjectIds = ["ENEMY"], TargetGenerations = new Dictionary<string, long> { ["ENEMY"] = 0 } };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with { StackItems = [forged] }));
    }
}
