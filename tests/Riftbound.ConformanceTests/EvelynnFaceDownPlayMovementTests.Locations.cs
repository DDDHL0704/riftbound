using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class EvelynnFaceDownPlayMovementTests
{
    [Fact]
    public async Task AnEnemyOnADifferentBattlefieldIsMovedToTheSourcesBattlefield()
    {
        var game = await Ready(enemyBattlefield: "OTHER");
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ENEMY", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await game.Top();
        Assert.Equal("F", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnEnemyActuallyMovedHereBeforeThePlayIsNotAtADifferentLocation()
    {
        var game = await Ready();
        await game.Act("P1", new TapRuneCommand("P1-FUND-1"));
        await game.Act("P1", new RecycleRuneCommand("P1-GREEN"));
        await game.Act("P1", new PlayCardCommand("MOVE-ENEMY", "OGN·043/298", ["ENEMY"],
            Destination: "BATTLEFIELD:F"));
        await game.Top();
        Assert.Equal("F", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal("P1", game.State.TurnPlayerId);
        if (game.State.PriorityPlayerId is { } next && next != "P1")
            await game.Act(next, new PassPriorityCommand());
        if (game.State.PriorityPlayerId is null && game.State.ActivePlayerId == "P2")
        {
            // The arriving enemy starts the duel with P2's focus even though
            // this remains P1's turn. Offer a real reaction window to P1.
            await game.TapRunes("P2", game.State.PlayerZones["P2"].Base.Where(id =>
                game.State.CardObjects.TryGetValue(id, out var card)
                    && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsExhausted).Take(2).ToArray());
            await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
            await game.Act("P2", new PassPriorityCommand());
        }
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        if (game.State.PendingCardChoice is { } choice)
        {
            Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.DoesNotContain(game.State.StackItems, skill => skill.UnitEvent?.CardNo == Evelynn);
        Assert.DoesNotContain(game.State.TriggerQueue, skill => skill.UnitEvent?.CardNo == Evelynn);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ARealResponseMovingTheSourceUsesHerCurrentBattlefieldAtResolution()
    {
        var game = await Ready(anotherFriendlyBattlefield: true);
        await game.Act("P1", new TapRuneCommand("P1-FUND-1"));
        await game.Act("P1", new TapRuneCommand("P1-FUND-2"));
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Act("P1", new PlayCardCommand("MOVE-SOURCE", "VEN·034", ["OTHER", "SHADOW"]));
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        var skill = Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Evelynn);
        Assert.Equal("F", skill.UnitEvent!.SourcePlayCondition!.FromStandby!.Battlefield.ObjectId);
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }
}
