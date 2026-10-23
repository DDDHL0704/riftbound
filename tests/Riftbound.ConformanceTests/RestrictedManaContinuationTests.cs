using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.RestrictedManaPaymentTests;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedManaContinuationTests
{
    [Theory]
    [InlineData("OGS·014/024")]
    [InlineData("VEN·SP6")]
    public async Task AnInstructionMayAcquireRestrictedManaButNeverSpendItAsInstructionPayment(string card)
    {
        var initial = Pool(RestrictedManaLifecycleTests.Position(card), new(1, 0));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        await driver.Top();
        var request = driver.State.PendingRuleChoice!.Request;
        var resource = Assert.Single(request.Options, o => o.Id.StartsWith("RESOURCE:LUX:"));
        await driver.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource.Id]));
        request = driver.State.PendingRuleChoice!.Request;
        Assert.Equal(2, request.Preview!.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(0, request.Preview.RunePools["P1"].Mana);
        Assert.DoesNotContain(request.Options, o => o.Id.StartsWith("SPEND_MANA:") || o.Id == "SPEND_POWER:1");
        await driver.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(0, driver.State.CardObjects["TARGET"].Damage);
        Assert.Single(driver.Events(), e => e.Event.Kind == "MANA_GAINED");
        Assert.True(driver.State.CardObjects["LUX"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AConditionalCounterCannotSpendRestrictedManaEvenAfterVoluntaryLuxAcquisition()
    {
        const string card = "SFD·136/221";
        var initial = OfficialCounterRepeatTests.Position(card) with { Tick = 0, UntilEndOfTurnEffects = [] };
        initial = AddSource(Pool(initial, new(0, 0, spellOnlyMana: 2), "P2"), "LUX", "OGS·014/024", "P2");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(card, out var behavior));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("C", card, ["S1"], Mode: behavior.Mode));
        for (var i = 0; i < 4 && driver.State.PendingRuleChoice is null; i++)
            await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        var request = driver.State.PendingRuleChoice!.Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.DoesNotContain(request.Options, o => o.Id == "SPEND_MANA:2");
        var hash = MatchStateHasher.Hash(driver.State);
        var forged = await new CoreRuleEngine().ResolveAsync(driver.State, new("forged-instruction-purpose", "P2", CommandTypes.PayCost),
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_MANA:2"]), default);
        Assert.False(forged.Accepted); Assert.Empty(forged.Events); Assert.Equal(hash, MatchStateHasher.Hash(forged.State));
        var resource = Assert.Single(request.Options, o => o.Id.StartsWith("RESOURCE:LUX:"));
        await driver.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource.Id]));
        // With neither ordinary mana nor another resource action, the optional
        // instruction cannot be paid and continues without another prompt.
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(4, driver.State.RunePools["P2"].SpellOnlyMana);
        Assert.Contains(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task GainedManaSurvivesTheLuxSourceLeavingPlay()
    {
        var state = AddSource(RestrictedManaLifecycleTests.Position(), "MALZ", "OGN·113/298", "P1");
        var initial = state;
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await driver.Act("P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["LUX"]));
        Assert.Contains("LUX", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        await driver.Act("P1", new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        Assert.Equal(1, driver.State.RunePools["P1"].SpellOnlyMana);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BothPlayersClearRestrictedManaAtMainStartAfterRealBeginningResponses()
    {
        var state = AddSource(AddSource(BeginningDrawTests.Mushroom(), "LUX", "OGS·014/024", "P1"),
            "OTHER-LUX", "VEN·SP6", "P2");
        var opened = await BeginningDrawTests.Start(state);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Single(opened.State.StackItems);
        var first = await Act(opened.State, new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        var passed = await Act(first.State, new PassPriorityCommand());
        var second = await Act(passed.State, new ActivateAbilityCommand("OTHER-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []), "P2");
        Assert.Equal(2, second.State.RunePools["P1"].SpellOnlyMana); Assert.Equal(2, second.State.RunePools["P2"].SpellOnlyMana);
        var main = await TurnSequenceTestDriver.Complete(second);
        Assert.Equal(MatchPhases.Main, main.State.Phase); Assert.Null(main.State.TurnStartStep);
        Assert.All(main.State.RunePools.Values, pool => Assert.Equal(RunePool.Empty, pool));
        Assert.Contains(main.Events, e => e.Kind == "RUNE_POOL_CLEARED");
        OfficialGraveyardRecastTests.Restore(main.State);
    }

    [Fact]
    public async Task BothPlayersClearRemainingManaAtTheActualTurnEnd()
    {
        var state = AddSource(RestrictedManaLifecycleTests.Position(), "OTHER-LUX", "VEN·SP6", "P2");
        // A nonempty next-turn deck avoids unrelated burnout while checking the boundary.
        state = AddToDeck(state, "NEXT", "SFD·125/221", "P2");
        var first = await Act(state, new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        var play = await Act(first.State, new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]));
        var pass = await Act(play.State, new PassPriorityCommand(), play.State.PriorityPlayerId!);
        var other = await Act(pass.State, new ActivateAbilityCommand("OTHER-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []), "P2");
        var payment = other.State;
        for (var i = 0; i < 2 && payment.PendingRuleChoice is null; i++)
            payment = (await Act(payment, new PassPriorityCommand(), payment.PriorityPlayerId!)).State;
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", payment.PendingRuleChoice!.Request.Kind);
        var finished = await InstructionPowerPaymentTimingTests.Pay(payment, "SPEND_POWER:0");
        Assert.Equal(1, finished.State.RunePools["P1"].SpellOnlyMana); Assert.Equal(2, finished.State.RunePools["P2"].SpellOnlyMana);
        var end = await Act(finished.State, new EndTurnCommand());
        Assert.All(end.State.RunePools.Values, pool => Assert.Equal(RunePool.Empty, pool));
        Assert.Contains(end.Events, e => e.Kind == "RUNE_POOL_CLEARED");
    }

    internal static MatchState AddSource(MatchState state, string id, string card, string player)
        => state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { Base = state.PlayerZones[player].Base.Append(id).ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, card, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") } };
    private static MatchState AddToDeck(MatchState state, string id, string card, string player)
        => state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { MainDeck = state.PlayerZones[player].MainDeck.Append(id).ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, card, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "MAIN_DECK") } };
}
