using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RuneCallReplacementRecoveryTests
{
    private const string Wyvern = "RAD·092/167";
    private const string Mobilize = "OGN·134/298";

    [Fact]
    public void AdmittedWyvernUsesTheSharedRuneCallReplacement()
    {
        Assert.True(ReviewedRuneCardSources.TryGet(Wyvern, out var source));
        var spec = Assert.Single(StaticAbilityParser.Parse(source.CardEffect!, []),
            ability => ability.Kind == StaticAbilityKinds.ControllerRuneCallExtraExhausted);
        Assert.Equal(1, spec.Amount);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Wyvern, out var behavior));
        Assert.Equal(7, behavior.ManaCost); Assert.Equal(7, behavior.SourceUnitPower);
        Assert.DoesNotContain(StaticAbilityParser.Parse("召出一枚休眠的符文。", []),
            ability => ability.Kind == StaticAbilityKinds.ControllerRuneCallExtraExhausted);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 4)]
    public async Task RealAttachmentsCreateIndependentReplacementInstancesAndReplay(int songs, int expected)
    {
        var state = Position();
        if (songs == 2) state = AttachedRuleTextTests.AddSong(state, "SONG2");
        var game = new Driver(state);
        foreach (var id in new[] { "GEAR", "SONG2" }.Take(songs))
        {
            await game.Act("P1", new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_GREEN"]));
            await game.Top();
            Assert.Equal(Wyvern, game.State.CardObjects[id].AttachedRuleText!.CardNo);
        }
        await game.Act("P1", new PlayCardCommand("SPELL", Mobilize, []));
        await game.Top();
        var called = game.Events().Where(e => e.Event.Kind == "RUNES_CALLED").ToArray();
        Assert.Equal(expected, Convert.ToInt32(Assert.Single(called).Event.Payload["count"]));
        Assert.Equal(expected, game.State.PlayerZones["P1"].Base.Count(id => id.StartsWith("CALL-RUNE-", StringComparison.Ordinal)));
        Assert.All(game.State.PlayerZones["P1"].Base.Where(id => id.StartsWith("CALL-RUNE-", StringComparison.Ordinal)),
            id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    private static MatchState Position()
    {
        var initial = AttachedRuleTextTests.Position(graveCount: 0);
        var cards = initial.CardObjects.ToDictionary();
        var positions = initial.ObjectLocations.ToDictionary();
        foreach (var id in initial.PlayerZones["P1"].RuneDeck)
        {
            cards.Remove(id);
            positions.Remove(id);
        }
        cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Wyvern, "P1");
        cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", Mobilize, "P1");
        positions["SPELL"] = new("P1", "HAND");
        var runes = Enumerable.Range(0, 4).Select(i => "CALL-RUNE-" + i).ToArray();
        foreach (var id in runes)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", "P1");
            positions[id] = new("P1", "RUNE_DECK");
        }
        var zones = initial.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Hand = ["SPELL"], RuneDeck = runes };
        return initial with { RoomId = "RUNE-REPLACEMENT-RECOVERY", CardObjects = cards,
            ObjectLocations = positions, PlayerZones = zones };
    }
}
