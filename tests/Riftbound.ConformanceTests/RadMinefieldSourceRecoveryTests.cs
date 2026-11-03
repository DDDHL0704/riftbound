using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMinefieldSourceRecoveryTests
{
    [Fact]
    public async Task RecyclingTheResolvedSourceDoesNotRemoveTheDelayedRebuild()
    {
        var initial = AttackDamageDistributionTests.Spell(RadHextechMinefieldTests.Board(), "RECYCLE", "UNL-103/219", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·142/167", ["B1"]));
        await game.Top();
        var sourceGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new EndTurnCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("BOMB_REBUILD_BATTLEFIELD", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OTHER"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["UNIT"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].MainDeck);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > sourceGeneration);
        var skill = Assert.Single(game.State.StackItems, item => item.BombRebuild is not null);
        var context = skill.BombRebuild!;
        foreach (var forged in new[] {
            context with { Source = new("RECYCLE", context.Source.Generation) },
            context with { CreatedAtTick = game.State.Tick + 1 },
            context with { BombTokenFactoryCardNo = "UNKNOWN-FACTORY" } })
        {
            var bad = game.State with { StackItems = [skill with { BombRebuild = forged }] };
            Assert.Contains("invalid captured bomb rebuild stack", MatchRecoveryValidator.ValidateComponentStateAndViews(bad));
        }
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        Assert.DoesNotContain("creatingStackItemId", views, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceGeneration", views, StringComparison.Ordinal);
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, bomb => bomb.ControllerId == "P1"
            && bomb.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo
            && game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "OTHER");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
