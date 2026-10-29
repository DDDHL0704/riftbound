using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ViktorOpponentTurnPlayTests
{
    private const string Effect = "CONTROLLER_OPPONENT_TURN_PLAY_CREATE_MINION";

    private static MatchState Position(string card, bool battlefield = false, bool copied = false)
    {
        var state = AttackDamageDistributionTests.Position(card, copied: copied);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 3 } } };
        if (battlefield) {
            state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "OTHER");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1", CardNo = "OGN·277/298" } } };
        }
        state = AttackDamageDistributionTests.Spell(state, "OPEN", "OGN·048/298", "P2");
        state = AttackDamageDistributionTests.Spell(state, "ANSWER", "OGN·169/298", "P1");
        return state with { TurnPlayerId = "P2", ActivePlayerId = "P2", CardPlays = new(state.TurnNumber, [], []) };
    }

    private static async Task OpenResponse(VenBasicFlowSpellTests.Driver game)
    {
        await game.Act("P2", new PlayCardCommand("OPEN", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("ANSWER", "OGN·169/298", ["ENEMY1"]));
    }

    private static CardObjectState[] Minions(MatchState state) => state.CardObjects.Values
        .Where(card => card.Tags.Contains(CardObjectTags.MinionTokenFamily)).ToArray();

    [Theory]
    [InlineData("OGN·117/298")]
    [InlineData("OGN·117a/298")]
    [InlineData("VEN·176")]
    public async Task EveryPrintingObservesCompletedCardPlayOnOpponentsTurnAndCreatesOneMinionAfterResponses(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal("创见先驱", card.SubTitle); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(3, card.Power);
        Assert.Contains("每当你在对手的回合内打出一张卡牌时", card.CardEffect);
        Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.ControllerOpponentTurnPlayCreateMinion);
        var game = new VenBasicFlowSpellTests.Driver(Position(no));
        await OpenResponse(game);
        Assert.Empty(Minions(game.State));
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Top();
        var observer = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Equal("P2", observer.OutsideHandPlayObserver!.TurnPlayerId);
        Assert.Empty(Minions(game.State));
        await game.Top();
        var token = Assert.Single(Minions(game.State));
        Assert.Equal("P1", token.OwnerId); Assert.Equal("P1", token.ControllerId); Assert.Equal(1, token.Power);
        Assert.True(token.IsExhausted); Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.State.CardPlays.Resolved, play => play.Source.ObjectId == token.ObjectId);
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·117/298")]
    [InlineData("OGN·117a/298")]
    [InlineData("VEN·176")]
    public async Task OpponentsCardAndOwnTurnCardDoNotTrigger(string no)
    {
        var other = new VenBasicFlowSpellTests.Driver(Position(no));
        await other.Act("P2", new PlayCardCommand("OPEN", "OGN·048/298", []));
        await other.Top();
        Assert.Empty(Minions(other.State)); Assert.DoesNotContain(other.State.StackItems, i => i.EffectKind == Effect);
        var ownState = VenFlowCombatKeywordTests.Position(false);
        ownState = ownState with {
            CardObjects = new Dictionary<string, CardObjectState>(ownState.CardObjects) {
                ["VIKTOR"] = new("VIKTOR", cardNo: no, power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(ownState.PlayerZones) {
                ["P1"] = ownState.PlayerZones["P1"] with { Base = [..ownState.PlayerZones["P1"].Base, "VIKTOR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(ownState.ObjectLocations) { ["VIKTOR"] = new("P1", "BASE") } };
        var own = new VenBasicFlowSpellTests.Driver(ownState);
        await own.Act("P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"])); await own.Top();
        Assert.Empty(Minions(own.State)); Assert.DoesNotContain(own.State.StackItems, i => i.EffectKind == Effect);
    }

    [Theory]
    [InlineData("OGN·117/298")]
    [InlineData("OGN·117a/298")]
    [InlineData("VEN·176")]
    public async Task IllegalResponseLeavesStateUnchangedAndDoesNotTrigger(string no)
    {
        var game = new VenBasicFlowSpellTests.Driver(Position(no));
        await game.Act("P2", new PlayCardCommand("OPEN", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        var before = game.State; var command = new PlayCardCommand("ANSWER", "OGN·169/298", ["ANSWER"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("invalid-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        Assert.Empty(Minions(rejected.State)); Assert.DoesNotContain(rejected.State.StackItems, i => i.EffectKind == Effect);
    }

    [Fact]
    public async Task CounteredResponseIsNotACompletedCardPlay()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·176"), "COUNTER", "OGN·064/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(initial); await OpenResponse(game);
        var target = Assert.Single(game.State.StackItems, i => i.SourceObjectId == "ANSWER").StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [target]));
        await game.Top();
        Assert.DoesNotContain(game.State.CardPlays.Resolved, p => p.Source.ObjectId == "ANSWER");
        Assert.Empty(Minions(game.State)); Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
    }

    [Theory]
    [InlineData("turn")]
    [InlineData("play")]
    [InlineData("card")]
    public async Task RecoveryRejectsForgedObserverWitness(string mutation)
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("VEN·176")); await OpenResponse(game); await game.Top();
        var item = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect); var witness = item.OutsideHandPlayObserver!;
        var forged = item with { OutsideHandPlayObserver = mutation switch {
            "turn" => witness with { TurnPlayerId = "P1" }, "play" => witness with { PlayId = "FORGED" },
            _ => witness with { CardNo = "OGN·001/298" } } };
        var stack = game.State.StackItems.Select(i => i == item ? forged : i).ToArray();
        Assert.Contains("invalid card play observer stack", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = stack }));
    }

    [Fact]
    public async Task CapturedTriggerStillCreatesMinionAfterSourceReturnsToHandWithoutLeakingIt()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·176", battlefield: true), "RETURN", "OGN·169/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(initial); await OpenResponse(game); await game.Top();
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var priorEvents = game.Events().Length;
        await game.Top();
        var token = Assert.Single(Minions(game.State)); Assert.DoesNotContain("HOST", token.ObjectId);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(priorEvents)));
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task CopiedPrintedTextCreatesTwoIndependentCardPlayTriggers()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("VEN·176", copied: true));
        await OpenResponse(game); await game.Top();
        var triggers = game.State.TriggerQueue.Where(t => t.EffectKind == Effect).ToArray();
        Assert.Equal(2, triggers.Length); Assert.Single(triggers, t => t.OutsideHandPlayObserver!.Origin is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggers.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count(i => i.EffectKind == Effect));
        await game.Top(); await game.Top();
        Assert.Equal(2, Minions(game.State).Length);
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·117/298")]
    [InlineData("OGN·117a/298")]
    [InlineData("VEN·176")]
    public async Task OpponentDirectedUnitPlayAlsoTriggersTheCardController(string no)
    {
        var initial = OfficialRevealedHandPlayTests.Position();
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["VIKTOR"] = new("VIKTOR", cardNo: no, power: 3, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, "VIKTOR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["VIKTOR"] = new("P2", "BASE") } };
        var opened = await OfficialRevealedHandPlayTests.Open(initial);
        var selected = await OfficialRevealedHandPlayTests.Select(opened.State, "U");
        var played = await Act(selected.State, "P2", new PlayCardCommand("U", "OGN·208/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal("P1", played.State.TurnPlayerId);
        Assert.NotNull(Assert.Single(played.State.StackItems, i => i.EffectKind == Effect).OutsideHandPlayObserver);
        Assert.Empty(Minions(played.State)); ArmamentMightTests.PublicRecovery(played.State);
        var done = await Top(played.State);
        var token = Assert.Single(Minions(done.State));
        Assert.Equal("P2", token.ControllerId); Assert.Contains(token.ObjectId, done.State.PlayerZones["P2"].Base);
        Restore(done.State);
    }

    [Fact]
    public async Task NewPrintingPaysBluePowerAndCanOpenAChinaStandardDeck()
    {
        const string no = "VEN·176"; const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var bad = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(bad, new("wrong-power", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(bad), MatchStateHasher.Hash(rejected.State));
        var game = new VenBasicFlowSpellTests.Driver(state); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Replay();

        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "OGN·265/298", no, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("blue"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("viktor-print-deck", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance, NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(false, format));
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(submit, options);
        foreach (var player in new[] { "P1", "P2" }) Assert.True((await session.SubmitDeckAsync(player, "deck-" + player, submit, raw, default)).Accepted);
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), options);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
    }
}
