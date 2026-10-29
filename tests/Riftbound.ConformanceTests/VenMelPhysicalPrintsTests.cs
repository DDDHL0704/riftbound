using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenMelPhysicalPrintsTests
{
    private static MatchState Position(string no, bool targets = true)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = AttackDamageDistributionTests.Spell(state, "FODDER", "VEN·049", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["WRONG"] = OfficialGarenConquestBattlefieldCountTests.Printed("WRONG", "OGN·219/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, "WRONG"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["WRONG"] = new("P1", "HAND") } };
        if (!targets) return state;
        state = OfficialHoldSequenceTests.AddUnit(state, "SMALL", "OGN·175/298", "OTHER", "P2");
        state = OfficialHoldSequenceTests.AddUnit(state, "BIG", "OGN·219/298", "OTHER", "P2");
        state = OfficialHoldSequenceTests.AddUnit(state, "FRIEND", "OGN·175/298", "BF", "P1");
        return state with { RoomId = "VEN-MEL-PRINT-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["BIG"] = state.CardObjects["BIG"] with { Power = 4 } } };
    }

    private static ActivateAbilityCommand Activate(params string[] costs)
        => new("UNIT", "EMPOWER:0:0", [], OptionalCosts: costs);

    [Theory]
    [InlineData("VEN·110", 37807, "弃置一张法术牌")]
    [InlineData("VEN·110a", 37808, "弃置一张法术牌")]
    [InlineData("VEN·188", 37891, "弃置一个法术")]
    public async Task OfficialPrintsPreserveTheTextAndResolveTheSameSpellDiscardCost(string no, long id, string printedCost)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("镜转乾坤", card.SubTitle); Assert.Equal(5, card.Energy); Assert.Equal(4, card.Power);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(printedCost, cost.AdditionalCost); Assert.False(cost.SupportedResources);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)); Assert.Equal(5, behavior.ManaCost);
        Assert.Equal(4, behavior.SourceUnitPower);
    }

    [Theory]
    [InlineData("VEN·110")] [InlineData("VEN·110a")] [InlineData("VEN·188")]
    public async Task EachPrintingDiscardsARealSpellThenBanishesOnlyAnEligibleEnemy(string no)
    {
        var game = new Driver(Position(no)); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        await game.Act("P1", Activate("DISCARD_HAND_CARD:FODDER"));
        Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("SMALL", choice.LegalObjectIds); Assert.DoesNotContain("BIG", choice.LegalObjectIds);
        Assert.DoesNotContain("FRIEND", choice.LegalObjectIds); Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        var illegal = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BIG"]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("illegal-target", "P1", illegal.CmdType), illegal, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SMALL"]));
        Assert.Contains("SMALL", game.State.PlayerZones["P2"].Battlefields); await game.Top();
        Assert.Contains("SMALL", game.State.PlayerZones["P2"].Banished);
        Assert.DoesNotContain("SMALL", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("BIG", game.State.PlayerZones["P2"].Battlefields); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·110", "missing")] [InlineData("VEN·110", "unit")] [InlineData("VEN·110", "field")]
    [InlineData("VEN·110a", "missing")] [InlineData("VEN·110a", "unit")] [InlineData("VEN·110a", "field")]
    [InlineData("VEN·188", "missing")] [InlineData("VEN·188", "unit")] [InlineData("VEN·188", "field")]
    public async Task MissingNonspellOrNonhandDiscardIsRejectedAtomically(string no, string kind)
    {
        var game = new Driver(Position(no, targets: false)); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        var command = kind switch { "unit" => Activate("DISCARD_HAND_CARD:WRONG"), "field" => Activate("DISCARD_HAND_CARD:GEAR"), _ => Activate() };
        var before = MatchStateHasher.Hash(game.State);
        var result = await new CoreRuleEngine().ResolveAsync(game.State, new("bad-cost", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Contains("FODDER", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·110")] [InlineData("VEN·110a")] [InlineData("VEN·188")]
    public async Task ChampionPlayPaysFiveManaAndRejectsInsufficientMana(string no)
    {
        var state = Position(no, targets: false);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray(), ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(5, 0) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var unpaid = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpaid, new("unpaid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(rejected.State));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    public static IEnumerable<object[]> LegalDecks() => new[] { "VEN·151", "VEN·195", "VEN·195*" }
        .SelectMany(legend => new[] { "VEN·110", "VEN·110a", "VEN·188" }.Select(hero => new object[] { legend, hero }));

    [Theory]
    [MemberData(nameof(LegalDecks))]
    public async Task ChinaStandardDeckAdmitsEveryLegendAndHeroPrinting(string legend, string hero)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legend, hero, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("blue"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("mel-" + legend + "-" + hero,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(command, json);
        foreach (var player in new[] { "P1", "P2" })
            Assert.True((await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default)).Accepted);
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), json);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        foreach (var player in new[] { "P1", "P2" })
            Assert.Contains(opened.State.PlayerZones[player].ChampionZone, id => opened.State.CardObjects[id].CardNo == hero);
    }
}
