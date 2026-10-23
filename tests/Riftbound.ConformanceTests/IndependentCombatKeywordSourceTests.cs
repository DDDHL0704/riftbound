using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 807.2 / 814.2: independent Assault / Shield grants add. A card's
// printed keyword and its own parsed representation are still one source.
public sealed class IndependentCombatKeywordSourceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PrintedAssaultAndIndependentFarronAddInTheRetainedEightCommandControl()
    {
        var initial = Position(1);
        var driver = Open(initial, "printed-plus-farron-control");
        await PlayAndReady(driver);
        await BeginAttack(driver, ["ALLY", "FARRON"]);
        var actual = Power(driver.State, "P1", "ALLY");
        await Replay(initial, driver, new { expected = 4, actual });
        Assert.Equal(8, driver.Commands().Length);
        Assert.Equal(22, driver.Events().Length);
        Assert.Equal(4, actual);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public async Task PrintedKeywordAndEachIndependentAuraReachSnapshotsPoolsAndActualDamage(bool steadfast, int auras)
    {
        var initial = Position(auras, steadfast);
        var driver = Open(initial, $"printed-{steadfast}-auras-{auras}");
        await PlayAndReady(driver, steadfast);
        var crew = Crew(auras);
        if (steadfast) await MoveDefendersAndBeginOpponentAttack(driver, crew);
        else await BeginAttack(driver, ["ALLY", ..crew]);
        var expected = 3 + auras;
        AssertPowerForBothViewers(driver.State, "ALLY", expected);
        if (!steadfast)
            foreach (var id in crew) AssertPowerForBothViewers(driver.State, id, 5 + auras - 1);
        await FinishBattle(driver, expected);
        var damage = driver.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "ALLY")).ToArray();
        Assert.NotEmpty(damage);
        Assert.All(damage, e => Assert.Equal(expected, Convert.ToInt32(e.Event.Payload["combatPower"])));
        Assert.Equal(expected, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
        await Replay(initial, driver, new { expected, auras, steadfast });
    }

    [Theory]
    [InlineData(false, 0, 1)] [InlineData(false, 1, 1)] [InlineData(false, 1, 2)]
    [InlineData(true, 0, 1)] [InlineData(true, 1, 1)] [InlineData(true, 1, 2)]
    public async Task SeparateTemporarySpellsAddAlongsidePrintedAndIndependentAura(bool steadfast, int auras, int spells)
    {
        var initial = Position(auras, steadfast);
        var grant = steadfast ? "OGN·057/298" : "OGN·004/298";
        for (var i = 1; i <= spells; i++) initial = AttackDamageDistributionTests.Spell(initial, "GRANT" + i, grant, "P1");
        if (steadfast)
            for (var i = 1; i <= 4; i++)
            {
                var id = "BLUE-RUNE-" + i;
                initial = initial with
                {
                    CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
                    {
                        [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R03", "P1")
                    },
                    PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                    {
                        ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, id] }
                    },
                    ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new("P1", "BASE") }
                };
            }
        var driver = Open(initial, $"temporary-{steadfast}-{auras}-{spells}");
        await PlayAndReady(driver, steadfast);
        var crew = Crew(auras);
        if (steadfast) await MoveDefendersAndBeginOpponentAttack(driver, crew, declare: false);
        else await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], SourceObjectIds: ["ALLY", ..crew]));
        for (var i = 1; i <= spells; i++)
        {
            if (driver.State.FocusPlayerId is { } focus && focus != "P1") await driver.Act(focus, new PassFocusCommand());
            Assert.Equal("P1", driver.State.FocusPlayerId);
            if (steadfast && i == 1) await driver.Act("P1", new TapRuneCommand("BLUE-RUNE-1",
                ["BLUE-RUNE-1", "BLUE-RUNE-2", "BLUE-RUNE-3", "BLUE-RUNE-4"]));
            await driver.Act("P1", new PlayCardCommand("GRANT" + i, grant, ["ALLY"]));
            await driver.Top();
        }
        if (steadfast) await DeclareAfterFocus(driver, "P2", "BF", ["ENEMY", "ENEMY2"], ["ALLY", ..crew]);
        else await DeclareAfterFocus(driver, "P1", "OTHER", ["ALLY", ..crew], ["ENEMY", "ENEMY2"]);
        var expected = 3 + auras + 3 * spells;
        AssertPowerForBothViewers(driver.State, "ALLY", expected);
        if (steadfast)
            Assert.Equal(spells, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith("COMBAT_KEYWORD:坚守3:", StringComparison.Ordinal)));
        await FinishBattle(driver, expected);
        await Replay(initial, driver, new { expected, auras, spells, steadfast });
    }

    [Theory]
    [InlineData("same-field", 4)] [InlineData("base", 3)]
    [InlineData("other-field", 3)] [InlineData("enemy-controller", 3)] [InlineData("graveyard", 3)]
    public async Task LocalAuraUsesItsCurrentLocationAndController(string branch, int expected)
    {
        var initial = Position(1);
        if (branch == "other-field") initial = LocalDestructionRecallTests.At(initial, "FARRON", "P1", "BATTLEFIELD", "BF");
        if (branch == "enemy-controller") initial = LocalDestructionRecallTests.At(initial, "FARRON", "P2", "BATTLEFIELD", "OTHER");
        if (branch == "graveyard") initial = initial with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
            {
                ["P1"] = initial.PlayerZones["P1"] with { Base = ["GEAR"], Graveyard = ["GRAVE", "FARRON"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FARRON"] = new("P1", "GRAVEYARD") }
        };
        var driver = Open(initial, "location-" + branch);
        await PlayAndReady(driver);
        await BeginAttack(driver, branch == "same-field" ? ["ALLY", "FARRON"] : ["ALLY"]);
        AssertPowerForBothViewers(driver.State, "ALLY", expected);
        await FinishBattle(driver, expected);
        await Replay(initial, driver, new { expected, branch });
    }

    [Theory]
    [InlineData("OGN·104/298", "HAND", 1)]
    [InlineData("OGS·011/024", "BASE", 0)]
    public async Task ReturningTheAuraSourceDuringFocusRemovesItsIndependentGrantBeforeAssignment(string spell, string zone, int generation)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(1), "RETURN", spell, "P1");
        var driver = Open(initial, "aura-return-response-" + zone);
        await PlayAndReady(driver);
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], SourceObjectIds: ["ALLY", "FARRON"]));
        AssertPowerForBothViewers(driver.State, "ALLY", 4);
        var originalBindings = DeclarationBindings(driver.State);
        await driver.Act("P1", new PlayCardCommand("RETURN", spell, ["FARRON"]));
        await driver.Top();
        Assert.Equal(zone, driver.State.ObjectLocations["FARRON"].Zone);
        Assert.Equal(generation, driver.State.CardObjects["FARRON"].ObjectGeneration);
        await DeclareAfterFocus(driver, "P1", "OTHER", ["ALLY"], ["ENEMY", "ENEMY2"]);
        AssertRetainedBindings(driver.State, originalBindings, ["ALLY", "ENEMY", "ENEMY2"]);
        AssertPowerForBothViewers(driver.State, "ALLY", 3);
        await FinishBattle(driver, 3);
        await Replay(initial, driver, new { spell, zone, generation, originalBindings });
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task TokensRemovedBeforeCombatStartDoNotLeaveMetadataOutsideNewParticipants(int removed)
    {
        var initial = Position(1);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        for (var i = 1; i <= 2; i++)
        {
            var id = "TOKEN" + i;
            initial = initial with
            {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [id] = factory.CreateObject(id, "P1", "P1") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                {
                    ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, id] }
                },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new("P1", "BASE") }
            };
            initial = AttackDamageDistributionTests.Spell(initial, "DESTROY" + i, "OGN·014/298", "P1");
        }
        var driver = Open(initial, "tokens-before-combat-" + removed);
        await PlayAndReady(driver);
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], SourceObjectIds: ["ALLY", "FARRON", "TOKEN1", "TOKEN2"]));
        var originalBindings = DeclarationBindings(driver.State);
        for (var i = 1; i <= removed; i++)
        {
            if (driver.State.FocusPlayerId is { } focus && focus != "P1") await driver.Act(focus, new PassFocusCommand());
            await driver.Act("P1", new PlayCardCommand("DESTROY" + i, "OGN·014/298", ["TOKEN" + i]));
            await driver.Top();
            Assert.False(driver.State.CardObjects.ContainsKey("TOKEN" + i));
        }
        Assert.Equal(removed, Declaration(driver.State).GetProperty("CeasedTokens").EnumerateObject().Count());
        var attackers = removed == 1 ? new[] { "ALLY", "FARRON", "TOKEN2" } : ["ALLY", "FARRON"];
        await DeclareAfterFocus(driver, "P1", "OTHER", attackers, ["ENEMY", "ENEMY2"]);
        var current = Declaration(driver.State);
        Assert.True(!current.TryGetProperty("CeasedTokens", out var ceased) || ceased.ValueKind == JsonValueKind.Null);
        AssertRetainedBindings(driver.State, originalBindings, [..attackers, "ENEMY", "ENEMY2"]);
        await FinishBattle(driver, 4);
        await Replay(initial, driver, new { removed, originalBindings });
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 5)] [InlineData(2, 3)]
    public async Task DerivedTagsRepresentOneConditionalGrantWhileEachTaricAddsAnother(int auras, int expected)
    {
        var initial = Position(auras, steadfast: true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
        {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "VEN·117", "P1")
        } };
        var driver = Open(initial, "derived-plus-taric-" + auras);
        await driver.Act("P1", new PlayCardCommand("ALLY", "VEN·117", [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new PlayCardCommand("CONTROL-READY", "UNL-009/219", ["ALLY"]));
        await driver.Top();
        await MoveDefendersAndBeginOpponentAttack(driver, Crew(auras));
        var card = driver.State.CardObjects["ALLY"];
        Assert.Equal(auras == 1, card.RuleDerivedTags?.Contains("坚守3") == true);
        if (auras == 1) Assert.Contains("坚守3", card.Tags);
        AssertPowerForBothViewers(driver.State, "ALLY", expected);
        await FinishBattle(driver, expected);
        await Replay(initial, driver, new { expected, auras });
    }

    private static MatchState Position(int auraCount, bool steadfast = false)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("UNIT", "UNL-009/219", "P1", "HAND");
        Put("CONTROL-READY", "UNL-009/219", "P1", "HAND");
        Put("ALLY", steadfast ? "OGN·052/298" : "OGN·210/298", "P1", "HAND");
        Put("GEAR", "SFD·022/221", "P1", "BASE");
        Put("GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        Put("ENEMY", "OGN·219/298", "P2", steadfast ? "BASE" : "BATTLEFIELD", steadfast ? null : "OTHER");
        Put("ENEMY2", "OGN·219/298", "P2", steadfast ? "BASE" : "BATTLEFIELD", steadfast ? null : "OTHER");
        Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF");
        Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = "P1" };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = "P2" };
        var crew = Enumerable.Range(1, auraCount).Select(i => i == 1 ? "FARRON" : "FARRON" + i).ToArray();
        foreach (var id in crew)
        {
            Put(id, steadfast ? "OGN·074/298" : "OGN·015/298", "P1", steadfast ? "BATTLEFIELD" : "BASE", steadfast ? "BF" : null);
            // Explicit already-played aura units retain their printed keywords.
            // The subject Poro / Disciple is always played through the server.
            if (steadfast) cards[id] = cards[id] with { Tags = [..cards[id].Tags, CardCombatKeywordNames.Steadfast, CardCombatKeywordNames.Bulwark] };
        }
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with
            {
                Base = player == "P1" ? steadfast ? ["GEAR"] : new[] { "GEAR" }.Concat(crew).ToArray() : steadfast ? ["ENEMY", "ENEMY2"] : [],
                Battlefields = player == "P1" ? steadfast ? ["BF", ..crew] : ["BF"] : steadfast ? ["OTHER"] : ["OTHER", "ENEMY", "ENEMY2"],
                Hand = player == "P1" ? ["UNIT", "CONTROL-READY", "ALLY"] : [],
                Graveyard = player == "P1" ? ["GRAVE"] : [], MainDeck = deck
            };
        }
        return new MatchState("COMBAT-KEYWORD-SOURCES", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool>
            {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 2, [RuneTrait.Blue] = 2, [RuneTrait.Yellow] = 2 }),
                ["P2"] = new(20, 20)
            }, seed: 424242) with { CardPlays = new(5, [], []) };
    }

    private static async Task PlayAndReady(Driver driver, bool steadfast = false)
    {
        await driver.Act("P1", new PlayCardCommand("ALLY", steadfast ? "OGN·052/298" : "OGN·210/298", [], Destination: steadfast ? "BATTLEFIELD:BF" : "BASE"));
        Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains(steadfast ? CardCombatKeywordNames.Steadfast : CardCombatKeywordNames.Assault, driver.State.CardObjects["ALLY"].Tags);
        await driver.Act("P1", new PlayCardCommand("CONTROL-READY", "UNL-009/219", ["ALLY"]));
        await driver.Top();
        Assert.Equal(2, Power(driver.State, "P1", "ALLY"));
    }

    private static async Task BeginAttack(Driver driver, string[] attackers)
    {
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], SourceObjectIds: attackers));
        var defenders = driver.State.PlayerZones["P2"].Battlefields.Where(id => driver.State.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)
            && driver.State.ObjectLocations[id].BattlefieldObjectId == "OTHER").ToArray();
        await DeclareAfterFocus(driver, "P1", "OTHER", attackers, defenders);
    }

    private static string[] Crew(int count) => Enumerable.Range(1, count).Select(i => i == 1 ? "FARRON" : "FARRON" + i).ToArray();

    private static JsonElement Declaration(MatchState state)
    {
        const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
        var marker = Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith(prefix, StringComparison.Ordinal));
        using var document = JsonDocument.Parse(Convert.FromBase64String(marker[prefix.Length..]));
        return document.RootElement.Clone();
    }

    private static ObjectBinding[] DeclarationBindings(MatchState state)
        => Declaration(state).GetProperty("PublicObjectBindings").Deserialize<ObjectBinding[]>(Json)!;

    private static void AssertRetainedBindings(MatchState state, ObjectBinding[] original, string[] remaining)
    {
        var retained = DeclarationBindings(state);
        Assert.Equal(remaining.Order(StringComparer.Ordinal), retained.Select(b => b.ObjectId).Order(StringComparer.Ordinal));
        Assert.All(retained, binding => Assert.Equal(Assert.Single(original, b => b.ObjectId == binding.ObjectId), binding));
    }

    private static async Task MoveDefendersAndBeginOpponentAttack(Driver driver, string[] crew, bool declare = true)
    {
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", [], SourceObjectIds: ["ENEMY", "ENEMY2"]));
        if (declare) await DeclareAfterFocus(driver, "P2", "BF", ["ENEMY", "ENEMY2"], ["ALLY", ..crew]);
    }

    private static async Task DeclareAfterFocus(Driver driver, string player, string field, string[] attackers, string[] defenders)
    {
        for (var step = 0; step < 4 && driver.State.FocusPlayerId is { } focus; step++) await driver.Act(focus, new PassFocusCommand());
        await driver.Act(player, new DeclareBattleCommand(field, attackers, defenders, ["COMBAT_ASSIGNMENT"]));
    }

    private static void AssertPowerForBothViewers(MatchState state, string id, int expected)
    {
        foreach (var viewer in new[] { "P1", "P2" })
        {
            var snapshot = CurrentPowerTargetTests.Snapshot(state, viewer);
            Assert.Equal(expected, snapshot.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32());
        }
    }

    private static async Task FinishBattle(Driver driver, int expectedAllyPower)
    {
        var assignments = 0;
        for (var step = 0; step < 12 && driver.State.BattleState.IsActive; step++)
        {
            var prompt = ResolutionResult.BuildPrompts(driver.State).Values.FirstOrDefault(p =>
                p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            Assert.NotNull(prompt);
            var metadata = JsonSerializer.SerializeToElement(prompt.View!.Metadata);
            Assert.Equal(expectedAllyPower, metadata.GetProperty("damagePool").GetProperty("ALLY").GetInt32());
            await driver.Act(prompt.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!,
                driver.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(prompt)));
            assignments++;
        }
        Assert.Equal(2, assignments);
        Assert.False(driver.State.BattleState.IsActive);
        Assert.Contains(driver.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
    }

    private static int Power(MatchState state, string player, string id)
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    private static Driver Open(MatchState initial, string scenario)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COMBAT_KEYWORD_SOURCES_EVIDENCE");
        var driver = new Driver(initial, root is null ? null : Path.Combine(root, scenario));
        driver.Export("initial.json", initial); return driver;
    }

    private static async Task Replay(MatchState initial, Driver driver, object? observations = null)
    {
        var events = driver.Events().Select(e => e with
        {
            Event = JsonSerializer.Deserialize<GameEvent>(JsonSerializer.Serialize(e.Event, Json), Json)!
        }).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        driver.Export("commands.json", driver.Commands()); driver.Export("events.json", events); driver.Export("final.json", driver.State);
        driver.Export("receipt.json", new { commands = driver.Commands().Length, events = events.Length, replay, observations,
            engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
            scope = "Explicit combat keyword fixtures; actual serialized MatchSession intents, cold restore, duplicate intents and original JSON journal replay. No SQL/API or whole-card certification." });
    }

    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly string? evidence;
        private MatchSession session;
        internal MatchState State { get; private set; }
        internal Driver(MatchState state, string? evidence)
        {
            initial = state; State = state; this.evidence = evidence;
            session = new(state, new CoreRuleEngine(), this);
        }
        internal void Export(string name, object value)
        {
            if (evidence is null) return;
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, name), JsonSerializer.Serialize(value, value.GetType(), Json));
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            Assert.Equal(initial.Seed, entry.OpeningSeed);
            Export("journal-" + entries.Count + ".json", entry);
            entries.Add(entry); return ValueTask.CompletedTask;
        }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
            e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
            e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        internal async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            var id = "keyword-step-" + entries.Count;
            var prompt = session.PromptFor(player);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(command, command.GetType(), Json), Json)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(State.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            Export(id + "-prompt.json", prompt); Export(id + "-command.json", raw);
            var result = await session.SubmitAsync(player, id, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            State = result.State;
            Export(id + "-state.json", State);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            OfficialGraveyardRecastTests.Restore(State); ArmamentMightTests.PublicRecovery(State);
            State = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(State, Json), Json)!;
            var seq = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, seq,
                snapshots[p], State.Tick, seq, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, seq, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, seq, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            session = MatchSession.Restore(new(State.RoomId, State.Tick, seq, Commands(), Events(), views, errors,
                State, initial, spectator) { OpeningSeed = initial.Seed }, new CoreRuleEngine(), this);
            var count = entries.Count;
            Assert.True((await session.SubmitAsync(player, id, GameCommandJsonMapper.Map(raw), raw, default)).Accepted);
            Assert.Equal(count, entries.Count);
            return result;
        }
        internal async Task<ResolutionResult> Top()
        {
            await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            return await Act(State.PriorityPlayerId!, new PassPriorityCommand());
        }
    }
}
