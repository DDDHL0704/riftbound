using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ZedLegendDiscardDrawTests
{
    private const string Ability = LegendActionAbilityCatalog.ZedLegendAbilityId;
    private static LegendActCommand Activate() => new("LEGEND", Ability, [], []);

    internal static MatchState Position(string cardNo = "VEN·143", bool empowered = true, bool hand = true)
    {
        var state = VenFlowCombatKeywordTests.Position(false);
        var zones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value);
        zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"], Hand = hand ? ["SPELL"] : [],
            Graveyard = hand ? zones["P1"].Graveyard : [..zones["P1"].Graveyard, "SPELL"] };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new CardObjectState("LEGEND", cardNo: cardNo, ownerId: "P1", controllerId: "P1", tags: ["传奇"]) with { IsEmpowered = empowered } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND"), ["SPELL"] = new("P1", hand ? "HAND" : "GRAVEYARD") } };
    }

    [Theory]
    [InlineData("VEN·143", 37846)] [InlineData("VEN·191", 37896)] [InlineData("VEN·191*", 37897)]
    public async Task EveryPrintingSpendsTheCostsThenDiscardsAndDrawsAfterResponses(string cardNo, long cardId)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.Equal(cardId, card.Id); Assert.Equal("影流之主", card.CardName);
        Assert.Contains("解除我的强化，{{横置}}：弃置一张手牌，然后抽一张牌。", card.CardEffect);
        var game = new VenBasicFlowSpellTests.Driver(Position(cardNo));
        await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingHandChoice);
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendDiscardDraw);
        await game.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(["SPELL"], choice.LegalObjectIds);
        Assert.DoesNotContain("SPELL", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Timing));
        await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL"]));
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-DRAW-1"], game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingHandChoice);
        await game.Replay();
    }

    [Fact]
    public async Task EmptyHandStillDrawsWithoutAChoice()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position(hand: false));
        await game.Act("P1", Activate()); await game.Top();
        Assert.Null(game.State.PendingHandChoice);
        Assert.Equal(["P1-DRAW-1"], game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("unempowered")] [InlineData("exhausted")] [InlineData("wrong-player")]
    [InlineData("wrong-source")] [InlineData("target")] [InlineData("unexpected-cost")]
    public async Task IllegalActivationHasNoEffects(string reason)
    {
        var state = Position(empowered: reason != "unempowered");
        if (reason == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var cmd = new LegendActCommand(reason == "wrong-source" ? "ALLY" : "LEGEND", Ability,
            reason == "target" ? ["ALLY"] : [], reason == "unexpected-cost" ? ["SPEND_MANA:1"] : []);
        var rejected = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-legend", reason == "wrong-player" ? "P2" : "P1", cmd.CmdType), cmd, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task IllegalChoiceAndForgedContinuationsAreRejected()
    {
        var opened = await Top((await Act(Position(), "P1", Activate())).State);
        var state = opened.State; var choice = Assert.IsType<PendingHandChoiceState>(state.PendingHandChoice);
        foreach (var (player, command) in new[] {
            ("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL"])),
            ("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-1"])),
            ("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])),
            ("P1", new ChooseHandCardsCommand("stale", choice.ChoiceWindow, ["SPELL"])) }) {
            var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-choice", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        }
        foreach (var forged in new[] { choice with { DrawCount = 2 }, choice with { PlayerId = "P2" },
            choice with { LegendDiscardDraw = null }, choice with { Reason = "OTHER" }, choice with { RequiredCount = 2, MaxCount = 2 } })
            Assert.Contains("invalid legend discard continuation", OfficialInsightAndSpellLockTests.Errors(state with { PendingHandChoice = forged }));
    }
}
