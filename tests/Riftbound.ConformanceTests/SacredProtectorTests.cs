using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SacredProtectorTests
{
    internal const string Card = "VEN·129";
    private const string Text = "除非我所在的战场上受你控制的其他单位有且仅有一个，否则我无法造成战斗伤害。";

    [Fact]
    public void CurrentOfficialTextParsesConditionalCombatDamagePermission()
    {
        var spec = Assert.Single(StaticAbilityParser.Parse(Text, KeywordParser.Parse(Text, "")),
            s => s.Kind == "UNIT_EXACT_OTHER_FRIENDLY_BATTLEFIELD_COMBAT_DAMAGE");
        Assert.Equal(1, spec.RequiredExactOtherFriendlyBattlefieldUnits);
    }

    [Fact]
    public async Task CurrentNonPreviewPrintingPreservesActualPrintedCharacteristics()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == Card);
        Assert.Equal(Text, card.CardEffect);
        Assert.Equal(4, card.Energy);
        Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(6, card.Power);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var printed));
        Assert.Equal(6, printed.SourceUnitPower);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 6)]
    [InlineData(false, 2, 0)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 1, 6)]
    [InlineData(true, 2, 0)]
    public async Task ActualCombatKeepsSixMightAndLethalThresholdWhileGatingOnlyDamage(bool attacking, int friends, int expectedDamage)
    {
        var initial = Position();
        for (var i = 0; i < friends; i++)
            initial = Unit(initial, "FRIEND-" + i, "P1");
        initial = Unit(initial, "ENEMY", "P2");
        if (attacking)
        {
            for (var i = 0; i < friends; i++) initial = LocalDestructionRecallTests.At(initial, "FRIEND-" + i, "P1", "BASE");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
                { ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        }
        else
        {
            initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F");
            initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
            initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        }
        var driver = Driver(initial, (attacking ? "attack-" : "defend-") + friends);
        await driver.Act(attacking ? "P1" : "P2", new MoveUnitCommand(attacking ? "HOST" : "ENEMY", "BASE", "BATTLEFIELD:F", [],
            SourceObjectIds: attacking ? new[] { "HOST" }.Concat(Enumerable.Range(0, friends).Select(i => "FRIEND-" + i)).ToArray() : null));
        Assert.Equal(6, DisplayPower(driver.State, "HOST"));
        var events = await FinishCombat(driver, expectedDamage);
        Assert.Equal(expectedDamage, events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
            && e.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Payload["damage"])));
        await Replay(initial, driver);
    }

    internal static async Task<List<GameEvent>> FinishCombat(StandbyDefendObserverTests.Driver driver, int? expectedHostPool = null)
    {
        var events = new List<GameEvent>();
        for (var step = 0; step < 30 && !events.Any(e => e.Kind == "BATTLE_CLOSED"); step++)
        {
            ResolutionResult result;
            var prompts = ResolutionResult.BuildPrompts(driver.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
            {
                var metadata = JsonSerializer.SerializeToElement(assignment.View!.Metadata);
                if (metadata.GetProperty("assignableDamagePool").TryGetProperty("HOST", out var pool) && expectedHostPool is not null)
                    Assert.Equal(expectedHostPool, pool.GetInt32());
                if (metadata.GetProperty("lethalDamageThreshold").TryGetProperty("HOST", out var threshold))
                {
                    Assert.Equal(6 - driver.State.CardObjects["HOST"].Damage, threshold.GetInt32());
                }
                result = await driver.Act(assignment.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!,
                    driver.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            }
            else if (driver.State.PriorityPlayerId is { } priority) result = await driver.Act(priority, new PassPriorityCommand());
            else if (driver.State.FocusPlayerId is { } focus) result = await driver.Act(focus, new PassFocusCommand());
            else
            {
                var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                var battle = driver.State.BattleState;
                result = await driver.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds,
                    battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
            events.AddRange(result.Events);
        }
        Assert.Contains(events, e => e.Kind == "BATTLE_CLOSED");
        return events;
    }

    [Theory]
    [InlineData("return", 0)]
    [InlineData("stun", 6)]
    [InlineData("takeover", 0)]
    public async Task ActualResponseReevaluatesCountBeforeDamage(string branch, int expected)
    {
        var initial = DefenderPosition(1);
        initial = AttackDamageDistributionTests.Spell(initial, "RESPONSE", branch == "return" ? "OGN·104/298"
            : branch == "stun" ? "OGN·262/298" : "SFD·202/221", branch == "takeover" ? "P2" : "P1");
        var driver = Driver(initial, "response-" + branch);
        if (branch == "takeover")
        {
            // Main-action takeover legally initiates the battle itself.
            await driver.Act("P2", new PlayCardCommand("RESPONSE", "SFD·202/221", ["FRIEND"]));
            await driver.Top();
            Assert.Equal("P2", driver.State.CardObjects["FRIEND"].ControllerId);
            Assert.Equal(6, DisplayPower(driver.State, "HOST"));
            await FinishCombat(driver, expected);
            await Replay(initial, driver);
            return;
        }
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        if (driver.State.FocusPlayerId != (branch == "takeover" ? "P2" : "P1"))
            await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        if (branch == "takeover")
            await driver.Act("P2", new RevealCardCommand("RESPONSE", "SFD·202/221", ["FRIEND"]));
        else
            await driver.Act("P1", new PlayCardCommand("RESPONSE", branch == "return" ? "OGN·104/298" : "OGN·262/298",
                branch == "stun" ? ["ENEMY"] : ["FRIEND"]));
        await driver.Top();
        Assert.Equal(6, DisplayPower(driver.State, "HOST"));
        await FinishCombat(driver, expected);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task SpellDamageAndTrueSixMightLethalThresholdRemainIndependentOfCombatPermission()
    {
        var initial = DefenderPosition(0);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["ENEMY"] = initial.CardObjects["ENEMY"] with { CardNo = "OGN·175/298", Power = 3, ManaCost = 3, Tags = [CardObjectTags.UnitCard] } } };
        initial = AttackDamageDistributionTests.Spell(initial, "DAMAGE", "UNL-014/219", "P2");
        var driver = Driver(initial, "spell-and-lethal");
        await driver.Act("P2", new PlayCardCommand("DAMAGE", "UNL-014/219", ["HOST"]));
        await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["HOST"].Damage);
        Assert.Equal(6, DisplayPower(driver.State, "HOST"));
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        var events = await FinishCombat(driver, 0);
        Assert.DoesNotContain(events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        // The actual opposing three-point attack combines with the two marked
        // points, leaving a six-Might unit alive. A false zero threshold kills it.
        Assert.Equal(3, events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "HOST")
            && e.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Payload["damage"])));
        Assert.Equal(0, driver.State.CardObjects["HOST"].Damage); // Battle cleanup clears marks only after lethal checks.
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ActualCopiedRestrictionAppliesToItsHostWithoutChangingMight(int companions)
    {
        var initial = DefenderPosition(companions);
        initial = AttachedRuleTextTests.AddSong(initial, "SONG");
        // Attach while P1 has a normal open action, then hand over the turn.
        initial = initial with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        var driver = Driver(initial, "copy-" + companions);
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        Assert.NotNull(driver.State.CardObjects["SONG"].AttachedRuleText);
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(6, DisplayPower(driver.State, "HOST"));
        await FinishCombat(driver, companions == 1 ? 6 : 0);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ZeroDamageSourceCannotForgeAnAssignmentWhileRemainingALegalDamageTarget()
    {
        var initial = DefenderPosition(2);
        var driver = Driver(initial, "forged-assignment");
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        for (var pass = 0; pass < 4 && driver.State.FocusPlayerId is not null; pass++)
            await driver.Act(driver.State.FocusPlayerId, new PassFocusCommand());
        var battle = driver.State.BattleState;
        await driver.Act("P2", new DeclareBattleCommand("F", battle.AttackerObjectIds, battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
        var firstPrompt = ResolutionResult.BuildPrompts(driver.State).Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
        Assert.Equal("P2", firstPrompt.PlayerId);
        await driver.Act("P2", new AssignCombatDamageCommand(driver.State.BattleState.BattleId!, "F", CombatTestDriver.Assignments(firstPrompt)));
        var before = driver.State;
        Assert.Equal("P1", ResolutionResult.BuildPrompts(before).Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true).PlayerId);
        var forged = new AssignCombatDamageCommand(before.BattleState.BattleId!, "F", [new("HOST", "ENEMY", 6)]);
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("forged-damage", "P1", CommandTypes.AssignCombatDamage), forged, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        var prompt = ResolutionResult.BuildPrompts(before).Values.First(p => p.Actions.Contains(CommandTypes.AssignCombatDamage));
        var meta = JsonSerializer.SerializeToElement(prompt.View!.Metadata);
        Assert.Equal(6, meta.GetProperty("lethalDamageThreshold").GetProperty("HOST").GetInt32());
        await FinishCombat(driver, 0);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task AddingThePrintingPreservesAllElevenOriginalPreconstructedDeckInputs()
    {
        var current = await OfficialCardCatalog.LoadDefaultAsync();
        var previous = current with { Total = current.Total - 1, Cards = current.Cards.Where(card => card.CardNo != Card).ToArray() };
        var before = PreconstructedDeckCatalog.Build(previous).ToDictionary(deck => deck.Id, StringComparer.Ordinal);
        var after = PreconstructedDeckCatalog.Build(current).ToDictionary(deck => deck.Id, StringComparer.Ordinal);
        Assert.Equal(11, before.Count);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var id in before.Keys)
            Assert.Equal(JsonSerializer.Serialize(before[id].Decklist), JsonSerializer.Serialize(after[id].Decklist));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ActualEquipmentUnitCopyUsesItsOwnBattlefieldAndCurrentCompanions(int companions)
    {
        var initial = DefenderPosition(companions) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["HOST"] = initial.CardObjects["HOST"] with { CardNo = "OGN·096/298", Power = 1, ManaCost = 2,
                Tags = [CardObjectTags.UnitCard] } } };
        initial = Unit(initial, "COPY-SOURCE", "P1", Card);
        initial = LocalDestructionRecallTests.At(initial, "COPY-SOURCE", "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["COPY-GEAR"] = new("COPY-GEAR", cardNo: "VEN·137", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) } }, "COPY-GEAR", "P1", "BASE");
        var driver = Driver(initial, "unit-copy-" + companions);
        await driver.Act("P1", new AssembleEquipmentCommand("COPY-GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
        await driver.Top();
        Assert.Equal("ATTACHMENT_COPY_SELECTION", driver.State.PendingRuleChoice!.Request.Kind);
        await driver.Act("P1", new PayCostCommand(driver.State.PendingRuleChoice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal(Card, driver.State.CardObjects["HOST"].CardNo);
        Assert.NotNull(driver.State.CardObjects["HOST"].AttachmentUnitCopies);
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(6, DisplayPower(driver.State, "HOST"));
        var events = await FinishCombat(driver, companions == 1 ? 6 : 0);
        Assert.Equal(companions == 1 ? 6 : 0, events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
            && e.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Payload["damage"])));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task HiddenStandbyAndBaseUnitsDoNotCountAsBattlefieldCompanions()
    {
        var initial = DefenderPosition(0) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        initial = Unit(initial, "BASE-FRIEND", "P1");
        initial = LocalDestructionRecallTests.At(initial, "BASE-FRIEND", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "FACE-DOWN-FRIEND", "OGN·135/298", "P1");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·135/298", out var printed));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["FACE-DOWN-FRIEND"] = initial.CardObjects["FACE-DOWN-FRIEND"] with { Power = printed.SourceUnitPower,
                ManaCost = printed.ManaCost, Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] } } };
        var driver = Driver(initial, "hidden-and-base");
        await driver.Act("P1", new HideCardCommand("FACE-DOWN-FRIEND", "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        Assert.True(driver.State.CardObjects["FACE-DOWN-FRIEND"].IsFaceDown);
        var events = await FinishCombat(driver, 0);
        Assert.DoesNotContain(events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await Replay(initial, driver);
    }

    [Fact]
    public async Task SimultaneouslyKilledCompanionDoesNotRevokeAlreadyPermittedCombatDamage()
    {
        var initial = DefenderPosition(1);
        var driver = Driver(initial, "simultaneous-companion-death");
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        for (var pass = 0; pass < 4 && driver.State.FocusPlayerId is not null; pass++)
            await driver.Act(driver.State.FocusPlayerId, new PassFocusCommand());
        var battle = driver.State.BattleState;
        await driver.Act("P2", new DeclareBattleCommand("F", battle.AttackerObjectIds, battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
        await driver.Act("P2", new AssignCombatDamageCommand(driver.State.BattleState.BattleId!, "F", [new("ENEMY", "FRIEND", 5)]));
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var meta = JsonSerializer.SerializeToElement(prompt.View!.Metadata);
        Assert.Equal(6, meta.GetProperty("assignableDamagePool").GetProperty("HOST").GetInt32());
        var resolved = await driver.Act("P1", new AssignCombatDamageCommand(driver.State.BattleState.BattleId!, "F", CombatTestDriver.Assignments(prompt)));
        Assert.Equal(6, resolved.Events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
            && e.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Payload["damage"])));
        Assert.Contains("FRIEND", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task PowerCappedSpellStillRejectsTheSixMightUnitWhenItsCombatDamageIsZero()
    {
        var initial = AttackDamageDistributionTests.Spell(DefenderPosition(0), "CAP", "UNL-159/219", "P2");
        var engine = new CoreRuleEngine();
        var command = new PlayCardCommand("CAP", "UNL-159/219", ["HOST"]);
        var prompt = ResolutionResult.BuildPrompts(initial)["P2"];
        var quote = engine.PreviewPlayCard(initial, "P2", new("cap-preview", prompt.PromptId!, initial.Tick, command));
        Assert.False(quote.IsValid);
        var rejected = await engine.ResolveAsync(initial, new("cap-rejected", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(6, DisplayPower(initial, "HOST"));
    }

    internal static MatchState Unit(MatchState state, string id, string controller, string no = "OGN·001/298")
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var printed));
        state = OfficialHoldSequenceTests.AddUnit(state, id, no, "F", controller);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { [id] = state.CardObjects[id] with { Power = printed.SourceUnitPower, ManaCost = printed.ManaCost,
                Tags = printed.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries).Append(CardObjectTags.UnitCard).Distinct().Order(StringComparer.Ordinal).ToArray() } } };
    }

    internal static MatchState DefenderPosition(int companions)
    {
        var initial = LocalDestructionRecallTests.At(Position(), "HOST", "P1", "BATTLEFIELD", "F");
        for (var i = 0; i < companions; i++)
            initial = Unit(initial, "FRIEND" + (companions == 1 ? "" : "-" + i), "P1");
        initial = Unit(initial, "ENEMY", "P2");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        return initial with { ActivePlayerId = "P2", TurnPlayerId = "P2", RunePools = new Dictionary<string, RunePool>(initial.RunePools)
            { ["P1"] = new(20,20), ["P2"] = new(20,20) } };
    }

    internal static MatchState Position()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var printed));
        var state = ShenDiscipleTests.Position();
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["HOST"] = state.CardObjects["HOST"] with { CardNo = Card, Power = printed.SourceUnitPower,
                ManaCost = printed.ManaCost, Tags = [CardObjectTags.UnitCard, "仙灵"] }
        } };
    }

    internal static int DisplayPower(MatchState state, string id)
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty(state.CardObjects[id].ControllerId!)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    internal static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SACRED_PROTECTOR_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }

    internal static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (driver.NativeCommands > 0) Assert.Equal(driver.Commands().Length, driver.NativeCommands);
        driver.Export("steps.json", driver.Steps);
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands, accepted = driver.Commands().Length,
            coldRestores = driver.Commands().Length, fullJournalReplay = replay });
    }
}
