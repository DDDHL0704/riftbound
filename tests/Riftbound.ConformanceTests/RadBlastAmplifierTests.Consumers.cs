using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed partial class RadBlastAmplifierTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealAnimationAndAttachedCopyEachAddOneLastBreathOccurrence(bool copy)
    {
        var initial = Put(Put(Put(Position(), "UNIT", "OGN·096/298", "P1", "HAND"), "SONG", "SFD·059/221", "P1", "BASE"),
            "KILL", Kill, "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        initial = OriannaEquipmentConsumerFixture.WithResources(initial);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·096/298", [], Destination: "BATTLEFIELD:BF"));
        await Deploy(game);
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "AMP");
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["AMP"].Tags);
        if (copy) { await game.Act("P1", new AssembleEquipmentCommand("SONG", "AMP", ["ASSEMBLE_GREEN"])); await game.Top();
            Assert.Equal(Amplifier, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo); }
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        Assert.Equal(copy ? 3 : 2, Draws(game.State).Length);
        await ResolveAllDraws(game); await game.Replay();
    }

    [Theory]
    [InlineData("SFD·036/221", false, true)] [InlineData("SFD·036/221", true, false)]
    [InlineData("UNL-156/219", true, true)] [InlineData("UNL-156/219", false, false)]
    public async Task RealEquipmentDoesNotCountAsAnotherUnitForConditionalLastBreaths(string card, bool ally, bool earns)
    {
        var initial = Put(Put(Position(), "UNIT", card, "P1", "HAND"), "KILL", Kill, "P1", "HAND");
        if (ally) initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", card, [], Destination: "BATTLEFIELD:BF"));
        await Deploy(game);
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        Assert.Equal(earns ? 2 : 0, Draws(game.State).Length);
        Assert.All(Draws(game.State), c => Assert.Equal(!ally, c.SourceWasAlone));
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await ResolveAllDraws(game); Assert.Equal(hand + (earns ? 2 : 0), game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealAmplifierAndCopiedAgentKeepSeparateDiscardChoicesAndPrivateHands(bool copy)
    {
        const string agent = "OGN·178/298";
        var initial = Put(Put(Put(Put(Position(), "UNIT", agent, "P1", "HAND"), "SONG", "SFD·059/221", "P1", "BASE"),
            "KILL", Kill, "P1", "HAND"), "H1", "OGN·219/298", "P1", "HAND");
        initial = Put(initial, "H2", "OGN·219/298", "P1", "HAND");
        for (var i = 6; i < 12; i++) initial = Put(initial, "P1-D" + i, "OGN·219/298", "P1", "MAIN_DECK");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", agent, [], Destination: "BATTLEFIELD:BF"));
        await Deploy(game);
        if (copy) { await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        var count = copy ? 4 : 2; Assert.Equal(count, Draws(game.State).Length);
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
        var choices = new HashSet<string>();
        for (var i = 0; i < count; i++) {
            var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
            await game.Top(); var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
            Assert.True(choices.Add(choice.ChoiceId)); Assert.Equal(2, choice.RequiredCount); Assert.Equal(2, choice.DrawCount);
            Assert.Equal(hand, choice.LegalObjectIds);
            await Reject(game.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, hand.Take(2).ToArray()));
            await Reject(game.State, "P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [hand[0], hand[0]]));
            await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, hand.Take(2).ToArray()));
            Assert.Equal(hand.Skip(2).Concat(deck.Take(2)), game.State.PlayerZones["P1"].Hand);
            Assert.Equal(deck.Skip(2), game.State.PlayerZones["P1"].MainDeck);
        }
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingHandChoice); await game.Replay();
    }
}
