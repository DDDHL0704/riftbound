using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AlbusFerrosBoonRuneTests
{
    private const string Albus = "OGN·230/298";

    [Theory]
    [InlineData(1, 3, "BASE")]
    [InlineData(2, 3, "BATTLEFIELD:BF")]
    [InlineData(2, 1, "BASE")]
    [InlineData(2, 0, "BASE")]
    public async Task PaysTheChosenBoonsBeforeResponsesAndCallsOnlyAvailableRunesAfterThem(int count, int runes, string destination)
    {
        var game = await Open(Position(runes), destination);
        var selected = count == 1 ? new[] { "ALLY" } : new[] { "SECOND", "ALLY" };
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(2, choice.MaxCount);
        Assert.Equal(new[] { "ALLY", "SECOND" }, choice.LegalObjectIds);
        Assert.Null(game.State.PriorityPlayerId);
        await Pay(game, selected);
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(item.TargetObjectIds);
        Assert.Null(item.TriggerCost!.ConsumedBoon);
        Assert.Equal(selected, item.TriggerCost.ConsumedBoons!.Select(b => b.ObjectId));
        Assert.All(selected, id => {
            Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects[id].Tags);
            Assert.Equal(4, game.State.CardObjects[id].Power);
        });
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED");
        await game.Top();
        var called = Math.Min(count, runes);
        Assert.Equal(runes - called, game.State.PlayerZones["P1"].RuneDeck.Count);
        for (var i = 1; i <= called; i++)
        {
            Assert.Contains("RUNE-" + i, game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects["RUNE-" + i].IsExhausted);
        }
        Assert.Equal(count, game.Events().Count(e => e.Event.Kind == "BOON_CONSUMED"));
        var ev = Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED");
        Assert.Equal(called, Convert.ToInt32(ev.Event.Payload["count"]));
        await game.Replay();
    }

    [Fact]
    public async Task WithNoPayableBoonTheOptionalAbilityIsNotConfirmed()
    {
        var game = new Driver(Position(boons: false));
        await game.Act("P1", new PlayCardCommand("UNIT", Albus, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "BOON_CONSUMED" or "RUNES_CALLED");
        await game.Replay();
    }

    [Fact]
    public async Task SelectingNoBoonsDeclinesWithoutSpendingOrCallingRunes()
    {
        var game = await Open(Position());
        await Pay(game, []);
        Assert.Empty(game.State.StackItems);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["SECOND"].Tags);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "BOON_CONSUMED" or "RUNES_CALLED");
        await game.Replay();
    }

    [Fact]
    public async Task EnemyEquipmentNonBoonDuplicateAndWrongPlayerPaymentsAreAtomicRejections()
    {
        var game = await Open(Boon(Boon(Position(), "ENEMY"), "GEAR"));
        var choice = game.State.PendingCardChoice!;
        foreach (var ids in new[] { new[] { "ENEMY" }, new[] { "GEAR" }, new[] { "UNIT" }, new[] { "ALLY", "ALLY" } })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await Pay(game, ["ALLY", "SECOND"]);
        await game.Top();
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "BOON_CONSUMED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("UNIT")]
    [InlineData("ALLY")]
    public async Task ReturningTheSourceOrAPaidUnitInResponseDoesNotRefundOrCancelTheRuneCalls(string returned)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(runes: 4), "RETURN", "OGN·104/298", "P1");
        var game = await Open(initial);
        await Pay(game, ["ALLY", "SECOND"]);
        var generation = game.State.CardObjects[returned].ObjectGeneration;
        if (game.State.PriorityPlayerId != "P1") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        Assert.Equal(generation + 1, game.State.CardObjects[returned].ObjectGeneration);
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        var runeCallsBeforeAbility = game.Events().Count(e => e.Event.Kind == "RUNES_CALLED");
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].RuneDeck);
        var albusCall = Assert.Single(game.Events().Where(e => e.Event.Kind == "RUNES_CALLED").Skip(runeCallsBeforeAbility));
        Assert.Equal(2, Convert.ToInt32(albusCall.Event.Payload["count"]));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["SECOND"].Tags);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "BOON_CONSUMED"));
        await game.Replay();
    }

    [Fact]
    public async Task CostConsumptionCanDestroyTheDamagedUnitAndStillPaysForItsRune()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Damage = 4 } } };
        var game = await Open(initial);
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await Pay(game, ["ALLY", "SECOND"]);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new ObjectBinding("ALLY", generation), Assert.Single(game.State.StackItems).TriggerCost!.ConsumedBoons![0]);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].RuneDeck);
        await game.Replay();
    }

    [Fact]
    public async Task AUnitKilledByPayingItsBoonQueuesLastBreathBeforeThePaidRuneAbility()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "OGN·096/298", "P1") with { Damage = 1 } } };
        initial = Boon(initial, "SECOND");
        var game = await Open(initial);
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await Pay(game, ["SECOND", "ALLY"]);
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count);
        await game.Top();
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "BOON_CONSUMED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("rebound")]
    [InlineData("unjournalled")]
    public async Task RecoveryRejectsForgedBoonCountsBindingsOrUnacceptedPayment(string mutation)
    {
        var initial = Boon(Position(), "ENEMY");
        var game = await Open(initial);
        MatchState forged;
        if (mutation == "unjournalled")
        {
            var choice = game.State.PendingCardChoice!;
            var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "SECOND"]);
            var simulated = await new CoreRuleEngine().ResolveAsync(game.State,
                new("albus-unjournalled", "P1", command.CmdType), command, default);
            Assert.True(simulated.Accepted, simulated.ErrorMessage);
            forged = simulated.State with { Tick = game.State.Tick, CardObjects = game.State.CardObjects };
        }
        else
        {
            await Pay(game, ["ALLY", "SECOND"]);
            var item = Assert.Single(game.State.StackItems);
            var receipt = item.TriggerCost! with { ConsumedBoons = mutation switch {
                "missing" => null,
                "partial" => [item.TriggerCost!.ConsumedBoons![0]],
                _ => [item.TriggerCost!.ConsumedBoons![0], new("ENEMY", game.State.CardObjects["ENEMY"].ObjectGeneration)] } };
            forged = game.State with { StackItems = [item with { TriggerCost = receipt }] };
        }
        var sequence = game.Events().Last().Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, forged.Tick, sequence,
            snapshots[p], forged.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, sequence, game.Commands(), game.Events(), views, forged, forged.Tick, spectator);
        Assert.Contains(errors, e => mutation == "missing" ? e.Contains("invalid trigger cost receipt", StringComparison.Ordinal)
            : e.Contains("boon cost", StringComparison.Ordinal) && e.Contains("payment history", StringComparison.Ordinal));
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, game.Commands(), game.Events(), views,
            errors, forged, initial, spectator) { OpeningSeed = initial.Seed };
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
    }

    private static MatchState Position(int runes = 3, bool boons = true)
    {
        var state = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(Albus), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = HandDiscardObserverTests.Put(state, "SECOND", "OGN·219/298", "P1", "BASE");
        if (boons) state = Boon(Boon(state, "ALLY"), "SECOND");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var deck = Enumerable.Range(1, runes).Select(i => "RUNE-" + i).ToArray();
        foreach (var id in deck)
        {
            cards[id] = new(id, cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", "RUNE_DECK");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { RuneDeck = deck } } };
    }
    private static MatchState Boon(MatchState state, string id) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { Power = state.CardObjects[id].Power + 1,
                Tags = [..state.CardObjects[id].Tags, CardObjectTags.Boon] } } };
    private static async Task<Driver> Open(MatchState initial, string destination = "BASE")
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Albus, [], Destination: destination));
        Assert.Equal("TRIGGER_COST_CONFIRMATION", Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice).ChoiceWindow);
        return game;
    }
    private static async Task Pay(Driver game, IReadOnlyList<string> ids)
    {
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("albus-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
