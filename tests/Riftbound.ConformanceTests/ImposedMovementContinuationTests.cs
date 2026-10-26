using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ImposedMovementContinuationTests
{
    [Theory]
    [InlineData("standard", true)]
    [InlineData("standard", false)]
    [InlineData("effect", true)]
    [InlineData("effect", false)]
    public async Task AcquiredPowerIsPaidOnceOrRetainedWhenOnlyThisMovementIsDeclined(string action, bool pay)
    {
        var initial = Position(action);
        var driver = await Open(initial, action, action + (pay ? "-pay" : "-decline"));
        var original = driver.State.PendingRuleChoice!;
        Assert.Equal(action == "effect" ? "P2" : null, original.Origin.PriorityPlayerId);
        Assert.Equal("P1", original.Request.PlayerId);
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", original.Request.Kind);
        Assert.Equal(1, original.Request.PowerCost);
        Assert.DoesNotContain(original.Request.Options, o => o.Id == "PAY");
        Assert.Equal(original.Origin.StackItems.Count, original.Request.Preview!.StackItems.Count);
        Assert.Equal(original.Origin.PriorityPlayerId, original.Request.Preview.PriorityPlayerId);
        await Decide(driver, "RECYCLE_RUNE:RUNE");
        var next = driver.State.PendingRuleChoice!;
        Assert.Equal(1, next.Request.Preview!.RunePools["P1"].TotalPower);
        Assert.Equal("RUNE_DECK", next.Request.Preview.ObjectLocations["RUNE"].Zone);
        Assert.Contains(next.Request.Options, o => o.Id == "PAY");
        Assert.DoesNotContain(next.Request.Options, o => o.Id == "RECYCLE_RUNE:RUNE");
        await Decide(driver, pay ? "PAY" : "DECLINE");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(pay ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        var movedIds = action == "effect" ? new[] { "D", "D2" } : ["A", "B"];
        foreach (var id in movedIds)
        {
            Assert.Equal(pay ? "FIELD2" : action == "effect" ? "FIELD1" : null, driver.State.ObjectLocations[id].BattlefieldObjectId);
            Assert.Equal(action == "standard" && pay, driver.State.CardObjects[id].IsExhausted);
            Assert.Equal(pay ? 1 : 0, driver.State.CardObjects[id].MovementCountThisTurn);
        }
        var events = driver.Events().Select(e => e.Event).ToArray();
        Assert.Single(events, e => e.Kind == "RUNE_RECYCLED");
        Assert.Equal((pay ? 1 : 0) + (action == "effect" ? 1 : 0), events.Count(e => e.Kind == "COST_PAID"));
        Assert.Equal(pay ? 1 : 0, events.Count(e => e.Kind == "COST_PAID" && e.Payload.GetValueOrDefault("reason") as string == "MAGESEEKER_PATROL_MOVE_TAX_STATIC"));
        Assert.Equal(pay ? 2 : 0, events.Count(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(pay ? 0 : 1, events.Count(e => e.Kind == "MOVEMENT_COST_DECLINED"));
        if (!pay) Assert.DoesNotContain(driver.State.TriggerQueue, t => t.MovementObserver is not null);
        if (action == "effect") { Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FundedEffectMovementStillLetsItsExecutorDecline(bool pay)
    {
        var initial = WithPool(Position("effect"), new(4, 2));
        var driver = await Open(initial, "effect", "funded-" + pay);
        Assert.Contains(driver.State.PendingRuleChoice!.Request.Options, o => o.Id == "PAY");
        await Decide(driver, pay ? "PAY" : "DECLINE");
        Assert.Equal(pay ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? "FIELD2" : "FIELD1", driver.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("effect")]
    public async Task AResourceThatOnlyGainsSpellManaCanBeUsedButCannotPayThisPowerCost(string action)
    {
        var initial = AddBase(Position(action), "LUX", "OGS·014/024");
        var driver = await Open(initial, action, action + "-lux");
        var lux = Assert.Single(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("RESOURCE:LUX:", StringComparison.Ordinal));
        await Decide(driver, lux.Id);
        var preview = driver.State.PendingRuleChoice!.Request.Preview!;
        Assert.Equal(2, preview.RunePools["P1"].SpellOnlyMana); Assert.Equal(0, preview.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id == "PAY");
        await Decide(driver, "DECLINE");
        Assert.True(driver.State.CardObjects["LUX"].IsExhausted);
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.False(driver.State.CardObjects["A"].IsExhausted);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            || e.Event.Kind == "COST_PAID" && e.Event.Payload.GetValueOrDefault("reason") as string == "MAGESEEKER_PATROL_MOVE_TAX_STATIC");
        Assert.Equal(action == "effect" ? 1 : 0, driver.Events().Count(e => e.Event.Kind == "COST_PAID"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("standard", true)]
    [InlineData("standard", false)]
    [InlineData("effect", true)]
    [InlineData("effect", false)]
    public async Task GoldCeasingToExistAndItsResourceSurviveEitherMovementDecision(string action, bool pay)
    {
        var initial = AddBase(Position(action), "GOLD", P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        var driver = await Open(initial, action, action + "-gold-" + pay);
        var gold = Assert.Single(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("RESOURCE:GOLD:", StringComparison.Ordinal));
        await Decide(driver, gold.Id);
        var preview = driver.State.PendingRuleChoice!.Request.Preview!;
        Assert.DoesNotContain("GOLD", preview.PlayerZones["P1"].Base);
        // CN186.1: a destroyed token ceases to exist rather than remaining banished.
        Assert.False(preview.CardObjects.ContainsKey("GOLD"));
        Assert.Equal(1, preview.RunePools["P1"].TotalPower);
        await Decide(driver, pay ? "PAY" : "DECLINE");
        Assert.DoesNotContain("GOLD", driver.State.PlayerZones["P1"].Base);
        Assert.False(driver.State.CardObjects.ContainsKey("GOLD"));
        Assert.Equal(pay ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        Assert.Single(driver.Events(), e => e.Event.Kind == "ABILITY_ACTIVATED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AStandardMoverCannotSpendItsExhaustionTwiceToGenerateMana()
    {
        var initial = Position("standard");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["A"] = OfficialGarenConquestBattlefieldCountTests.Printed("A", "OGS·014/024", "P1") } };
        var driver = await Open(initial, "standard", "exclude-moving-lux");
        Assert.DoesNotContain(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("RESOURCE:A:", StringComparison.Ordinal));
        await Decide(driver, "RECYCLE_RUNE:RUNE"); await Decide(driver, "PAY");
        Assert.True(driver.State.CardObjects["A"].IsExhausted);
        Assert.Equal(0, driver.State.RunePools["P1"].SpellOnlyMana);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "ABILITY_ACTIVATED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("stale")]
    [InlineData("unavailable")]
    [InlineData("multiple")]
    [InlineData("purpose")]
    public async Task ForgedOrUnavailablePaymentChoicesRejectWithoutChangingAnyView(string branch)
    {
        var driver = await Open(Position("effect"), "effect", "reject-" + branch);
        var state = driver.State; var request = state.PendingRuleChoice!.Request;
        var option = branch == "purpose" ? "PURPOSE:PLAY_SPELL" : branch == "unavailable" ? "PAY" : "DECLINE";
        var command = new PayCostCommand(branch == "stale" ? "OLD" : request.Id, "RULE_REPLACEMENT",
            branch == "multiple" ? [option, "RECYCLE_RUNE:RUNE"] : [option]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged-movement-payment", branch == "opponent" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state)), MatchStateHasher.HashValue(result.Snapshots));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(state)), MatchStateHasher.HashValue(result.Prompts));
    }

    [Theory]
    [InlineData("payer")]
    [InlineData("kind")]
    [InlineData("cost")]
    [InlineData("option")]
    [InlineData("preview")]
    [InlineData("transcript")]
    public async Task OriginalCheckpointRejectsCoherentLookingTamperedPaymentContinuations(string branch)
    {
        var driver = await Open(Position("effect"), "effect", "tamper-" + branch);
        await Decide(driver, "RECYCLE_RUNE:RUNE");
        var state = OfficialInsightAndSpellLockTests.Restore(driver.State); var pending = state.PendingRuleChoice!;
        var request = pending.Request;
        request = branch switch {
            "payer" => request with { PlayerId = "P2" },
            "kind" => request with { Kind = "INSTRUCTION_POWER_PAYMENT" },
            "cost" => request with { PowerCost = 0 },
            "option" => request with { Options = [.. request.Options, new("FREE", "伪造免费移动")] },
            "preview" => request with { Preview = request.Preview! with {
                RunePools = new Dictionary<string, RunePool>(request.Preview!.RunePools) { ["P1"] = new(0, 99) } } },
            _ => request };
        var forged = pending with { Request = request };
        if (branch == "transcript") forged = forged with { Answers = [new("FORGED", "RECYCLE_RUNE:RUNE")] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { PendingRuleChoice = forged }));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public async Task PaymentPromptAndResourcePreviewDoNotRevealOpponentPrivateIdentities()
    {
        var initial = Position("effect");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
            Hand = ["PRIVATE-HAND"], MainDeck = ["PRIVATE-DECK"], RuneDeck = ["PRIVATE-RUNE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["PRIVATE-HAND"] = OfficialGarenConquestBattlefieldCountTests.Printed("PRIVATE-HAND", "OGN·268/298", "P2"),
                ["PRIVATE-DECK"] = OfficialGarenConquestBattlefieldCountTests.Printed("PRIVATE-DECK", "OGN·268/298", "P2"),
                ["PRIVATE-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("PRIVATE-RUNE", "OGN·007/298", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["PRIVATE-HAND"] = new("P2", "HAND"), ["PRIVATE-DECK"] = new("P2", "MAIN_DECK"), ["PRIVATE-RUNE"] = new("P2", "RUNE_DECK") } };
        var driver = await Open(initial, "effect", "private");
        await Decide(driver, "RECYCLE_RUNE:RUNE");
        var views = JsonSerializer.Serialize(new { prompt = ResolutionResult.BuildPrompts(driver.State)["P1"],
            snapshot = ResolutionResult.BuildSnapshots(driver.State)["P1"],
            spectator = MatchReplayRedactor.BuildSpectatorFrame(initial.RoomId, driver.State.Tick, driver.Events().LongLength, [], driver.State) });
        foreach (var id in new[] { "PRIVATE-HAND", "PRIVATE-DECK", "PRIVATE-RUNE" }) Assert.DoesNotContain(id, views);
        await Decide(driver, "DECLINE");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TappingThenRecyclingTheSameRuneRetainsManaAndDoesNotDoublePayMovement(bool pay)
    {
        var initial = Position("standard"); var driver = await Open(initial, "standard", "tap-recycle-" + pay);
        await Decide(driver, "TAP_RUNE:RUNE");
        Assert.Equal(1, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"].Mana);
        Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id == "PAY" || o.Id == "TAP_RUNE:RUNE");
        Assert.Contains(driver.State.PendingRuleChoice.Request.Options, o => o.Id == "RECYCLE_RUNE:RUNE");
        await Decide(driver, "RECYCLE_RUNE:RUNE"); await Decide(driver, pay ? "PAY" : "DECLINE");
        Assert.Equal(1, driver.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_TAPPED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AFullCombinedManaPoolNeverOffersAnOverflowingLuxOrRuneGain()
    {
        var initial = WithPool(Position("effect"), new RunePool(4, 1, spellOnlyMana: int.MaxValue - 4));
        foreach (var id in new[] { "LUX1", "LUX2", "LUX3" }) initial = AddBase(initial, id, "OGS·014/024");
        var driver = await Open(initial, "effect", "full-combined-mana");
        foreach (var id in new[] { "LUX1", "LUX2" })
            await Decide(driver, driver.State.PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:" + id + ":", StringComparison.Ordinal)).Id);
        var preview = driver.State.PendingRuleChoice!.Request.Preview!;
        Assert.Equal(int.MaxValue, (long)preview.RunePools["P1"].Mana + preview.RunePools["P1"].SpellOnlyMana);
        Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id.StartsWith("RESOURCE:LUX3:", StringComparison.Ordinal) || o.Id == "TAP_RUNE:RUNE");
        Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id == "PAY");
        await Decide(driver, "DECLINE");
        Assert.True(driver.State.RunePools["P1"].HasSupportedAmounts); Assert.False(driver.State.CardObjects["LUX3"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AFullPowerPoolNeverOffersAnOverflowingGoldOrRuneGain()
    {
        var initial = WithPool(Position("effect"), new(4, int.MaxValue));
        foreach (var id in new[] { "GOLD1", "GOLD2" }) initial = AddBase(initial, id, P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        var driver = await Open(initial, "effect", "full-power");
        await Decide(driver, driver.State.PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:GOLD1:", StringComparison.Ordinal)).Id);
        Assert.Equal(int.MaxValue, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id.StartsWith("RESOURCE:GOLD2:", StringComparison.Ordinal) || o.Id == "RECYCLE_RUNE:RUNE");
        await Decide(driver, "PAY"); Assert.Equal(int.MaxValue - 1, driver.State.RunePools["P1"].TotalPower);
        Assert.True(driver.State.RunePools["P1"].HasSupportedAmounts); Assert.Contains("GOLD2", driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OrdinaryManaGenerationThenConversionRecomputesTheCurrentPaymentPool()
    {
        var initial = AddBase(AddBase(Position("standard"), "CHANNEL", P4ActivatedAbilityCatalog.EnergyChannelCardNo), "STELE", P4ActivatedAbilityCatalog.AncientSteleCardNo);
        var driver = await Open(initial, "standard", "ordinary-conversion");
        Assert.EndsWith(P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix + "0",
            Assert.Single(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("RESOURCE:STELE:", StringComparison.Ordinal)).Id);
        await Decide(driver, driver.State.PendingRuleChoice.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:CHANNEL:", StringComparison.Ordinal)).Id);
        var stele = driver.State.PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:STELE:", StringComparison.Ordinal)
            && o.Id.EndsWith(P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix + "1", StringComparison.Ordinal));
        await Decide(driver, stele.Id); await Decide(driver, "PAY");
        Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.All(new[] { "CHANNEL", "STELE" }, id => Assert.True(driver.State.CardObjects[id].IsExhausted));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static MatchState Position(string action)
        => action == "effect" ? WithPool(ImposedMovementPaymentTests.Position(), new(4, 1)) : ImposedMovementPaymentTests.Position();
    internal static MatchState WithPool(MatchState state, RunePool pool)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    internal static MatchState AddBase(MatchState state, string id, string no)
        => state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Append(id).ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = P6TokenFactoryCatalog.TryGetByCardNo(no, out var token)
                ? token.CreateObject(id, "P1", "P1", false) : OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "BASE") } };
    internal static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial, string action, string branch)
    {
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, evidence is { Length: > 0 } ? Path.Combine(evidence, branch) : null);
        if (action == "standard") await driver.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", OptionalCosts: [], SourceObjectIds: ["A", "B"]));
        else { await driver.Act("P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2")); await driver.Top(); }
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", driver.State.PendingRuleChoice?.Request.Kind);
        return driver;
    }
    internal static Task<ResolutionResult> Decide(StandbyDefendObserverTests.Driver driver, string option)
    {
        var request = driver.State.PendingRuleChoice!.Request;
        return driver.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
    }
}
