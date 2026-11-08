using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VexStunMovementTests
{
    private const string Vex = "UNL-055/219";
    private const string Stun = "OGN·050/298";
    private const string Movement = "ENEMY_STUNNED_MOVE_SOURCE_TO_BATTLEFIELD";

    private static MatchState Position(string printing = Vex, string sourceZone = "BASE")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", printing, "P1", sourceZone,
            sourceZone == "BATTLEFIELD" ? "BF" : null);
        state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "STUN", Stun, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY-RETURN", "OGN·104/298", "P2", "HAND");
        return state;
    }

    private static async Task StunEnemy(Driver game, string target = "ENEMY")
    {
        await game.Act("P1", new PlayCardCommand("STUN", Stun, [target]));
        await game.Top();
    }

    private static Task Decide(Driver game, string option = "MOVE")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(CoreRuleEngine.OptionalTriggerWindow, choice.ChoiceWindow);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            option == "MOVE" ? ["HOST"] : []));
    }

    private static async Task Priority(Driver game, string player)
    {
        if (game.State.PriorityPlayerId != player)
            await game.Act(Assert.IsType<string>(game.State.PriorityPlayerId), new PassPriorityCommand());
        Assert.Equal(player, game.State.PriorityPlayerId);
    }

    [Theory, InlineData("UNL-055/219"), InlineData("UNL-055a/219")]
    public async Task EachActualPrintingUsesTheSharedEnemyStunMovementProfile(string printing)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == printing);
        Assert.Equal("玩世不恭", card.SubTitle);
        Assert.Contains("当你{{眩晕}}战场上的一名敌方单位时，你可以选择将我移动到该战场。", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(printing, out var behavior));
        Assert.True(behavior.SourceMayMoveToStunnedEnemyBattlefield);
        var game = new Driver(Position(printing)); await StunEnemy(game);
        await Decide(game); await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("BASE", false), InlineData("BATTLEFIELD", true)]
    public async Task TheIndependentOptionalSkillMovesTheSameUnitAfterResponsesWithoutExhaustingIt(string zone, bool exhausted)
    {
        var state = Position(sourceZone: zone);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = exhausted } } };
        var game = new Driver(state); await StunEnemy(game);
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(zone, game.State.ObjectLocations["HOST"].Zone);
        var pending = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["HOST"]));
        await Decide(game);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(Movement, item.FieldContext!.Kind);
        Assert.Equal("OTHER", item.FieldContext.StunObserver!.Battlefield!.ObjectId);
        Assert.Equal(zone, game.State.ObjectLocations["HOST"].Zone);
        Assert.NotNull(game.State.PriorityPlayerId);
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(exhausted, game.State.CardObjects["HOST"].IsExhausted);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DecliningRemovesOnlyTheMovementSkillAndKeepsTheActualStun()
    {
        var game = new Driver(Position()); await StunEnemy(game); await Decide(game, "DECLINE");
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("HAND"), InlineData("GRAVEYARD"), InlineData("BANISHED")]
    public async Task AnInactivePrintingDoesNotObserveAnActualEnemyStun(string sourceZone)
    {
        var game = new Driver(Position(sourceZone: sourceZone)); await StunEnemy(game);
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Equal(sourceZone, game.State.ObjectLocations["HOST"].Zone);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("base"), InlineData("friendly"), InlineData("already-stunned"), InlineData("enemy-source")]
    public async Task OnlyANewStunOfAnEnemyBattlefieldUnitByTheSourceControllerEarnsTheSkill(string branch)
    {
        var state = Position();
        if (branch == "base") state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", "BASE");
        if (branch == "friendly") state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        if (branch == "already-stunned") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = state.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        if (branch == "enemy-source") state = RadGearTestPosition.Put(state, "HOST", Vex, "P2", "BASE");
        var game = new Driver(state); await StunEnemy(game, branch == "friendly" ? "ALLY" : "ENEMY");
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningTheStunnedUnitInResponseRetainsItsOriginalBattlefield()
    {
        var game = new Driver(Position()); await StunEnemy(game); await Decide(game); await Priority(game, "P2");
        await game.Act("P2", new PlayCardCommand("ENEMY-RETURN", "OGN·104/298", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningTheSourceInResponseCannotMoveItsHiddenNewIncarnation()
    {
        var game = new Driver(Position()); await StunEnemy(game); await Decide(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var next = Assert.Single(game.State.StackItems);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > next.FieldContext!.SourceGeneration);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.PromptFor("P2")));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ActualSongCopiesEarnIndependentSkillsButMovingTheSourceTwiceToTheSameFieldIsOneMove()
    {
        var state = RadGearTestPosition.Put(Position(), "GEAR", "SFD·059/221", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        await StunEnemy(game);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        await Decide(game); await Decide(game);
        Assert.Equal(2, game.State.StackItems.Count);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("OTHER", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task CounteringTheActualStunDoesNotEarnAMovementSkill()
    {
        var state = RadGearTestPosition.Put(Position(), "COUNTER", "OGN·045/298", "P2", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("STUN", Stun, ["ENEMY"]));
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await Priority(game, "P2"); await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [spell]));
        await game.Top();
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await game.ColdCheckpoint();
    }
}
