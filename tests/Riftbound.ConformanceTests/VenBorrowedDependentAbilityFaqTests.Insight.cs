using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("normal", 0)]
    [InlineData("normal", 1)]
    [InlineData("normal", 2)]
    [InlineData("token", 0)]
    [InlineData("token", 1)]
    [InlineData("token", 2)]
    [InlineData("hourglass", 0)]
    [InlineData("hourglass", 1)]
    [InlineData("hourglass", 2)]
    public async Task BorrowedInsightPaysSelfDestructionAndPrivatelyResumesIntoDrawAndExperience(string source, int recycle)
    {
        var initial = BorrowedConditionPosition("UNL-136/219");
        if (source == "hourglass")
            initial = RadGearTestPosition.Put(initial, "HOURGLASS", "OGN·077/298", "P1", "BASE");
        if (source == "token")
        {
            var host = initial.CardObjects["HOST"];
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = host with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                    CopyFaceTransitions = [new(0, host.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, host.CardNo!)] } } };
        }
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "洞察 2");
        var deck = initial.PlayerZones["P1"].MainDeck.ToArray();
        var experience = initial.PlayerExperience.GetValueOrDefault("P1");
        await RejectBorrowedResource(initial, "HOST", id, ["ALLY"]);
        await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 20) } }, "HOST", id, []);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(initial.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(experience, game.State.PlayerExperience.GetValueOrDefault("P1"));
        if (source == "token") Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else Assert.Contains("HOST", source == "hourglass" ? game.State.PlayerZones["P1"].Base : game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        Assert.Equal(deck.Take(2), choice.ContextObjectIds);
        var otherPrompt = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"]);
        foreach (var card in choice.ContextObjectIds) Assert.DoesNotContain(JsonSerializer.Serialize(card), otherPrompt);
        foreach (var (actor, selected) in new[] { ("P2", new[] { deck[0] }), ("P1", new[] { "STATUS" }),
            ("P1", new[] { deck[0], deck[0] }) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
                new("borrowed-insight-invalid", actor, CommandTypes.ChooseCards),
                new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected), default);
            Assert.False(rejected.Accepted);
            Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        }
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [game.State.StackItems[0] with { BorrowedExhaust = game.State.StackItems[0].BorrowedExhaust! with { AbilityIndex = 99 } }] }));
        if (source == "token")
        {
            var item = Assert.Single(game.State.StackItems);
            var borrowed = item.BorrowedExhaust!;
            var receipt = Assert.IsType<CeasedSelfAbilitySource>(borrowed.Borrower.CeasedSource);
            foreach (var invalid in new CeasedSelfAbilitySource?[] { null, receipt with { DepartedTick = game.State.Tick + 1 } })
                Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
                    StackItems = [item with { BorrowedExhaust = borrowed with {
                        Borrower = borrowed.Borrower with { CeasedSource = invalid } } }] }));
        }
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, deck.Take(recycle).ToArray()));
        if (recycle == 0)
        {
            var order = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
            await game.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, [deck[1], deck[0]]));
        }
        Assert.Contains(recycle < 2 ? deck[1] : deck[2], game.State.PlayerZones["P1"].Hand);
        Assert.Equal(deck.Length - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        if (recycle == 0) Assert.Equal(deck[0], game.State.PlayerZones["P1"].MainDeck[0]);
        if (recycle == 1) Assert.Equal(deck[0], game.State.PlayerZones["P1"].MainDeck[^1]);
        if (recycle == 2) Assert.Equal(deck.Take(2).Order(StringComparer.Ordinal), game.State.PlayerZones["P1"].MainDeck.TakeLast(2).Order(StringComparer.Ordinal));
        Assert.Equal(experience + 1, game.State.PlayerExperience["P1"]);
        Assert.Contains("DONOR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }
}
