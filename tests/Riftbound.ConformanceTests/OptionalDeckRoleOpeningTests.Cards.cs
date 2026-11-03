using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class OptionalDeckRoleOpeningTests
{
    // CN 602.4.a.6.a; the official reminder on all three prints explicitly
    // limits copies in a deck. Once admitted, copies retain separate objects.
    [Theory]
    [InlineData("SFD·190/221")]
    [InlineData("SFD·191/221")]
    [InlineData("SFD·192/221")]
    public async Task AdmittedUniqueEquipmentCopiesCanBothBePlayedAndRecovered(string cardNo)
    {
        var initial = await CardPosition(cardNo, 2);
        var run = new Run(initial);
        var first = await run.Act("alice", new PlayCardCommand("COPY-1", cardNo, []));
        Assert.Contains("COPY-1", first.State.PlayerZones["alice"].Base);
        Assert.Contains("COPY-2", first.State.PlayerZones["alice"].Hand);
        Assert.Equal(cardNo, first.State.CardObjects["COPY-1"].CardNo);
        Assert.Equal(cardNo, first.State.CardObjects["COPY-2"].CardNo);
        if (cardNo == "SFD·192/221") Assert.True(first.State.CardObjects["HOST"].IsExhausted);
        await run.Cold();
        first = await FinishCardStack(run, first);
        if (cardNo == "SFD·192/221") Assert.False(first.State.CardObjects["HOST"].IsExhausted);
        await run.Cold();

        var second = await run.Act("alice", new PlayCardCommand("COPY-2", cardNo, []));
        await run.Cold();
        second = await FinishCardStack(run, second);
        Assert.Empty(second.State.PlayerZones["alice"].Hand);
        Assert.Contains("COPY-1", second.State.PlayerZones["alice"].Base);
        Assert.Contains("COPY-2", second.State.PlayerZones["alice"].Base);
        Assert.NotSame(second.State.CardObjects["COPY-1"], second.State.CardObjects["COPY-2"]);
        Assert.All(new[] { "COPY-1", "COPY-2" }, id =>
        {
            Assert.Equal(cardNo, second.State.CardObjects[id].CardNo);
            Assert.Contains(CardObjectTags.EquipmentCard, second.State.CardObjects[id].Tags);
            Assert.Equal(new ObjectLocationState("alice", "BASE"), second.State.ObjectLocations[id]);
        });
        Assert.Equal(RunePool.Empty, second.State.RunePools["alice"]);
        await run.Cold();
    }

    [Fact]
    public async Task BardCanBePlayedWithoutALegendAndWithoutTheOptionalLegendCost()
    {
        const string bard = "SFD·079/221";
        var initial = await CardPosition(bard, 1);
        Assert.Empty(initial.PlayerZones["alice"].LegendZone);
        var run = new Run(initial);
        var played = await run.Act("alice", new PlayCardCommand("COPY-1", bard, []));
        Assert.Contains("COPY-1", played.State.PlayerZones["alice"].Base);
        Assert.Contains(CardObjectTags.UnitCard, played.State.CardObjects["COPY-1"].Tags);
        Assert.Empty(played.State.PlayerZones["alice"].LegendZone);
        Assert.Null(played.State.PendingCardChoice);
        Assert.DoesNotContain(played.Events, item => item.Kind == "LEGEND_EXHAUSTED");
        await run.Cold();
    }

    // CN 055 / 055.1 / 355.10.g: the mana cost can be paid even when the
    // subsequent non-target instruction has no controlled legend to ready.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HallOfLegendsConquestWithoutALegendCanDeclineOrPayWithoutReadyingACard(bool pay)
    {
        var initial = ConquestLifecycleRegressionTests.State("SFD·210/221", 0) with
        {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 0), ["P2"] = RunePool.Empty }
        };
        Assert.Empty(initial.PlayerZones["P1"].LegendZone);
        var run = new Run(initial);
        await run.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await run.Act("P1", new PassFocusCommand());
        var conquered = await run.Act("P2", new PassFocusCommand());
        Assert.Equal("P1", conquered.State.CardObjects["BF"].ControllerId);
        Assert.Equal(1, conquered.State.PlayerScores["P1"]);
        Assert.Equal(1, conquered.State.RunePools["P1"].Mana);
        var payment = Assert.IsType<PendingPaymentState>(conquered.State.PendingPayment);
        Assert.Contains(conquered.Events, item => item.Kind == "PAYMENT_WINDOW_OPENED");
        Assert.Contains(conquered.Prompts["P1"].Candidates ?? [], candidate => candidate.Action == CommandTypes.PayCost && candidate.Enabled);
        Assert.DoesNotContain(conquered.Prompts["P2"].Candidates ?? [], candidate => candidate.Action == CommandTypes.PayCost && candidate.Enabled);
        await run.Cold();

        var resolved = await run.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
            [pay ? "SPEND_MANA:1" : "DECLINE"]));
        Assert.Null(resolved.State.PendingPayment);
        Assert.Null(resolved.State.PendingRuleChoice);
        Assert.Equal(pay ? 0 : 1, resolved.State.RunePools["P1"].Mana);
        Assert.Empty(resolved.State.PlayerZones["P1"].LegendZone);
        Assert.Equal(pay, resolved.Events.Any(item => item.Kind == "COST_PAID"));
        Assert.DoesNotContain(resolved.Events, item => item.Kind is "LEGEND_READIED" or "TARGETS_CONFIRMED" or "RULE_CHOICE_REQUESTED");
        await run.Cold();
    }

    private static async Task<ResolutionResult> FinishCardStack(Run run, ResolutionResult result)
    {
        for (var step = 0; result.State.StackItems.Count > 0 || result.State.TriggerQueue.Count > 0; step++)
        {
            Assert.True(step < 8, "Card trigger did not finish within the expected response sequence.");
            Assert.False(string.IsNullOrWhiteSpace(result.State.PriorityPlayerId));
            result = await run.Act(result.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        return result;
    }

    // Explicit midgame positions isolate the shared play/recovery mechanism.
    // Sealed format admission and pool ownership are verified in their own step.
    private static async Task<MatchState> CardPosition(string cardNo, int copies)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var definition = catalog.Cards.Single(card => card.CardNo == cardNo);
        if (copies > 1)
        {
            Assert.Equal(1, definition.CardGroupLimit);
            Assert.Contains("{{唯我}}", definition.CardEffect);
        }
        var ids = Enumerable.Range(1, copies).Select(number => $"COPY-{number}").ToArray();
        var cards = ids.ToDictionary(id => id,
            id => PrintedCardFactory.Create(id, "alice", definition), StringComparer.Ordinal);
        cards["HOST"] = PrintedCardFactory.Create("HOST", "alice", catalog.Cards.Single(card => card.CardNo == "OGN·012/298"))
            with { IsExhausted = true };
        var locations = ids.ToDictionary(id => id, _ => new ObjectLocationState("alice", "HAND"), StringComparer.Ordinal);
        locations["HOST"] = new("alice", "BASE");
        return new("optional-role-card-copies", 0, 3, "alice",
            new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["alice"] = new(4 * copies, 2 * copies), ["bob"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["alice"] = PlayerZones.Empty with { Hand = ids, Base = ["HOST"] },
                ["bob"] = PlayerZones.Empty
            }, cardObjects: cards, objectLocations: locations, seed: 424242);
    }
}
