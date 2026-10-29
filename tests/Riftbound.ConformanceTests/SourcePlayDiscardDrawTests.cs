using System.Text.Json;
using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SourcePlayDiscardDrawTests
{
    private static readonly (string No, int Discard, int Draw)[] Prints = [
        ("OGN·030/298", 2, 0), ("OGN·030a/298", 2, 0), ("VEN·168", 2, 0),
        ("SFD·149/221", 1, 2), ("SFD·149a/221", 1, 2), ("VEN·SP5", 1, 2),
        ("OGN·003/298", 1, 0), ("UNL-123/219", 1, 1) ];

    public static IEnumerable<object[]> Cases() => Prints.SelectMany(p => new[] { 0, 1, 3 }
        .Select(hand => new object[] { p.No, p.Discard, p.Draw, hand }));

    internal static MatchState Position(string no, int remainingHand)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        var hand = Enumerable.Range(1, remainingHand).Select(i => $"H{i}").ToArray();
        foreach (var id in hand) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", "P1");
            locations[id] = new("P1", "HAND");
        }
        return state with { RoomId = $"PLAY-DISCARD-{no}-{remainingHand}", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = ["UNIT", ..hand] } } };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EntryThenIndependentSkillDiscardsAsManyAsPossibleAndDraws(string no, int discard, int draw, int remainingHand)
    {
        var initial = Position(no, remainingHand);
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", played.State.PlayerZones["P1"].Base);
        Assert.Equal(remainingHand, played.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal("CAPTURED_SOURCE_PLAY_DISCARD_DRAW", Assert.Single(played.State.StackItems).EffectKind);
        Assert.Null(played.State.PendingHandChoice); Restore(played.State);
        var result = await Top(played.State); var state = result.State;
        var amount = Math.Min(remainingHand, discard);
        var selected = Enumerable.Range(1, amount).Select(i => $"H{i}").ToArray();
        if (remainingHand >= discard) {
            var choice = Assert.IsType<PendingHandChoiceState>(state.PendingHandChoice);
            Assert.Equal(discard, choice.RequiredCount); Assert.Equal(draw, choice.DrawCount);
            Assert.Equal(remainingHand, state.PlayerZones["P1"].Hand.Count); Restore(state);
            state = (await Act(state, "P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected))).State;
        }
        Assert.Null(state.PendingHandChoice); Assert.Empty(state.StackItems);
        Assert.All(selected, id => Assert.Contains(id, state.PlayerZones["P1"].Graveyard));
        Assert.Equal(remainingHand - amount + draw, state.PlayerZones["P1"].Hand.Count);
        Assert.Equal(3 - draw, state.PlayerZones["P1"].MainDeck.Count); Restore(state);
    }

    [Theory]
    [InlineData("OGN·030/298", 2, 0)] [InlineData("OGN·030a/298", 2, 0)] [InlineData("VEN·168", 2, 0)]
    [InlineData("SFD·149/221", 1, 2)] [InlineData("SFD·149a/221", 1, 2)] [InlineData("VEN·SP5", 1, 2)]
    [InlineData("OGN·003/298", 1, 0)] [InlineData("UNL-123/219", 1, 1)]
    public async Task OnlyUnconditionalOfficialClausesProduceTheTypedTrigger(string no, int discard, int draw)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.SourceUnitPlayedDiscardDraw);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, spec.Timing); Assert.Equal(discard, spec.DiscardCount); Assert.Equal(draw, spec.DrawCount);
        var conditional = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == "OGN·020/298");
        Assert.DoesNotContain(RuleTextParser.Parse(conditional).Triggers, t => t.Kind == TriggerKinds.SourceUnitPlayedDiscardDraw);
    }

    [Theory]
    [InlineData("VEN·168", 37871)] [InlineData("VEN·SP5", 37920)]
    public async Task OfficialNewPrintingIdentityIsAdmitted(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(no == "VEN·168" ? 4 : 3, card.Power);
    }

    [Theory]
    [InlineData("VEN·168")] [InlineData("VEN·SP5")]
    public async Task HandCardsCannotBeSubmittedAsTargetsDuringThePlay(string no)
    {
        var state = Position(no, 2);
        var command = new PlayCardCommand("UNIT", no, ["H1"], Destination: "BASE");
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("premature-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("VEN·168", 2)] [InlineData("VEN·SP5", 1)]
    public async Task InvalidHandChoicesLeaveTheContinuationAndStateUnchanged(string no, int count)
    {
        var played = await Act(Position(no, 3), "P1", new PlayCardCommand("UNIT", no, []));
        var state = (await Top(played.State)).State; var choice = Assert.IsType<PendingHandChoiceState>(state.PendingHandChoice);
        var valid = Enumerable.Range(1, count).Select(i => $"H{i}").ToArray();
        foreach (var (player, ids, id, window) in new[] {
            ("P2", valid, choice.ChoiceId, choice.ChoiceWindow),
            ("P1", Array.Empty<string>(), choice.ChoiceId, choice.ChoiceWindow),
            ("P1", Enumerable.Repeat("H1", count + 1).ToArray(), choice.ChoiceId, choice.ChoiceWindow),
            ("P1", Enumerable.Repeat("P1-DRAW-1", count).ToArray(), choice.ChoiceId, choice.ChoiceWindow),
            ("P1", valid, "expired", choice.ChoiceWindow),
            ("P1", valid, choice.ChoiceId, "wrong-window") }) {
            var command = new ChooseHandCardsCommand(id, window, ids);
            var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-" + id + player + ids.Length, player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        }
        var context = Assert.IsType<SourcePlayDrawContext>(choice.SourcePlayDraw);
        foreach (var forgedChoice in new[] {
            choice with { DrawCount = choice.DrawCount + 1 }, choice with { SourcePlayDraw = null },
            choice with { SourcePlayDraw = context with { ControllerId = "P2" } },
            choice with { SourcePlayDraw = context with { TriggerId = "forged" } },
            choice with { SourcePlayDraw = context with { CapturedTick = state.Tick + 1 } },
            choice with { SourcePlayDraw = context with { SourceGeneration = 999 } } }) {
            var forged = state with { PendingHandChoice = forgedChoice };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("source play discard", StringComparison.Ordinal));
        }
        var result = await Act(state, "P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, valid));
        Assert.Null(result.State.PendingHandChoice); Restore(result.State);
    }

    [Theory]
    [InlineData("VEN·168", RuneTrait.Red)] [InlineData("VEN·SP5", RuneTrait.Purple)]
    public async Task HeroFromChampionZonePaysPrintedManaAndCorrectPower(string no, string trait)
    {
        var state = Position(no, 0);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0, new Dictionary<string, int> { [trait] = 1 }) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        foreach (var pool in new[] { new RunePool(2, 0, new Dictionary<string, int> { [trait] = 1 }),
                new RunePool(3, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) }) {
            var unpaid = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
            var result = await new CoreRuleEngine().ResolveAsync(unpaid, new("unpaid", "P1", command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(result.State));
        }
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(trait));
        await game.Top(); Assert.Equal(no == "VEN·SP5" ? 2 : 0, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·030/298")] [InlineData("OGN·030a/298")] [InlineData("VEN·168")]
    public async Task JinxHastePaysItsSeparateManaAndRedPowerAndEntersReady(string no)
    {
        var state = Position(no, 0) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 2 }), ["P2"] = new(20, 20) } };
        var command = new PlayCardCommand("UNIT", no, [], OptionalCosts: ["HASTE_READY"], Destination: "BASE");
        var insufficient = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(insufficient, new("bad-haste", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(insufficient), MatchStateHasher.Hash(rejected.State));
        var played = await Act(state, "P1", command); Assert.False(played.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, played.State.RunePools["P1"].Mana); Assert.Equal(0, played.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Contains("强攻2", played.State.CardObjects["UNIT"].Tags); var done = await Top(played.State); Restore(done.State);
    }

    [Theory]
    [InlineData("VEN·168")] [InlineData("VEN·SP5")]
    public async Task ChinaStandardDeckAcceptsTheNewHeroPrinting(string hero)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            hero == "VEN·168" ? "OGN·251/298" : "SFD·199/221", hero, Array.Empty<string>(),
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune(hero == "VEN·168" ? "red" : "blue"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }

    [Theory]
    [InlineData("VEN·168")] [InlineData("VEN·SP5")]
    public async Task PrintedTriggerSurvivesItsSourceReturningToHandWithoutLeakingIt(string no)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(no, 2), "RETURN", "OGN·104/298");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Single(game.State.StackItems);
        await game.Top(); var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        Assert.DoesNotContain("\"UNIT\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var selected = choice.LegalObjectIds.Where(id => id != "UNIT").Take(choice.RequiredCount).ToArray();
        await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·168")] [InlineData("VEN·SP5")]
    public async Task DiscardObserverDoesNotExposeAFormerSourceInThePublicEvent(string no)
    {
        var state = Position(no, 2);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OBSERVER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OBSERVER", "OGN·202/298", "P1") with { IsExhausted = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OBSERVER"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = [..state.PlayerZones["P1"].Base, "OBSERVER"] } } };
        state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top(); await game.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            choice.LegalObjectIds.Where(id => id != "UNIT").Take(choice.RequiredCount).ToArray()));
        Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null); await game.Top();
        var resolved = Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "JINX_DISCARDED_HAND_CARDS_READY_POWER_1"));
        Assert.Equal("HIDDEN", resolved.Event.Payload["discardSourceObjectId"]);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task ReturnedSourceIsNotExposedByTheOpponentsSnapshot()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position("VEN·168", 2), "RETURN", "OGN·104/298");
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", "VEN·168", [], Destination: "BASE"));
        var response = await Act(played.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        var returned = await Top(response.State);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(returned.Snapshots["P2"]));
        var paths = new List<string>();
        void Visit(JsonElement e, string path) {
            if (e.ValueKind == JsonValueKind.String && e.GetString() == "UNIT") paths.Add(path);
            if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) Visit(p.Value, path + "." + p.Name);
            if (e.ValueKind == JsonValueKind.Array) { var i = 0; foreach (var v in e.EnumerateArray()) Visit(v, path + "[" + i++ + "]"); }
        }
        Visit(json.RootElement, "$"); Assert.True(paths.Count == 0, string.Join(", ", paths));
    }
}
