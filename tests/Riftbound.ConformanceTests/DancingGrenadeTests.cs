using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DancingGrenadeTests
{
    internal const string Card = "UNL-020/219";

    [Fact]
    public async Task ActualDamageOffersOnlyTheAffectedUnitsControllerTheSourceReplay()
    {
        var initial = Position();
        var driver = Driver(initial, "decline");
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"]));
        var resolved = await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["VICTIM"].Damage);
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.Equal("P2", pending.PlayerId);
        Assert.Equal("STACK", pending.SourceZone);
        Assert.Single(pending.Sources, s => s.Key == "GRENADE");
        Assert.DoesNotContain("GRENADE", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(CommandTypes.PlayCard, resolved.Prompts["P2"].Actions);
        Assert.Contains(resolved.Prompts["P2"].Candidates!.Single(c => c.Action == CommandTypes.PlayCard).Sources!, s => s.Id == "GRENADE");
        await driver.Act("P2", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(2, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Contains("GRENADE", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualReplaysPayOneGenericPowerAndDealThreeThenFourDamage()
    {
        var initial = Position();
        var driver = Driver(initial, "three-four");
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"]));
        await driver.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.Equal("P2", pending.PlayerId);
        var old = driver.State.RunePools["P2"];
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(old.Mana, driver.State.RunePools["P2"].Mana);
        Assert.Equal(old.TotalPower - 1, driver.State.RunePools["P2"].TotalPower);
        var second = await driver.Top();
        Assert.Equal(3, Convert.ToInt32(Assert.Single(second.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]));
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"]));
        var third = await driver.Top();
        Assert.Equal(4, Convert.ToInt32(Assert.Single(third.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]));
        pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        await driver.Act("P2", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("VICTIM", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("GRENADE", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static MatchState Position()
        => AttackDamageDistributionTests.Spell(DeadlyFlourishTests.Position(), "GRENADE", Card, "P1");

    [Fact]
    public async Task LethalDamageWaitsForTheReplayDecisionBeforeCleanup()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = initial.CardObjects["VICTIM"] with { Damage = 3 } } };
        var driver = Driver(initial, "lethal-before-cleanup");
        await Cast(driver);
        Assert.Equal(5, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Equal("BASE", driver.State.ObjectLocations["VICTIM"].Zone);
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.Equal("P2", pending.PlayerId);
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        Assert.Contains("VICTIM", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Single(driver.State.StackItems);
        await driver.Top();
        await Decline(driver);
        Assert.Equal(3, driver.State.CardObjects["HOST"].Damage);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task PreventedDamageIsNotCountedButTheLinkedReplayCanStillBeChosen()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = initial.CardObjects["VICTIM"] with { UntilEndOfTurnEffects = ["PREVENT_NEXT_DAMAGE_THIS_TURN"] } } };
        var driver = Driver(initial, "prevented-damage");
        await Cast(driver);
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["HOST"].Damage);
        await Decline(driver);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEchoInstructionsKeepSeparateDecisionsAndSharedDamageHistory(bool replayFirst)
    {
        var initial = Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, replayFirst ? "echo-replay-first" : "echo-decline-both");
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"], OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["VICTIM"])]));
        await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["VICTIM"].Damage);
        if (replayFirst) await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        else await Decline(driver);
        Assert.Equal(4, driver.State.CardObjects["VICTIM"].Damage);
        Assert.NotNull(driver.State.PendingEffectPlay);
        await Decline(driver);
        if (replayFirst)
        {
            await driver.Top();
            Assert.Equal(4, driver.State.CardObjects["HOST"].Damage);
            await Decline(driver);
        }
        Assert.Contains("GRENADE", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualInvalidTargetSkipsBothDamageAndItsLinkedReplay()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P2");
        var driver = Driver(initial, "invalid-target");
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"]));
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["VICTIM"]));
        await driver.Top();
        var resolved = await driver.Top();
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Contains("GRENADE", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(2, 3, true)]
    public async Task ActualReplayQuotesTheAlternativeBaseCostPlusSpellshield(int shield, int power, bool payable)
    {
        var initial = Position();
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P2"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = power }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { Tags = shield == 0 ? initial.CardObjects["HOST"].Tags : [..initial.CardObjects["HOST"].Tags, "法盾2"] } } };
        var driver = Driver(initial, $"cost-{shield}-{power}");
        await Cast(driver);
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P2"];
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", new("quote", prompt.PromptId!, driver.State.Tick,
            new("GRENADE", Card, ["HOST"])));
        Assert.True(quote.IsValid, quote.Message);
        Assert.Equal(payable, quote.CanPay);
        if (payable)
        {
            await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
            Assert.Equal(0, driver.State.RunePools["P2"].Mana);
            Assert.Equal(power - 1 - shield, driver.State.RunePools["P2"].TotalPower);
            await driver.Top();
        }
        await Decline(driver);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task TheNewlyPlayedSpellCanBeCounteredBeforeItsNextDamage()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·064/298", "P1");
        var driver = Driver(initial, "counter-replay");
        await Cast(driver);
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        var child = Assert.Single(driver.State.StackItems);
        Assert.Equal("P2", child.ControllerId);
        await driver.Act("P2", new PassPriorityCommand());
        await driver.Act("P1", new PlayCardCommand("COUNTER", "OGN·064/298", [child.StackItemId]));
        await driver.Top();
        Assert.Equal(0, driver.State.CardObjects["HOST"].Damage);
        Assert.Empty(driver.State.StackItems);
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Contains("GRENADE", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("source-generation")]
    [InlineData("source-zone")]
    [InlineData("power-waiver")]
    [InlineData("mana-waiver")]
    [InlineData("completion")]
    [InlineData("turn")]
    [InlineData("history-amount")]
    [InlineData("history-tick")]
    [InlineData("unit-generation")]
    [InlineData("execution-index")]
    [InlineData("actor-and-controller")]
    public async Task RecoveryRejectsIndependentSourceReplayContinuationMutations(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await Cast(driver);
        var state = driver.State; var p = state.PendingEffectPlay!;
        var c = p.SourceReplay!; var progress = c.Progress;
        var forged = mutation switch {
            "actor" => p with { PlayerId = "P1" },
            "actor-and-controller" => p with { PlayerId = "P1", SourceReplay = c with { ControllerId = "P1" } },
            "unit-generation" => p with { SourceReplay = c with { Unit = new("VICTIM", 999999) } },
            "execution-index" => p with { Parent = p.Parent with { CompletedRepeatExecutions = 999999 } },
            "source-generation" => p with { Sources = new Dictionary<string, long> { ["GRENADE"] = 999999 } },
            "source-zone" => p with { SourceZone = "HAND" },
            "power-waiver" => p with { IgnoreBasePower = false },
            "mana-waiver" => p with { IgnoreBaseMana = false },
            "completion" => p with { SourceReplay = c with { DecisionCompleted = true } },
            "turn" => p with { SourceReplay = c with { Progress = progress with { TurnNumber = 999999 } } },
            "history-amount" => p with { SourceReplay = c with { Progress = progress with {
                DamageOccurrences = [progress.DamageOccurrences[0] with { Amount = 0 }] } } },
            _ => p with { SourceReplay = c with { Progress = progress with {
                DamageOccurrences = [progress.DamageOccurrences[0] with { Tick = 999999 }] } } }
        };
        if (mutation is "turn" or "history-amount" or "history-tick")
            forged = forged with { Parent = p.Parent with { SourceReplay = forged.SourceReplay!.Progress } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { PendingEffectPlay = forged }),
            e => e.Contains("source replay"));
    }

    [Fact]
    public async Task AReturnedEarlierDamageRecipientCannotLeakItsCurrentPrivateIncarnation()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P2");
        var driver = Driver(initial, "history-recipient-private");
        await Cast(driver);
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        await driver.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["VICTIM"]));
        await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations["VICTIM"].Zone);
        await driver.Top();
        Assert.Equal(3, driver.State.CardObjects["HOST"].Damage);
        await Decline(driver);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ACompletedTurnAndFreshIncarnationDoNotInheritOldDamageHistory()
    {
        var initial = Position();
        var driver = Driver(initial, "expires-new-incarnation");
        await Cast(driver);
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        await driver.Top();
        await Decline(driver);
        await driver.Act("P1", new EndTurnCommand());
        Assert.True(driver.State.TurnNumber > initial.TurnNumber);
        Assert.Equal(0, driver.State.CardObjects["HOST"].Damage);
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Null(driver.State.PendingEffectPlay);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        var fresh = Position() with { TurnNumber = driver.State.TurnNumber };
        fresh = fresh with { CardObjects = new Dictionary<string, CardObjectState>(fresh.CardObjects) {
            ["GRENADE"] = fresh.CardObjects["GRENADE"] with { ObjectGeneration = 2 } } };
        var independent = Driver(fresh, "fresh-incarnation");
        await Cast(independent);
        Assert.Equal(2, independent.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(3, independent.State.PendingEffectPlay!.SourceReplay!.Progress.Source.Generation);
        await Decline(independent);
        await RideTheWindCurrentMovementTests.Replay(fresh, independent);
    }

    [Fact]
    public async Task ARealMinionCanHandOffTheSpellBeforeItsCleanupDeparture()
    {
        var initial = Position();
        var token = Assert.Single(P6TokenFactoryCatalog.GetAll(), c => c.CardNo == P6TokenFactoryCatalog.ZaunMinionTokenCardNo);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = token.CreateObject("VICTIM", "P2", "P2") } };
        var driver = Driver(initial, "minion-handoff");
        await Cast(driver);
        Assert.True(driver.State.CardObjects.ContainsKey("VICTIM"));
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        Assert.False(driver.State.CardObjects.ContainsKey("VICTIM"));
        await driver.Top();
        Assert.Equal(3, driver.State.CardObjects["HOST"].Damage);
        await Decline(driver);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealFriendlyTargetGivesItsControllerTheSameReplayPermission()
    {
        var initial = Position();
        var driver = Driver(initial, "friendly-recipient");
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        await driver.Top();
        Assert.Equal("P1", driver.State.PendingEffectPlay!.PlayerId);
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"]));
        await driver.Top();
        Assert.Equal(3, driver.State.CardObjects["VICTIM"].Damage);
        await Decline(driver);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("source-null")]
    [InlineData("source-generation")]
    [InlineData("history-null")]
    [InlineData("history-id")]
    [InlineData("premature-completion")]
    public async Task RecoveryRejectsMissingOrForgedProgressOnTheActuallyReplayedStackItem(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await Cast(driver);
        await driver.Act("P2", new PlayCardCommand("GRENADE", Card, ["HOST"]));
        var item = Assert.Single(driver.State.StackItems); var progress = item.SourceReplay!;
        var forged = mutation switch {
            "missing" => item with { SourceReplay = null },
            "premature-completion" => item with { SourceReplayDecisionCompleted = true },
            "source-null" => item with { SourceReplay = progress with { Source = null! } },
            "source-generation" => item with { SourceReplay = progress with { Source = new("GRENADE", 999999) } },
            "history-null" => item with { SourceReplay = progress with { DamageOccurrences = null! } },
            _ => item with { SourceReplay = progress with { DamageOccurrences = [progress.DamageOccurrences[0] with { StackItemId = "STACK-FORGED-GRENADE" }] } }
        };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [forged] }), e => e.Contains("source replay"));
    }

    internal static async Task<ResolutionResult> Cast(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new PlayCardCommand("GRENADE", Card, ["VICTIM"]));
        return await driver.Top();
    }
    internal static Task<ResolutionResult> Decline(StandbyDefendObserverTests.Driver driver)
    {
        var p = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        return driver.Act(p.PlayerId, new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []));
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_DANCING_GRENADE_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
