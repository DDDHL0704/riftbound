using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    private static async Task<string[]> CreateTokens(Driver game)
    {
        for (var i = 0; i < 2; i++)
        {
            await game.Act("P1", new PlayCardCommand("MAKE-" + i, Sand, []));
            await game.Top();
        }
        var ids = game.State.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.SandSoldier))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, ids.Length); return ids;
    }
    private static Task Select(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    [Theory, InlineData("HOST"), InlineData("ALLY"), InlineData("ENEMY"), InlineData("MAKE-0"), InlineData("BF"), InlineData("duplicate")]
    public async Task EnemyUnitsNonTokensOffFieldCardsAndDuplicateTargetsAreRejectedWithoutMutation(string invalid)
    {
        var game = new Driver(Position("SFD·177/221")); var tokens = await CreateTokens(game);
        await EnterAndAttack(game, "SFD·177/221");
        var choice = game.State.PendingCardChoice!;
        var selected = invalid == "duplicate" ? new[] { tokens[0], tokens[0] } : new[] { invalid };
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, tokens));
        await Select(game, tokens); await game.Top();
        Assert.All(tokens, id => Assert.Equal("OTHER", game.State.ObjectLocations[id].BattlefieldObjectId));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ARealReturnResponseEitherRemovesTheSourceLocationOrOnlyOneChosenToken(bool source)
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await EnterAndAttack(game, "SFD·177/221"); await Select(game, tokens);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [source ? "HOST" : tokens[0]]));
        await game.Top(); Assert.Single(game.State.StackItems); await game.Top();
        if (source)
        {
            Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
            Assert.All(tokens, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        }
        else
        {
            Assert.False(game.State.CardObjects.ContainsKey(tokens[0]));
            Assert.Equal("OTHER", game.State.ObjectLocations[tokens[1]].BattlefieldObjectId);
        }
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnActualFlashResponseLeavesNoSourceBattlefieldAtResolution()
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "FLASH", "OGS·011/024", "P1", "HAND");
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await EnterAndAttack(game, "SFD·177/221"); await Select(game, tokens);
        await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        await game.Top(); Assert.Single(game.State.StackItems); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.All(tokens, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task OneTwoTokenBatchPaysOnlyOnePatrolExtraUnitFeeAndMovesActuallyAttachedEquipment(bool pay)
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "PATROL", "UNL-163/219", "P2", "BATTLEFIELD", "OTHER");
        initial = RadGearTestPosition.Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "RUNE", "OGN·007/298", "P1", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Green] = 1 }) } };
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", tokens[0], ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(tokens[0], game.State.CardObjects["SONG"].AttachedToObjectId);
        await EnterAndAttack(game, "SFD·177/221"); await Select(game, tokens);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal(CoreRuleEngine.ImposedMovementPaymentKind, request.Kind);
        Assert.Equal(0, request.ManaCost); Assert.Equal(1, request.PowerCost);
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["PAY"]));
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["RECYCLE_RUNE:RUNE"]));
        request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "PAY" : "DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.All(tokens, id => Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations[id].Zone));
        Assert.Equal(pay ? "OTHER" : null, game.State.ObjectLocations["SONG"].BattlefieldObjectId);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? 1 : 0, game.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "IMPOSED_MOVEMENT_COST")));
        var moved = game.Events().Where(e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            && e.Event.Payload.GetValueOrDefault("targetObjectId") is string target && tokens.Contains(target)).ToArray();
        Assert.Equal(pay ? 2 : 0, moved.Length);
        foreach (var movement in moved)
            Assert.Equal(tokens, Assert.IsAssignableFrom<IReadOnlyList<string>>(movement.Event.Payload["simultaneousSourceObjectIds"]).Order(StringComparer.Ordinal));
        await game.ColdCheckpoint();
    }
}
