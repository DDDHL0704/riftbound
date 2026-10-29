using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenLegacyHeroPrintsTests
{
    public static IEnumerable<object[]> Printings() => new object[][] {
        ["VEN·168", 37871L, 3, 4], ["VEN·172", 37875L, 5, 3],
        ["VEN·184", 37887L, 4, 4], ["VEN·SP2", 37917L, 4, 4], ["VEN·SP5", 37920L, 3, 3] };
    public static IEnumerable<object[]> Costs() => Printings().Select(row => new[] { row[0], row[2] });
    public static IEnumerable<object[]> CardNumbers() => Printings().Select(row => new[] { row[0] });

    private static MatchState Position(string no)
        => AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Spell(
            VenUnitEntryBasicTests.Position(no), "FODDER", "VEN·049", "P1"), "FODDER2", "VEN·049", "P1");

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-legacy-hero", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EachOfficialPrintingPaysItsRealCostAndResolvesItsEntry(string no, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power); Assert.Equal(1, card.ReturnEnergy);
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(power, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        if (no is "VEN·168" or "VEN·SP5") {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            foreach (var bad in new string[][] { ["GEAR"], ["P2-DRAW-1"], ["FODDER", "FODDER"] })
                await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, bad));
            var discards = no == "VEN·168" ? new[] { "FODDER", "FODDER2" } : ["FODDER"];
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, discards));
            Assert.All(discards, target => Assert.Contains(target, game.State.PlayerZones["P1"].Hand));
            await game.Top();
            Assert.All(discards, target => Assert.Contains(target, game.State.PlayerZones["P1"].Graveyard));
            if (no == "VEN·SP5") {
                Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
                Assert.Contains("P1-DRAW-2", game.State.PlayerZones["P1"].Hand);
            }
        }
        if (no == "VEN·168") {
            Assert.Contains("急速", game.State.CardObjects["UNIT"].Tags);
            Assert.Contains("强攻2", game.State.CardObjects["UNIT"].Tags);
        }
        if (no == "VEN·184") Assert.Contains("坚守", game.State.CardObjects["UNIT"].Tags);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(Costs))]
    public async Task EveryPrintingRejectsInsufficientManaWithoutEnteringOrDiscarding(string no, int mana)
    {
        var state = Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana - 1, 20) } };
        await Reject(state, new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory]
    [MemberData(nameof(CardNumbers))]
    public async Task EachPrintingIsAcceptedAsTheChampionOfAChinaStandardDeck(string no)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        var hero = Assert.Single(allowed.Cards, c => c.CardNo == no);
        var legend = allowed.Cards.Where(c => c.CardCategory == "legendary" && c.Hero == hero.Hero)
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First();
        var runes = legend.CardColorList.SelectMany(color => Enumerable.Repeat(allowed.Cards
            .Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo, 6)).ToArray();
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legend.CardNo, no, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, runes])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("ven-legacy-deck-" + no,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var submitted = await session.SubmitDeckAsync("P1", "deck", command, raw, default);
        Assert.True(submitted.Accepted, submitted.ErrorMessage); Assert.Equal(no, submitted.State.PlayerDecklists["P1"].ChampionCardNo);
    }

    [Theory]
    [InlineData("OGN·028/298", 0)] [InlineData("OGN·028/298", 3)]
    [InlineData("VEN·172", 0)] [InlineData("VEN·172", 3)]
    public async Task ScoreBonusUsesCurrentControllerAndDisappearsOutsidePlay(string no, int score)
    {
        var state = Position(no) with { PlayerScores = new Dictionary<string, int> { ["P1"] = score, ["P2"] = 5 } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        int Might(MatchState current) => CurrentPowerTargetTests.Snapshot(current)
            .GetProperty("players").GetProperty(current.ObjectLocations["UNIT"].PlayerId)
            .GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
        Assert.Equal(3 + score, Might(game.State));
        var changedScore = game.State with { PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 5 } };
        Assert.Equal(9, Might(changedScore));
        var stolen = LocalDestructionRecallTests.At(game.State, "UNIT", "P2", "BASE");
        stolen = stolen with { CardObjects = new Dictionary<string, CardObjectState>(stolen.CardObjects) {
            ["UNIT"] = stolen.CardObjects["UNIT"] with { ControllerId = "P2" } } };
        Assert.Equal(8, Might(stolen));
        var hidden = LocalDestructionRecallTests.At(game.State, "UNIT", "P1", "HAND");
        Assert.Equal(3, Might(hidden));
        Assert.DoesNotContain(hidden.ContinuousEffects, effect => effect.EffectKind == StaticAuraKinds.SourceControllerScorePower);
        await game.Replay();
    }

    [Fact]
    public async Task PublicRecoveryRejectsForgedScoreBonusAndProvenance()
    {
        var game = new Driver(Position("VEN·172") with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 3, ["P2"] = 0 } });
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·172", []));
        var state = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var aura = Assert.Single(effects, effect => Equals(effect.GetValueOrDefault("effectKind"), StaticAuraKinds.SourceControllerScorePower));
            foreach (var key in new[] { "powerDelta", "effectivePower", "sourceCardNo", "sourcePath", "lifecycle" }) {
                var original = aura[key]; aura[key] = key is "powerDelta" or "effectivePower" ? 99 : "FORGED";
                Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator)); aura[key] = original;
            }
        }
        ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("OGN·238/298")] [InlineData("VEN·184")]
    public async Task AttackStunsOnlyAnEnemyHereAfterTheOpponentCanRespond(string no)
    {
        var state = AttackDamageDistributionTests.Position(no);
        state = OfficialHoldSequenceTests.AddUnit(state, "DISTANT", "OGN·001/298", "F", "P2");
        state = LocalDestructionRecallTests.At(state, "DISTANT", "P2", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++) await game.Act(focus, new PassFocusCommand());
        if (ResolutionResult.BuildPrompts(game.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        foreach (var id in new[] { "HOST", "GEAR", "DISTANT" }) {
            Assert.DoesNotContain(id, choice.LegalObjectIds);
            await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        }
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY2"].UntilEndOfTurnEffects); await game.Replay();
    }
}
