using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustReadyTests
{
    private const string Card = "RAD·114/167", Ability = "EXHAUST-READY:0", Kind = "OTHER_FRIENDLY_EXHAUSTION_POWER";
    private const string Used = "EXHAUST_READY_USED|";
    public static IEnumerable<object[]> Printings() => new object[][] { [Card, 39618L], ["RAD·114a/167", 39619L] };

    private static MatchState Board(string no = Card, bool copied = false)
    {
        var s = ArmamentMightTests.Position(unit: no, power: 5, gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        s = s with { RoomId = "RAD-EXHAUST-READY", CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
        for (var i = 0; i < 4; i++) s = HandDiscardObserverTests.Put(s, "U" + i, "OGN·001/298", "P1", "BASE");
        return HandDiscardObserverTests.Put(s, "ENEMY", "OGN·001/298", "P2", "BASE");
    }
    private static MatchState Exhausted(MatchState s, params string[] ids)
        => s with { CardObjects = s.CardObjects.ToDictionary(p => p.Key, p => ids.Contains(p.Key) ? p.Value with { IsExhausted = true } : p.Value) };
    private static ActivateAbilityCommand Activate(string cost = "HOST", string target = "U0", string? instance = null)
        => new("HOST", Ability, [target], ["EXHAUST_FRIENDLY_UNIT:" + cost]) with { AbilityInstanceId = instance };
    private static async Task Drain(Driver game)
    {
        for (var i = 0; i < 30 && (game.State.TriggerQueue.Count > 0 || game.State.StackItems.Count > 0); i++) {
            if (game.State.TriggerQueue.Count > 0 && game.State.PriorityPlayerId is null)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else await game.Top();
        }
        Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task EachPrintingPaysPurplePowerAndDoesNotTriggerOnItsOwnExhaustedEntry(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("弃暗投明", card.SubTitle); Assert.Equal(5, card.Energy); Assert.Equal(5, card.Power);
        Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.OtherFriendlyUnitExhaustedSourcePower);
        Assert.Single(ActivatedAbilityParser.Parse(card.CardEffect), a => a.Kind == ActivatedAbilityKinds.ExhaustFriendlyUnitReadyUnit);
        var s = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }), ["P2"] = RunePool.Empty } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task MissingThePrintedPurplePowerCannotPartiallyPlayTheUnit(string no, long _)
    {
        var s = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0), ["P2"] = RunePool.Empty } };
        await Reject(s, new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task EveryOtherFriendlyExhaustionEarnsOneSeparateTurnMightSkill(string no, long _)
    {
        var game = new Driver(Board(no));
        await game.Act("P1", new MoveUnitCommand("U0", "BASE", "BATTLEFIELD:F", SourceObjectIds: ["U0", "U1", "U2"]));
        Assert.Equal(3, game.State.TriggerQueue.Count(t => t.FieldContext?.Kind == Kind) + game.State.StackItems.Count(t => t.FieldContext?.Kind == Kind));
        Assert.Equal(5, ArmamentMightTests.Might(game.State)); await Drain(game); Assert.Equal(8, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(5, ArmamentMightTests.Might(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("INTERNAL_"));
        Assert.DoesNotContain("internalExhaustionObservers", JsonSerializer.Serialize(game.Events()));
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task SourceCanPayForItsOwnReadyWithoutTriggeringOnItself(string no, long _)
    {
        var game = new Driver(Board(no)); await game.Act("P1", Activate(target: "HOST"));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        var item = Assert.Single(game.State.StackItems); Assert.NotNull(item.ExhaustReadyActivation); Assert.Equal(5, ArmamentMightTests.Might(game.State));
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await Reject(game.State, Activate("U1", "HOST"));
        Assert.DoesNotContain(Rows(game.State), row => Equals(row.GetValueOrDefault("abilityId"), Ability));
    }

    [Theory]
    [InlineData("U0")] [InlineData("ENEMY")] [InlineData("U1")]
    public async Task ReadyTargetsMayBeFriendlyEnemyOrTheUnitThatPaidTheCost(string target)
    {
        var game = new Driver(Exhausted(Board(), target == "U1" ? "U0" : target));
        await game.Act("P1", Activate("U1", target));
        Assert.True(game.State.CardObjects["U1"].IsExhausted);
        Assert.Contains(game.State.StackItems, i => i.ExhaustReadyActivation is not null);
        await Drain(game); Assert.False(game.State.CardObjects[target].IsExhausted); Assert.Equal(6, ArmamentMightTests.Might(game.State));
    }

    [Fact]
    public async Task AnExhaustedSourceCanPayWithAnotherUnitAndItsTriggerResolvesBeforeTheReadySkill()
    {
        var game = new Driver(Exhausted(Board(), "HOST", "U0"));
        await game.Act("P1", Activate("U1"));
        Assert.Equal(Kind, game.State.StackItems[^1].FieldContext?.Kind);
        Assert.True(game.State.CardObjects["U0"].IsExhausted); await game.Top();
        Assert.Equal(6, ArmamentMightTests.Might(game.State)); Assert.True(game.State.CardObjects["U0"].IsExhausted);
        await game.Top(); Assert.False(game.State.CardObjects["U0"].IsExhausted); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
    }

    [Fact]
    public async Task ExhaustingTheSourceOrAnAlreadyExhaustedAllyDoesNotEarnAnExtraSkill()
    {
        var initial = Exhausted(Board(), "U0", "U1", "U2", "U3");
        initial = AttackDamageDistributionTests.Spell(initial, "BURST", "OGN·123/298", "P1");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(5, ArmamentMightTests.Might(game.State));
    }

    [Fact]
    public async Task AnEnemyUnitBecomingExhaustedDoesNotTriggerTheFriendlyObserver()
    {
        var initial = Board() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial); await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        Assert.True(game.State.CardObjects["ENEMY"].IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Equal(5, ArmamentMightTests.Might(game.State));
    }

    [Fact]
    public async Task ExhaustionEarnedBeforeLethalDamageSurvivesItsSourceLeavingTheBattlefield()
    {
        var initial = LocalDestructionRecallTests.At(Board(), "HOST", "P1", "BATTLEFIELD", "F");
        initial = AttackDamageDistributionTests.Spell(initial, "BURST", "OGN·123/298", "P1");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, game.State.TriggerQueue.Count(t => t.FieldContext?.Kind == Kind) + game.State.StackItems.Count(t => t.FieldContext?.Kind == Kind));
        await Drain(game); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
    }

    [Fact]
    public async Task ARealCopiedTokenKeepsEarnedExhaustionSkillsAfterItCeases()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Board(), "MIRROR", "UNL-200/219");
        initial = (await OfficialCopyIdentityTests.Copy(initial, "HOST", "MIRROR")).State;
        const string token = "MIRROR-TOKEN-001";
        initial = LocalDestructionRecallTests.At(initial, token, "P1", "BATTLEFIELD", "F");
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·104/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "BURST", "OGN·123/298", "P1");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(token));
        Assert.All(game.State.TriggerQueue, t => Assert.True(t.FieldContext!.SourceCeasedToExist));
        Assert.All(game.State.StackItems, i => Assert.True(i.FieldContext!.SourceCeasedToExist));
        await Drain(game);
    }

    [Fact]
    public async Task ExhaustionCostAlsoTriggersThePaidUnitsOwnSkillButItsExhaustedTokenEntryDoesNotTriggerAgain()
    {
        var initial = HandDiscardObserverTests.Put(Exhausted(Board(), "U0"), "SERA", "RAD·138/167", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", Activate("SERA"));
        Assert.Equal(2, game.State.TriggerQueue.Count + game.State.StackItems.Count - 1); // Both earned skills, plus the activation.
        await Drain(game); Assert.Equal(6, ArmamentMightTests.Might(game.State));
        var token = Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
        Assert.True(token.IsExhausted); Assert.False(game.State.CardObjects["U0"].IsExhausted);
    }

    [Fact]
    public async Task PromptRequiresOneExhaustionCostAndShowsOnlyPublicUnitTargets()
    {
        var s = Board(); var row = Assert.Single(Rows(s), r => Equals(r.GetValueOrDefault("abilityId"), Ability));
        Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
        var costs = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]).Select(c => c.Id).ToArray();
        Assert.Contains("EXHAUST_FRIENDLY_UNIT:HOST", costs); Assert.Contains("EXHAUST_FRIENDLY_UNIT:U0", costs);
        Assert.DoesNotContain("EXHAUST_FRIENDLY_UNIT:ENEMY", costs); Assert.DoesNotContain("EXHAUST_FRIENDLY_UNIT:GEAR", costs);
    }

    [Theory]
    [InlineData("missing-cost")] [InlineData("duplicate-cost")] [InlineData("enemy-cost")]
    [InlineData("exhausted-cost")] [InlineData("equipment-cost")] [InlineData("hand-cost")]
    [InlineData("missing-target")] [InlineData("equipment-target")] [InlineData("duplicate-target")]
    [InlineData("unknown-ability")] [InlineData("forged-instance")] [InlineData("wrong-player")]
    public async Task IllegalCostsTargetsOrInstancesLeaveEveryComponentUnchanged(string branch)
    {
        var s = Board(); var command = Activate();
        if (branch == "missing-cost") command = command with { OptionalCosts = [] };
        if (branch == "duplicate-cost") command = command with { OptionalCosts = ["EXHAUST_FRIENDLY_UNIT:HOST", "EXHAUST_FRIENDLY_UNIT:HOST"] };
        if (branch == "enemy-cost") command = Activate("ENEMY");
        if (branch == "exhausted-cost") s = Exhausted(s, "HOST");
        if (branch == "equipment-cost") command = Activate("GEAR");
        if (branch == "hand-cost") s = LocalDestructionRecallTests.At(s, "HOST", "P1", "HAND");
        if (branch == "missing-target") command = command with { TargetObjectIds = [] };
        if (branch == "equipment-target") command = Activate(target: "GEAR");
        if (branch == "duplicate-target") command = command with { TargetObjectIds = ["U0", "U0"] };
        if (branch == "unknown-ability") command = command with { AbilityId = "EXHAUST-READY:9" };
        if (branch == "forged-instance") command = command with { AbilityInstanceId = "ATTACHED:GEAR:0:1:" + Ability };
        await Reject(s, command, branch == "wrong-player" ? "P2" : "P1");
    }

    [Fact]
    public async Task EnemySpellshieldIsPaidBeforeExhaustion()
    {
        var s = HandDiscardObserverTests.Put(Board(), "WARD", "SFD·037/221", "P2", "BASE");
        s = Exhausted(s, "WARD"); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["WARD"] = s.CardObjects["WARD"] with { Tags = [..s.CardObjects["WARD"].Tags, CardObjectTags.Spellshield] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(s, Activate(target: "WARD"));
        await Reject(s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(0, 0, spellOnlyPower: 1, equipmentOnlyPower: 1) } }, Activate(target: "WARD"));
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(0, 1) } };
        var game = new Driver(s); await game.Act("P1", Activate(target: "WARD"));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["WARD"].IsExhausted); await game.Top(); Assert.False(game.State.CardObjects["WARD"].IsExhausted);
    }

    [Fact]
    public async Task CopiedTextInstancesHaveIndependentTurnAllowances()
    {
        var game = new Driver(Exhausted(Board(copied: true), "U0", "U1"));
        await game.Act("P1", Activate(target: "U0")); await game.Top();
        await game.Act("P1", Activate("U0", "U1", "ATTACHED:GEAR:0:1:" + Ability)); await Drain(game);
        Assert.False(game.State.CardObjects["U1"].IsExhausted); Assert.Equal(7, ArmamentMightTests.Might(game.State));
        Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith(Used)));
        await Reject(game.State, Activate("U2", "U1", "ATTACHED:GEAR:0:1:" + Ability));
    }

    [Fact]
    public async Task LosingTheStackItemDoesNotRefundThePaidCostOrTurnAllowance()
    {
        var game = new Driver(Exhausted(Board(), "U0")); await game.Act("P1", Activate());
        var removed = game.State with { StackItems = [], TimingState = TimingStates.NeutralOpen, PriorityPlayerId = null, ActivePlayerId = "P1" };
        ArmamentMightTests.PublicRecovery(removed); await Reject(removed, Activate("U1"));
        Assert.True(removed.CardObjects["HOST"].IsExhausted); Assert.True(removed.CardObjects["U0"].IsExhausted);
    }

    [Fact]
    public async Task ANewTurnRestoresTheTextInstancesAllowance()
    {
        var game = new Driver(Exhausted(Board(), "U0")); await game.Act("P1", Activate()); await game.Top();
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        await game.Act("P1", Activate(target: "HOST")); await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
    }

    [Fact]
    public async Task SourceReturningToHandPreservesTheSkillAndDoesNotExposeItsNewPrivateIdentity()
    {
        var initial = AttackDamageDistributionTests.Spell(Exhausted(Board(), "U0"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial); await game.Act("P1", Activate());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        Assert.DoesNotContain(Used, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)));
        var prior = game.Events().Length; await game.Top(); Assert.False(game.State.CardObjects["U0"].IsExhausted);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(prior)));
    }

    [Fact]
    public async Task ATargetReturningToHandIsNotReadiedInItsNewGeneration()
    {
        var initial = AttackDamageDistributionTests.Spell(Exhausted(Board(), "U0"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial); await game.Act("P1", Activate());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["U0"])); await game.Top();
        var before = game.State.CardObjects["U0"]; await game.Top();
        Assert.Equal(before, game.State.CardObjects["U0"]); Assert.Contains("U0", game.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task ARealCopiedTokenMayCeaseAfterActivationWithoutCancelingTheReadySkill()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Exhausted(Board(), "U0"), "MIRROR", "UNL-200/219");
        initial = (await OfficialCopyIdentityTests.Copy(initial, "HOST", "MIRROR")).State;
        const string token = "MIRROR-TOKEN-001";
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·104/298", "P1");
        // A copied token enters exhausted; it can activate using another ally.
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(token, Ability, ["U0"], ["EXHAUST_FRIENDLY_UNIT:U1"]));
        await DrainEarnedSkills(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [token])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(token));
        await game.Top(); Assert.False(game.State.CardObjects["U0"].IsExhausted);
    }

    private static async Task DrainEarnedSkills(Driver game)
    {
        if (game.State.TriggerQueue.Count > 0 && game.State.PriorityPlayerId is null)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        while (game.State.StackItems.Count > 1) await game.Top();
    }

    [Theory]
    [InlineData("receipt")] [InlineData("cost")] [InlineData("source")] [InlineData("target")]
    [InlineData("index")] [InlineData("tick")] [InlineData("instance")]
    public async Task RecoveryRejectsForgedActivationFactsOrMissingPaidReceipt(string branch)
    {
        var game = new Driver(Exhausted(Board(), "U0")); await game.Act("P1", Activate());
        var s = game.State; var item = Assert.Single(s.StackItems); var c = item.ExhaustReadyActivation!;
        var forged = item with { ExhaustReadyActivation = branch switch {
            "cost" => c with { CostUnit = c.CostUnit with { Unit = new("MISSING", 0) } },
            "source" => c with { Source = c.Source with { Generation = 999 } },
            "target" => c with { Target = new("U1", 0) },
            "index" => c with { AbilityIndex = 99 },
            "tick" => c with { AcquiredTick = s.Tick + 1 },
            "instance" => c with { InstanceId = "FORGED" }, _ => c } };
        s = s with { StackItems = [forged], UntilEndOfTurnEffects = branch == "receipt" ? [] : s.UntilEndOfTurnEffects };
        Assert.Contains("invalid captured exhaust-ready activation", OfficialInsightAndSpellLockTests.Errors(s));
    }

    [Theory]
    [InlineData("count")] [InlineData("self")] [InlineData("card")] [InlineData("event")]
    public async Task RecoveryRejectsForgedOtherUnitExhaustionContexts(string branch)
    {
        var game = new Driver(Board()); await game.Act("P1", new MoveUnitCommand("U0", "BASE", "BATTLEFIELD:F", []));
        var s = game.State; var item = Assert.Single(s.StackItems); var c = item.FieldContext!; var birth = c.ExhaustionObserver!;
        var forged = item with { FieldContext = c with { ExhaustionObserver = branch switch {
            "count" => birth with { Units = [] },
            "self" => birth with { Units = [birth.Units[0] with { Unit = birth.Source }] },
            "card" => birth with { Units = [birth.Units[0] with { CardNo = "OGN·048/298" }] },
            _ => birth with { Confirmed = true } } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { StackItems = [forged] }));
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Rows(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? []).Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]));
    private static async Task Reject(MatchState state, GameCommand command, string actor = "P1")
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-exhaust-ready", actor, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
