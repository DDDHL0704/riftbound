using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SourceHereFaerieTokenTests
{
    private const string Mother = "OGN·106/298", Trevor = "UNL-048/219";
    private static CardObjectState[] Faeries(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.FaerieTokenCardNo).ToArray();

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task HunterCreatesOnlyOneDormantWarhawkThroughTheSharedHereSkill(string destination)
    {
        const string hunter = "UNL-033/219";
        var game = new Driver(VenUnitEntryBasicTests.Position(hunter));
        await game.Act("P1", new PlayCardCommand("UNIT", hunter, [], Destination: destination));
        Assert.DoesNotContain(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Equal("PLAY", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        var expected = game.State.ObjectLocations["UNIT"];
        await game.Top();
        var warhawk = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo);
        Assert.Equal(1, warhawk.Power); Assert.True(warhawk.IsExhausted);
        Assert.Contains(CardObjectTags.Spellshield, warhawk.Tags);
        Assert.Equal(expected, game.State.ObjectLocations[warhawk.ObjectId]);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_TOKEN_CREATED");
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task MotherCreatesOneActiveEphemeralFaerieHereAfterThePlaySkillResponseWindow(string destination)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(Mother));
        await game.Act("P1", new PlayCardCommand("UNIT", Mother, [], Destination: destination));
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Empty(Faeries(game.State));
        Assert.Equal("PLAY", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        var expected = game.State.ObjectLocations["UNIT"];
        await game.Top();
        var faerie = Assert.Single(Faeries(game.State));
        Assert.Equal(3, faerie.Power); Assert.False(faerie.IsExhausted);
        Assert.Contains(CardObjectTags.Ephemeral, faerie.Tags);
        Assert.Equal(expected, game.State.ObjectLocations[faerie.ObjectId]);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_TOKEN_CREATED");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MotherRechecksHereAfterARealMoveOrReturnResponse(bool leave)
    {
        var no = leave ? "OGN·104/298" : "OGS·011/024";
        var initial = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(Mother), "RESPONSE", no, "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Mother, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("RESPONSE", no, ["UNIT"]));
        await game.Top();
        Assert.Equal(leave ? "HAND" : "BASE", game.State.ObjectLocations["UNIT"].Zone);
        await game.Top();
        if (leave) Assert.Empty(Faeries(game.State));
        else Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations[Assert.Single(Faeries(game.State)).ObjectId]);
        await game.Replay();
    }

    [Fact]
    public async Task TrevorDoesNotCreateAFaerieWhenPlayed()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(Trevor));
        await game.Act("P1", new PlayCardCommand("UNIT", Trevor, [], Destination: "BATTLEFIELD:BF"));
        Assert.Empty(game.State.StackItems); Assert.Empty(Faeries(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task TrevorCreatesHisActiveEphemeralFaerieOnlyWhenHeHoldsTheBattlefield()
    {
        var game = new Driver(RadUnitHoldTests.Position(Trevor));
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("CHANNEL", game.State.TurnStartStep);
        var context = Assert.Single(game.State.StackItems).SourceUnitTokens!;
        Assert.Equal("HOLD", context.Timing); Assert.Empty(Faeries(game.State));
        await game.Top();
        var faerie = Assert.Single(Faeries(game.State));
        Assert.Equal(3, faerie.Power); Assert.False(faerie.IsExhausted);
        Assert.Contains(CardObjectTags.Ephemeral, faerie.Tags);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "F"), game.State.ObjectLocations[faerie.ObjectId]);
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TrevorUsesTheOriginalObjectsCurrentLocationAfterARealResponse(bool leave)
    {
        var no = leave ? "OGN·104/298" : "OGS·011/024";
        var initial = AttackDamageDistributionTests.Spell(RadUnitHoldTests.Position(Trevor), "RESPONSE", no, "P1");
        initial = RadGearTestPosition.Put(initial, "RESPONSE-RUNE-1", "UNL-R04", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "RESPONSE-RUNE-2", "UNL-R04", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P2", new EndTurnCommand());
        await game.TapRunes("P1", ["RESPONSE-RUNE-1", "RESPONSE-RUNE-2"]);
        await game.Act("P1", new PlayCardCommand("RESPONSE", no, ["HOST"]));
        await game.Top();
        await game.Top();
        if (leave) Assert.Empty(Faeries(game.State));
        else Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations[Assert.Single(Faeries(game.State)).ObjectId]);
        await game.Replay();
    }

    [Fact]
    public async Task TrevorInBaseDoesNotTriggerFromAnotherFriendlyUnitsHold()
    {
        var initial = RadUnitEffectTestFixture.Place(RadUnitHoldTests.Position(Trevor), "HOST", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P2", new EndTurnCommand());
        Assert.Empty(Faeries(game.State));
        Assert.DoesNotContain(game.State.StackItems, item => item.SourceUnitTokens?.CardNo == Trevor);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CopiedTrevorTextAndBlueSentinelCreateIndependentHereSkills(bool blue)
    {
        var initial = RadUnitHoldTests.Position(Trevor) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        initial = AttachedRuleTextTests.AddSong(initial, "SONG");
        if (blue) initial = OfficialHoldSequenceTests.AddUnit(initial, "BLUE", "UNL-087/219", "F", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        var held = game.State.TriggerQueue.Where(t => t.SourceUnitTokens is not null).ToArray();
        Assert.Equal(blue ? 4 : 2, held.Length);
        Assert.Equal(blue ? 2 : 1, held.Count(t => t.SourceUnitTokens!.Origin is not null));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(blue ? 4 : 2, Faeries(game.State).Length);
        Assert.All(Faeries(game.State), faerie => {
            Assert.False(faerie.IsExhausted);
            Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "F"), game.State.ObjectLocations[faerie.ObjectId]);
        });
        await game.Replay();
    }

    [Theory]
    [InlineData(Mother, "timing")] [InlineData(Trevor, "timing")]
    [InlineData(Mother, "generation")] [InlineData(Trevor, "generation")]
    public async Task RecoveryRejectsForgedSourceGenerationAndPlayOrHoldTiming(string no, string boundary)
    {
        var game = new Driver(no == Mother ? VenUnitEntryBasicTests.Position(no) : RadUnitHoldTests.Position(no));
        if (no == Mother) await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        else await game.Act("P2", new EndTurnCommand());
        var item = Assert.Single(game.State.StackItems); var context = item.SourceUnitTokens!;
        var forged = boundary == "generation" ? context with { Source = context.Source with { Generation = context.Source.Generation + 1 } }
            : context with { Timing = no == Mother ? "HOLD" : "PLAY", BattlefieldId = no == Mother ? "BF" : null };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { SourceUnitTokens = forged }] }),
            error => error.Contains("source unit token"));
    }
}
