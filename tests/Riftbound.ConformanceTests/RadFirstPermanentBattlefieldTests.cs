using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadFirstPermanentBattlefieldTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RakelstakeInsightsOnceForThePlayerWhoPlayedHere(bool recycle)
    {
        var state = Position("RAD·165/167");
        state = Put(state, "DECK-FIRST", "OGN·219/298", "MAIN_DECK");
        state = Put(state, "DECK-SECOND", "OGN·219/298", "MAIN_DECK");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        var trigger = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", trigger.ControllerId);
        Assert.Equal("FIRST_PERMANENT", trigger.InsightContext!.Kind);
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(["DECK-FIRST"], choice.LegalObjectIds);
        var illegal = new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", ["DECK-SECOND"]);
        var hash = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("wrong-insight-card", "P1", illegal.CmdType), illegal, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", recycle ? ["DECK-FIRST"] : []));
        Assert.Equal(recycle ? new[] { "DECK-SECOND", "DECK-FIRST" } : ["DECK-FIRST", "DECK-SECOND"],
            game.State.PlayerZones["P1"].MainDeck);
        await game.Act("P1", new PlayCardCommand("SECOND", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Empty(game.State.TriggerQueue);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task RakelstakeIgnoresBasePlaysAndEmptyDeckInsightDoesNotBurnOut()
    {
        var game = new Driver(Position("RAD·165/167"));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·219/298", [], Destination: "BASE"));
        Assert.Empty(game.State.TriggerQueue);
        await game.Act("P1", new PlayCardCommand("SECOND", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal("FIRST_PERMANENT", Assert.Single(game.State.StackItems).InsightContext!.Kind);
        await game.Top();
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.Contains("BURN", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData("BATTLEFIELD:BF", 1)]
    [InlineData("BASE", 0)]
    public async Task WindsweptPlazaQuotesAndPaysOnlyTheFirstPermanentHere(string destination, int reduction)
    {
        var game = new Driver(Position());
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·219/298", out var unit));
        var first = new PlayCardCommand("FIRST", unit.CardNo, [], Destination: destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, first));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(unit.ManaCost - reduction, quote.Cost!.Mana);
        await game.Act("P1", first);
        await game.Act("P1", new PlayCardCommand("SECOND", unit.CardNo, [], Destination: destination));
        Assert.Equal(30 - 2 * unit.ManaCost + reduction, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task BasePlayAndRejectedPlayDoNotConsumeTheLocalReduction()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·219/298", [], Destination: "BASE"));
        var hash = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("invalid-plaza", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("MISSING", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·219/298", out var unit));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("SECOND", unit.CardNo, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(mana - unit.ManaCost + 1, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task FirstPermanentUsesAreIndependentForEachBattlefield()
    {
        var state = Put(Position(), "OTHER", "RAD·163/167", "BATTLEFIELD", "OTHER");
        state = Put(state, "OTHER-ANCHOR", "OGN·219/298", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("SECOND", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·219/298", out var unit));
        Assert.Equal(30 - 2 * unit.ManaCost + 2, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryRejectsMissingAndForgedBattlefieldPlayUses(bool forge)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        var uses = game.State.UntilEndOfTurnEffects.Where(e => e.StartsWith("BATTLEFIELD_PERMANENT_PLAY_THIS_TURN:")).ToArray();
        var actual = Assert.Single(uses);
        var changed = game.State with { UntilEndOfTurnEffects = forge
            ? game.State.UntilEndOfTurnEffects.Append(actual.Replace("P1", "P2", StringComparison.Ordinal)).ToArray()
            : game.State.UntilEndOfTurnEffects.Where(e => e != actual).ToArray() };
        var commands = game.Commands();
        var errors = MatchRecoveryValidator.Validate(changed.RoomId, commands[^1].CompletedEventSequence,
            commands, game.Events(), new Dictionary<string, RecoveredPlayerView>(), changed, changed.Tick);
        Assert.Contains("battlefield permanent play uses differ from accepted play history", errors);
    }

    private static MatchState Position(string battlefieldCardNo = "RAD·163/167")
    {
        var state = new MatchState("RAD-FIRST-PERMANENT", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty, ["P2"] = PlayerZones.Empty },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) }, seed: 564);
        state = Put(state, "BF", battlefieldCardNo, "BATTLEFIELD", "BF");
        state = Put(state, "ANCHOR", "OGN·219/298", "BATTLEFIELD", "BF");
        state = Put(state, "FIRST", "OGN·219/298", "HAND");
        return Put(state, "SECOND", "OGN·219/298", "HAND");
    }

    private static MatchState Put(MatchState state, string id, string no, string zone, string? field = null)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") with { ControllerId = "P1" } } };
        return RadUnitEffectTestFixture.Place(state, id, "P1", zone, field);
    }
}
