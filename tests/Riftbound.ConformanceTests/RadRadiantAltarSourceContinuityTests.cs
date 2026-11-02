using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadRadiantAltarSourceContinuityTests
{
    [Fact]
    public async Task ActualDestructionPreservesTheConfirmedBuffDrawAndDormantRuneCall()
    {
        // The printed equipment is fixture setup; conversion, payment,
        // confirmation, destruction and both earned skills use real commands.
        var state = Put(Put(Create(), "ALTAR", "RAD·046/167", "P1", "BASE"),
            "DESTROY", "SFD·163/221", "P1", "HAND");
        var game = new Driver(WithOrianna(state));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "ALTAR");
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].TotalPower;
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
        Assert.NotEmpty(runes);
        var sourcePower = game.State.CardObjects["ALTAR"].Power;
        var allyPower = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", "RAD_046_EXHAUST_DRAW_CALL_RUNE", []));
        Assert.True(game.State.CardObjects["ALTAR"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var buff = Assert.Single(game.State.StackItems, item => item.UnitEvent is not null);
        var activation = Assert.Single(game.State.StackItems, item => item.SimpleActivation is not null);
        Assert.Equal("BECAME_EXHAUSTED", buff.UnitEvent!.Timing);
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("DESTROY", "SFD·163/221", ["ALTAR", "ALLY"]));
        await game.Top();

        Assert.Contains("ALTAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["ALTAR"].ObjectGeneration > buff.UnitEvent.SourceGeneration);
        Assert.Equal(allyPower + sourcePower, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(buff, Assert.Single(game.State.StackItems, item => item.StackItemId == buff.StackItemId));
        Assert.Equal(activation, Assert.Single(game.State.StackItems, item => item.StackItemId == activation.StackItemId));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = game.State.StackItems.Select(item => item.StackItemId == activation.StackItemId
                ? item with { SimpleActivation = item.SimpleActivation! with { AcquiredTick = game.State.Tick + 1 } }
                : item).ToArray() }));
        await game.Top();
        Assert.Equal(allyPower + sourcePower + 3, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        Assert.Equal(deck - 2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(runes.Skip(1), game.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains(runes[0], game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects[runes[0]].IsExhausted);
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ActualRecallPreservesBothConfirmedSkillsAndOnlyExposesTheCapturedIncarnation(string owner)
    {
        // The public equipment/control boundary is setup. Animation, confirmation,
        // recall and both skills retain the complete original command history.
        var initial = Put(Create(), "ALTAR", "RAD·046/167", owner, "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "ALTAR", "P1", "BASE");
        initial = Put(initial, "RECALL", "OGN·104/298", "P1", "HAND");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 6; i++)
                initial = Put(initial, $"ALTAR-RUNE-{player}-{i}", "UNL-R03", player, "RUNE_DECK");
        initial = WithOrianna(initial);
        var setup = new Driver(initial);
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(setup, "ALTAR");
        var power = setup.State.CardObjects["ALLY"].Power;
        var deck = setup.State.PlayerZones["P1"].MainDeck.Count;
        var opponentDeck = setup.State.PlayerZones["P2"].MainDeck.ToArray();
        var runeDecks = setup.State.PlayerZones.ToDictionary(p => p.Key, p => p.Value.RuneDeck.ToArray());
        Assert.All(runeDecks.Values, runes => Assert.NotEmpty(runes));
        var pools = setup.State.RunePools.ToDictionary(p => p.Key, p => p.Value);
        await setup.Act("P1", new ActivateAbilityCommand("ALTAR", "RAD_046_EXHAUST_DRAW_CALL_RUNE", []));
        var choice = Assert.IsType<PendingCardChoiceState>(setup.State.PendingCardChoice);
        await setup.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var buff = Assert.Single(setup.State.StackItems, item => item.UnitEvent is not null);
        var activation = Assert.Single(setup.State.StackItems, item => item.SimpleActivation is not null);
        Assert.Equal("P1", buff.ControllerId); Assert.Equal("P1", activation.ControllerId);
        if (setup.State.PriorityPlayerId != "P1")
            await setup.Act(setup.State.PriorityPlayerId!, new PassPriorityCommand());
        await setup.Act("P1", new PlayCardCommand("RECALL", "OGN·104/298", ["ALTAR"]));
        await setup.Top();
        Assert.Contains("ALTAR", setup.State.PlayerZones[owner].Hand);
        Assert.True(setup.State.CardObjects["ALTAR"].ObjectGeneration > buff.UnitEvent!.SourceGeneration);
        Assert.Null(setup.State.CardObjects["ALTAR"].LegendEquipmentUnit);
        Assert.Equal(buff, Assert.Single(setup.State.StackItems, item => item.StackItemId == buff.StackItemId));
        Assert.Equal(activation, Assert.Single(setup.State.StackItems, item => item.StackItemId == activation.StackItemId));
        Assert.Equal(power, setup.State.CardObjects["ALLY"].Power);
        Assert.Equal(deck, setup.State.PlayerZones["P1"].MainDeck.Count);
        foreach (var player in new[] { "P1", "P2" })
            Assert.Equal(runeDecks[player].Skip(player == owner ? 1 : 0), setup.State.PlayerZones[player].RuneDeck);
        var recalledRune = runeDecks[owner][0];
        Assert.Contains(recalledRune, setup.State.PlayerZones[owner].Base);
        Assert.True(setup.State.CardObjects[recalledRune].IsExhausted);

        // The original Driver keeps its strict privacy check. Continue its exact
        // journal with the skill captured above, before the source entered hand.
        var game = new ConfirmedSkillContinuation(initial, setup, buff);
        await game.Top();
        Assert.Equal(power + 3, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(game.LastResult!));
        foreach (var forged in new[] {
            buff.UnitEvent! with { SourceGeneration = game.State.CardObjects["ALTAR"].ObjectGeneration },
            buff.UnitEvent! with { CapturedTick = game.State.Tick + 1 } })
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(game.LastResult!,
                [buff with { UnitEvent = forged }]));
        await game.Top();
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(opponentDeck, game.State.PlayerZones["P2"].MainDeck);
        foreach (var player in new[] { "P1", "P2" })
        {
            var called = (player == owner ? 1 : 0) + (player == "P1" ? 1 : 0);
            Assert.Equal(runeDecks[player].Skip(called), game.State.PlayerZones[player].RuneDeck);
            foreach (var id in runeDecks[player].Take(called))
            {
                Assert.Contains(id, game.State.PlayerZones[player].Base);
                Assert.True(game.State.CardObjects[id].IsExhausted);
            }
            Assert.Equal(pools[player].Mana - (player == "P1" ? 1 : 0), game.State.RunePools[player].Mana);
            Assert.Equal(pools[player].TotalPower, game.State.RunePools[player].TotalPower);
        }
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        await game.Replay();
    }

    // Prefix commands/events are taken unchanged from the accepted setup. This
    // is a continuation of that original history, not a derived state fixture.
    private sealed class ConfirmedSkillContinuation : IMatchJournal
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly MatchState initial;
        private readonly StackItemState captured;
        private readonly List<RecoveredCommand> commands;
        private readonly List<RecoveredEvent> events;
        private MatchSession session;
        internal MatchState State { get; private set; }
        internal ResolutionResult? LastResult { get; private set; }
        internal ConfirmedSkillContinuation(MatchState initial, Driver setup, StackItemState captured)
        {
            this.initial = initial; this.captured = captured; State = setup.State;
            commands = [..setup.Commands()]; events = [..setup.Events()];
            Assert.Equal(captured, Assert.Single(State.StackItems, item => item.StackItemId == captured.StackItemId));
            session = MatchSession.Restore(Frame(), new CoreRuleEngine(), this);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            commands.Add(new(entry.PlayerId, entry.ClientIntentId, entry.CommandType, entry.RawCommand,
                entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence,
                entry.Accepted, entry.ErrorMessage) { TypedCommand = entry.TypedCommand, ErrorCode = entry.ErrorCode, RuleInputs = entry.RuleInputs });
            events.AddRange(entry.Events.Select((value, index) =>
                new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, value)));
            return ValueTask.CompletedTask;
        }
        private MatchRecoveryFrame Frame()
        {
            var sequence = commands[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State);
            var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence,
                snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, commands, events, views, State, State.Tick, spectator);
            Assert.Empty(errors);
            return new(State.RoomId, State.Tick, sequence, commands.ToArray(), events.ToArray(), views,
                errors, State, initial, spectator) { OpeningSeed = initial.Seed };
        }
        internal async Task Top()
        {
            var top = State.StackItems[^1].StackItemId;
            for (var passes = 0; State.StackItems.Any(item => item.StackItemId == top); passes++)
            {
                Assert.InRange(passes, 0, 3);
                var player = State.PriorityPlayerId!;
                var intent = "altar-confirmed-" + commands.Count;
                var raw = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.PassPriority,
                    promptId = session.PromptFor(player).PromptId, snapshotTick = State.Tick }, Json);
                var command = GameCommandJsonMapper.Map(raw);
                var result = await session.SubmitAsync(player, intent, command, raw, default);
                Assert.True(result.Accepted, result.ErrorMessage); State = result.State; LastResult = result;
                FullGameEndToEndTests.AssertNoHiddenZoneLeak(result, [captured]);
                var frame = Frame();
                session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
                var count = commands.Count; var eventCount = events.Count; var hash = MatchStateHasher.Hash(State);
                var duplicate = await session.SubmitAsync(player, intent, command, raw, default);
                Assert.True(duplicate.Accepted); Assert.Equal(count, commands.Count); Assert.Equal(eventCount, events.Count);
                Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
                await Replay();
            }
        }
        internal async Task Replay()
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.ToArray(), State,
                new CoreRuleEngine(), default, events.ToArray());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
    }

    private static MatchState WithOrianna(MatchState state)
        => OriannaEquipmentConsumerFixture.WithResources(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } });
}
