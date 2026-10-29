using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class VenDravenScoreMightTests
{
    private static MatchState Position(string no, int ownScore = 0, int enemyScore = 0, bool copied = false)
    {
        var state = ArmamentMightTests.Position(gear: copied ? "SFD·059/221" : "SFD·022/221", unit: no, power: 3, attached: copied);
        return state with { RoomId = "DRAVEN-SCORE-" + no,
            PlayerScores = new Dictionary<string, int> { ["P1"] = ownScore, ["P2"] = enemyScore } };
    }

    private static int Might(MatchState state, string player = "P1", string id = "HOST")
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty(player)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    [Theory]
    [InlineData("OGN·028/298", 31235)] [InlineData("VEN·172", 37875)]
    public async Task BothOfficialPrintingsDescribeTheSameControllerScoreAura(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("夸耀角斗士", card.SubTitle); Assert.Equal(5, card.Energy);
        Assert.Equal(1, card.ReturnEnergy); Assert.Equal(3, card.Power);
        var aura = Assert.Single(StaticAuraParser.Parse(card.CardEffect), a => a.Kind == StaticAuraKinds.SourceControllerScorePower);
        Assert.Equal("CONTROLLER_SCORE", aura.PowerMultiplierZone); Assert.Equal(1, aura.PowerDeltaPerParticipant);
    }

    [Theory]
    [InlineData("OGN·028/298", 0, 3)] [InlineData("OGN·028/298", 2, 5)] [InlineData("OGN·028/298", 7, 10)]
    [InlineData("VEN·172", 0, 3)] [InlineData("VEN·172", 2, 5)] [InlineData("VEN·172", 7, 10)]
    public void CurrentScoreChangesDisplayedAndEffectiveMightWithoutChangingPrintedPower(string no, int score, int expected)
    {
        var state = Position(no, ownScore: score, enemyScore: 6);
        Assert.Equal(expected, Might(state)); Assert.Equal(3, state.CardObjects["HOST"].Power);
        ArmamentMightTests.PublicRecovery(state); Restore(state);
        var changed = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 1, ["P2"] = 6 } };
        Assert.Equal(4, Might(changed)); ArmamentMightTests.PublicRecovery(changed);
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public void ControlChangeUsesTheNewControllersScore(string no)
    {
        var state = Position(no, ownScore: 1, enemyScore: 4); Assert.Equal(4, Might(state));
        state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BASE");
        Assert.Equal(7, Might(state, "P2")); ArmamentMightTests.PublicRecovery(state); Restore(state);
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public async Task ActualControlChangeImmediatelyUsesTheNewControllersScore(string no)
    {
        var state = CurrentPowerTargetTests.Position("UNL-140/219", 3);
        state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 2, ["P2"] = 0 },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["D"] = OfficialGarenConquestBattlefieldCountTests.Printed("D", no, "P2") } };
        var played = await Act(state, "P1", new PlayCardCommand("AOE", "UNL-140/219", ["D"]));
        var done = await Top(played.State);
        Assert.Equal("P1", done.State.CardObjects["D"].ControllerId); Assert.Equal("P2", done.State.CardObjects["D"].OwnerId);
        Assert.Contains("D", done.State.PlayerZones["P1"].Base); Assert.Equal(5, Might(done.State, id: "D"));
        ArmamentMightTests.PublicRecovery(done.State); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public void CopiedRuleTextAddsAnotherIndependentCurrentScoreBonus(string no)
    {
        var state = Position(no, ownScore: 2, copied: true);
        Assert.Equal(7, Might(state)); Assert.Equal(3, state.CardObjects["HOST"].Power);
        ArmamentMightTests.PublicRecovery(state); Restore(state);
        state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 3, ["P2"] = 0 } };
        Assert.Equal(9, Might(state)); ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public async Task ChampionRequiresFiveManaAndRedPowerAndGetsTheScoreBonusOnEntry(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 2, ["P2"] = 0 },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var unpaid = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpaid, new("wrong-power", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(rejected.State));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Equal(5, Might(game.State, id: "UNIT")); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public async Task ChinaStandardDeckAcceptsBothHeroPrintings(string hero)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·185/221", hero, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("red"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("draven-" + hero,
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

    [Theory]
    [InlineData("OGN·028/298", 0, true)] [InlineData("OGN·028/298", 1, false)]
    [InlineData("VEN·172", 0, true)] [InlineData("VEN·172", 1, false)]
    public async Task TargetLegalityUsesCurrentControllerScore(string no, int score, bool legal)
    {
        var state = CurrentPowerTargetTests.Position("UNL-159/219", 3);
        state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = score },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["D"] = OfficialGarenConquestBattlefieldCountTests.Printed("D", no, "P2") } };
        Assert.Equal(3 + score, CurrentPowerTargetTests.DisplayPower(state));
        Assert.Equal(legal, CurrentPowerTargetTests.Quote(state, "D").IsValid);
        var command = new PlayCardCommand("AOE", "UNL-159/219", ["D"]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("score-target", "P1", command.CmdType), command, default);
        Assert.Equal(legal, result.Accepted);
        if (!legal) { Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State)); }
        else { var done = await Top(result.State); Assert.Contains("D", done.State.PlayerZones["P2"].Graveyard); Restore(done.State); }
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public async Task ConquestScoreCrossingCapturesBothScoresForMightyTriggersAndRecovery(string no)
    {
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298");
        state = state with { RoomId = "DRAVEN-SCORE-CROSSING-" + no,
            PlayerScores = new Dictionary<string, int> { ["P1"] = 1, ["P2"] = 0 },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1"),
                ["FIORA"] = new("FIORA", cardNo: "SFD·180/221", power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
        state = LocalDestructionRecallTests.At(LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE"), "FIORA", "P1", "BASE");
        Assert.Equal(4, Might(state)); var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal(2, game.State.PlayerScores["P1"]); Assert.Equal(5, Might(game.State));
        var skill = Assert.Single(game.State.StackItems, item => item.MightySkill is not null).MightySkill!;
        Assert.Equal("HOST", skill.Unit.ObjectId); Assert.Equal(4, skill.BeforeMight); Assert.Equal(5, skill.AfterMight);
        Assert.Equal(1, skill.Before.PlayerScores!["P1"]); Assert.Equal(2, skill.After.PlayerScores!["P1"]);
        ArmamentMightTests.PublicRecovery(game.State);
        var forged = game.State with { StackItems = game.State.StackItems.Select(item => item.MightySkill is null ? item
            : item with { MightySkill = item.MightySkill with { After = item.MightySkill.After with { PlayerScores = null } } }).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("Mighty transition", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·028/298")] [InlineData("VEN·172")]
    public void ForgedPublicDynamicScoreAuraIsRejected(string no)
    {
        var state = Position(no, ownScore: 2); var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        ArmamentMightTests.PublicRecovery(state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var aura = Assert.Single(effects, effect => Equals(effect.GetValueOrDefault("effectKind"), StaticAuraKinds.SourceControllerScorePower));
            var old = aura["powerDelta"]; aura["powerDelta"] = 99;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator)); aura["powerDelta"] = old;
        }
    }

    [Fact]
    public async Task AHistoricalFrameWithoutScoreDependentTextRejectsInjectedScores()
    {
        var result = await MightyTransitionTests.OpenUnit();
        var item = Assert.Single(result.State.StackItems);
        var context = Assert.IsType<MightySkillContext>(item.MightySkill);
        Assert.Null(context.Before.PlayerScores); Assert.Null(context.After.PlayerScores);
        var forged = context with { After = context.After with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 } } };
        var state = result.State with { StackItems = [item with { MightySkill = forged }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("Mighty transition", StringComparison.Ordinal));
    }
}
