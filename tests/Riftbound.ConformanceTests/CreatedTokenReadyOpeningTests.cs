using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CreatedTokenReadyOpeningTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Theory]
    [InlineData(SandSoldierEmbeddedReadyTests.Protect, 0, true)]
    [InlineData(SandSoldierEmbeddedReadyTests.Protect, 0, false)]
    [InlineData(SandSoldierEmbeddedReadyTests.Rise, 0, false)]
    [InlineData(SandSoldierEmbeddedReadyTests.Rise, 2, false)]
    public async Task LegalEmptyOpeningColdRecoversTheCreatedTokenTriggerAndReplaysEveryOriginalCommand(string card, int equipment, bool pay)
    {
        var name = card == SandSoldierEmbeddedReadyTests.Protect ? "protect-" + (pay ? "pay" : "decline") : "rise-" + equipment;
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        string Rune(string trait) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(trait))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·197/221", "SFD·177/221", new[] { card, card, card, "SFD·033/221", "SFD·033/221", "SFD·033/221" },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("green"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var legality = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legality.IsValid, string.Join("; ", legality.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("created-token-ready-" + name,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        Assert.Equal(0, initial.Tick); Assert.Empty(initial.CardObjects); Assert.Equal(MatchStatuses.Seating, initial.Status);
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal); var state = initial;
        async Task<ResolutionResult> Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), Json);
            var id = "created-ready-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, id, raw, default),
                _ => await session.SubmitAsync(actor, id, command, raw, default)
            };
            Assert.True(result.Accepted, command.CmdType + ": " + result.ErrorMessage);
            state = result.State; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result); return result;
        }
        var submitted = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("P1", submitted); await Act("P2", submitted); await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await Act(first, new MulliganCommand([])); await Act(second, new MulliganCommand([]));
        for (var turn = 0; state.TurnPlayerId != "P1" || Runes().Count() < 7
            || !Hand().Any(id => state.CardObjects[id].CardNo == card)
            || Hand().Count(id => state.CardObjects[id].CardNo == "SFD·033/221") < equipment; turn++)
        { Assert.True(turn < 100, "Natural draw must find this legal deck's finite required cards."); await Act(state.TurnPlayerId, new EndTurnCommand()); }
        await TapAll();
        foreach (var gear in Hand().Where(id => state.CardObjects[id].CardNo == "SFD·033/221").Take(equipment).ToArray())
        { await Act("P1", new PlayCardCommand(gear, "SFD·033/221", [])); await Settle(false); }
        if (equipment > 0)
        {
            do { await Act(state.TurnPlayerId, new EndTurnCommand()); } while (state.TurnPlayerId != "P1");
            await TapAll();
        }
        var source = Hand().First(id => state.CardObjects[id].CardNo == card);
        var runeTrait = card == SandSoldierEmbeddedReadyTests.Protect ? RuneTrait.Yellow : RuneTrait.Green;
        var paymentRune = Runes().First(id => state.CardObjects[id].Tags.Contains("COLOR:" + runeTrait));
        await Act("P1", new RecycleRuneCommand(paymentRune));
        await Act("P1", new PlayCardCommand(source, card, [])); await Settle(true);
        var context = Assert.Single(state.StackItems, i => i.CreatedTokenReady is not null).CreatedTokenReady!;
        var expectedCount = card == SandSoldierEmbeddedReadyTests.Protect ? 1 : equipment;
        var expectedTokens = Enumerable.Range(1, expectedCount).Select(i => source + "-TOKEN-" + i.ToString("D3")).ToArray();
        Assert.Equal(expectedTokens, context.Units.Select(u => u.ObjectId));
        Assert.All(expectedTokens, id => { Assert.Equal("SFD·T02", state.CardObjects[id].CardNo); Assert.True(state.CardObjects[id].IsExhausted); });
        Assert.Contains(source, state.PlayerZones["P1"].Graveyard);
        var prefix = Frame(initial, journal, state); var pendingHash = MatchStateHasher.Hash(state);
        session = await new InMemoryMatchSessionRegistry(engine, journal, new Store(prefix)).GetOrCreateAsync(state.RoomId, default);
        Assert.Equal(pendingHash, MatchStateHasher.Hash(prefix.AuthoritativeState!));
        var mana = state.RunePools["P1"].Mana; var yellow = state.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow);
        if (card == SandSoldierEmbeddedReadyTests.Protect)
        {
            var p = state.PendingPayment!; Assert.Equal(0, p.ManaCost); Assert.Equal(1, p.PowerCostByTrait[RuneTrait.Yellow]);
            Assert.True(yellow >= 1, "Both pay and decline are exercised while affordable.");
            await Act("P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            Assert.Equal(mana, state.RunePools["P1"].Mana);
            Assert.Equal(yellow - (pay ? 1 : 0), state.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        }
        else if (equipment > 0)
        {
            var choice = state.PendingCardChoice!; Assert.Equal(0, choice.RequiredCount); Assert.Equal(2, choice.MaxCount);
            Assert.Equal(expectedTokens, choice.LegalObjectIds);
            await Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, expectedTokens.Take(2).ToArray()));
        }
        Assert.All(expectedTokens, id => Assert.True(state.CardObjects[id].IsExhausted));
        var confirmed = Frame(initial, journal, state);
        session = await new InMemoryMatchSessionRegistry(engine, journal, new Store(confirmed)).GetOrCreateAsync(state.RoomId, default);
        await Settle(false);
        Assert.All(expectedTokens, id => Assert.Equal(card == SandSoldierEmbeddedReadyTests.Protect && !pay, state.CardObjects[id].IsExhausted));
        var final = Frame(initial, journal, state); Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default));
        Assert.NotNull(await new InMemoryMatchSessionRegistry(engine, NoopMatchJournal.Instance, new Store(final)).GetOrCreateAsync(state.RoomId, default));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_CREATED_READY_OPENING_EVIDENCE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output); var path = Path.Combine(output, "embedded-ready-" + name + ".json"); Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { sourceRoomId = state.RoomId, seats = initial.Seats,
                originalOpeningSeed = initial.Seed, acceptedCommands = prefix.Commands, expectedSourceObjectId = source, expectedCardNo = card,
                expectedTokenObjectIds = expectedTokens, expectedEquipment = equipment, expectedPayYellow = pay,
                expectedReadyObjectIds = card == SandSoldierEmbeddedReadyTests.Protect ? pay ? expectedTokens : [] : expectedTokens.Take(2).ToArray(),
                fullOriginalActionEventStateReplayMatched = true, finalCommands = final.Commands.Count, finalEvents = final.Events.Count,
                finalStateHash = MatchStateHasher.Hash(state), coldRestores = 3,
                scope = "Ordinary legal empty opening and current created-token follow-up only; no developer command, imported opening, future deck inspection or whole-card certification." }, Json));
        }
        IEnumerable<string> Hand() => state.PlayerZones["P1"].Hand;
        IEnumerable<string> Runes() => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        async Task TapAll() { foreach (var rune in Runes().Where(id => !state.CardObjects[id].IsExhausted).ToArray()) await Act("P1", new TapRuneCommand(rune)); }
        async Task Settle(bool stopAtReady)
        {
            for (var step = 0; step < 128; step++)
            {
                if (stopAtReady && state.StackItems.Any(i => i.CreatedTokenReady is not null)
                    && (state.PendingPayment?.ResolvingStackItemId is { } p && state.StackItems.Any(i => i.StackItemId == p && i.CreatedTokenReady is not null)
                        || state.PendingCardChoice?.EffectKind == "CREATED_TOKEN_READY"
                        || state.PendingPayment is null && state.PendingCardChoice is null && state.TriggerQueue.Count == 0
                            && state.StackItems.LastOrDefault()?.CreatedTokenReady is not null)) return;
                if (state.PendingRuleChoice is { } rule)
                {
                    var decline = rule.Request.Options.First(o => o.Id is "DECLINE" or "SKIP");
                    await Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [decline.Id]));
                }
                else if (state.PendingPayment is { } cost) await Act(cost.PlayerId, new PayCostCommand(cost.PaymentId, cost.PaymentWindow, ["DECLINE"]));
                else if (state.PendingCardChoice is { } choice)
                {
                    Assert.Equal(0, choice.RequiredCount);
                    await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
                }
                else if (ResolutionResult.BuildPrompts(state).FirstOrDefault(p => p.Value.EnabledActions().Contains(CommandTypes.OrderTriggers)).Key is { } actor)
                    await Act(actor, new OrderTriggersCommand(state.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                else if (state.StackItems.Count > 0) await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                else return;
            }
            Assert.Fail("Finite trigger setup exceeded its explicit bound.");
        }
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    { public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame); }
    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state)
    {
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }
}
