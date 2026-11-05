using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class DariusSecondCardTriggerTests
{
    [Theory]
    [InlineData("OGN·027/298")]
    [InlineData("OGN·027a/298")]
    public async Task OgnDariusTriggersWhenTheFollowingSpellIsTheSecondCard(string dariusCardNo)
    {
        var game = new Driver(Position(dariusCardNo, "OGN·048/298"));
        await game.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);

        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        Assert.Equal(2, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Top();
        var trigger = Assert.Single(game.State.StackItems);
        Assert.Equal("SECOND_CARD_PLAYED", trigger.UnitEvent!.Timing);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·027/298")]
    [InlineData("OGN·027a/298")]
    public async Task AThirdCardResolvingFirstDoesNotChangeWhichCardWasSecond(string dariusCardNo)
    {
        var game = new Driver(Position(dariusCardNo, "OGN·048/298", "VEN·126"));
        await game.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await game.Act("P1", new PlayCardCommand("SPELL2", "VEN·126", ["ALLY"]));
        Assert.Equal(3, game.State.PlayerCardsPlayedThisTurn["P1"]);
        await game.Top();
        Assert.Equal("SPELL", Assert.Single(game.State.StackItems).SourceObjectId);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);

        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·027/298")]
    [InlineData("OGN·027a/298")]
    public async Task ACounteredSecondCardDoesNotTriggerOrMakeTheThirdCardSecond(string dariusCardNo)
    {
        var initial = AttackDamageDistributionTests.Spell(
            Position(dariusCardNo, "OGN·048/298", "OGN·048/298"), "COUNTER", "OGN·045/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var target = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [target]));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);

        await game.Act("P1", new PlayCardCommand("SPELL2", "OGN·048/298", []));
        Assert.Equal(3, game.State.PlayerCardsPlayedThisTurn["P1"]);
        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·027/298")]
    [InlineData("OGN·027a/298")]
    public async Task OgnDariusItselfIsTheSecondCardWhenPlayedAfterTheSpell(string dariusCardNo)
    {
        var game = new Driver(Position(dariusCardNo, "OGN·048/298"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);

        await game.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        Assert.Equal(2, game.State.PlayerCardsPlayedThisTurn["P1"]);
        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·027/298")]
    [InlineData("OGN·027a/298")]
    public async Task OgnDariusDoesNotTriggerAgainOnTheThirdCard(string dariusCardNo)
    {
        var game = new Driver(Position(dariusCardNo, "OGN·048/298", "OGN·048/298"));
        await game.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);

        await game.Act("P1", new PlayCardCommand("SPELL2", "OGN·048/298", []));
        Assert.Equal(3, game.State.PlayerCardsPlayedThisTurn["P1"]);
        await ResolveTopAndQueuedTriggers(game);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_POWER_CHANGED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·236/221")]
    [InlineData("SFD·236*/221")]
    [InlineData("OGN·243/298")]
    [InlineData("OGN·243a/298")]
    public async Task DariusExecutionerReadiesOnlyWhenItIsPlayedAfterAnotherCard(string dariusCardNo)
    {
        var first = new Driver(Position(dariusCardNo, "OGN·048/298"));
        await first.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        Assert.Equal(1, first.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.True(first.State.CardObjects["UNIT"].IsExhausted);
        Assert.DoesNotContain(first.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await first.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await ResolveTopAndQueuedTriggers(first);
        Assert.True(first.State.CardObjects["UNIT"].IsExhausted);
        await first.Replay();

        var afterSpell = new Driver(Position(dariusCardNo, "OGN·048/298"));
        await afterSpell.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await ResolveTopAndQueuedTriggers(afterSpell);
        await afterSpell.Act("P1", new PlayCardCommand("UNIT", dariusCardNo, [], Destination: "BASE"));
        Assert.Equal(2, afterSpell.State.PlayerCardsPlayedThisTurn["P1"]);
        await ResolveTopAndQueuedTriggers(afterSpell);
        Assert.False(afterSpell.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(afterSpell.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await afterSpell.Act("P1", new EndTurnCommand());
        await afterSpell.Replay();
    }

    private static async Task ResolveTopAndQueuedTriggers(Driver game)
    {
        while (game.State.StackItems.Count > 0)
            await game.Top();
    }

    private static MatchState Position(string firstCardNo, string secondCardNo, string? thirdCardNo = null)
    {
        var initial = VenUnitEntryBasicTests.Position(firstCardNo);
        var cards = initial.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var locations = initial.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", secondCardNo, "P1");
        locations["SPELL"] = new("P1", "HAND");
        if (thirdCardNo is not null)
        {
            cards["SPELL2"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL2", thirdCardNo, "P1");
            locations["SPELL2"] = new("P1", "HAND");
        }
        var zones = initial.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        zones["P1"] = zones["P1"] with { Hand = thirdCardNo is null ? ["UNIT", "SPELL"] : ["UNIT", "SPELL", "SPELL2"] };
        return initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
