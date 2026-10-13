using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyDefendObserverTests
{
    [Theory]
    [InlineData("OGN·121/298", false)]
    [InlineData("OGN·121/298", true)]
    [InlineData("SFD·230/221", false)]
    [InlineData("SFD·230/221", true)]
    public async Task RealShadyCopyDefendsWithAnIndependentCapturedAbilityAndSurvivesActualGaleDeparture(string card, bool sourceLeaves)
    {
        var initial = ShadySpectaclesTests.Position(card);
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ATTACKER", "UNL-057/219", "F", "P2");
        initial = LocalDestructionRecallTests.At(initial, "ATTACKER", "P2", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "GALE", "OGN·169/298", "P2");
        var cards = initial.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = initial.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        var zones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        foreach (var player in initial.Seats.Keys)
        {
            foreach (var id in zones[player].MainDeck) { cards.Remove(id); locations.Remove(id); }
            var deck = Enumerable.Range(1, 8).Select(i => "DEFEND-DECK-" + player + "-" + i).ToArray();
            for (var i = 0; i < deck.Length; i++)
            {
                var no = player == "P1" && i < 2 ? "OGN·121/298" : "SFD·125/221";
                cards[deck[i]] = new(deck[i], cardNo: no, ownerId: player, controllerId: player);
                locations[deck[i]] = new(player, "MAIN_DECK");
            }
            zones[player] = zones[player] with { MainDeck = deck };
        }
        foreach (var id in Enumerable.Range(1, 5).Select(i => "DEFEND-RUNE-" + i))
        {
            cards[id] = new(id, cardNo: "UNL-R03", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard, "COLOR:blue"]);
            locations[id] = new("P2", "BASE"); zones["P2"] = zones["P2"] with { Base = zones["P2"].Base.Append(id).ToArray() };
        }
        foreach (var id in zones["P1"].RuneDeck)
            cards[id] = cards[id] with { CardNo = "UNL-R01", Tags = [CardObjectTags.RuneCard, "COLOR:red"] };
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
        var driver = new Driver(initial);
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
        await driver.Top();
        Assert.NotNull(driver.State.PendingRuleChoice);
        var copy = driver.State.PendingRuleChoice;
        await driver.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.NotNull(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var pass = 0; pass < 4 && driver.State.FocusPlayerId is not null && driver.State.StackItems.Count == 0; pass++)
            await driver.Act(driver.State.FocusPlayerId, new PassFocusCommand());
        Assert.Null(driver.State.FocusPlayerId);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(MatchPhases.Main, driver.State.Phase);
        Assert.Equal("P2", driver.State.TurnPlayerId);
        var moved = await driver.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:F", []));
        Assert.DoesNotContain(moved.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED");
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("P1", choice.PlayerId);
        Assert.Equal(new[] { "ATTACKER" }, choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ATTACKER"]));
        var ability = Assert.Single(driver.State.StackItems);
        Assert.Equal("STANDBY_DEFEND", ability.FieldContext!.Kind);
        Assert.Equal(card, ability.CardNo);
        Assert.NotNull(ability.FieldContext.Origin);
        Assert.Equal("P1", ability.ControllerId);
        var generation = ability.FieldContext.SourceGeneration;
        if (sourceLeaves)
        {
            await driver.Act("P1", new PassPriorityCommand());
            for (var i = 1; i <= 3; i++) await driver.Act("P2", new TapRuneCommand("DEFEND-RUNE-" + i));
            for (var i = 4; i <= 5; i++) await driver.Act("P2", new RecycleRuneCommand("DEFEND-RUNE-" + i));
            await driver.Act("P2", new PlayCardCommand("GALE", "OGN·169/298", ["HOST"]));
            await driver.Top();
            Assert.Contains("HOST", driver.State.PlayerZones["P1"].Hand);
            Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > generation);
            Assert.Null(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
            Assert.Equal(ability.StackItemId, Assert.Single(driver.State.StackItems).StackItemId);
            var publicHistory = driver.State.BattlefieldResolutions.First(r => r.PublicObjectBindings?.Any(b => b.ObjectId == "HOST") == true);
            Assert.Contains(publicHistory.PublicObjectBindings!, b => b.ObjectId == "HOST" && b.Generation <= generation);
            var opponentSnapshot = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(driver.State)["P2"],
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.False(opponentSnapshot.GetProperty("players").GetProperty("P1").GetProperty("objects").TryGetProperty("HOST", out _));
            var forgedHistory = publicHistory with { PublicObjectBindings = publicHistory.PublicObjectBindings!.Select(b =>
                b.ObjectId == "HOST" ? b with { Generation = driver.State.CardObjects["HOST"].ObjectGeneration + 1 } : b).ToArray() };
            var forged = driver.State with { BattlefieldResolutions = driver.State.BattlefieldResolutions.Select(r =>
                r.ResolutionId == publicHistory.ResolutionId ? forgedHistory : r).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured public battlefield history reference"));
        }
        var done = await driver.Top();
        var reveal = Assert.Single(done.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED");
        Assert.Equal(2, reveal.Payload["damageAmount"]);
        Assert.Equal("P1", reveal.Payload["playerId"]);
        Assert.Contains(done.Events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload["targetObjectId"], "ATTACKER") && Equals(e.Payload["damage"], 2));
        Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED");
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState);
        Assert.Equal("P2", driver.State.FocusPlayerId);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private MatchSession session;
        public MatchState State { get; private set; }
        public Driver(MatchState state)
        {
            initial = state; State = state; session = new(state, new CoreRuleEngine(), this);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        public RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        public RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        public async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            var name = "defend-step-" + entries.Count;
            var prompt = session.PromptFor(player);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), options), options)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(State.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(player, name, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            State = result.State;
            OfficialGraveyardRecastTests.Restore(State); ArmamentMightTests.PublicRecovery(State);
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, State.Tick, sequence, snapshots[id], State.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            session = MatchSession.Restore(new(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator), new CoreRuleEngine(), this);
            var count = entries.Count;
            Assert.True((await session.SubmitAsync(player, name, command, raw, default)).Accepted);
            Assert.Equal(count, entries.Count);
            return result;
        }
        public async Task<ResolutionResult> Top()
        {
            await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            return await Act(State.PriorityPlayerId!, new PassPriorityCommand());
        }
    }
}
