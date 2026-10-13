using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeadlyFlourishTests
{
    internal const string Card = "UNL-073/219";
    internal const string DelayedEffect = "DELAYED_UNIT_DESTRUCTION_CREATE_GOLD";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualImmediateAndLaterDeathQueueAnAnswerableDormantGoldForTheSpellController(bool immediate)
    {
        var initial = Position(immediate);
        var driver = Driver(initial, immediate ? "immediate-death" : "later-death");
        await driver.Act("P1", new PlayCardCommand("FLOURISH", Card, ["VICTIM"]));
        var damage = await driver.Top();
        Assert.Equal(3, Convert.ToInt32(Assert.Single(damage.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]));
        if (!immediate)
        {
            Assert.Equal(3, driver.State.CardObjects["VICTIM"].Damage);
            Assert.DoesNotContain(driver.State.TriggerQueue, t => t.EffectKind == DelayedEffect);
            await driver.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["VICTIM"]));
            await driver.Top();
        }
        Assert.Contains("VICTIM", driver.State.PlayerZones["P2"].Graveyard);
        var captured = Assert.Single(driver.State.StackItems, t => t.EffectKind == DelayedEffect);
        Assert.Equal("P1", captured.ControllerId);
        Assert.Empty(Gold(driver.State, "P1"));
        Assert.NotNull(driver.State.PriorityPlayerId);
        await driver.Top();
        var gold = Assert.Single(Gold(driver.State, "P1"));
        Assert.True(gold.IsExhausted);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, gold.CardNo);
        Assert.Empty(Gold(driver.State, "P2"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static CardObjectState[] Gold(MatchState state, string player)
        => state.PlayerZones[player].Base.Select(id => state.CardObjects[id])
            .Where(c => c.CardNo == P4ActivatedAbilityCatalog.GoldTokenUnlCardNo).ToArray();

    [Fact]
    public async Task ARealTurnEndExpiresTheInstructionBeforeNextTurnsDestruction()
    {
        var initial = Position();
        var driver = Driver(initial, "expires");
        await Cast(driver);
        Assert.Single(Markers(driver.State));
        await driver.Act("P1", new EndTurnCommand());
        Assert.True(driver.State.TurnNumber > initial.TurnNumber);
        Assert.Empty(Markers(driver.State));
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARealReturnBeforeOrAfterResolutionCannotFollowANewIncarnation(bool before)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P2");
        var driver = Driver(initial, before ? "return-before" : "return-after");
        await driver.Act("P1", new PlayCardCommand("FLOURISH", Card, ["VICTIM"]));
        if (!before) await driver.Top();
        if (before) await driver.Act("P1", new PassPriorityCommand());
        else
        {
            await driver.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["VICTIM"]));
            await driver.Act("P1", new PassPriorityCommand());
        }
        await driver.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["VICTIM"]));
        await driver.Top();
        {
            var result = await driver.Top();
            Assert.DoesNotContain(result.Events, e => e.Kind is "DAMAGE_APPLIED" or "DELAYED_DESTRUCTION_SCHEDULED");
        }
        Assert.Equal("HAND", driver.State.ObjectLocations["VICTIM"].Zone);
        Assert.Empty(Markers(driver.State));
        Assert.Empty(Gold(driver.State, "P1"));
        Assert.Equal(1, driver.State.CardObjects["VICTIM"].ObjectGeneration);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealBanishReplacementDoesNotCountAsDestruction()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "PUNISH", "UNL-007/219", "P1");
        initial = LocalDestructionRecallTests.At(initial, "VICTIM", "P2", "BATTLEFIELD", "F");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        var driver = Driver(initial, "banish-replacement");
        await Cast(driver);
        var marker = Assert.Single(Markers(driver.State));
        await driver.Act("P1", new PlayCardCommand("PUNISH", "UNL-007/219", ["VICTIM"]));
        var result = await driver.Top();
        Assert.Equal("BANISHED", driver.State.ObjectLocations["VICTIM"].Zone);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Empty(Markers(driver.State));
        Assert.Empty(Gold(driver.State, "P1"));
        Assert.Empty(driver.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealRecallReplacementPreservesTheInstructionUntilActualDeath()
    {
        var initial = SacredProtectorTests.Unit(Position(), "PROTECT", "P2", "SFD·173/221");
        var printedVictim = SacredProtectorTests.Unit(LocalDestructionRecallTests.At(initial, "VICTIM", "P2", "BASE"), "VICTIM", "P2", "OGN·175/298");
        initial = printedVictim;
        initial = LocalDestructionRecallTests.At(initial, "VICTIM", "P2", "BATTLEFIELD", "F");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        var driver = Driver(initial, "recall-replacement");
        var result = await Cast(driver);
        Assert.Equal("BASE", driver.State.ObjectLocations["VICTIM"].Zone);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Single(Markers(driver.State));
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].ObjectGeneration);
        await Kill(driver);
        Assert.Single(driver.State.StackItems, i => i.EffectKind == DelayedEffect);
        await driver.Top();
        Assert.Single(Gold(driver.State, "P1"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task TwoActualEchoExecutionsProduceIndependentOnceOnlyTriggers()
    {
        var initial = Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo-two-instances");
        await driver.Act("P1", new PlayCardCommand("FLOURISH", Card, ["VICTIM"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["VICTIM"])]));
        await driver.Top();
        Assert.Contains("VICTIM", driver.State.PlayerZones["P2"].Graveyard);
        var triggers = driver.State.TriggerQueue.Where(t => t.EffectKind == DelayedEffect).ToArray();
        Assert.Equal(2, triggers.Length);
        Assert.Equal(2, triggers.Select(t => t.DelayedDestruction!.InstanceId).Distinct().Count());
        Assert.Empty(Markers(driver.State));
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await driver.Top();
        await driver.Top();
        Assert.Equal(2, Gold(driver.State, "P1").Length);
        Assert.All(Gold(driver.State, "P1"), c => Assert.True(c.IsExhausted));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("source-generation")]
    [InlineData("unit-generation")]
    [InlineData("turn")]
    [InlineData("card")]
    [InlineData("controller")]
    [InlineData("instance")]
    [InlineData("execution")]
    [InlineData("created-tick")]
    [InlineData("duplicate")]
    public async Task RecoveryRejectsIndependentInstructionProvenanceTampering(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await Cast(driver);
        var marker = Assert.Single(Markers(driver.State)); var f = marker.Split('|');
        if (mutation == "source-generation") f[5] = "999999";
        if (mutation == "unit-generation") f[7] = "999999";
        if (mutation == "turn") f[8] = "999999";
        if (mutation == "card") f[2] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("OGN·229/298"));
        if (mutation == "controller") f[3] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("FORGED"));
        if (mutation == "instance") f[1] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("FORGED"));
        if (mutation == "execution") f[9] = "-1";
        if (mutation == "created-tick") f[10] = "999999";
        var state = driver.State with { UntilEndOfTurnEffects = mutation == "duplicate"
            ? [..driver.State.UntilEndOfTurnEffects, marker]
            : driver.State.UntilEndOfTurnEffects.Select(e => e == marker ? string.Join('|', f) : e).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("delayed destruction"));
    }

    internal static string[] Markers(MatchState state) => state.UntilEndOfTurnEffects
        .Where(e => e.StartsWith("DELAYED_UNIT_DESTRUCTION|", StringComparison.Ordinal)).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARealCounterStopsTheSpellWhileTargetRestrictedCountersCannotChooseTheUntargetedDelayedSkill(bool delayed)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(delayed), "COUNTER",
            delayed ? "UNL-106/219" : "OGN·064/298", "P2");
        var driver = Driver(initial, delayed ? "counter-delayed" : "counter-spell");
        await driver.Act("P1", new PlayCardCommand("FLOURISH", Card, ["VICTIM"]));
        if (delayed) await driver.Top();
        var item = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand());
        if (delayed)
        {
            var prompt = ResolutionResult.BuildPrompts(driver.State)["P2"];
            var command = new PlayCardCommand("COUNTER", "UNL-106/219", ["VICTIM", item.StackItemId]);
            var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", new("quote", prompt.PromptId!, driver.State.Tick, command));
            Assert.False(quote.IsValid);
            Assert.Empty(item.TargetObjectIds);
            await driver.Act("P2", new PassPriorityCommand());
            Assert.Single(Gold(driver.State, "P1"));
        }
        else
        {
            await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [item.StackItemId]));
            await driver.Top();
            Assert.Empty(driver.State.StackItems);
            Assert.Empty(Markers(driver.State));
            Assert.Empty(Gold(driver.State, "P1"));
        }
        if (!delayed) Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealSpellControlChangeCreatesGoldForTheNewController()
    {
        var initial = RebuttalRulesTests.Position(Card, Position());
        var driver = Driver(initial, "rebuttal-controller");
        await driver.Act("P1", new PlayCardCommand("R-SPELL", Card, ["VICTIM"]));
        var spellId = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("REBUTTAL", RebuttalRulesTests.Card, [spellId]));
        await driver.Top();
        var pay = driver.State.PendingRuleChoice!.Request;
        await driver.Act("P2", new PayCostCommand(pay.Id, "RULE_REPLACEMENT", ["PAY:ANY"]));
        var remake = driver.State.PendingRuleChoice!.Request;
        var selected = Assert.Single(remake.Options, o => o.Label.Contains("重选目标 1 为") && o.Label.Contains("（HOST）"));
        await driver.Act("P2", new PayCostCommand(remake.Id, "RULE_REPLACEMENT", [selected.Id]));
        var confirm = driver.State.PendingRuleChoice!.Request;
        await driver.Act("P2", new PayCostCommand(confirm.Id, "RULE_REPLACEMENT", ["CONFIRM"]));
        await driver.Top();
        Assert.Single(Markers(driver.State));
        await driver.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"]));
        await driver.Top();
        Assert.Equal("P2", Assert.Single(driver.State.StackItems, i => i.EffectKind == DelayedEffect).ControllerId);
        await driver.Top();
        Assert.Single(Gold(driver.State, "P2"));
        Assert.Empty(Gold(driver.State, "P1"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("context")]
    [InlineData("controller")]
    [InlineData("unit")]
    [InlineData("source")]
    [InlineData("turn")]
    public async Task RecoveryRejectsTamperingOfTheEarnedSkill(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position(true));
        await Cast(driver);
        var item = Assert.Single(driver.State.StackItems); var c = item.DelayedDestruction!;
        var forged = mutation switch {
            "context" => item with { DelayedDestruction = null },
            "controller" => item with { DelayedDestruction = c with { ControllerId = "P2" } },
            "unit" => item with { DelayedDestruction = c with { Unit = new("HOST", 0) } },
            "source" => item with { DelayedDestruction = c with { Source = new("KILL", 1) } },
            _ => item with { DelayedDestruction = c with { TurnNumber = c.TurnNumber + 1 } }
        };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [forged] }),
            e => e.Contains("delayed destruction"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedSkillRecoverySurvivesLaterSourceOrDeadUnitIncarnations(bool source)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position(true));
        await Cast(driver);
        var state = driver.State; var id = source ? "FLOURISH" : "VICTIM";
        var owner = source ? "P1" : "P2";
        var card = state.CardObjects[id]; var zone = state.PlayerZones[owner];
        var changed = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = card with { ObjectGeneration = card.ObjectGeneration + 1 } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone with {
                Graveyard = zone.Graveyard.Where(x => x != id).ToArray(), Hand = [..zone.Hand, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") }
        };
        // Isolated recovery fixture: never rewrite the accepted journal.
        OfficialGraveyardRecastTests.Restore(changed);
        ArmamentMightTests.PublicRecovery(changed);
        var engine = new CoreRuleEngine();
        var first = await engine.ResolveAsync(changed, new("first", changed.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var second = await engine.ResolveAsync(first.State, new("second", first.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(second.Accepted, second.ErrorMessage);
        OfficialGraveyardRecastTests.Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(second);
        Assert.Single(Gold(second.State, "P1"));
        Assert.Equal("HAND", second.State.ObjectLocations[id].Zone);
    }

    [Fact]
    public async Task ARealMinionDeathKeepsTheDelayedSkillAfterTheTokenCeasesToExist()
    {
        var initial = Position();
        var definition = Assert.Single(P6TokenFactoryCatalog.GetAll(), c => c.CardNo == P6TokenFactoryCatalog.ZaunMinionTokenCardNo);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = definition.CreateObject("VICTIM", "P2", "P2", false) } };
        var driver = Driver(initial, "minion-ceases");
        await Cast(driver);
        Assert.False(driver.State.CardObjects.ContainsKey("VICTIM"));
        Assert.NotNull(Assert.Single(driver.State.StackItems).DelayedDestruction!.Destroyed);
        await driver.Top();
        Assert.Single(Gold(driver.State, "P1"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static async Task<ResolutionResult> Cast(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new PlayCardCommand("FLOURISH", Card, ["VICTIM"]));
        return await driver.Top();
    }
    internal static async Task<ResolutionResult> Kill(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["VICTIM"]));
        return await driver.Top();
    }

    internal static MatchState Position(bool immediate = false)
    {
        var s = SacredProtectorTests.Unit(RideTheWindCurrentMovementTests.Position(), "VICTIM", "P2");
        s = LocalDestructionRecallTests.At(s, "VICTIM", "P2", "BASE");
        s = AttackDamageDistributionTests.Spell(s, "FLOURISH", Card, "P1");
        s = AttackDamageDistributionTests.Spell(s, "KILL", "OGN·229/298", "P1");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["VICTIM"] = s.CardObjects["VICTIM"] with { Damage = immediate ? 2 : 0 } } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_DEADLY_FLOURISH_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
