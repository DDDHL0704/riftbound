using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GrantedOriginHasteOpeningTests
{
    private const string GrantCard = "SFD·029/221";
    private const string VoidDrone = "SFD·010/221";

    [Theory]
    [InlineData(VoidDrone, 2, false)]
    [InlineData("OGN·208/298", 5, true)]
    public async Task AnActualEmptyOpeningRecoversAndPaysGrantedHasteEvenWhenTheGrantPaysTheMandatoryCost(string card, int mana, bool sacrifice)
    {
        var (prefix, journal, source, grant) = await NormalOpening(card);
        Assert.True(prefix.IsConsistent);
        Assert.Equal(0, prefix.ReplayInitialState!.Tick); Assert.Equal(0, prefix.ReplayInitialState.Seed);
        Assert.Empty(prefix.ReplayInitialState.CardObjects);
        var engine = new CoreRuleEngine();
        var session = await new InMemoryMatchSessionRegistry(engine, journal, new SourceStore(prefix)).GetOrCreateAsync(prefix.RoomId, default);
        Assert.NotNull(session); var before = prefix.AuthoritativeState!;
        Assert.Equal(GrantCard, before.CardObjects[grant].CardNo); Assert.Contains(grant, before.PlayerZones["P1"].Battlefields);
        Assert.Equal("BATTLEFIELD", before.ObjectLocations[grant].Zone);
        Assert.Equal("STACK", before.ObjectLocations[source].Zone); Assert.DoesNotContain(source, before.PlayerZones["P1"].Hand);
        var costs = sacrifice ? new[] { "DESTROY_FRIENDLY_UNIT:" + grant, HasteOptionalCostNames.HasteReady } : [HasteOptionalCostNames.HasteReady];
        var command = new PlayCardCommand(source, card, [], OptionalCosts: costs);
        var quote = engine.PreviewPlayCard(before, "P1", new("cold-haste-quote", session.PromptFor("P1").PromptId!, before.Tick, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(mana, quote.Cost!.Mana);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var result = await session.SubmitAsync("P1", "normal-granted-haste-pay", command, JsonSerializer.SerializeToElement(command, json), default);
        Assert.True(result.Accepted, result.ErrorMessage); var state = result.State;
        Assert.Equal(before.RunePools["P1"].Mana - mana, state.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower - 1, state.RunePools["P1"].TotalPower);
        Assert.Contains(source, state.PlayerZones["P1"].Base); Assert.False(state.CardObjects[source].IsExhausted);
        Assert.Equal(sacrifice, state.PlayerZones["P1"].Graveyard.Contains(grant));
        Assert.Equal(!sacrifice, state.PlayerZones["P1"].Battlefields.Contains(grant));
        var sequence = journal.Entries[^1].CompletedEventSequence;
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        var final = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, events, views, [], state, prefix.ReplayInitialState,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
        var recovered = await new InMemoryMatchSessionRegistry(engine, NoopMatchJournal.Instance, new SourceStore(final)).GetOrCreateAsync(final.RoomId, default);
        Assert.NotNull(recovered);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(prefix.ReplayInitialState, commands, state, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_GRANTED_HASTE_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence); var path = Path.Combine(evidence, "granted-origin-haste-" + (sacrifice ? "sacrifice" : "unit") + ".json");
            Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new {
                sourceRoomId = prefix.RoomId, seats = prefix.ReplayInitialState.Seats, expectedCardNo = card,
                expectedSourceObjectId = source, expectedGrantObjectId = grant, expectedGrantCardNo = GrantCard,
                expectedMana = mana, expectedPower = 1, sacrificeGrant = sacrifice, acceptedCommands = prefix.Commands,
                originalOpeningSeed = prefix.ReplayInitialState.Seed,
                scope = "New fixed controlled room/deck from empty seating with ordinary commands and visible choices. Original143/110 seeds and native game unchanged. No future deck inspection, developer state or imported snapshot."
            }, json));
        }
    }

    private sealed class SourceStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(room == frame.RoomId ? frame : null);
    }

    private sealed class OpeningJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static async Task<(MatchRecoveryFrame Frame, OpeningJournal Journal, string Source, string Grant)> NormalOpening(string desired)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        var yellow = allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains("yellow"))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var red = allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains("red"))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var desiredTrait = desired == VoidDrone ? RuneTrait.Red : RuneTrait.Yellow;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·187/221", GrantCard, new[] { desired, desired, desired },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, Enumerable.Repeat(yellow, 6).Concat(Enumerable.Repeat(red, 6)).ToArray()])!;
        var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("normal-granted-origin-haste-" + (desired == VoidDrone ? "unit-case-1" : "sacrifice-case-1"),
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new OpeningJournal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal); var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        async Task Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
            var id = "normal-granted-haste-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, id, raw, default),
                _ => await session.SubmitAsync(actor, id, command, raw, default)
            };
            Assert.True(result.Accepted, command.CmdType + ": " + result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_GRANTED_HASTE_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence) && state.PendingCardChoice is { PlayerId: "P1" } visible)
            {
                Directory.CreateDirectory(evidence);
                await File.AppendAllTextAsync(Path.Combine(evidence, desired == VoidDrone ? "unit-choice-trace.jsonl" : "spell-choice-trace.jsonl"),
                    JsonSerializer.Serialize(new { state.Tick, state.TurnNumber, state.PlayerScores, visible.ChoiceWindow,
                        legal = visible.LegalObjectIds.Select(id => state.CardObjects.GetValueOrDefault(id)?.CardNo ?? id),
                        context = visible.ContextObjectIds.Select(id => state.CardObjects.GetValueOrDefault(id)?.CardNo ?? id),
                        knownDesiredInOwnHand = state.PlayerZones["P1"].Hand.Count(id => state.CardObjects[id].CardNo == desired) }) + "\n");
            }
        }
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("P1", submit); await Act("P2", submit); await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await Act(first, new MulliganCommand(state.PlayerZones[first].Hand.Where(id => state.CardObjects[id].CardNo == desired).ToArray()));
        await Act(second, new MulliganCommand(state.PlayerZones[second].Hand.Where(id => state.CardObjects[id].CardNo == desired).ToArray()));
        for (var turn = 0; state.TurnPlayerId != "P1" || Runes().Count() < 6; turn++)
        { Assert.True(turn < 40); await Act(state.TurnPlayerId, new EndTurnCommand()); }
        await Resources(false);
        var queen = state.PlayerZones["P1"].ChampionZone.Single();
        var grant = queen;
        await Act("P1", new PlayCardCommand(queen, GrantCard, [], OptionalCosts: []));
        await NextOwnTurn();
        var field = state.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First();
        string? source = null;
        for (var attempt = 0; attempt < 12 && source is null; attempt++)
        {
            Assert.Equal("IN_PROGRESS", state.Status);
            await Resources(true, desiredTrait);
            if (state.ObjectLocations[queen].Zone == "BATTLEFIELD")
            { await Act("P1", new MoveUnitCommand(queen, "BATTLEFIELD", "BASE", [])); await NextOwnTurn(); await Resources(true, desiredTrait); }
            await Act("P1", new MoveUnitCommand(queen, "BASE", "BATTLEFIELD:" + field, []));
            for (var step = 0; step < 40 && source is null; step++)
            {
                if (state.PendingRuleChoice is { } rule)
                {
                    var option = rule.Request.Options.FirstOrDefault(o => o.Id == "REVEAL")
                        ?? rule.Request.Options.FirstOrDefault(o => o.Id == "SKIP");
                    Assert.NotNull(option);
                    await Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [option.Id]));
                }
                else if (state.PendingCardChoice is { } choice)
                {
                    var found = choice.LegalObjectIds.FirstOrDefault(id => state.CardObjects.TryGetValue(id, out var c) && c.CardNo == desired);
                    if (found is not null)
                    { await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [found])); source = found; }
                    else if (choice.ChoiceWindow == "TRIGGER_COST_CONFIRMATION"
                        && choice.LegalObjectIds.Contains(state.PlayerZones["P1"].LegendZone.Single()))
                        await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                            [state.PlayerZones["P1"].LegendZone.Single()]));
                    else if (choice.LegalObjectIds.Contains("PAY"))
                        await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["PAY"]));
                    else if (choice.LegalObjectIds.Contains("DECLINE"))
                        await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DECLINE"]));
                    else
                    { Assert.Equal(0, choice.RequiredCount); await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); }
                }
                else if (state.StackItems.Count > 0) await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                else if (state.SpellDuelState is { IsActive: true })
                {
                    var actor = ResolutionResult.BuildPrompts(state).Single(p => p.Value.EnabledActions().Contains(CommandTypes.PassFocus)).Key;
                    await Act(actor, new PassFocusCommand());
                }
                else break;
            }
            if (source is null) await NextOwnTurn();
        }
        Assert.NotNull(source); Assert.NotNull(state.PendingEffectPlay);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return (new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)), journal, source!, grant);

        IEnumerable<string> Runes() => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        async Task Resources(bool power, string trait = RuneTrait.Yellow)
        {
            foreach (var rune in Runes().Where(id => !state.CardObjects[id].IsExhausted).ToArray()) await Act("P1", new TapRuneCommand(rune));
            if (power && state.RunePools["P1"].PowerByTrait.GetValueOrDefault(trait) == 0)
                await Act("P1", new RecycleRuneCommand(Runes().First(id => PrintedPowerCostRules.ForCard(state.CardObjects[id].CardNo!).Traits.Contains(trait))));
        }
        async Task NextOwnTurn()
        { do { await Act(state.TurnPlayerId, new EndTurnCommand()); } while (state.TurnPlayerId != "P1"); }
    }
}
