using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using System.Reflection;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ConfirmedPlayOriginTests
{
    private const string StayAway = "UNL-042/219";

    // Current CN printing id34570; DOC01 135.2.b.7 preserves confirmation
    // action facts when control changes. Standby is not a play from hand.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task StayAwayDrawRequiresActualHandPlayAndBelongsToItsResolvingController(bool fromHand, bool changeControl)
    {
        var state = fromHand ? StandbySourceRulesTests.Position(StayAway)
            : await StandbySourceRulesTests.ReadyShadow(StayAway);
        if (fromHand) state = state with
        {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) }
        };
        Assert.Equal(fromHand, state.PlayerZones["P1"].Hand.Contains("SHADOW"));
        Assert.Equal(!fromHand, state.CardObjects["SHADOW"].IsFaceDown);
        var played = await Act(state, "P1", new PlayCardCommand("SHADOW", StayAway, ["HOST"]));
        state = played.State;
        Assert.Equal(fromHand, state.StackItems.Single(item => item.SourceObjectId == "SHADOW").PlayedFromHand);
        Assert.Equal(!fromHand, state.StackItems.Single(item => item.SourceObjectId == "SHADOW").PlayedFromStandby is not null);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
        if (changeControl)
        {
            var offered = await Act(state, "P1", new PassPriorityCommand());
            var spell = state.StackItems.Single(item => item.SourceObjectId == "SHADOW");
            var response = await Act(offered.State, "P2", new PlayCardCommand("SHADOW-REBUTTAL", RebuttalRulesTests.Card, [spell.StackItemId]));
            state = (await Top(response.State)).State;
            Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT", state.PendingRuleChoice!.Request.Kind);
            var payment = state.PendingRuleChoice.Request.Options.First(option => option.Id.StartsWith("PAY:", StringComparison.Ordinal)).Id;
            state = await RebuttalRulesTests.Choose(state, payment);
            state = await RebuttalRulesTests.Choose(state, "KEEP_ALL");
        }
        var controller = changeControl ? "P2" : "P1";
        Assert.Equal(controller, state.StackItems.Last().ControllerId);
        Assert.Equal(fromHand, state.StackItems.Single(item => item.SourceObjectId == "SHADOW").PlayedFromHand);
        var hand = state.PlayerZones[controller].Hand.Count;
        var deck = state.PlayerZones[controller].MainDeck.Count;
        var other = changeControl ? "P1" : "P2";
        var otherHand = state.PlayerZones[other].Hand.Count;
        var resolved = await Top(state);
        Assert.Equal(fromHand ? 1 : 0, resolved.State.PlayerZones[controller].Hand.Count - hand);
        Assert.Equal(fromHand ? 1 : 0, deck - resolved.State.PlayerZones[controller].MainDeck.Count);
        Assert.Equal(otherHand, resolved.State.PlayerZones[other].Hand.Count);
        Assert.Equal(fromHand ? 1 : 0, resolved.Events.Count(e => e.Kind == "CARD_DRAWN"));
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task ARealFizzGraveyardRecastDoesNotBecomeAHandPlay()
    {
        var initial = Fizz(StayAway);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["C"] = initial.CardObjects["C"] with { ManaCost = 3 } } };
        var entered = await Act(initial, "P1", new PlayCardCommand("F", "SFD·140/221", []));
        var opened = await Top(entered.State);
        Assert.Equal("GRAVEYARD", opened.State.PendingEffectPlay!.SourceZone);
        Assert.DoesNotContain("C", opened.State.PlayerZones["P1"].Hand);
        var played = await Act(opened.State, "P1", new PlayCardCommand("C", StayAway, ["F"]));
        var item = played.State.StackItems.Single(stack => stack.SourceObjectId == "C");
        Assert.False(item.PlayedFromHand); Assert.Null(item.PlayedFromStandby);
        Assert.NotNull(item.AfterPlayRecycle);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
        var hand = played.State.PlayerZones["P1"].Hand.Count;
        var done = await Top(played.State);
        Assert.Equal(hand, done.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(done.Events, ev => ev.Kind == "CARD_DRAWN");
        Assert.Equal("C", done.State.PlayerZones["P1"].MainDeck[^1]);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AnObservedOpponentDeckCardReallyPlayedFromExileDoesNotDrawAsAHandPlay()
    {
        var initial = DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BlindFury), "P2_TOP", StayAway);
        var driver = DeckPlaySequenceTests.Driver(initial, "origin-opponent-deck");
        await driver.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BlindFury, []));
        await driver.Top(); await DeckPlaySequenceTests.Select(driver, "P2_TOP");
        Assert.DoesNotContain("P2_TOP", driver.State.PlayerZones.Values.SelectMany(z => z.Hand));
        Assert.Equal("STACK", driver.State.ObjectLocations["P2_TOP"].Zone);
        await driver.Act("P1", new PlayCardCommand("P2_TOP", StayAway, ["HOST"]));
        var item = Assert.Single(driver.State.StackItems);
        Assert.False(item.PlayedFromHand); Assert.Null(item.PlayedFromStandby);
        Assert.Equal("P1", item.ControllerId); Assert.Equal("P2", driver.State.CardObjects["P2_TOP"].OwnerId);
        var before = driver.State.PlayerZones["P1"].Hand.Count;
        var done = await driver.Top();
        Assert.Equal(before, done.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(done.Events, ev => ev.Kind == "CARD_DRAWN");
        Assert.Contains("P2_TOP", done.State.PlayerZones["P2"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProductionRecoveryRejectsAFlippedOriginEvenWithMatchingForgedViews(bool fromHand)
    {
        var (valid, source) = await NormalOpeningFrame(fromHand);
        var initial = valid.ReplayInitialState!; var state = valid.AuthoritativeState!;
        Assert.Equal(0, initial.Tick); Assert.Equal("SEATING", initial.Status); Assert.Empty(initial.CardObjects);
        Assert.Equal(0, initial.Seed);
        Assert.Equal(fromHand, state.StackItems.Single(item => item.SourceObjectId == source).PlayedFromHand);
        var sequence = valid.LastEventSequence;
        MatchRecoveryFrame Frame(MatchState current)
        {
            var snapshots = ResolutionResult.BuildSnapshots(current); var prompts = ResolutionResult.BuildPrompts(current);
            var views = current.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
                current.Tick, sequence, snapshots[id], current.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(current.RoomId, current.Tick, sequence, [], current);
            return new(current.RoomId, current.Tick, sequence, valid.Commands, valid.Events, views, [], current, initial, spectator);
        }
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new Store(valid));
        Assert.NotNull(await registry.GetOrCreateAsync(state.RoomId, default));
        var forged = state with { StackItems = state.StackItems.Select(item => item.SourceObjectId == source
            ? item with { PlayedFromHand = !fromHand } : item).ToArray() };
        var bad = Frame(forged);
        Assert.Empty(MatchRecoveryValidator.Validate(bad.RoomId, sequence, bad.Commands, bad.Events, bad.PlayerViews,
            forged, forged.Tick, bad.SpectatorReplayFrame));
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(bad, new CoreRuleEngine(), default);
        Assert.NotEmpty(errors);
        var rejected = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new Store(bad));
        await Assert.ThrowsAsync<MatchSessionException>(async () => { await rejected.GetOrCreateAsync(state.RoomId, default); });
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, valid.Commands, state, new CoreRuleEngine(), default, valid.Events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OriginRoundTripsExplicitlyAndCannotDefaultAMissingSerializedFact(bool fromHand)
    {
        var initial = fromHand ? StandbySourceRulesTests.Position(StayAway)
            : await StandbySourceRulesTests.ReadyShadow(StayAway);
        if (fromHand) initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 20) } };
        var played = await Act(initial, "P1", new PlayCardCommand("SHADOW", StayAway, ["HOST"]));
        var source = played.State.StackItems.Single(item => item.SourceObjectId == "SHADOW");
        var node = JsonSerializer.SerializeToNode(source)!.AsObject();
        Assert.Equal(fromHand, node["PlayedFromHand"]!.GetValue<bool>());
        Assert.Equal(fromHand, JsonSerializer.Deserialize<StackItemState>(node.ToJsonString())!.PlayedFromHand);
        Assert.True(node.Remove("PlayedFromHand"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StackItemState>(node.ToJsonString()));
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static async Task<(MatchRecoveryFrame Frame, string Source)> NormalOpeningFrame(bool fromHand)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(card, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        var preset = PreconstructedDeckCatalog.Build(allowed, OfficialDeckFormat.ChinaStandard20260724).Single(deck => deck.Id == "lillia-lowcurve").Decklist;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            preset.LegendCardNo, preset.ChampionCardNo, new[] { StayAway, StayAway, StayAway, "OGN·096/298", "OGN·096/298", "OGN·096/298" },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, preset.RuneDeck])!;
        var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("normal-origin-" + fromHand,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal); var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        async Task ActNormal(string actor, GameCommand command)
        {
            var id = "normal-origin-" + journal.Entries.Count;
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
            var result = command switch
            {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, id, raw, default),
                _ => await session.SubmitAsync(actor, id, command, raw, default)
            };
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await ActNormal("P1", submit); await ActNormal("P2", submit);
        await ActNormal("P1", new ReadyCommand()); await ActNormal("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await ActNormal(first, new MulliganCommand([])); await ActNormal(second, new MulliganCommand([]));
        string? source = null, unit = null;
        for (var turns = 0; turns < 160; turns++)
        {
            Assert.Equal("IN_PROGRESS", state.Status); Assert.Equal(MatchPhases.Main, state.Phase);
            if (state.TurnPlayerId == "P1")
            {
                source = state.PlayerZones["P1"].Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == StayAway);
                unit = state.PlayerZones["P1"].Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == "OGN·096/298");
                if (source is not null && unit is not null && Runes().Count() >= 6) break;
            }
            await ActNormal(state.TurnPlayerId, new EndTurnCommand());
        }
        Assert.NotNull(source); Assert.NotNull(unit);
        foreach (var rune in Runes().Where(id => !state.CardObjects[id].IsExhausted).ToArray()) await ActNormal("P1", new TapRuneCommand(rune));
        await ActNormal("P1", new PlayCardCommand(unit!, "OGN·096/298", [], OptionalCosts: []));
        if (!fromHand)
        {
            await NextOwnTurn();
            var field = state.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First();
            await ActNormal("P1", new MoveUnitCommand(unit!, "BASE", "BATTLEFIELD:" + field, []));
            for (var passes = 0; state.SpellDuelState is { IsActive: true } && passes < 4; passes++)
            {
                var focus = ResolutionResult.BuildPrompts(state).Single(pair => pair.Value.EnabledActions().Contains(CommandTypes.PassFocus)).Key;
                await ActNormal(focus, new PassFocusCommand());
            }
            Assert.False(state.SpellDuelState?.IsActive == true);
            await ActNormal("P1", new RecycleRuneCommand(Runes().First()));
            await ActNormal("P1", new HideCardCommand(source!, StayAway, "BATTLEFIELD:" + field, ["STANDBY_A"]));
            Assert.True(state.CardObjects[source!].IsFaceDown);
            await NextOwnTurn();
        }
        Assert.Equal(fromHand, state.PlayerZones["P1"].Hand.Contains(source!));
        await ActNormal("P1", new PlayCardCommand(source!, StayAway, [unit!], OptionalCosts: []));
        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId, entry.CommandType, entry.RawCommand,
            entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { ErrorCode = entry.ErrorCode, RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((ev, index) => new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(actor => actor, actor => new RecoveredPlayerView(actor, state.Tick, sequence, snapshots[actor], state.Tick, sequence, prompts[actor]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return (new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial, spectator), source!);

        IEnumerable<string> Runes() => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        async Task NextOwnTurn()
        {
            do { await ActNormal(state.TurnPlayerId, new EndTurnCommand()); } while (state.TurnPlayerId != "P1");
        }
    }
}
