using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenSonaEndRuneTests
{
    [Fact]
    public async Task VendettaPrintingUsesTheCorrectedOfficialEndStepText()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == "VEN·SP2");
        Assert.Equal(37917, card.Id); Assert.Equal("清心谐律", card.SubTitle);
        Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal("在你的回合结束时，如果我位于战场上，则让最多四枚友方符文变为活跃状态。", card.CardEffect);
        var trigger = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.TurnEndReadyRunes);
        Assert.Equal(TriggerTimings.TurnEnd, trigger.Timing); Assert.Equal(4, trigger.RuneReadyCount);
        Assert.True(trigger.RequiresSourceBattlefieldAtTrigger); Assert.True(trigger.Optional);
    }

    [Fact]
    public async Task ActualPlayRequiresFourManaAndGreenPower()
    {
        var state = VenUnitEntryBasicTests.Position("VEN·SP2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        var command = new PlayCardCommand("UNIT", "VEN·SP2", [], Destination: "BATTLEFIELD:BF");
        foreach (var pool in new[] { new RunePool(3, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }),
                new RunePool(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) }) {
            var unpaid = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
            var rejected = await new CoreRuleEngine().ResolveAsync(unpaid, new("unpaid", "P1", command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(rejected.State));
        }
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·073/298")] [InlineData("VEN·SP2")]
    public async Task RuneSelectionRejectsExtraDuplicateWrongPlayerAndNonRunes(string card)
    {
        var initial = EndDeckPlayTests.RunePosition(card, true); var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new EndTurnCommand()); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var runes = EndDeckPlayTests.RuneIds;
        foreach (var (player, selected, id, window) in new[] {
            ("P1", runes, choice.ChoiceId, choice.ChoiceWindow),
            ("P1", new[] { runes[0], runes[0] }, choice.ChoiceId, choice.ChoiceWindow),
            ("P1", new[] { "HOST" }, choice.ChoiceId, choice.ChoiceWindow),
            ("P2", new[] { runes[0] }, choice.ChoiceId, choice.ChoiceWindow),
            ("P1", new[] { runes[0] }, "expired", choice.ChoiceWindow),
            ("P1", new[] { runes[0] }, choice.ChoiceId, "wrong-window") }) {
            var command = new ChooseCardsCommand(id, window, selected); var before = game.State;
            var rejected = await new CoreRuleEngine().ResolveAsync(before, new("bad-rune", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        }
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, runes.Take(4).ToArray()));
        await game.Top(); Assert.Equal("P2", game.State.TurnPlayerId);
        foreach (var id in runes.Take(4)) Assert.False(game.State.CardObjects[id].IsExhausted);
        Assert.True(game.State.CardObjects[runes[4]].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task ChinaStandardDeckCanUseTheNewPrintingInItsMainDeck()
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "OGN·255/298", "OGN·066/298", new[] { "VEN·SP2" },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("green"), 6).Concat(Enumerable.Repeat(Rune("blue"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors)); Assert.Contains("VEN·SP2", deck.MainDeck);
        var initial = MatchReplayInitialStateBuilder.FromSeats("sona-print-deck", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance, NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(command, options);
        foreach (var player in new[] { "P1", "P2" }) Assert.True((await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default)).Accepted);
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), options);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
    }
}
