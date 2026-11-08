using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class KatarinaFaceDownObserverTests
{
    private const string Katarina = "UNL-023/219";
    private const string Shadow = "KATARINA-PRIVATE-SHADOW";

    private static MatchState Position(string card = "OGN·135/298", string sourceZone = "BASE", bool exhausted = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "HOST", Katarina, "P1", sourceZone,
            sourceZone == "BATTLEFIELD" ? "BF" : null);
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, Shadow, card, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY-RETURN", "OGN·104/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "OPENER", "OGN·048/298", "P2", "HAND");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 8; i++) state = RadGearTestPosition.Put(state, player + "-R" + i, "UNL-R03", player, "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = exhausted } } };
    }

    private static Task Hide(Driver game, string card = "OGN·135/298", string field = "BF", string source = Shadow)
        => game.Act("P1", new HideCardCommand(source, card, "BATTLEFIELD:" + field, ["STANDBY_A"]));

    private static async Task Order(Driver game)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
    }

    private static async Task HiddenNextTurn(Driver game, string card, IReadOnlyList<string>? wards = null)
    {
        await Hide(game, card); await Order(game);
        while (game.State.StackItems.Count > 0) await game.Top();
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.TurnPlayerId); Assert.Null(game.State.TurnStartStep);
        Assert.True(game.State.CardObjects[Shadow].IsFaceDown);
        await Resources(game, "P2");
        if (wards is { Count: > 0 })
        {
            await game.TapRunes("P2", Enumerable.Range(4, wards.Count * 2).Select(i => "P2-R" + i));
            foreach (var id in wards)
            {
                await game.Act("P2", new PlayCardCommand(id, "OGN·013/298", []));
                Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects[id].Tags);
            }
        }
        await game.Act("P2", new PlayCardCommand("OPENER", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        Assert.Equal("P1", game.State.PriorityPlayerId);
    }

    private static Task FromFaceDown(Driver game, string card, bool reveal = false)
        => game.Act("P1", reveal ? new RevealCardCommand(Shadow, card, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"],
            card == "OGN·083/298" ? "STACK" : "BATTLEFIELD:BF") : new PlayCardCommand(Shadow, card, []));

    private static async Task Target(Driver game, string target = "ENEMY")
    {
        await Order(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }

    private static async Task Priority(Driver game, string player)
    {
        if (game.State.PriorityPlayerId != player)
            await game.Act(Assert.IsType<string>(game.State.PriorityPlayerId), new PassPriorityCommand());
        Assert.Equal(player, game.State.PriorityPlayerId);
    }

    private static async Task Resources(Driver game, string player)
    {
        await game.TapRunes(player, new[] { player + "-R0", player + "-R1", player + "-R2" });
        await game.Act(player, new RecycleRuneCommand(player + "-R3"));
    }

    [Fact]
    public async Task TheActualPrintingDeclaresBothIndependentControllerActionObservers()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == Katarina);
        Assert.Equal("暗里藏刀", card.SubTitle);
        Assert.Equal("当你正面朝下放置一张卡牌时，让我变为活跃状态。\n当你将一张卡牌从正面朝下的状态打出时，对一名敌方单位造成2点伤害。", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Katarina, out var behavior));
        Assert.True(behavior.SourceReadiesWhenControllerHidesCard);
        Assert.Equal(2, behavior.ControllerFaceDownPlayEnemyDamage);
    }

    [Theory, InlineData("BASE"), InlineData("BATTLEFIELD")]
    public async Task ActualHideCompletesBeforeAnIndependentSkillReadiesTheSameSource(string sourceZone)
    {
        var game = new Driver(Position(sourceZone: sourceZone)); await Hide(game);
        Assert.True(game.State.CardObjects[Shadow].IsFaceDown); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(CoreRuleEngine.StandbyHiddenReadyEffect, item.EffectKind);
        Assert.Empty(item.TargetObjectIds); Assert.Null(item.TargetGenerations);
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.DoesNotContain(Shadow, JsonSerializer.Serialize(game.PromptFor("P2")));
        Assert.DoesNotContain(Shadow, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects[Shadow].IsFaceDown); await game.ColdCheckpoint();
    }

    [Theory, InlineData("HAND"), InlineData("GRAVEYARD"), InlineData("BANISHED"), InlineData("enemy")]
    public async Task InactiveOrEnemyControlledCopiesDoNotObserveTheActorsHide(string branch)
    {
        var initial = Position(sourceZone: branch == "enemy" ? "BASE" : branch);
        if (branch == "enemy") initial = RadGearTestPosition.Put(initial, "HOST", Katarina, "P2", "BASE");
        var game = new Driver(initial); await Hide(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ARejectedHideCannotReadyTheSourceOrCreateASkill()
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        await RadGearTestPosition.Reject(initial, "P1", new HideCardCommand(Shadow, "OGN·135/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(initial.CardObjects["HOST"].IsExhausted); Assert.Empty(initial.StackItems);
    }

    [Fact]
    public async Task ReturningTheSourceInResponseDoesNotReadyItsNewHiddenIncarnation()
    {
        var game = new Driver(Position()); await Hide(game);
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.PromptFor("P2")));
        await game.Top(); Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task RepeatedRealHidesHaveNoOncePerTurnLimitAndReadyingDoesNotTargetTheSource()
    {
        var initial = RadGearTestPosition.Put(Position(), "SECOND-FIELD", "OGN·275/298", "P1", "BATTLEFIELD", "SECOND-FIELD");
        initial = RadGearTestPosition.Put(initial, "SECOND-HOLDER", "OGN·219/298", "P1", "BATTLEFIELD", "SECOND-FIELD");
        initial = RadGearTestPosition.Put(initial, "SECOND-SHADOW", "OGN·135/298", "P1", "HAND");
        var game = new Driver(initial); await Hide(game); await game.Top();
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await Hide(game, field: "SECOND-FIELD", source: "SECOND-SHADOW");
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("effectKind"), CoreRuleEngine.StandbyHiddenReadyEffect)));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("OGN·083/298", false)] [InlineData("OGN·083/298", true)]
    [InlineData("OGN·135/298", false)] [InlineData("OGN·135/298", true)]
    [InlineData("SFD·139/221", false)]
    public async Task ActualFaceDownSpellsUnitsAndEquipmentEarnATargetedResponseBeforeDamage(string card, bool reveal)
    {
        var game = new Driver(Position(card)); await HiddenNextTurn(game, card); await FromFaceDown(game, card, reveal);
        // Night's Edge has its own independent attachment confirmation. Do not
        // replace it with a direct state mutation or preselected play target.
        while (game.State.PendingCardChoice is { } pending && pending.EffectKind != CoreRuleEngine.StandbyPlayedEnemyDamageEffect)
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["ALLY"]));
        await Target(game);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.NotNull(game.State.PriorityPlayerId);
        var item = Assert.Single(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect);
        Assert.Equal(game.State.CardObjects["ENEMY"].ObjectGeneration, item.TargetGenerations!["ENEMY"]);
        await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        while (game.State.StackItems.Count > 0) await game.Top();
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task PlayingTheSameSpellFaceUpDoesNotCreateTheFaceDownDamageSkill()
    {
        var game = new Driver(Position("OGN·083/298"));
        await game.Act("P1", new PlayCardCommand(Shadow, "OGN·083/298", []));
        Assert.DoesNotContain(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect);
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.ColdCheckpoint();
    }
}
