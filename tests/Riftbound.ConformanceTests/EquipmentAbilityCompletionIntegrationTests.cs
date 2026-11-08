using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentAbilityCompletionIntegrationTests
{
    [Fact]
    public async Task DestroyingGoldAsTheThiefsIndicatedCostDoesNotUseAGoldAbility()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "PROGRESS", "SFD·075/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "THIEF", "SFD·074/221", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "GOLD", "SFD·033/221", "P1", "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T03", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GOLD"] = factory.CreateObject("GOLD", "P1", "P1") } };
        var game = new Driver(state);
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P1", new PlayCardCommand("THIEF", "SFD·074/221", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GOLD"]));
        Assert.DoesNotContain("GOLD", game.State.CardObjects.Keys);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("THIEF", Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        var gold = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var token) && token.TokenFamilyName == "金币");
        Assert.True(gold.IsExhausted);
        await game.Replay();
    }
}
