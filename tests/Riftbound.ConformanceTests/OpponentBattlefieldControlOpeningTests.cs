using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OpponentBattlefieldControlOpeningTests
{
    private const string Vayne = "OGN·035/298";
    private const string Jhin = "UNL-022/219";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANewEmptyOpeningRecoversNeutralOrSwappedBattlefieldControlBeforeVayneEntry(bool swapped)
    {
        var (prefix, journal, source, ownField, otherField) = await Opening(swapped);
        Assert.True(prefix.IsConsistent);
        Assert.Equal(0, prefix.ReplayInitialState!.Tick);
        Assert.Equal(0, prefix.ReplayInitialState.Seed);
        Assert.Empty(prefix.ReplayInitialState.CardObjects);
        var before = prefix.AuthoritativeState!;
        Assert.Equal("P1", before.CardObjects[ownField].OwnerId);
        Assert.Equal("P2", before.CardObjects[otherField].OwnerId);
        Assert.Equal(swapped ? "P2" : null, before.CardObjects[ownField].ControllerId);
        Assert.Equal(swapped ? "P1" : null, before.CardObjects[otherField].ControllerId);
        Assert.Contains(ownField, before.PlayerZones["P1"].Battlefields);
        Assert.Contains(otherField, before.PlayerZones["P2"].Battlefields);
        var engine = new CoreRuleEngine();
        var session = await new InMemoryMatchSessionRegistry(engine, journal, new SourceStore(prefix))
            .GetOrCreateAsync(prefix.RoomId, default);
        var command = new PlayCardCommand(source, Vayne, []);
        var quote = engine.PreviewPlayCard(before, "P1", new("control-entry-quote", session.PromptFor("P1").PromptId!, before.Tick, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        var result = await session.SubmitAsync("P1", "normal-control-entry-confirm", command, Raw(command), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(!swapped, result.State.CardObjects[source].IsExhausted);
        Assert.Equal(before.RunePools["P1"].Mana - 4, result.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower - 1, result.State.RunePools["P1"].TotalPower);
        var entered = Assert.Single(result.Events, ev => ev.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Equal(!swapped, entered.Payload["isExhausted"]);
        Assert.Equal(swapped, entered.Payload.ContainsKey("entryStaticAbilityKind"));
        var final = Frame(prefix.ReplayInitialState, journal, result.State);
        Assert.NotNull(await new InMemoryMatchSessionRegistry(engine, NoopMatchJournal.Instance, new SourceStore(final))
            .GetOrCreateAsync(final.RoomId, default));
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default);
        Assert.Empty(errors);
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_OPPONENT_CONTROL_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            var path = Path.Combine(evidence, "opponent-battlefield-control-" + (swapped ? "swapped" : "neutral") + ".json");
            Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new {
                sourceRoomId = prefix.RoomId, seats = prefix.ReplayInitialState.Seats,
                expectedCardNo = Vayne, expectedSourceObjectId = source,
                expectedOwnBattlefieldObjectId = ownField, expectedOtherBattlefieldObjectId = otherField,
                expectedReady = swapped, expectedMana = 4, expectedPower = 1,
                acceptedCommands = prefix.Commands, originalOpeningSeed = prefix.ReplayInitialState.Seed,
                scope = "New legal decks and ordinary visible-hand/resource/movement/focus intents from empty seating. No imported snapshot, developer command, old native match migration, or change to original143/110/seed424242."
            }, JsonOptions));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private static JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), JsonOptions);

    private sealed class SourceStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(room == frame.RoomId ? frame : null);
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state)
    {
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }

    private static async Task<(MatchRecoveryFrame, Journal, string, string, string)> Opening(bool swapped)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        var red = allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains("red"))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "UNL-181/219", Jhin, new[] { Vayne, Vayne, Vayne },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, Enumerable.Repeat(red, 12).ToArray()])!;
        var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("normal-opponent-battlefield-control-" + (swapped ? "swapped" : "neutral"),
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal); var state = initial;
        async Task Act(string actor, GameCommand command)
        {
            var id = "normal-control-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, Raw(command), default),
                ReadyCommand => await session.ReadyAsync(actor, id, Raw(command), default),
                _ => await session.SubmitAsync(actor, id, command, Raw(command), default)
            };
            Assert.True(result.Accepted, command.CmdType + ": " + result.ErrorMessage);
            state = result.State; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("P1", submit); await Act("P2", submit);
        await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await Act(first, new MulliganCommand([])); await Act(second, new MulliganCommand([]));
        for (var turn = 0; state.TurnPlayerId != "P1" || Runes("P1").Count() < 4
            || !state.PlayerZones["P1"].Hand.Any(id => state.CardObjects[id].CardNo == Vayne); turn++)
        { Assert.True(turn < 64, "Find only naturally drawn own-hand Vayne before establishing control"); await Act(state.TurnPlayerId, new EndTurnCommand()); }
        var source = state.PlayerZones["P1"].Hand.First(id => state.CardObjects[id].CardNo == Vayne);
        string Field(string owner) => state.PlayerZones[owner].Battlefields.Single(id => state.CardObjects[id].Tags.Contains("CARD_TYPE:BATTLEFIELD"));
        var ownField = Field("P1"); var otherField = Field("P2");
        if (swapped)
        {
            var ownHero = state.PlayerZones["P1"].ChampionZone.Single(); var otherHero = state.PlayerZones["P2"].ChampionZone.Single();
            await Resources("P1"); await Act("P1", new PlayCardCommand(ownHero, Jhin, []));
            await Act("P1", new EndTurnCommand());
            await Resources("P2"); await Act("P2", new PlayCardCommand(otherHero, Jhin, []));
            await Act("P2", new EndTurnCommand());
            await Claim("P1", ownHero, otherField); await Act("P1", new EndTurnCommand());
            await Claim("P2", otherHero, ownField); await Act("P2", new EndTurnCommand());
        }
        await Resources("P1");
        return (Frame(initial, journal, state), journal, source, ownField, otherField);

        IEnumerable<string> Runes(string actor) => state.PlayerZones[actor].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        async Task Resources(string actor)
        {
            Assert.Equal(actor, state.TurnPlayerId);
            foreach (var rune in Runes(actor).Where(id => !state.CardObjects[id].IsExhausted).ToArray()) await Act(actor, new TapRuneCommand(rune));
            if (state.RunePools[actor].TotalPower == 0) await Act(actor, new RecycleRuneCommand(Runes(actor).First()));
        }
        async Task Claim(string actor, string unit, string field)
        {
            Assert.Equal(actor, state.TurnPlayerId);
            await Act(actor, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field, []));
            for (var pass = 0; state.SpellDuelState is { IsActive: true } && pass < 4; pass++)
            {
                var focus = ResolutionResult.BuildPrompts(state).Single(p => p.Value.EnabledActions().Contains(CommandTypes.PassFocus)).Key;
                await Act(focus, new PassFocusCommand());
            }
            Assert.Equal(actor, state.CardObjects[field].ControllerId);
            Assert.Null(state.PendingCardChoice); Assert.Null(state.PendingPayment); Assert.Empty(state.StackItems);
        }
    }
}
