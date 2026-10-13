using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ShenDiscipleTests
{
    private const string Card = "VEN·117";
    private const string Text = "{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n如果我所在的战场上受你控制的其他单位有且仅有一个，则我获得{{坚守3}}。（如果我是防守方，则{{S}}+3。）";

    [Fact]
    public void CurrentOfficialTextParsesExactOtherFriendlyUnitKeywordCondition()
    {
        var spec = Assert.Single(StaticAbilityParser.Parse(Text, KeywordParser.Parse(Text, "")),
            s => s.Kind == "UNIT_EXACT_OTHER_FRIENDLY_BATTLEFIELD_KEYWORDS");
        Assert.Contains("坚守3", spec.GrantedKeywords!);
    }

    [Fact]
    public async Task CurrentNonPreviewPrintingIsAvailableWithoutUnconditionalSteadfast()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == Card);
        Assert.Equal(Text, card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(2, behavior.ManaCost);
        Assert.Equal(1, behavior.SourceUnitPower);
        Assert.DoesNotContain("坚守", behavior.SourceUnitTags);
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
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before[id].Decklist), System.Text.Json.JsonSerializer.Serialize(after[id].Decklist));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task ActualUnitMovementsUpdateExactCountAndColdReplay(int companions, bool expected)
    {
        var initial = Position();
        for (var i = 0; i < companions; i++)
            initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND-" + i, "OGN·001/298", "F", "P1");
        var driver = Driver(initial, "count-" + companions);
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(expected, driver.State.CardObjects["HOST"].RuleDerivedTags?.Contains("坚守3") == true);
        Assert.Equal(1, driver.State.CardObjects["HOST"].Power);
        if (companions == 1)
        {
            for (var pass = 0; pass < 4 && driver.State.FocusPlayerId is not null; pass++)
                await driver.Act(driver.State.FocusPlayerId, new PassFocusCommand());
            await driver.Act("P1", new MoveUnitCommand("FRIEND-0", "BATTLEFIELD", "BASE", []));
            Assert.Null(driver.State.CardObjects["HOST"].RuleDerivedTags);
            Assert.DoesNotContain("坚守3", driver.State.CardObjects["HOST"].Tags);
        }
        await Finish(initial, driver);
    }

    [Theory]
    [InlineData(false, 0, 1)]
    [InlineData(false, 1, 4)]
    [InlineData(false, 2, 1)]
    [InlineData(true, 1, 1)]
    public async Task ActualBattleRolesApplySteadfastOnlyToDefenders(bool attacking, int friends, int expectedMight)
    {
        var initial = Position();
        for (var i = 0; i < friends; i++)
            initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND-" + i, "OGN·001/298", "F", "P1");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY", "OGN·001/298", "F", "P2");
        if (attacking)
        {
            for (var i = 0; i < friends; i++)
                initial = LocalDestructionRecallTests.At(initial, "FRIEND-" + i, "P1", "BASE");
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
        Assert.Equal(attacking, driver.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(!attacking, driver.State.CardObjects["HOST"].IsDefending);
        var snapshot = CurrentPowerTargetTests.Snapshot(driver.State);
        Assert.Equal(expectedMight, snapshot.GetProperty("players").GetProperty("P1").GetProperty("objects")
            .GetProperty("HOST").GetProperty("effectivePower").GetInt32());
        var events = new List<GameEvent>();
        for (var step = 0; step < 30 && !events.Any(e => e.Kind == "BATTLE_CLOSED"); step++)
        {
            ResolutionResult result;
            var prompts = ResolutionResult.BuildPrompts(driver.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
                result = await driver.Act(assignment.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!,
                    driver.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            else if (driver.State.PriorityPlayerId is { } priority)
                result = await driver.Act(priority, new PassPriorityCommand());
            else if (driver.State.FocusPlayerId is { } focus)
                result = await driver.Act(focus, new PassFocusCommand());
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
        Assert.Equal(expectedMight, events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
            && e.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Payload["damage"])));
        await Finish(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualTakeoverUsesCurrentControllerForCompanionCount(bool takeSource)
    {
        var initial = Position();
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND", "OGN·001/298", "F", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "TAKEOVER", "SFD·202/221", "P2");
        initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 20) } };
        var driver = Driver(initial, takeSource ? "control-source" : "control-friend");
        await driver.Act("P2", new PlayCardCommand("TAKEOVER", "SFD·202/221", [takeSource ? "HOST" : "FRIEND"]));
        Assert.Contains("坚守3", driver.State.CardObjects["HOST"].RuleDerivedTags!);
        await driver.Top();
        Assert.Null(driver.State.CardObjects["HOST"].RuleDerivedTags);
        Assert.Equal("P2", driver.State.CardObjects[takeSource ? "HOST" : "FRIEND"].ControllerId);
        Assert.Equal("P1", driver.State.CardObjects[takeSource ? "HOST" : "FRIEND"].OwnerId);
        await Finish(initial, driver);
    }

    [Fact]
    public async Task FaceDownStandbyDoesNotCountAsACompanion()
    {
        var initial = Position();
        initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND", "OGN·001/298", "F", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "FACE-DOWN-COMPANION", "OGN·135/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["FACE-DOWN-COMPANION"] = initial.CardObjects["FACE-DOWN-COMPANION"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby], Power = 3 } } };
        var driver = Driver(initial, "hidden-companion");
        await driver.Act("P1", new HideCardCommand("FACE-DOWN-COMPANION", "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Contains("坚守3", driver.State.CardObjects["HOST"].RuleDerivedTags!);
        Assert.True(driver.State.CardObjects["FACE-DOWN-COMPANION"].IsFaceDown);
        await Finish(initial, driver);
    }

    [Theory]
    [InlineData(1, "坚守6")]
    [InlineData(2, "坚守9")]
    public async Task ActualCopiedConditionalTextHasIndependentCountedInstances(int copies, string expected)
    {
        var initial = AttachedRuleTextTests.AddSong(Position(), "SONG-0");
        if (copies == 2) initial = AttachedRuleTextTests.AddSong(initial, "SONG-1");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND", "OGN·001/298", "F", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "SFD·135/221", "P1");
        var driver = Driver(initial, "copies-" + copies);
        for (var i = 0; i < copies; i++)
        {
            await driver.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "HOST", ["ASSEMBLE_GREEN"]));
            await driver.Top();
            Assert.NotNull(driver.State.CardObjects["SONG-" + i].AttachedRuleText);
        }
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Contains(expected, driver.State.CardObjects["HOST"].RuleDerivedTags!);
        for (var pass = 0; pass < 4 && driver.State.FocusPlayerId is not null; pass++)
            await driver.Act(driver.State.FocusPlayerId, new PassFocusCommand());
        await driver.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["SONG-0"]));
        await driver.Top();
        Assert.Contains(copies == 1 ? "坚守3" : "坚守6", driver.State.CardObjects["HOST"].RuleDerivedTags!);
        Assert.DoesNotContain(expected, driver.State.CardObjects["HOST"].RuleDerivedTags!);
        await Finish(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualHandOrLaterTurnStandbyPlayUsesCurrentBattlefieldCondition(bool standby)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "SHADOW", Card, "P1");
        initial = initial with
        {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(2, 3) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["SHADOW"] = initial.CardObjects["SHADOW"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby], Power = 1, ManaCost = 2 } }
        };
        initial = OfficialHoldSequenceTests.AddUnit(initial, "FRIEND", "OGN·001/298", "F", "P1");
        var driver = Driver(initial, standby ? "standby-play" : "hand-play");
        if (standby)
        {
            await driver.Act("P1", new HideCardCommand("SHADOW", Card, "BATTLEFIELD:F", ["STANDBY_A"]));
            await driver.Act("P1", new EndTurnCommand());
            await driver.Act("P2", new EndTurnCommand());
            await driver.Act("P1", new RevealCardCommand("SHADOW", Card, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        }
        else await driver.Act("P1", new PlayCardCommand("SHADOW", Card, [], Destination: "BASE"));
        while (driver.State.PriorityPlayerId is not null) await driver.Top();
        Assert.Equal(standby ? "F" : null, driver.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Equal(standby, driver.State.CardObjects["SHADOW"].RuleDerivedTags?.Contains("坚守3") == true);
        Assert.Equal(1, driver.State.CardObjects["SHADOW"].Power);
        await Finish(initial, driver);
    }

    [Fact]
    public async Task CurrentCountRejectsForgedDerivedKeywordAfterActualMovement()
    {
        var initial = Position();
        var driver = Driver(initial, "tamper");
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var forged = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects)
        {
            ["HOST"] = driver.State.CardObjects["HOST"] with { RuleDerivedTags = ["坚守3"],
                Tags = driver.State.CardObjects["HOST"].Tags.Append("坚守3").Order(StringComparer.Ordinal).ToArray() }
        } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("current rule-derived keywords"));
        await Finish(initial, driver);
    }

    internal static MatchState Position()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var printed));
        var state = ArmamentMightTests.Position(unit: Card, power: printed.SourceUnitPower);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        { ["HOST"] = state.CardObjects["HOST"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] } };
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        var deck = Enumerable.Range(0, 8).Select(i => "DISCIPLE-P2-DRAW-" + i).ToArray();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·001/298", out var draw));
        foreach (var id in deck)
        {
            cards[id] = new(id, cardNo: draw.CardNo, ownerId: "P2", controllerId: "P2", manaCost: draw.ManaCost,
                power: draw.SourceUnitPower, tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P2", "MAIN_DECK");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                { ["P2"] = state.PlayerZones["P2"] with { MainDeck = deck } } };
    }

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SHEN_DISCIPLE_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }

    private static async Task Finish(MatchState initial, StandbyDefendObserverTests.Driver driver)
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
