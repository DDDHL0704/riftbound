using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven050RuneDiscountConsumerTests
{
    private const string No = "VEN·050";
    private const string Text = "{{强化12}}。你每控制一枚符文，此技能的费用便减少{{1}}。（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{法盾}}和{{坚守3}}。（对手必须支付{{A}}才能将我选作法术或技能的目标。如果我是防守方，则{{S}}+3。）";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCnPrintingRegistersItsDiscountAndConditionalKeywordsWithoutFlatMight()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37741, card.Id); Assert.Equal("凶暴的岩熊", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Blue], card.CardColorList); Assert.Empty(card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(4, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.DoesNotContain("法盾", behavior.SourceUnitTags ?? ""); Assert.DoesNotContain("坚守", behavior.SourceUnitTags ?? "");
        var spec = Assert.Single(EmpowerParser.Parse(card.CardEffect)); var cost = Assert.Single(spec.Alternatives);
        Assert.True(spec.ReduceManaPerRune); Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        Assert.Equal(12, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(0, self.Might); Assert.Equal(["法盾", "坚守3"], self.Keywords);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ActualPlayAndActivationPaySeparatelyAndKeepTheKeywordsAcrossBothTurns(string destination)
    {
        var initial = Position(); var game = await Play(initial, destination);
        Assert.Equal(36, game.State.RunePools["P1"].Mana); Assert.Equal(4, Might(game.State));
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.DoesNotContain("法盾", game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", Activate()); Assert.Equal(24, game.State.RunePools["P1"].Mana);
        Assert.Equal(12, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
        Assert.Empty(game.State.StackItems.Single().TargetObjectIds); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.DoesNotContain("坚守3", game.State.CardObjects["UNIT"].Tags); JsonRestore(initial, game);
        await game.Top(); CheckSelf(game.State); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new EndTurnCommand()); CheckSelf(game.State);
        await game.Act("P2", new EndTurnCommand()); CheckSelf(game.State);
        Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
        await Finish("permanent-" + destination.Replace(':', '-'), initial, game);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(5)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public async Task ActualActivationCountsControlledFieldRunesAndFloorsTheCostAtZero(int runes)
    {
        var initial = Position(runes); var game = await Play(initial); await game.Act("P1", Activate());
        var context = Assert.Single(game.State.StackItems).EmpowerActivation!;
        Assert.Equal(runes, context.ControlledRunes); Assert.Equal(Math.Max(0, 12 - runes), context.PaidMana);
        Assert.Equal(36 - Math.Max(0, 12 - runes), game.State.RunePools["P1"].Mana);
        Assert.Contains("UNRELEASED", game.State.PlayerZones["P1"].RuneDeck);
        for (var i = 0; i < runes; i++) Assert.Equal(i % 2 == 0, game.State.CardObjects["RUNE-" + i].IsExhausted);
        JsonRestore(initial, game); await game.Top(); CheckSelf(game.State); await Finish("runes-" + runes, initial, game);
    }

    [Fact]
    public async Task ActualDiscountUsesRuneControlRatherThanOwnership()
    {
        var initial = Position(1); var cards = initial.CardObjects.ToDictionary();
        cards["RUNE-0"] = cards["RUNE-0"] with { OwnerId = "P2" };
        cards["OPPONENT-RUNE"] = cards["OPPONENT-RUNE"] with { OwnerId = "P1" };
        initial = initial with { CardObjects = cards }; var game = await Play(initial); await game.Act("P1", Activate());
        var cost = Assert.Single(game.State.StackItems).EmpowerActivation!;
        Assert.Equal(1, cost.ControlledRunes); Assert.Equal(11, cost.PaidMana); Assert.Equal(25, game.State.RunePools["P1"].Mana);
        await game.Top(); CheckSelf(game.State); await Finish("control-not-ownership", initial, game);
    }

    [Theory]
    [InlineData(false, false, 4)] [InlineData(false, true, 7)]
    [InlineData(true, false, 4)] [InlineData(true, true, 4)]
    public async Task ActualMovementAndCombatApplySteadfastOnlyToAnEmpoweredDefender(bool attacking, bool empowered, int expected)
    {
        var initial = Position();
        if (!attacking) initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        var game = await Play(initial, attacking ? "BASE" : "BATTLEFIELD:BF");
        if (empowered) { await game.Act("P1", Activate()); await game.Top(); }
        Assert.Equal(4, Might(game.State));
        await game.Act("P1", new EndTurnCommand());
        if (attacking) await game.Act("P2", new EndTurnCommand());
        await game.Act(attacking ? "P1" : "P2", new MoveUnitCommand(attacking ? "UNIT" : "ENEMY", "BASE", attacking ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF", []));
        Assert.Equal(attacking, game.State.CardObjects["UNIT"].IsAttacking);
        Assert.Equal(!attacking, game.State.CardObjects["UNIT"].IsDefending); Assert.Equal(expected, Might(game.State));
        for (var step = 0; step < 30 && !game.Events().Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++)
        {
            var prompts = ResolutionResult.BuildPrompts(game.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
                await game.Act(assignment.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!, game.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else
            {
                var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                var battle = game.State.BattleState;
                await game.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds, battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        Assert.Contains(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
        Assert.Equal(expected, game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT")
            && e.Event.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
        Assert.False(game.State.CardObjects["UNIT"].IsDefending); Assert.Equal(4, Might(game.State));
        Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered);
        await Finish($"combat-{attacking}-{empowered}", initial, game);
    }

    [Theory]
    [InlineData(false, 0, true)] [InlineData(true, 0, false)] [InlineData(true, 1, true)]
    public async Task AnActualEnemySpellPaysExactlyOneWardOnlyAfterEmpower(bool empowered, int power, bool accepted)
    {
        var initial = Add(Position(), "NEGATIVE", "UNL-066/219", "P2", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, power) } };
        var game = await Play(initial); if (empowered) { await game.Act("P1", Activate()); await game.Top(); }
        await game.Act("P1", new EndTurnCommand()); var command = new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"]);
        if (!accepted) await Reject(game, "P2", command);
        else
        {
            await game.Act("P2", command); Assert.Equal(13, game.State.RunePools["P2"].Mana);
            Assert.Equal(power - (empowered ? 1 : 0), game.State.RunePools["P2"].TotalPower);
            Assert.Equal(empowered ? 1 : 0, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
            await game.Top(); Assert.Equal(-6, game.State.CardObjects["UNIT"].Power); Assert.Equal(0, Might(game.State));
            await game.Act("P2", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        }
        Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered);
        await Finish($"enemy-spell-{empowered}-{power}", initial, game);
    }

    [Theory]
    [InlineData(false, 0, true)] [InlineData(true, 0, false)] [InlineData(true, 1, true)]
    public async Task AnActualEnemyAbilityPaysWardBeforeItsExhaustCost(bool empowered, int power, bool accepted)
    {
        var initial = Add(Position(), "ORB", OrbOfRegretTests.Card, "P2", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, power) } };
        var game = await Play(initial); if (empowered) { await game.Act("P1", Activate()); await game.Top(); }
        await game.Act("P1", new EndTurnCommand()); var command = new ActivateAbilityCommand("ORB", OrbOfRegretTests.Ability, ["UNIT"]);
        if (!accepted) { await Reject(game, "P2", command); Assert.False(game.State.CardObjects["ORB"].IsExhausted); }
        else
        {
            await game.Act("P2", command); Assert.True(game.State.CardObjects["ORB"].IsExhausted);
            Assert.Equal(empowered ? 1 : 0, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
            Assert.Equal(power - (empowered ? 1 : 0), game.State.RunePools["P2"].TotalPower);
            await game.Top(); Assert.Equal(3, Might(game.State));
        }
        await Finish($"enemy-ability-{empowered}-{power}", initial, game);
    }

    [Fact]
    public async Task AnActualFriendlySpellDoesNotPayWardAndItsTemporaryMightExpires()
    {
        var initial = Add(Position(), "FRIENDLY", "VEN·081", "P1", "HAND"); var game = await Play(initial);
        await game.Act("P1", Activate()); await game.Top(); var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("FRIENDLY", "VEN·081", ["UNIT"]));
        Assert.Equal(before.Mana - 4, game.State.RunePools["P1"].Mana); Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        await game.Top(); Assert.Equal(10, Might(game.State)); await game.Act("P1", new EndTurnCommand()); CheckSelf(game.State);
        await Finish("friendly-no-ward", initial, game);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AnActualReturnClearsTheKeywordsAndCannotEmpowerTheNewHandGeneration(bool before)
    {
        var initial = Add(Position(5), "RETURN", "OGN·104/298", "P1", "HAND"); var game = await Play(initial);
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", Activate()); if (!before) { await game.Top(); CheckSelf(game.State); }
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(game.State.CardObjects["UNIT"].RuleDerivedTags);
        Assert.DoesNotContain("法盾", game.State.CardObjects["UNIT"].Tags); Assert.DoesNotContain("坚守3", game.State.CardObjects["UNIT"].Tags);
        if (before)
        {
            var row = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"], Json).GetProperty("stack").EnumerateArray().Single();
            Assert.Equal("HIDDEN", row.GetProperty("sourceObjectId").GetString()); Assert.False(row.TryGetProperty("empowerActivation", out _));
            JsonRestore(initial, game); await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        }
        Assert.Equal(4, Might(game.State)); await Finish("return-" + before, initial, game);
    }

    [Theory]
    [InlineData("mana")] [InlineData("controller")] [InlineData("repeated")]
    [InlineData("target")] [InlineData("timing")] [InlineData("gear")]
    public async Task AnActualIllegalActivationDoesNotPayOrGainConditionalKeywords(string branch)
    {
        var initial = Position();
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(15, 0) } };
        var game = await Play(initial); if (branch == "repeated") { await game.Act("P1", Activate()); await game.Top(); }
        if (branch == "timing") await game.Act("P1", new EndTurnCommand());
        await Reject(game, branch == "controller" ? "P2" : "P1", new ActivateAbilityCommand(branch == "gear" ? "GEAR" : "UNIT", "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []));
        Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].IsEmpowered);
        if (branch != "repeated") Assert.DoesNotContain("法盾", game.State.CardObjects["UNIT"].Tags);
        await Finish("illegal-" + branch, initial, game);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheUntouchedActualLogRejectsInventedDiscountAndRefundedMana(bool coordinated)
    {
        var initial = Position(5); var game = await Play(initial); await game.Act("P1", Activate());
        JsonRestore(initial, game); var item = Assert.Single(game.State.StackItems); var context = item.EmpowerActivation!;
        Assert.Equal(7, context.PaidMana); Assert.Equal(29, game.State.RunePools["P1"].Mana);
        var forged = game.State with { StackItems = [item with { EmpowerActivation = context with { ControlledRunes = 6, PaidMana = coordinated ? 6 : 7 } }] };
        if (coordinated) forged = forged with { RunePools = new Dictionary<string, RunePool>(forged.RunePools) { ["P1"] = forged.RunePools["P1"] with { Mana = 30 } } };
        var result = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged, new CoreRuleEngine(), default, game.Events());
        Assert.False(result.IsMatch); Assert.NotEmpty(result.Errors); Assert.Equal(7, game.State.StackItems.Single().EmpowerActivation!.PaidMana);
        await Finish("forged-discount-" + coordinated, initial, game);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningNaturallyPaysTheNonzeroDiscountAndColdRecoversItsPermanentKeywords(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R03", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 8, No); var runes = game.Runes(actor).Length; Assert.InRange(runes, 8, 11);
            await game.Tap(actor); var unit = game.Hand(actor, No).First(); var before = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(unit, No, [])); Assert.Equal(before - 4, game.State.RunePools[actor].Mana);
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(12 - runes, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.DoesNotContain("法盾", game.State.CardObjects[unit].Tags);
            await game.ColdCheckpoint(); await game.Top(); Assert.Equal(4, game.Might(actor, unit));
            Assert.Contains("法盾", game.State.CardObjects[unit].Tags); Assert.Contains("坚守3", game.State.CardObjects[unit].Tags);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Contains("坚守3", game.State.CardObjects[unit].Tags);
            Assert.Equal(4, game.Might(actor, unit)); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven050-" + actor); }
    }

    private static CardObjectState Printed(string id, string no, string player) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static void CheckSelf(MatchState state)
    {
        var unit = state.CardObjects["UNIT"]; Assert.True(unit.IsEmpowered); Assert.Equal(4, unit.Power); Assert.Equal(4, Might(state));
        Assert.Contains("法盾", unit.Tags); Assert.Contains("坚守3", unit.Tags); Assert.Equal(["坚守3", "法盾"], unit.RuleDerivedTags!.Order(StringComparer.Ordinal));
        Assert.Empty(unit.UntilEndOfTurnPowerModifiers); Assert.Empty(unit.UntilEndOfTurnEffects);
    }
    private static async Task<Driver> Play(MatchState initial, string destination = "BASE")
    { var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination)); return game; }
    private static async Task Reject(Driver game, string actor, GameCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        node["promptId"] = ResolutionResult.BuildPrompts(game.State)[actor].PromptId; node["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new(actor, "ven050-reject-" + game.Commands().Length, JsonSerializer.SerializeToElement(node, Json)), false);
    }
    private static void JsonRestore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
    }
    private static async Task Finish(string label, MatchState initial, Driver game)
    {
        await game.Replay(); JsonRestore(initial, game);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN050_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new { scope = "Explicit midgame semantic fixture; canonical openings are separate", initial,
            final = game.State, commands = game.Commands(), events = game.Events(), runtime, ruleInputs = RuleInputIdentity.Current }, Json));
    }
    private static MatchState Add(MatchState state, string id, string no, string owner, string zone)
    {
        var zones = state.PlayerZones.ToDictionary(); var z = zones[owner];
        zones[owner] = zone == "HAND" ? z with { Hand = [..z.Hand, id] } : z with { Base = [..z.Base, id] };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) }, PlayerZones = zones };
    }
    private static MatchState Position(int runes = 0)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        { cards[id] = Printed(id, no, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", No, "P1", "HAND"); Put("ENEMY", "OGN·096/298", "P2", "BATTLEFIELD", "OTHER");
        Put("GEAR", "SFD·022/221", "P1", "BASE"); Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var p in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 4).Select(i => p + "-DRAW-" + i).ToArray(); foreach (var id in deck) Put(id, "OGN·096/298", p, "MAIN_DECK");
            zones[p] = PlayerZones.Empty with { Base = p == "P1" ? ["GEAR"] : [], Battlefields = p == "P1" ? ["BF"] : ["OTHER", "ENEMY"], Hand = p == "P1" ? ["UNIT"] : [], MainDeck = deck };
        }
        for (var i = 0; i < runes; i++)
        {
            var id = "RUNE-" + i; Put(id, "UNL-R03", "P1", "BASE"); cards[id] = cards[id] with { IsExhausted = i % 2 == 0 };
            zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] };
        }
        Put("UNRELEASED", "UNL-R03", "P1", "RUNE_DECK"); zones["P1"] = zones["P1"] with { RuneDeck = ["UNRELEASED"] };
        Put("OPPONENT-RUNE", "UNL-R03", "P2", "BASE"); zones["P2"] = zones["P2"] with { Base = ["OPPONENT-RUNE"] };
        return new MatchState("VEN050-EXPLICIT-MIDGAME", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, runePools: new Dictionary<string, RunePool> { ["P1"] = new(40, 0), ["P2"] = new(20, 1) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }
}
