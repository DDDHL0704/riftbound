using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class EvelynnFaceDownPlayMovementTests
{
    private const string Evelynn = "UNL-141/219";

    private static async Task<Driver> Ready(bool ownTurn = true, bool enemy = true,
        string? enemyBattlefield = null, bool anotherFriendlyBattlefield = false)
    {
        var state = StandbySourceRulesTests.Position(Evelynn);
        if (enemyBattlefield is not null || anotherFriendlyBattlefield)
            state = RadGearTestPosition.Put(state, "OTHER", "OGN·296/298", "P2", "BATTLEFIELD", "OTHER");
        if (anotherFriendlyBattlefield)
        {
            state = RadGearTestPosition.Put(state, "FOOTHOLD", "OGN·001/298", "P1", "BATTLEFIELD", "OTHER");
            // A controlled opening declares its current control before its
            // first command; the physical battlefield still belongs to P2.
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1" } } };
        }
        if (enemy) state = RadGearTestPosition.Put(state, "ENEMY", "OGN·096/298", "P2",
            enemyBattlefield is null ? "BASE" : "BATTLEFIELD", enemyBattlefield);
        state = RadGearTestPosition.Put(state, "REPLY", "OGN·048/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "MOVE-SOURCE", "VEN·034", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "MOVE-ENEMY", "OGN·043/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "P1-GREEN", "UNL-R02", "P1", "BASE");
        for (var i = 1; i <= 3; i++)
            state = RadGearTestPosition.Put(state, "P1-FUND-" + i, "UNL-R03", "P1", "BASE");
        var game = new Driver(state) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new HideCardCommand("SHADOW", Evelynn, "BATTLEFIELD:F", ["STANDBY_A"]));
        await FaceDownPlayTestDriver.NextTurn(game);
        if (ownTurn) await FaceDownPlayTestDriver.NextTurn(game);
        game.RecoverAfterEveryCommand = true;
        await game.ColdCheckpoint();
        return game;
    }

    [Fact]
    public async Task AnActualOwnTurnFaceDownPlayEarnsAnOptionalIndependentEnemyMove()
    {
        var game = await Ready();
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal("SHADOW", choice.SourceObjectId);
        Assert.Contains("ENEMY", choice.LegalObjectIds);
        Assert.DoesNotContain("HOST", choice.LegalObjectIds);
        await RadGearTestPosition.Reject(game.State, "P1",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await RadGearTestPosition.Reject(game.State, "P2",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await RadGearTestPosition.Reject(game.State, "P1",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY", "ENEMY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(Evelynn, skill.UnitEvent!.CardNo);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal("F", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnActualOpponentsTurnFaceDownPlayDoesNotEarnTheOwnTurnSkill()
    {
        var game = await Ready(ownTurn: false);
        await game.TapRunes("P2", game.State.PlayerZones["P2"].Base.Where(id =>
            game.State.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsExhausted).ToArray());
        await game.Act("P2", new RecycleRuneCommand("SHADOW-P2-RUNE-1"));
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, skill => skill.UnitEvent?.CardNo == Evelynn);
        Assert.DoesNotContain(game.State.TriggerQueue, skill => skill.UnitEvent?.CardNo == Evelynn);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheOptionalMoveCanBeDeclinedWithoutMovingItsEnemy()
    {
        var game = await Ready();
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.DoesNotContain(game.State.StackItems, skill => skill.UnitEvent?.CardNo == Evelynn);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task HavingNoEnemyDoesNotLeaveAPhantomMovementSkill()
    {
        var game = await Ready(enemy: false);
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        if (game.State.PendingCardChoice is { } choice)
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.DoesNotContain(game.State.StackItems, skill => skill.UnitEvent?.CardNo == Evelynn);
        Assert.DoesNotContain(game.State.TriggerQueue, skill => skill.UnitEvent?.CardNo == Evelynn);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ARealReturnOfEvelynnDoesNotMoveHerEnemyToAStaleBattlefield()
    {
        var game = await Ready();
        await game.Act("P1", new TapRuneCommand("P1-FUND-1"));
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (game.State.PriorityPlayerId is { } next && next != "P1")
            await game.Act(next, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SHADOW"]));
        await game.Top();
        Assert.Contains("SHADOW", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems, skill => skill.UnitEvent?.CardNo == Evelynn);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AReturnedCardReplayedFromHandCannotReuseItsOldFaceDownCondition()
    {
        var game = await Ready();
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.Act("P1", new TapRuneCommand("P1-FUND-1"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SHADOW"]));
        await game.Top();
        await game.Act("P1", new TapRuneCommand("P1-FUND-2"));
        await game.Act("P1", new TapRuneCommand("P1-FUND-3"));
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        Assert.Contains("SHADOW", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, skill => skill.UnitEvent?.CardNo == Evelynn);
        Assert.DoesNotContain(game.State.TriggerQueue, skill => skill.UnitEvent?.CardNo == Evelynn);
        await game.ColdCheckpoint();
    }
}
