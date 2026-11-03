using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("VEN·143", false)]
    [InlineData("VEN·191", false)]
    [InlineData("VEN·191*", false)]
    [InlineData("VEN·143", true)]
    [InlineData("VEN·191", true)]
    [InlineData("VEN·191*", true)]
    public async Task BorrowedDiscardDrawUsesTheBorrowersPrivateHandAfterResponses(string donor, bool empty)
    {
        var initial = AttachedRuleTextTests.Position("OGN·111/298", 0);
        if (!empty) initial = HandDiscardObserverTests.Put(initial, "PRIVATE-DISCARD", "OGN·104/298", "P1", "HAND");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = true },
                ["DONOR"] = new("DONOR", cardNo: donor, ownerId: "P1", controllerId: "P1", isExhausted: true) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["DONOR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["DONOR"] = new("P1", "LEGEND") } };
        Assert.Equal(empty ? 0 : 1, initial.PlayerZones["P1"].Hand.Count);
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "弃置一张手牌");
        var unempowered = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = false } } };
        await RejectBorrowedResource(unempowered, "HOST", id, []);
        await RejectBorrowedResource(initial, "HOST", id, ["HOST"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.False(game.State.CardObjects["DONOR"].IsEmpowered);
        Assert.Equal(initial.PlayerZones["P1"].Hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        await game.Top();
        if (!empty)
        {
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("PRIVATE_HAND_DISCARD", choice.Request.Kind);
            Assert.DoesNotContain("PRIVATE-DISCARD", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"]));
            var selected = Assert.Single(choice.Request.Options, o => o.ObjectIds!.Contains("PRIVATE-DISCARD"));
            foreach (var (actor, options) in new[] { ("P2", new[] { selected.Id }), ("P1", new[] { "FORGED" }),
                ("P1", new[] { selected.Id, selected.Id }) })
            {
                var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
                    new("borrowed-discard-draw-invalid", actor, CommandTypes.PayCost),
                    new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", options), default);
                Assert.False(rejected.Accepted);
                Assert.Empty(rejected.Events);
                Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
            }
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [selected.Id]));
            Assert.Contains("PRIVATE-DISCARD", game.State.PlayerZones["P1"].Graveyard);
        }
        else Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DISCARDED");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(new[] { initial.PlayerZones["P1"].MainDeck[0] }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck.Skip(1), game.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
