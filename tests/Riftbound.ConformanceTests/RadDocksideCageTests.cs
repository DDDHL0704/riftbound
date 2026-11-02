using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadDocksideCageTests
{
    private const string Cage = "RAD·136/167";

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ExactPrintingExilesHereThenPaysSelfDestructionBeforePlayingTheOwnedCardForFree(string owner)
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Cage);
        Assert.Equal(39642, printed.Id); Assert.Equal(6, printed.Energy); Assert.Equal(2, printed.ReturnEnergy);
        var game = await Capture(owner);
        var group = Assert.Single(game.State.LinkedExiles);
        Assert.Equal(owner, group.UnitEventOrigin!.TargetOwnerId);
        Assert.Contains("PRISONER", game.State.PlayerZones[owner].Banished);
        Assert.Equal("P1", group.ControllerId);
        await Hold(game);
        Assert.Contains("CARD", game.State.PendingCardChoice!.LegalObjectIds);
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Battlefields);
        await Choose(game, ["CARD"]);
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.NotNull(Assert.Single(game.State.StackItems).TriggerCost);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        Assert.Equal("P1", game.State.PendingEffectPlay.PlayerId);
        Assert.Equal(owner, game.State.PendingEffectPlay.SourcePlayerId);
        var pool = game.State.RunePools["P1"];
        await Reject(game.State, "P2", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: "BASE"));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("PRISONER", game.State.PlayerZones[owner].Banished);
        Assert.Equal(owner, game.State.CardObjects["PRISONER"].OwnerId);
        Assert.Equal("P1", game.State.CardObjects["PRISONER"].ControllerId);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:missing")]
    public async Task ADeploymentRejectsInvalidDestinationsWithoutSpending(string destination)
        => await Reject(Position(), "P1", new PlayCardCommand("CARD", Cage, [], Destination: destination));

    [Fact]
    public async Task EntryTargetsOnlyUnitsAtTheSourcesCurrentBattlefield()
    {
        var state = Put(Position(), "ELSEWHERE", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(new[] { "ALLY", "PRISONER" }, game.State.PendingCardChoice!.LegalObjectIds);
        foreach (var invalid in new[] { "ENEMY", "ELSEWHERE", "CARD" })
            await Reject(game.State, "P1", Command(game.State, [invalid]));
        await Choose(game, ["PRISONER"]); await game.Top();
        Assert.Single(game.State.LinkedExiles);
        await game.Replay();
    }

    [Fact]
    public async Task ATokenDisappearsAndCannotBecomeALinkedCard()
    {
        var state = Position();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        var token = factory.CreateObject("PRISONER", "P1", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["PRISONER"] = token } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        await Choose(game, ["PRISONER"]); await game.Top();
        Assert.DoesNotContain("PRISONER", game.State.CardObjects.Keys);
        Assert.Empty(game.State.LinkedExiles);
        await Hold(game); await Choose(game, ["CARD"]); await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("target-generation")] [InlineData("source-departure")]
    public async Task EntryRechecksTargetGenerationAndTheSourcesCurrentHere(string branch)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        await Choose(game, ["PRISONER"]);
        var state = game.State;
        if (branch == "target-generation") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PRISONER"] = state.CardObjects["PRISONER"] with { ObjectGeneration = state.CardObjects["PRISONER"].ObjectGeneration + 2 } } };
        else {
            state = RadUnitEffectTestFixture.Place(state, "CARD", "P1", "GRAVEYARD");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["CARD"] = state.CardObjects["CARD"] with { ObjectGeneration = state.CardObjects["CARD"].ObjectGeneration + 1 } } };
        }
        var result = await OfficialGraveyardRecastTests.Top(state);
        Assert.Empty(result.State.LinkedExiles);
        Assert.Contains("PRISONER", result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(result.State));
    }

    [Theory]
    [InlineData("missing-origin")] [InlineData("target")]
    [InlineData("source-generation")] [InlineData("future-tick")]
    public async Task RestoreRejectsForgedLinkedExileReceipts(string branch)
    {
        var game = await Capture(); var group = Assert.Single(game.State.LinkedExiles); var origin = group.UnitEventOrigin!;
        var forged = branch switch {
            "missing-origin" => group with { UnitEventOrigin = null },
            "target" => group with { Objects = new Dictionary<string, long> { ["ALLY"] = 1 } },
            "source-generation" => group with { SourceGeneration = group.SourceGeneration + 1 },
            _ => group with { UnitEventOrigin = origin with { CreatedTick = game.State.Tick + 1 } }
        };
        Assert.Contains("invalid linked exile ledger", OfficialInsightAndSpellLockTests.Errors(game.State with { LinkedExiles = [forged] }));
        await game.Replay();
    }

    [Theory]
    [InlineData("all-costs")] [InlineData("owner")]
    [InlineData("source-generation")] [InlineData("receipt")]
    public async Task RestoreRejectsForgedFreePlayContinuations(string branch)
    {
        var game = await Capture("P2"); await Hold(game); await Choose(game, ["CARD"]); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        var forged = branch switch {
            "all-costs" => pending with { IgnoreAllCosts = false },
            "owner" => pending with { SourcePlayerId = "P1" },
            "source-generation" => pending with { Sources = new Dictionary<string, long> { ["PRISONER"] = 99 } },
            _ => pending with { Parent = pending.Parent with { TriggerCost = null } }
        };
        Assert.Contains("invalid banished unit effect-play continuation", OfficialInsightAndSpellLockTests.Errors(
            game.State with { PendingEffectPlay = forged }));
        await game.Replay();
    }

    [Fact]
    public async Task AcceptedHistoryAuthenticatesTheLinkAndRejectsDeletionOrRebinding()
    {
        var game = await Capture();
        var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(game.State))!;
        var commands = JsonSerializer.Deserialize<RecoveredCommand[]>(JsonSerializer.Serialize(game.Commands()))!;
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events()))!;
        Assert.Empty(Errors(state, events));
        Assert.Contains("linked exile ledger differs from accepted banishment history", Errors(state with { LinkedExiles = [] }, events));
        var group = Assert.Single(state.LinkedExiles);
        Assert.Contains("linked exile ledger differs from accepted banishment history", Errors(state with {
            LinkedExiles = [group with { ControllerId = "P2" }] }, events));
        Assert.Contains("linked exile ledger differs from accepted banishment history", Errors(state,
            events.Where(e => e.Event.Kind != "LINKED_EXILE_CREATED").ToArray()));
        await game.Replay();

        IReadOnlyList<string> Errors(MatchState current, IReadOnlyList<RecoveredEvent> history)
            => MatchRecoveryValidator.Validate(current.RoomId, commands[^1].CompletedEventSequence, commands, history,
                new Dictionary<string, RecoveredPlayerView>(), current, current.Tick);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AnActuallyAnimatedEquipmentIsExiledAndReplayedAsItsPrintedEquipment(string owner)
    {
        var state = WithOrianna(Put(Position(), "PRISONER", "OGN·077/298", "P1", "BASE"));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PRISONER"] = state.CardObjects["PRISONER"] with { OwnerId = owner } } };
        var game = new Driver(state);
        await Animate(game, "PRISONER");
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["PRISONER"].Tags);
        await game.Act("P1", new MoveUnitCommand("PRISONER", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        await Choose(game, ["PRISONER"]); await game.Top();
        Assert.Contains("PRISONER", game.State.PlayerZones[owner].Banished);
        Assert.Null(game.State.CardObjects["PRISONER"].LegendEquipmentUnit);
        Assert.DoesNotContain(CardObjectTags.UnitCard, game.State.CardObjects["PRISONER"].Tags);
        await Hold(game); await Choose(game, ["CARD"]); await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        var pool = game.State.RunePools["P1"];
        await Reject(game.State, "P1", new PlayCardCommand("PRISONER", "OGN·077/298", [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("PRISONER", "OGN·077/298", [], Destination: "BASE"));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Base);
        Assert.Equal(owner, game.State.CardObjects["PRISONER"].OwnerId);
        Assert.Equal("P1", game.State.CardObjects["PRISONER"].ControllerId);
        Assert.DoesNotContain(CardObjectTags.UnitCard, game.State.CardObjects["PRISONER"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task AReplacementOfTheAnimatedSourcesDestructionStillPaysForItsLinkedPlay()
    {
        var game = new Driver(Put(WithOrianna(Position()), "GLASS", "OGN·077/298", "P1", "BASE"));
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        await Choose(game, ["PRISONER"]); await game.Top();
        await Animate(game, "CARD");
        await Hold(game); await Choose(game, ["CARD"]);
        if (game.State.PendingRuleChoice is { } pending)
        {
            var replacement = Assert.Single(pending.Request.Options, option => option.Id.StartsWith("GEAR:", StringComparison.Ordinal)
                && option.ObjectIds?.Contains("GLASS") == true);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [replacement.Id]));
        }
        Assert.Contains("GLASS", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["CARD"].IsExhausted);
        Assert.NotNull(Assert.Single(game.State.StackItems).TriggerCost);
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: "BASE"));
        Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsActualHoldDestroysTheDeploymentWithoutPlayingItsLinkedCard()
    {
        var game = await Capture();
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD", "BASE", []));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        // The move opens a real conquest duel. Both players close it before ending the turn.
        Assert.True(game.State.SpellDuelState.IsActive);
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        Assert.False(game.State.SpellDuelState.IsActive);
        await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.StackItems, item => item.HeldContext?.Kind == "PLAY_LINKED_EXILE");
        await game.Top();
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Banished);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Fact]
    public async Task MultipleActualCagesKeepTheirLinksAndSelfDestructionCostsIndependent()
    {
        var game = new Driver(Put(Put(Position(), "CAGE2", Cage, "P1", "HAND"), "PRISONER2", "OGN·219/298", "P1", "BATTLEFIELD", "BF"));
        foreach (var pair in new[] { (Source: "CARD", Target: "PRISONER"), (Source: "CAGE2", Target: "PRISONER2") })
        {
            await game.Act("P1", new PlayCardCommand(pair.Source, Cage, [], Destination: "BATTLEFIELD:BF"));
            await Choose(game, [pair.Target]); await game.Top();
        }
        Assert.Equal(2, game.State.LinkedExiles.Count);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        for (var count = 0; game.State.PendingCardChoice is not null; count++)
        {
            Assert.InRange(count, 0, 1);
            await Choose(game, [Assert.Single(game.State.PendingCardChoice.LegalObjectIds)]);
        }
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("CAGE2", game.State.PlayerZones["P1"].Graveyard);
        for (var count = 0; game.State.StackItems.Count > 0; count++)
        {
            Assert.InRange(count, 0, 1); await game.Top();
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            var target = Assert.Single(pending.Sources).Key;
            await game.Act("P1", new PlayCardCommand(target, "OGN·219/298", [], Destination: "BASE"));
        }
        Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Base);
        Assert.Contains("PRISONER2", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData("reentered")] [InlineData("controller")]
    public async Task DerivedSourceCheckpointsUseTheCurrentGenerationAndCapturedHoldController(string branch)
    {
        var captured = await Capture(); var state = captured.State;
        if (branch == "reentered")
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["CARD"] = state.CardObjects["CARD"] with { ObjectGeneration = state.CardObjects["CARD"].ObjectGeneration + 2 } } };
        else
        {
            state = RadUnitEffectTestFixture.Place(state, "ALLY", "P2", "BATTLEFIELD", "BF");
            state = RadUnitEffectTestFixture.Place(state, "CARD", "P2", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        }
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P1", new EndTurnCommand())).State;
        if (branch == "reentered") state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new EndTurnCommand())).State;
        var player = branch == "reentered" ? "P1" : "P2";
        Assert.Equal(player, state.TurnPlayerId);
        Assert.Equal("PLAY_LINKED_EXILE", Assert.Single(state.StackItems).HeldContext!.Kind);
        state = (await OfficialInsightAndSpellLockTests.Act(state, player, Command(state, ["CARD"]))).State;
        state = (await OfficialGraveyardRecastTests.Top(state)).State;
        if (branch == "reentered") Assert.Null(state.PendingEffectPlay);
        else { Assert.NotNull(state.PendingEffectPlay); Assert.Equal("P2", state.PendingEffectPlay.PlayerId); }
        OfficialInsightAndSpellLockTests.Restore(state);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AnActuallyCopiedHoldCannotBorrowThePrintedInstancesExiledCard(bool copiedFirst)
    {
        var game = new Driver(Put(WithOrianna(Position()), "SONG", "SFD·059/221", "P1", "BASE"));
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        await Choose(game, ["PRISONER"]); await game.Top();
        var originalLink = Assert.Single(game.State.LinkedExiles);
        Assert.StartsWith("PRINTED:", originalLink.UnitEventOrigin!.InstanceId);
        await Animate(game, "CARD");
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "CARD", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.Equal(Cage, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Single(game.State.TriggerQueue, item => item.HeldContext!.Origin is not null);
        Assert.Equal(2, game.State.TriggerQueue.Select(item => item.HeldContext!.RuleTextInstanceId).Distinct().Count());
        // Ordering pushes in reverse; shared trigger costs are confirmed from the bottom of the stack.
        var ordered = game.State.TriggerQueue.OrderBy(item => (item.HeldContext!.Origin is not null) == copiedFirst ? 1 : 0).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered.Select(item => item.TriggerId).ToArray()));
        var paying = Assert.Single(game.State.StackItems, item => item.StackItemId == game.State.PendingCardChoice!.ResolvingStackItemId);
        Assert.Equal(copiedFirst, paying.HeldContext!.Origin is not null);
        await Choose(game, ["CARD"]);
        Assert.Contains("CARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.State.StackItems);
        await game.Top();
        if (copiedFirst)
        {
            Assert.Null(game.State.PendingEffectPlay);
            Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Banished);
        }
        else
        {
            Assert.NotNull(game.State.PendingEffectPlay);
            Assert.Equal(originalLink.UnitEventOrigin!.InstanceId, game.State.PendingEffectPlay.Parent.HeldContext!.RuleTextInstanceId);
            await game.Act("P1", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: "BASE"));
            Assert.Contains("PRISONER", game.State.PlayerZones["P1"].Base);
        }
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    private static MatchState WithOrianna(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: "RAD·145/167", ownerId: "P1", controllerId: "P1", tags: ["传奇"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };

    private static async Task Animate(Driver game, string id)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.OriannaLegendAbilityId, [id], ["SPEND_MANA:1"]));
        await game.Top();
    }

    private static MatchState Position(string owner = "P1")
    {
        var state = Put(Create(), "CARD", Cage, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "PRISONER", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PRISONER"] = state.CardObjects["PRISONER"] with { OwnerId = owner } } };
    }

    private static async Task<Driver> Capture(string owner = "P1")
    {
        var game = new Driver(Position(owner));
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(24, game.State.RunePools["P1"].Mana);
        await Choose(game, ["PRISONER"]); await game.Top();
        Assert.Single(game.State.LinkedExiles);
        return game;
    }

    private static async Task Hold(Driver game)
    {
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Equal("PLAY_LINKED_EXILE", Assert.Single(game.State.StackItems).HeldContext!.Kind);
    }

    private static ChooseCardsCommand Command(MatchState state, IReadOnlyList<string> selected)
        => new(state.PendingCardChoice!.ChoiceId, state.PendingCardChoice.ChoiceWindow, selected);
    private static async Task Choose(Driver game, IReadOnlyList<string> selected)
        => await game.Act(game.State.PendingCardChoice!.PlayerId, Command(game.State, selected));
}
