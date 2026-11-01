using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadNextUnitHasteBattlefieldTests
{
    private const string GrantPrefix = "NEXT_UNIT_PLAY_GRANT:";
    private const string Unit = "OGN·096/298";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConquestGrantsHasteOnlyAfterResponsesAndConsumesItEvenWhenDeclined(bool pay)
    {
        var game = new Driver(Position());
        await Conquer(game);
        Assert.NotNull(Assert.Single(game.State.StackItems).NextUnitPlayGrant);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        var command = new PlayCardCommand("FIRST", Unit, [], OptionalCosts: pay ? [HasteOptionalCostNames.HasteReady] : []);
        var before = game.State.RunePools["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(pay ? 3 : 2, quote.Cost!.Mana);
        await game.Act("P1", command);
        Assert.Equal(!pay, game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(before.Mana - (pay ? 3 : 2), game.State.RunePools["P1"].Mana);
        Assert.Equal(before.PowerByTrait.GetValueOrDefault(RuneTrait.Blue) - (pay ? 1 : 0), game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await Reject(game.State, new("SECOND", Unit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await game.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task InvalidPaymentDoesNotConsumeTheNextUnitGrant()
    {
        var state = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Red] = 2 }), ["P2"] = new(30, 30) } };
        var game = new Driver(state);
        await Conquer(game); await game.Top();
        await Reject(game.State, new("FIRST", Unit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.Single(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Replay();
    }

    [Fact]
    public async Task EquipmentAndSpellsPreserveTheGrantUntilAUnitIsPlayed()
    {
        var game = new Driver(Put(Put(Position(), "GEAR", "SFD·022/221"), "SPELL", "OGN·048/298"));
        await Conquer(game); await game.Top();
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", ["UNIT"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [])); await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUnitTokenCanUseTheGrantAndDecliningStillConsumesIt(bool pay)
    {
        var game = new Driver(Put(Position(), "TOKENS", "SFD·031/221"));
        await Conquer(game); await game.Top();
        await game.Act("P1", new PlayCardCommand("TOKENS", "SFD·031/221", [])); await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("TOKEN_HASTE_PAYMENT", request.Kind);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "PAY:ANY" : "DECLINE"]));
        var token = Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.Equal(!pay, token.IsExhausted);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await Reject(game.State, new("FIRST", Unit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await game.Replay();
    }

    [Fact]
    public async Task TheUnconsumedGrantExpiresAtTurnEnd()
    {
        var game = new Driver(Position());
        await Conquer(game); await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyTheFirstTokenInAReplacementBatchGetsTheOptionalHasteCost(bool pay)
    {
        var state = Put(Put(Put(Position(), "TOKENS", "SFD·031/221"), "OTHER", "OGN·275/298"), "ZILEAN", "UNL-086/219");
        state = RadUnitEffectTestFixture.Place(state, "OTHER", "P1", "BATTLEFIELD", "OTHER");
        state = RadUnitEffectTestFixture.Place(state, "ZILEAN", "P1", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await Conquer(game); await game.Top();
        await game.Act("P1", new PlayCardCommand("TOKENS", "SFD·031/221", [])); await game.Top();
        var replacement = game.State.PendingCardChoice!;
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", replacement.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(replacement.ChoiceId, replacement.ChoiceWindow, ["ZILEAN"]));
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("TOKEN_HASTE_PAYMENT", request.Kind);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "PAY:ANY" : "DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        var tokens = game.State.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.SandSoldier)).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.Equal(pay ? 1 : 0, tokens.Count(t => !t.IsExhausted));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Replay();
    }

    [Fact]
    public async Task TheSecondPlayersConquestRewardsThatPlayerDespiteTheFieldsOwner()
    {
        var state = Position() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FIRST"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIRST", Unit, "P2"),
            ["BF"] = state.CardObjects["BF"] with { OwnerId = "P1" } } };
        state = RadUnitEffectTestFixture.Place(state, "FIRST", "P2", "HAND");
        var game = new Driver(state);
        await Conquer(game, "P2");
        Assert.Equal("P2", Assert.Single(game.State.StackItems).NextUnitPlayGrant!.ControllerId);
        await game.Top();
        await game.Act("P2", new PlayCardCommand("FIRST", Unit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, m => m.StartsWith(GrantPrefix));
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("player")]
    [InlineData("field")]
    [InlineData("generation")]
    public async Task RecoveryRejectsForgedConquestGrantBindings(string boundary)
    {
        var game = new Driver(Position());
        await Conquer(game);
        var item = Assert.Single(game.State.StackItems);
        var context = item.NextUnitPlayGrant!;
        var forged = boundary switch {
            "missing" => null,
            "player" => context with { ControllerId = "P2" },
            "field" => context with { Source = context.Source with { ObjectId = "UNIT" } },
            _ => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { NextUnitPlayGrant = forged }] }),
            e => e.Contains("next unit play grant", StringComparison.Ordinal));
        await game.Replay();
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-haste", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static async Task Conquer(Driver game, string player = "P1")
    {
        await game.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(player == "P1" ? "P2" : "P1", new PassFocusCommand());
    }

    private static MatchState Position()
    {
        var state = ConquestLifecycleRegressionTests.State("RAD·162/167", 0);
        state = state with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(30, 30, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }), ["P2"] = new(30, 30) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", "OGN·219/298", "P1") } };
        return Put(Put(state, "FIRST", Unit), "SECOND", Unit);
    }

    private static MatchState Put(MatchState state, string id, string no)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") } };
        return RadUnitEffectTestFixture.Place(state, id, "P1", "HAND");
    }
}
