using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GrantedTokenHasteOpeningTests
{
    private const string Spell = "SFD·154/221";
    private const string Grant = "SFD·029/221";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEmptyOpeningColdRecoversBeforeTheTokenPaysHasteForHandOrStandbyParent(bool standby)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        string Rune(string trait) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(trait))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·187/221", Grant, new[] { Spell, Spell, Spell }, new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("yellow"), 6).Concat(Enumerable.Repeat(Rune("red"), 6)).ToArray()])!;
        var legality = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legality.IsValid, string.Join("; ", legality.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("normal-token-haste-" + (standby ? "standby-case-1" : "hand-case-1"),
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal); var state = initial;
        async Task<ResolutionResult> Act(string actor, GameCommand command, JsonElement? native = null)
        {
            var raw = native ?? Raw(command);
            if (native is not null) command = GameCommandJsonMapper.Map(raw);
            var id = "normal-token-haste-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, id, raw, default),
                _ => await session.SubmitAsync(actor, id, command, raw, default)
            };
            Assert.True(result.Accepted, command.CmdType + ": " + result.ErrorMessage);
            state = result.State; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            return result;
        }
        var submitted = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("P1", submitted); await Act("P2", submitted); await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await Act(first, new MulliganCommand([])); await Act(second, new MulliganCommand([]));
        for (var turns = 0; state.TurnPlayerId != "P1" || Runes().Count() < 4
            || !state.PlayerZones["P1"].Hand.Any(id => state.CardObjects[id].CardNo == Spell); turns++)
        { Assert.True(turns < 64); await Act(state.TurnPlayerId, new EndTurnCommand()); }
        await TapAll();
        var grant = state.PlayerZones["P1"].ChampionZone.Single();
        await Act("P1", new PlayCardCommand(grant, Grant, []));
        await NextOwnTurn();
        var source = state.PlayerZones["P1"].Hand.First(id => state.CardObjects[id].CardNo == Spell);
        if (standby)
        {
            var field = state.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First();
            await Act("P1", new MoveUnitCommand(grant, "BASE", "BATTLEFIELD:" + field, []));
            for (var steps = 0; steps < 40; steps++)
            {
                if (state.PendingRuleChoice is { } rule)
                {
                    var decline = rule.Request.Options.FirstOrDefault(o => o.Id is "SKIP" or "DECLINE");
                    Assert.NotNull(decline); await Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [decline.Id]));
                }
                else if (state.PendingCardChoice is { } choice)
                {
                    var selected = choice.LegalObjectIds.Contains("DECLINE") ? new[] { "DECLINE" } : [];
                    Assert.True(selected.Length > 0 || choice.RequiredCount == 0);
                    await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
                }
                else if (state.StackItems.Count > 0) await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                else if (ResolutionResult.BuildPrompts(state).FirstOrDefault(p => p.Value.EnabledActions().Contains(CommandTypes.PassFocus)).Key is { } actor)
                    await Act(actor, new PassFocusCommand());
                else break;
            }
            await Act("P1", new RecycleRuneCommand(Runes().First()));
            await Act("P1", new HideCardCommand(source, Spell, "BATTLEFIELD:" + field, ["STANDBY_A"]));
            await NextOwnTurn();
            Assert.True(state.CardObjects[source].IsFaceDown);
        }
        await TapAll();
        await Act("P1", new RecycleRuneCommand(Runes().First()));
        var mana = state.RunePools["P1"].Mana; var power = state.RunePools["P1"].TotalPower;
        await Act("P1", new PlayCardCommand(source, Spell, []));
        Assert.Equal(mana - (standby ? 0 : 3), state.RunePools["P1"].Mana);
        await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        var pending = Assert.IsType<RuleChoiceContinuation>(state.PendingRuleChoice);
        Assert.Equal("TOKEN_HASTE_PAYMENT", pending.Request.Kind);
        var offered = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.PayCost);
        var offeredCost = JsonSerializer.SerializeToElement(offered.Metadata).GetProperty("cost");
        Assert.Equal(1, offeredCost.GetProperty("mana").GetInt32());
        Assert.Equal(1, offeredCost.GetProperty("power").GetInt32());
        Assert.Equal("指示物急速支付", offered.Label);
        Assert.DoesNotContain(state.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        var prefix = Frame(initial, journal, state);
        session = await new InMemoryMatchSessionRegistry(engine, journal, new Store(prefix)).GetOrCreateAsync(state.RoomId, default);
        var payment = pending.Request.Options.First(o => o.Id.StartsWith("PAY:TRAIT:", StringComparison.Ordinal)).Id;
        var command = new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [payment]);
        var nativeRoot = Environment.GetEnvironmentVariable("RIFTBOUND_TOKEN_HASTE_OPENING_NATIVE_EVIDENCE");
        string? nativeDirectory = null; JsonElement? nativeCommand = null;
        if (!string.IsNullOrWhiteSpace(nativeRoot))
        {
            nativeDirectory = Path.Combine(nativeRoot, standby ? "standby" : "hand"); Directory.CreateDirectory(nativeDirectory);
            void Export(string name, object value) => File.WriteAllText(Path.Combine(nativeDirectory, name), JsonSerializer.Serialize(value, value.GetType(), Json));
            Export("pay-prompt.json", session.PromptFor("P1")); Export("pay-snapshot.json", ResolutionResult.BuildSnapshots(state)["P1"]);
            Export("pay-selection.json", command); Export("steps.json", new[] { new { name = "pay", action = CommandTypes.PayCost } });
            var path = Path.Combine(nativeDirectory, "pay-command.json");
            if (File.Exists(path)) { using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path)); nativeCommand = document.RootElement.Clone(); }
        }
        var paid = await Act("P1", command, nativeCommand);
        var token = Assert.Single(state.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.False(token.IsExhausted);
        Assert.Equal(standby ? "BATTLEFIELD" : "BASE", state.ObjectLocations[token.ObjectId].Zone);
        Assert.Equal(mana - (standby ? 1 : 4), state.RunePools["P1"].Mana);
        Assert.Equal(power - 1, state.RunePools["P1"].TotalPower);
        var entry = Assert.Single(paid.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        Assert.Equal(true, entry.Payload["hasteReadyOptionalCostPaid"]);
        var final = Frame(initial, journal, state);
        Assert.NotNull(await new InMemoryMatchSessionRegistry(engine, NoopMatchJournal.Instance, new Store(final)).GetOrCreateAsync(state.RoomId, default));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default));
        if (nativeDirectory is not null) await File.WriteAllTextAsync(Path.Combine(nativeDirectory, "replay.json"), JsonSerializer.Serialize(new {
            nativeCommands = nativeCommand is null ? 0 : 1, coldRestores = 2, fullOriginalActionEventStateReplayMatched = true,
            finalHash = MatchStateHasher.Hash(state), originalOpeningTick = initial.Tick, originalOpeningCards = initial.CardObjects.Count
        }, Json));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_TOKEN_HASTE_OPENING_EVIDENCE") is { Length: > 0 } evidence)
        {
            Directory.CreateDirectory(evidence); var path = Path.Combine(evidence, "token-haste-" + (standby ? "standby" : "hand") + ".json");
            Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { sourceRoomId = state.RoomId, seats = initial.Seats,
                originalOpeningSeed = initial.Seed, acceptedCommands = prefix.Commands, expectedSourceObjectId = source, expectedCardNo = Spell,
                expectedTokenCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo, expectedDestination = standby ? "BATTLEFIELD" : "BASE",
                expectedHasteMana = 1, expectedHastePower = 1, expectedReady = true,
                scope = "Actual legal empty opening, no imported snapshot/developer command/future-deck inspection. This certifies only granted token Haste; SFD154 independent printed post-entry Yellow optional instruction remains separately OPEN. Original143/110/424242 unchanged."
            }, Json));
        }
        IEnumerable<string> Runes() => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        async Task TapAll() { foreach (var rune in Runes().Where(id => !state.CardObjects[id].IsExhausted).ToArray()) await Act("P1", new TapRuneCommand(rune)); }
        async Task NextOwnTurn() { do { await Act(state.TurnPlayerId, new EndTurnCommand()); } while (state.TurnPlayerId != "P1"); }
    }

    private static JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), Json);
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(room == frame.RoomId ? frame : null);
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state)
    {
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }
}
