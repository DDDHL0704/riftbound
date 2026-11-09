using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentSacrificePlayTests
{
    private const string Thug = "SFD·160/221";
    private static MatchState Position(string targetController = "P2", bool stolenCost = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "THUG", Thug, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COST-GEAR", "OGN·023/298", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "TARGET-GEAR", "SFD·139/221", targetController, "BASE");
        if (stolenCost) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["COST-GEAR"] = state.CardObjects["COST-GEAR"] with { OwnerId = "P2" } } };
        return state;
    }

    private static PlayCardCommand Play(string[]? costs = null) => new("THUG", Thug, [], OptionalCosts: costs ?? []);
    private static readonly string[] Fee = ["DESTROY_FRIENDLY_PERMANENT:COST-GEAR"];

    [Fact]
    public async Task DecliningTheAdditionalEquipmentCostEntersNormallyWithoutOfferingTheConditionalSkill()
    {
        var game = new Driver(Position()); var prompt = JsonSerializer.Serialize(game.PromptFor("P1"));
        Assert.Contains(Fee[0], prompt); Assert.DoesNotContain("DESTROY_FRIENDLY_PERMANENT:ALLY", prompt);
        await game.Act("P1", Play());
        Assert.Contains("THUG", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["THUG"].IsExhausted);
        Assert.Contains("COST-GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P2", false)] [InlineData("P1", true)] [InlineData("P2", true)]
    public async Task AControlledEquipmentIsDestroyedBeforeEntryThenAnyEquipmentIsConfirmedForTheIndependentSkill(string targetController, bool stolenCost)
    {
        var game = new Driver(Position(targetController, stolenCost)); var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", Play(Fee));
        Assert.Contains("COST-GEAR", game.State.PlayerZones[stolenCost ? "P2" : "P1"].Graveyard);
        Assert.Contains("THUG", game.State.PlayerZones["P1"].Base); Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("TARGET-GEAR", choice.LegalObjectIds); Assert.DoesNotContain("COST-GEAR", choice.LegalObjectIds);
        Assert.DoesNotContain("ALLY", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["TARGET-GEAR"]));
        Assert.Contains("TARGET-GEAR", game.State.PlayerZones[targetController].Base);
        await game.Top(); Assert.Contains("TARGET-GEAR", game.State.PlayerZones[targetController].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("unit")] [InlineData("enemy")] [InlineData("hand")] [InlineData("hidden")]
    [InlineData("multiple")] [InlineData("battlefield")] [InlineData("source")]
    public async Task UnpaidOrNonEquipmentChoicesCannotEnterPayOrChangeTheState(string branch)
    {
        var state = Position();
        if (branch == "hand") state = RadUnitEffectTestFixture.Place(state, "COST-GEAR", "P1", "HAND");
        if (branch == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["COST-GEAR"] = state.CardObjects["COST-GEAR"] with { IsFaceDown = true } } };
        var id = branch switch { "unit" => "ALLY", "enemy" => "TARGET-GEAR", "battlefield" => "BF", "source" => "THUG", _ => "COST-GEAR" };
        await RadGearTestPosition.Reject(state, "P1", Play(branch == "multiple"
            ? [Fee[0], "DESTROY_FRIENDLY_PERMANENT:ALLY"] : ["DESTROY_FRIENDLY_PERMANENT:" + id]));
    }

    [Fact]
    public async Task ReturningTheEnteredThugDoesNotRefundItsPaidCostOrCancelItsConfirmedEquipmentDestruction()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1"));
        await game.Act("P1", Play(Fee)); var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["TARGET-GEAR"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["THUG"])); await game.Top();
        Assert.Contains("THUG", game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.Contains("COST-GEAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("TARGET-GEAR", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task PayingTheCostStillCompletesEntryWhenNoEquipmentTargetRemains()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "TARGET-GEAR", "P2", "HAND"));
        await game.Act("P1", Play(Fee)); Assert.Contains("THUG", game.State.PlayerZones["P1"].Base);
        Assert.Contains("COST-GEAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAConditionalSkillWhoseAdditionalCostWasErased()
    {
        var game = new Driver(Position()); await game.Act("P1", Play(Fee));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            StackItems = [item with { OptionalCosts = [] }] }));
    }
}
