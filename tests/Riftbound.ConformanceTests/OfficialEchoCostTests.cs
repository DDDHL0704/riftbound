using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 204.1.a, 356.1.c, 820.1.c.2-3: complete, independently payable costs.
public sealed class OfficialEchoCostTests
{
    [Theory]
    [InlineData("UNL-061/219", 2, "", 0)]
    [InlineData("SFD·023/221", 2, "red", 0)]
    [InlineData("SFD·077/221", 4, "blue", 0)]
    [InlineData("SFD·080/221", 1, "blue", 0)]
    [InlineData("SFD·122/221", 0, "purple", 0)]
    [InlineData("SFD·182/221", 1, "", 1)]
    public void OfficialCostIncludesEveryResourceSymbol(string card, int mana, string trait, int generic)
    {
        var cost = Assert.IsType<EchoCostRules.Cost>(EchoCostRules.PrintedFor(card));
        Assert.Equal(mana, cost.Mana);
        Assert.Equal(generic, cost.GenericPower);
        Assert.Equal(trait.Length == 0 ? 0 : 1, cost.TypedPower.Values.Sum());
        if (trait.Length > 0) Assert.Equal(1, cost.TypedPower[trait]);
    }

    [Theory]
    [InlineData("UNL-007/219", true, 4, 2)]
    [InlineData("SFD·023/221", false, 4, 2)]
    [InlineData("SFD·182/221", false, 2, 2)]
        public async Task PreviewAndCommitChargeCompleteCost(string card, bool grant, int mana, int power)
    {
        var state = Position(card, new(10, 10), grant ? 1 : 0);
        var command = Command(card, ["ECHO"]);
        var engine = new CoreRuleEngine();
        var before = MatchStateHasher.Hash(state);
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        var result = await Play(state, command);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(10 - mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(10 - power, result.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(result.State.StackItems).EffectRepeatCount);
        Assert.Equal(power, Assert.Single(result.Events, e => e.Kind == "COST_PAID").Payload!["power"]);
    }

    [Theory]
    [InlineData("UNL-007/219", 1)]
    [InlineData("SFD·023/221", 0)]
    [InlineData("SFD·182/221", 0)]
    public async Task MissingEchoPowerRejectsAtomicallyAndIsNotOffered(string card, int grants)
    {
        var state = Position(card, new(10, 1), grants);
        var command = Command(card, ["ECHO"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        var result = await Play(state, command);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain(OptionalChoices(state), c => c.Id == "ECHO");
    }

    [Theory]
    [InlineData("ECHO", 4, 2, 2)]
    [InlineData("ECHO:GRANTED:1", 4, 2, 2)]
    [InlineData("ECHO,ECHO:GRANTED:1", 6, 3, 3)]
    public async Task PrintedAndGrantedEchoAreIndependent(string ids, int mana, int power, int repeats)
    {
        var state = Position("SFD·023/221", new(10, 10), 1);
        Assert.Equal(["ECHO", "ECHO:GRANTED:1"], OptionalChoices(state).Where(c => EchoCostRules.IsEcho(c.Id)).Select(c => c.Id));
        var result = await Play(state, Command("SFD·023/221", ids.Split(',')));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(10 - mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(10 - power, result.State.RunePools["P1"].TotalPower);
        Assert.Equal(repeats, Assert.Single(result.State.StackItems).EffectRepeatCount);
        Assert.DoesNotContain(result.State.UntilEndOfTurnEffects, effect => EchoCostRules.IsGrant(effect, "P1"));
    }

    [Theory]
    [InlineData("P1", 4)]
    [InlineData("P2", 6)]
    public async Task MaraiReducesEachEchoManaButNeverPower(string controller, int mana)
    {
        var state = Position("SFD·023/221", new(10, 10), 1);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["FIELD"] = state.CardObjects["FIELD"] with { CardNo = "SFD·211/221", ControllerId = controller } } };
        var command = Command("SFD·023/221", ["ECHO", "ECHO:GRANTED:1"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        var result = await Play(state, command);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(10 - mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(7, result.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task EchoReducedToZeroManaStillRequiresItsPower()
    {
        var state = Position("SFD·182/221", new(1, 2));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["FIELD"] = state.CardObjects["FIELD"] with { CardNo = "SFD·211/221" } } };
        Assert.Contains(OptionalChoices(state), c => c.Id == "ECHO" && c.Label.Contains("0 法力") && c.Label.Contains("1 任意符能"));
        var result = await Play(state, Command("SFD·182/221", ["ECHO"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
    }

    [Theory]
    [InlineData("ECHO,ECHO")]
    [InlineData("ECHO:GRANTED:99")]
    public async Task ForgedOrRepeatedEchoCostCannotAddExecutions(string ids)
    {
        var state = Position("SFD·023/221", new(10, 10), 1);
        var result = await Play(state, Command("SFD·023/221", ids.Split(',')));
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task RuneNeededOnlyForEchoIsOfferedAndPaidAtomically()
    {
        var state = Position("UNL-007/219", new(4, 0, new Dictionary<string, int> { ["red"] = 1 }), 1);
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                { ["RUNE"] = new("RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = ["RUNE"] } }
        };
        Assert.Contains(OptionalChoices(state), c => c.Id == "ECHO" && c.Label.Contains("符能"));
        var result = await Play(state, Command("UNL-007/219", ["ECHO", "RECYCLE_RUNE:RUNE"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Contains("RUNE", result.State.PlayerZones["P1"].RuneDeck);
    }

    [Fact]
    public void BaseAndEchoMulticolorAllocationsAreIndependent()
    {
        var pool = new RunePool(0, 0, new Dictionary<string, int> { ["red"] = 1, ["blue"] = 1 });
        Assert.True(PrintedPowerCostRules.TrySelect("SFD·182/221", "PRINTED_POWER:red:1", pool,
            0, new Dictionary<string, int>(), out var generic, out var typed, ["SFD·182/221"]));
        Assert.Equal(1, typed["red"]);
        Assert.Equal(1, typed["blue"]);
        Assert.True(PaymentCostRules.CanPayPowerCost(pool, generic, typed));
    }

    [Fact]
    public async Task MultipleGrantsSurviveSerializationAndAreConsumedEvenWhenDeclined()
    {
        var state = Position("UNL-061/219", new(10, 10), 2);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
        var result = await Play(restored, Command("UNL-061/219", ["ECHO", "ECHO:GRANTED:1", "ECHO:GRANTED:2"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(4, Assert.Single(result.State.StackItems).EffectRepeatCount);
        Assert.Equal(2, result.State.RunePools["P1"].Mana);
        result = await Play(state, Command("UNL-061/219", []));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.State.UntilEndOfTurnEffects, effect => EchoCostRules.IsGrant(effect, "P1"));
    }

    [Fact]
    public async Task DeckSelectionCannotBeSubmittedBeforeResolution()
    {
        var state = Position("SFD·122/221", new(10, 10));
        var result = await Play(state, Command("SFD·122/221", ["ECHO"]));
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Contains(OptionalChoices(state), c => EchoCostRules.IsEcho(c.Id));
    }

    [Fact]
    public async Task NativeSelectionMatchesAuthoritativeQuoteAndJournalReplay()
    {
        var state = Position("SFD·182/221", new(6, 3), 1);
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                { ["MECH"] = new("MECH", cardNo: "SFD·007/221", ownerId: "P1", controllerId: "P1", power: 2, tags: [CardObjectTags.UnitCard, "机械"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = ["MECH"] } }
        };
        var command = Command("SFD·182/221", ["ECHO", "ECHO:GRANTED:1"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        var journal = new EchoJournal();
        var session = new MatchSession(state, engine, journal);
        async ValueTask<ResolutionResult> Submit(string player, GameCommand action)
        {
            var result = await session.SubmitAsync(player, "echo-" + journal.Entries.Count, action,
                JsonSerializer.SerializeToElement(action, action.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
            return result;
        }
        var played = await Submit("P1", command);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        var resolved = played;
        while (resolved.State.StackItems.Count > 0)
            resolved = await Submit(resolved.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(5, resolved.State.CardObjects["MECH"].Power);
        Assert.Equal(3, journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, commands, resolved.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_ECHO_EVIDENCE") is { Length: > 0 } root)
        {
            Directory.CreateDirectory(root);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
            await File.WriteAllTextAsync(Path.Combine(root, "native-candidate.json"), JsonSerializer.Serialize(candidate, json));
            await File.WriteAllTextAsync(Path.Combine(root, "native-quote.json"), JsonSerializer.Serialize(quote, json));
            await File.WriteAllTextAsync(Path.Combine(root, "replay.json"), JsonSerializer.Serialize(replay, json));
            var path = Path.Combine(root, "native-command.json");
            if (File.Exists(path))
            {
                using var native = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                var result = await Play(state, Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(native.RootElement)));
                Assert.True(result.Accepted, result.ErrorMessage);
                var reference = await Play(state, command);
                Assert.Equal(MatchStateHasher.Hash(reference.State), MatchStateHasher.Hash(result.State));
                await File.WriteAllTextAsync(Path.Combine(root, "native-command-verified.json"), JsonSerializer.Serialize(new { accepted = true, hash = MatchStateHasher.Hash(result.State) }, json));
            }
        }
    }

    private sealed class EchoJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static IReadOnlyList<ActionPromptChoiceDto> OptionalChoices(MatchState state)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements);
        return Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
    }

    private static PlayCardCommand Command(string card, string[] costs)
        => new("CARD", card, card is "UNL-007/219" or "SFD·023/221" ? ["UNIT"] : card == "SFD·122/221" ? ["DECK1"] : [], OptionalCosts: costs);
    private static ValueTask<ResolutionResult> Play(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().ResolveAsync(state, new("echo", "P1", CommandTypes.PlayCard), command, default);
    private static MatchState Position(string card, RunePool pool, int grants = 0)
        => new("ECHO-OFFICIAL", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], Battlefields = ["FIELD"], MainDeck = ["DECK1", "DECK2", "DECK3", "DECK4"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER", "UNIT"] }
            },
            cardObjects: new Dictionary<string, CardObjectState>
            {
                ["CARD"] = new("CARD", cardNo: card, ownerId: "P1", controllerId: "P1"),
                ["FIELD"] = new("FIELD", cardNo: "UNL-216/219", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["OTHER"] = new("OTHER", cardNo: "OGN·280/298", ownerId: "P2", controllerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["UNIT"] = new("UNIT", cardNo: "OGN·003/298", ownerId: "P2", controllerId: "P2", power: 20, tags: [CardObjectTags.UnitCard]),
                ["DECK1"] = new("DECK1", cardNo: "OGN·003/298", ownerId: "P1", controllerId: "P1"),
                ["DECK2"] = new("DECK2", cardNo: "OGN·003/298", ownerId: "P1", controllerId: "P1"),
                ["DECK3"] = new("DECK3", cardNo: "OGN·003/298", ownerId: "P1", controllerId: "P1"),
                ["DECK4"] = new("DECK4", cardNo: "OGN·003/298", ownerId: "P1", controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["UNIT"] = new("P2", "BATTLEFIELD", "OTHER") },
            untilEndOfTurnEffects: Enumerable.Range(0, grants).Select(i => EchoCostRules.GrantPrefix + "P1" + (i == 0 ? "" : $":{i}")).ToArray());
}
