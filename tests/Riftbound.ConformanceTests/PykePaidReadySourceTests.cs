using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 124 / 383.2.a.1: the paid play ability retains the unit that entered.
public sealed class PykePaidReadySourceTests
{
    private const string Extra = "SPEND_POWER:red:1";
    private static MatchState Position(string no)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "PYKE", no, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return RadGearTestPosition.Put(state, "FLASH", "OGS·011/024", "P1", "HAND");
    }
    private static PlayCardCommand Paid(string no, string destination = "BASE")
        => new("PYKE", no, [], OptionalCosts: [Extra], Destination: destination);

    [Theory]
    [InlineData("UNL-028/219", "BASE")]
    [InlineData("UNL-028a/219", "BASE")]
    [InlineData("UNL-028/219", "BATTLEFIELD:BF")]
    [InlineData("UNL-028a/219", "BATTLEFIELD:BF")]
    public async Task PaidExtraCapturesAnIndependentSkillAndOnlyReadiesAndBuffsAfterResponses(string no, string destination)
    {
        var initial = Position(no);
        var command = Paid(no, destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        Assert.Equal(0, quote.Cost.GenericPower);
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.Equal(initial.RunePools["P1"].TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        var item = Assert.Single(game.State.StackItems);
        Assert.True(item.SourceConfirmed); Assert.Contains(Extra, item.OptionalCosts);
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted);
        Assert.Equal(2, game.State.CardObjects["PYKE"].Power);
        await game.Top();
        Assert.False(game.State.CardObjects["PYKE"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["PYKE"].Power);
        Assert.Equal(2, game.State.CardObjects["PYKE"].UntilEndOfTurnPowerModifier);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(2, game.State.CardObjects["PYKE"].Power); await game.Replay();
    }

    [Theory, InlineData("UNL-028/219"), InlineData("UNL-028a/219")]
    public async Task APreventedReadyStillAppliesThePaidIndependentMightInstruction(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "WARDEN", "OGN·070/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", Paid(no));
        Assert.Contains("PYKE", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted);
        Assert.Equal(2, game.State.CardObjects["PYKE"].Power);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["PYKE"].Power);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_READIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "PYKE"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(2, game.State.CardObjects["PYKE"].Power);
        await game.Replay();
    }

    [Theory, InlineData("UNL-028/219"), InlineData("UNL-028a/219")]
    public async Task UnpaidExtraDoesNotCreateAnEmptyTriggerOrAResponseWindow(string no)
    {
        var game = new Driver(Position(no)); await game.Act("P1", new PlayCardCommand("PYKE", no, []));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PriorityPlayerId); Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted); Assert.Equal(2, game.State.CardObjects["PYKE"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"); await game.Replay();
    }

    [Theory, InlineData("UNL-028/219"), InlineData("UNL-028a/219")]
    public async Task AnActualReturnResponseLeavesThePrivateHandIncarnationUnchanged(string no)
    {
        var game = new Driver(Position(no)); await game.Act("P1", Paid(no));
        var generation = game.State.CardObjects["PYKE"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["PYKE"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["PYKE"].Zone);
        Assert.True(game.State.CardObjects["PYKE"].ObjectGeneration > generation);
        var hash = MatchStateHasher.HashValue(game.State.CardObjects["PYKE"]); var prefix = game.Events().Length;
        await game.Top(); Assert.Equal(hash, MatchStateHasher.HashValue(game.State.CardObjects["PYKE"]));
        Assert.DoesNotContain(game.Events().Skip(prefix), e => Equals(e.Event.Payload.GetValueOrDefault("reason"), "SOURCE_OPTIONAL_POWER_READY"));
        await game.Replay();
    }

    [Theory, InlineData("UNL-028/219"), InlineData("UNL-028a/219")]
    public async Task AForcedMoveWithinTheFieldKeepsTheOriginalObjectEligible(string no)
    {
        var game = new Driver(Position(no)); await game.Act("P1", Paid(no, "BATTLEFIELD:BF"));
        var generation = game.State.CardObjects["PYKE"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["PYKE"])); await game.Top();
        Assert.Equal(generation, game.State.CardObjects["PYKE"].ObjectGeneration);
        Assert.Equal("BASE", game.State.ObjectLocations["PYKE"].Zone);
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted);
        await game.Top(); Assert.Equal(4, game.State.CardObjects["PYKE"].Power);
        Assert.False(game.State.CardObjects["PYKE"].IsExhausted); await game.Replay();
    }

    [Theory, InlineData("UNL-028/219"), InlineData("UNL-028a/219")]
    public async Task ActualStandbyHelpReplaysTheSamePhysicalCardWithoutLettingTheOldSkillModifyItsNewGeneration(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "HELP", "SFD·111/221", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < 12; i++) initial = RadGearTestPosition.Put(initial, "FUND" + i, i < 10 ? "UNL-R01" : "UNL-R03", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("HELP", "SFD·111/221", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["HELP"].IsFaceDown);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.True(game.State.CardObjects["HELP"].IsFaceDown);
        Assert.Equal("BF", game.State.ObjectLocations["HELP"].BattlefieldObjectId);
        await game.TapRunes("P1", Enumerable.Range(0, 12).Select(i => "FUND" + i));
        await game.Act("P1", new RecycleRuneCommand("FUND0"));
        await game.Act("P1", new RecycleRuneCommand("FUND10"));
        await game.Act("P1", Paid(no));
        var old = Assert.Single(game.State.StackItems); var original = game.State.CardObjects["PYKE"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["PYKE"])); await game.Top();
        await game.Act("P1", new RevealCardCommand("HELP", "SFD·111/221", [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK")); await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("PYKE", no, [], Destination: "BATTLEFIELD:BF"));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(old.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        Assert.True(game.State.CardObjects["PYKE"].ObjectGeneration > original);
        Assert.Equal("BF", game.State.ObjectLocations["PYKE"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["PYKE"].IsExhausted); Assert.Equal(2, game.State.CardObjects["PYKE"].Power);
        var hash = MatchStateHasher.HashValue(game.State.CardObjects["PYKE"]);
        await game.Top(); Assert.Equal(hash, MatchStateHasher.HashValue(game.State.CardObjects["PYKE"]));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("future")]
    [InlineData("unpaid")]
    public async Task RecoveryRejectsMissingOrFutureSourceReceiptsAndAnUnpaidConditionalSkill(string mutation)
    {
        var game = new Driver(Position("UNL-028/219")); await game.Act("P1", Paid("UNL-028/219"));
        var state = game.State; var item = Assert.Single(state.StackItems);
        var receipt = Assert.Single(state.CardPlays.Resolved, p => p.PlayId == item.StackItemId);
        var forged = mutation switch {
            "missing" => state with { CardPlays = state.CardPlays with { Resolved = state.CardPlays.Resolved.Where(p => p != receipt).ToArray() } },
            "future" => state with { CardPlays = state.CardPlays with { Resolved = state.CardPlays.Resolved.Select(p => p == receipt ? p with { Source = p.Source with { Generation = p.Source.Generation + 1 } } : p).ToArray() } },
            _ => state with { StackItems = [item with { OptionalCosts = [] }] }
        };
        var guard = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PermanentPlayTriggerRecoveryRules")!
            .GetMethod("Valid", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Assert.False((bool)guard.Invoke(null, [forged, Assert.Single(forged.StackItems)])!);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged)); await game.ColdCheckpoint();
    }
}
