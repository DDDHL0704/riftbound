using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPowerPlayTests
{
    public static IEnumerable<object[]> Plays()
        => from kind in new[] { "spell", "equipment", "unit" }
           from spellResource in new[] { false, true }
           from explicitSpend in new[] { false, true }
           select new object[] { kind, spellResource, explicitSpend };

    [Theory]
    [MemberData(nameof(Plays))]
    public async Task OnlyTheActualEligibleCardCanSpendBankedLegendPower(
        string kind, bool spellResource, bool explicitSpend)
    {
        var no = kind switch { "spell" => "UNL-010/219", "equipment" => "OGN·063/298", _ => "SFD·143/221" };
        var initial = Position(no, spellResource);
        var game = new Driver(initial);
        var ability = spellResource ? LegendActionAbilityCatalog.KaisaLegendAbilityId : LegendActionAbilityCatalog.OrnnLegendAbilityId;
        await game.Act("P1", new LegendActCommand("LEGEND", ability, [], []));
        var expected = kind == (spellResource ? "spell" : "equipment");
        var command = new PlayCardCommand("CARD", no, kind == "unit" ? [] : ["HOST"],
            OptionalCosts: explicitSpend ? ["POWER_SPEND:A:1"] : [], Destination: kind == "unit" ? "BASE" : null);
        var before = MatchStateHasher.Hash(game.State);
        var eventCount = game.Events().Length;
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.Equal(expected, quote.CanPay);
        Assert.Equal(before, MatchStateHasher.Hash(game.State));
        if (expected)
        {
            Assert.True(quote.IsValid, quote.Message);
            Assert.Equal(1, quote.Cost!.AvailableRainbowPower);
            Assert.Equal(0, quote.Cost.MissingPower);
            Assert.Equal(0, quote.Cost.RemainingRainbowPower);
        }
        else if (!explicitSpend)
        {
            Assert.True(quote.IsValid, quote.Message);
            Assert.Equal(0, quote.Cost!.AvailableRainbowPower);
            Assert.Equal(1, quote.Cost.MissingPower);
            Assert.Null(quote.Cost.RemainingRainbowPower);
        }

        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        var prompt = ResolutionResult.BuildPrompts(game.State)["P1"];
        raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new("P1", "restricted-power-play", JsonSerializer.SerializeToElement(raw, Web)), expected);
        if (expected)
        {
            Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
            Assert.Equal(0, game.State.RunePools["P1"].Power);
            var paid = Assert.Single(game.Events().Skip(eventCount), e => e.Event.Kind == "COST_PAID").Event;
            Assert.Equal(spellResource ? "PLAY_SPELL" : "EQUIPMENT", paid.Payload["powerPurpose"]);
            Assert.Equal(1, paid.Payload["totalPowerCost"]);
        }
        else
        {
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            Assert.Equal(eventCount, game.Events().Length);
            Assert.Equal(1, game.State.RunePools["P1"].TotalStoredPower);
        }
        await game.Replay();
    }

    private static MatchState Position(string no, bool spellResource)
    {
        // Explicit turn-three local fixture; this is not a complete legal opening.
        var state = PlayCostPreviewTests.Position(new(10, 0), no);
        state = state with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            { ["P1"] = state.PlayerZones["P1"] with { Base = ["HOST"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["CARD"] = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", no, "P1"),
                ["P2-CARD"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-CARD", no, "P2"),
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "OGN·001/298", "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            { ["CARD"] = new("P1", "HAND"), ["P2-CARD"] = new("P2", "HAND"), ["HOST"] = new("P1", "BASE") }
        };
        return OfficialHoldSequenceTests.AddLegend(state, spellResource ? "OGN·247/298" : "SFD·189/221");
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
