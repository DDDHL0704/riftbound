using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialLegendConquestTests
{
    internal static MatchState Position(string no, bool exhausted = true, int mana = 2)
    {
        var s = ConquestLifecycleRegressionTests.State("OGN·296/298", 0);
        var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["LEGEND"] = new("LEGEND", cardNo: no, ownerId: "P1", controllerId: "P1", isExhausted: exhausted, tags: ["CARD_TYPE:LEGEND"]);
        cards["RUNE"] = cards["RUNE"] with { IsExhausted = false };
        return s with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(mana, 2) } };
    }

    [Theory]
    [InlineData("SFD·195/221")]
    [InlineData("OGN·269/298")]
    public async Task ConquestDoesNotReadyOrSpendBeforeResponse(string no)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position(no));
        Assert.True(opened.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(2, opened.State.RunePools["P1"].Mana);
        Assert.Single(opened.State.StackItems); Restore(opened.State);
        if (no == "SFD·195/221") Assert.NotNull(opened.State.PendingPayment);
        else Assert.NotNull(opened.State.PriorityPlayerId);
    }
    internal static Task<ResolutionResult> Pay(MatchState s, bool pay) => Act(s, "P1",
        new PayCostCommand(s.PendingPayment!.PaymentId, s.PendingPayment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
    internal static Task<ResolutionResult> Choose(MatchState s, params string[] ids) => Act(s, "P1",
        new ChooseCardsCommand(s.PendingCardChoice!.ChoiceId, s.PendingCardChoice.ChoiceWindow, ids));

    [Theory]
    [InlineData("SFD·195/221", true)]
    [InlineData("SFD·195a/221·P", true)]
    [InlineData("SFD·246/221", true)]
    [InlineData("SFD·195/221", false)]
    public async Task PaidReadyConfirmsChoiceBeforeResponsesAndReadiesOnlyAtResolution(string no, bool pay)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position(no));
        Assert.Equal(1, opened.State.PendingPayment!.ManaCost); Assert.Equal(0, opened.State.PendingPayment.PowerCost);
        var confirmed = await Pay(opened.State, pay); Restore(confirmed.State);
        Assert.True(confirmed.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(pay ? 1 : 2, confirmed.State.RunePools["P1"].Mana);
        if (!pay) { Assert.Empty(confirmed.State.StackItems); return; }
        var done = await Top(confirmed.State);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(1, done.State.RunePools["P1"].Mana); Restore(done.State);
    }

    [Fact]
    public async Task ResourcePaymentFailureIsAtomicAndTappingRuneCanFinish()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position("SFD·195/221", mana: 0));
        var pending = opened.State.PendingPayment!;
        var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("poor", "P1", CommandTypes.PayCost),
            new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        var tapped = await Act(opened.State, "P1", new TapRuneCommand("RUNE")); Restore(tapped.State);
        Assert.Null(tapped.State.PriorityPlayerId); Assert.Equal(1, tapped.State.RunePools["P1"].Mana);
        var paid = await Pay(tapped.State, true); var done = await Top(paid.State);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·269/298")]
    [InlineData("OGN·310/298")]
    [InlineData("OGN·310*/298")]
    public async Task MandatoryReadyTriggersEvenWhileSourceIsAlreadyActive(string no)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position(no, exhausted: false));
        Assert.Single(opened.State.StackItems); Assert.Null(opened.State.PendingPayment); Assert.Null(opened.State.PendingCardChoice);
        var changed = opened.State with { CardObjects = new Dictionary<string, CardObjectState>(opened.State.CardObjects) {
            ["LEGEND"] = opened.State.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var done = await Top(changed); Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Restore(done.State);
    }

    [Theory]
    [InlineData("SFD·195/221")]
    [InlineData("OGN·269/298")]
    public async Task NewSourceObjectCannotBeReadiedByOldTrigger(string no)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position(no));
        var confirmed = opened.State.PendingPayment is null ? opened : await Pay(opened.State, true);
        var changed = confirmed.State with { CardObjects = new Dictionary<string, CardObjectState>(confirmed.State.CardObjects) {
            ["LEGEND"] = confirmed.State.CardObjects["LEGEND"] with { ObjectGeneration = 2 } } };
        var done = await Top(changed); Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(done.Events, e => e.Kind == "LEGEND_READIED"); Restore(done.State);
    }

    internal static MatchState CombatPosition(string no = "UNL-187/219", int power = 4, bool exhausted = false)
    {
        var s = DeathAndDuelLifecycleTests.State(); var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["BF"] = cards["BF"] with { CardNo = "OGN·296/298" };
        cards["D"] = cards["D"] with { CardNo = "SFD·125/221" };
        cards["A"] = cards["A"] with { CardNo = "OGN·096/298", Power = power };
        cards["LEGEND"] = new("LEGEND", cardNo: no, ownerId: "P1", controllerId: "P1", isExhausted: exhausted, tags: ["CARD_TYPE:LEGEND"]);
        cards["TARGET"] = new("TARGET", cardNo: "OGN·096/298", ownerId: "P1", controllerId: "P1", isExhausted: true, power: 1, tags: [CardObjectTags.UnitCard]);
        cards["ENEMY"] = new("ENEMY", cardNo: "OGN·096/298", ownerId: "P2", controllerId: "P2", isExhausted: true, power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]);
        return s with { CardObjects = cards, RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(2,2) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["LEGEND"], Base = ["A", "TARGET"] },
                ["P2"] = s.PlayerZones["P2"] with { Base = ["ENEMY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND"), ["TARGET"] = new("P1", "BASE"), ["ENEMY"] = new("P2", "BASE") } };
    }
    internal static async Task<ResolutionResult> CombatConquer(MatchState s)
    {
        var moved = await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var second = await Act(first.State, "P2", new PassFocusCommand());
        var declared = await Act(second.State, "P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        return await CombatTestDriver.FinishAsync(declared);
    }

    [Theory]
    [InlineData("UNL-187/219", true)]
    [InlineData("UNL-229/219", true)]
    [InlineData("UNL-229*/219", true)]
    [InlineData("UNL-187/219", false)]
    public async Task OverkillPlayerSelectsAnyUnitAndPaysExhaustionBeforeResponses(string no, bool accept)
    {
        var opened = await CombatConquer(CombatPosition(no));
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted); Assert.True(opened.State.CardObjects["TARGET"].IsExhausted);
        Assert.Contains("TARGET", opened.State.PendingCardChoice!.LegalObjectIds); Assert.Contains("ENEMY", opened.State.PendingCardChoice.LegalObjectIds); Restore(opened.State);
        var confirmed = await Choose(opened.State, accept ? ["TARGET"] : []);
        Assert.Equal(accept, confirmed.State.CardObjects["LEGEND"].IsExhausted); Assert.True(confirmed.State.CardObjects["TARGET"].IsExhausted); Restore(confirmed.State);
        if (accept) { var done = await Top(confirmed.State); Assert.False(done.State.CardObjects["TARGET"].IsExhausted); Restore(done.State); }
        else Assert.Empty(confirmed.State.StackItems);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public async Task OverkillThresholdUsesActualCombatAssignment(int power, bool triggers)
    {
        var done = await CombatConquer(CombatPosition(power: power));
        Assert.Equal(triggers, done.State.PendingCardChoice is not null);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Restore(done.State);
        Assert.Equal(power - 1, Assert.Single(done.Events, e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["assignedOverkillDamageToEnemyUnits"]);
    }

    [Fact]
    public async Task ActiveUnitIsLegalAndSourceThatCannotExhaustIsNotCharged()
    {
        var s = CombatPosition(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["TARGET"] = s.CardObjects["TARGET"] with { IsExhausted = false } } };
        var opened = await CombatConquer(s); Assert.Contains("TARGET", opened.State.PendingCardChoice!.LegalObjectIds);
        var confirmed = await Choose(opened.State, "TARGET"); var done = await Top(confirmed.State); Restore(done.State);
        Assert.False(done.State.CardObjects["TARGET"].IsExhausted); Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_READIED");
        var unavailable = await CombatConquer(CombatPosition(exhausted: true));
        Assert.Null(unavailable.State.PendingCardChoice); Assert.Empty(unavailable.State.StackItems);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnemyWardAndExhaustionAreCommittedTogetherOrDeclined(bool pay)
    {
        var opened = await CombatConquer(CombatPosition());
        var selected = await Choose(opened.State, "ENEMY"); Restore(selected.State);
        Assert.False(selected.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(2, selected.State.RunePools["P1"].TotalPower);
        var confirmed = await Pay(selected.State, pay); Restore(confirmed.State);
        Assert.Equal(pay, confirmed.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(pay ? 1 : 2, confirmed.State.RunePools["P1"].TotalPower);
        if (pay) { var done = await Top(confirmed.State); Assert.False(done.State.CardObjects["ENEMY"].IsExhausted); Restore(done.State); }
        else Assert.Empty(confirmed.State.StackItems);
    }

    [Fact]
    public async Task WardFailureDoesNotExhaustAndCannotPayWhenSourceBecameUnavailable()
    {
        var opened = await CombatConquer(CombatPosition()); var selected = await Choose(opened.State, "ENEMY");
        var pending = selected.State.PendingPayment!;
        foreach (var loseSource in new[] { false, true })
        {
            var s = selected.State;
            if (loseSource) s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["LEGEND"] = s.CardObjects["LEGEND"] with { IsExhausted = true } } };
            else s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(8,0) } };
            var rejected = await new CoreRuleEngine().ResolveAsync(s, new("bad-ward", "P1", CommandTypes.PayCost), new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]), default);
            Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
            var declined = await Pay(s, false); Assert.Empty(declined.State.StackItems); Restore(declined.State);
        }
    }

    [Fact]
    public async Task ProtectedTargetIsExcludedAndNewTargetObjectDoesNotReady()
    {
        var s = CombatPosition(power: 6); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["ENEMY"] = s.CardObjects["ENEMY"] with { CardNo = "UNL-147/219" } } };
        var opened = await CombatConquer(s); Assert.DoesNotContain("ENEMY", opened.State.PendingCardChoice!.LegalObjectIds);
        var paid = await Choose(opened.State, "TARGET");
        s = paid.State with { CardObjects = new Dictionary<string, CardObjectState>(paid.State.CardObjects) { ["TARGET"] = paid.State.CardObjects["TARGET"] with { ObjectGeneration = 2 } } };
        var done = await Top(s); Assert.True(done.State.CardObjects["TARGET"].IsExhausted); Assert.True(done.State.CardObjects["LEGEND"].IsExhausted); Restore(done.State);
    }

    [Fact]
    public async Task DeathDrawAndConquestUseApnapOrderingAndSeparateResponses()
    {
        var s = CombatPosition();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { CardNo = "OGN·096/298" } } };
        var opened = await CombatConquer(s);
        Assert.Equal(2, opened.State.TriggerQueue.Count); Restore(opened.State);
        var order = opened.State.TriggerQueue.OrderBy(t => t.ControllerId == "P2" ? 0 : 1).Select(t => t.TriggerId).ToArray();
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        var paid = await Choose(ordered.State, "TARGET"); Restore(paid.State);
        Assert.True(paid.State.CardObjects["TARGET"].IsExhausted);
        var handCount = paid.State.PlayerZones["P2"].Hand.Count;
        var death = await Top(paid.State);
        Assert.Equal(handCount + 1, death.State.PlayerZones["P2"].Hand.Count);
        Assert.True(death.State.CardObjects["TARGET"].IsExhausted);
        var conquest = await Top(death.State);
        Assert.False(conquest.State.CardObjects["TARGET"].IsExhausted); Restore(conquest.State);
    }

    [Fact]
    public async Task PaidUnitReadyIsIndependentOfSourceControlAndGeneration()
    {
        var opened = await CombatConquer(CombatPosition()); var paid = await Choose(opened.State, "TARGET");
        var s = paid.State with { CardObjects = new Dictionary<string, CardObjectState>(paid.State.CardObjects) {
            ["LEGEND"] = paid.State.CardObjects["LEGEND"] with { ControllerId = "P2", ObjectGeneration = 2 } } };
        var done = await Top(s); Assert.False(done.State.CardObjects["TARGET"].IsExhausted); Restore(done.State);
    }

    [Fact]
    public async Task OverkillDoesNotTriggerFromAnUnopposedConquest()
    {
        var done = await ConquestLifecycleRegressionTests.Conquer(Position("UNL-187/219", exhausted: false));
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice); Restore(done.State);
    }

    [Theory]
    [InlineData("SFD·195/221")]
    [InlineData("OGN·269/298")]
    public async Task LegendsControlledByOpponentCannotTriggerForMe(string no)
    {
        var s = Position(no); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["LEGEND"] = s.CardObjects["LEGEND"] with { ControllerId = "P2" } } };
        var done = await ConquestLifecycleRegressionTests.Conquer(s); Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingPayment);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted); Restore(done.State);
    }

    [Fact]
    public async Task RecoveryRejectsMissingConquestContextUnpaidCostAndForgedCostReceipt()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position("SFD·195/221"));
        var paid = await Pay(opened.State, true); var item = paid.State.StackItems.Single();
        foreach (var forged in new[] { item with { LegendConquest = null }, item with { TriggerCost = null }, item with { TriggerCost = item.TriggerCost! with { Mana = 0 } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [forged] }));
        var vi = await Choose((await CombatConquer(CombatPosition())).State, "TARGET");
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(vi.State with { StackItems = [vi.State.StackItems.Single() with { TargetGenerations = null }] }), e => e.Contains("legend conquest"));
    }

}
