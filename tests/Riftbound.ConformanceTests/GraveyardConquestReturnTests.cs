using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GraveyardConquestReturnTests
{
    private const string Rocket = "OGN·252/298";

    private static MatchState Position(string player = "P1", string zone = "GRAVEYARD", int hand = 2, int copies = 1)
    {
        var state = RadGearTestPosition.Create("OGN·296/298") with {
            ActivePlayerId = player, TurnPlayerId = player, FocusPlayerId = null };
        for (var i = 0; i < copies; i++)
            state = RadGearTestPosition.Put(state, "ROCKET" + i, Rocket, player, zone);
        for (var i = 0; i < hand; i++)
            state = RadGearTestPosition.Put(state, "COST" + i, "OGN·004/298", player, "HAND");
        return state;
    }

    private static async Task Conquer(Driver game, string player = "P1")
    {
        var target = player == "P1" ? "OTHER" : "BF";
        await game.Act(player, new MoveUnitCommand(player == "P1" ? "ALLY" : "ENEMY", "BASE", "BATTLEFIELD:" + target, []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(player == "P1" ? "P2" : "P1", new PassFocusCommand());
        if (game.State.TriggerQueue.Count > 0 && game.State.PendingCardChoice is null)
            await game.Act(player, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task Pay(Driver game, string player = "P1", string? card = "COST0")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(player, choice.PlayerId);
        await game.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, card is null ? [] : [card]));
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TheGraveyardOwnerDiscardsAtConfirmationAndReturnsTheOriginalCardAfterResponses(string player)
    {
        var initial = Position(player); var game = new Driver(initial);
        await Conquer(game, player);
        Assert.Contains("ROCKET0", game.State.PlayerZones[player].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DISCARDED");
        var skill = Assert.Single(game.State.StackItems, item => item.UnitConquestReturn is not null);
        Assert.Equal(TriggerKinds.ConquestDiscardReturnSelfFromGraveyard, skill.EffectKind);
        Assert.Equal("GRAVEYARD", skill.UnitConquestReturn!.OriginZone);
        Assert.Null(skill.TriggerCost);
        await Pay(game, player);
        Assert.Contains("COST0", game.State.PlayerZones[player].Graveyard);
        Assert.Contains("ROCKET0", game.State.PlayerZones[player].Graveyard);
        Assert.Equal(0, game.State.CardObjects["ROCKET0"].ObjectGeneration);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.EndsWith("EXHAUSTED", StringComparison.Ordinal));
        var paid = Assert.Single(game.State.StackItems, item => item.UnitConquestReturn is not null);
        Assert.Equal(new ObjectBinding("COST0", 0), paid.TriggerCost!.Discarded);
        Assert.Equal(new ObjectBinding("ROCKET0", 0), paid.TriggerCost.Source);
        var validFrame = Frame(initial, game, game.State);
        Assert.Empty(validFrame.ValidationErrors);
        var loaded = MatchSession.Restore(validFrame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(game.State)[player]),
            MatchStateHasher.HashValue(loaded.SnapshotFor(player)));
        await game.Top();
        Assert.Contains("ROCKET0", game.State.PlayerZones[player].Hand);
        Assert.Equal(1, game.State.CardObjects["ROCKET0"].ObjectGeneration);
        Assert.Contains(game.Events(), e => e.Event.Kind == "CARD_RETURNED_TO_HAND"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ROCKET0"));
        await game.Replay();
    }

    [Fact]
    public async Task DecliningTheDiscardKeepsBothTheHandAndGraveyardUnchanged()
    {
        var game = new Driver(Position()); await Conquer(game); await Pay(game, card: null);
        Assert.Contains("ROCKET0", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "COST0", "COST1" }, game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_DISCARDED" or "CARD_RETURNED_TO_HAND");
        await game.Replay();
    }

    [Fact]
    public async Task NoHandCardCannotConfirmTheOptionalReturn()
    {
        var game = new Driver(Position(hand: 0)); await Conquer(game);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains("ROCKET0", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_DISCARDED" or "CARD_RETURNED_TO_HAND");
        await game.Replay();
    }

    [Theory]
    [InlineData("HAND")] [InlineData("MAIN_DECK")] [InlineData("BANISHED")]
    public async Task TheOfficial385Point2ConditionDoesNotObserveConquestFromAnotherZone(string zone)
    {
        var game = new Driver(Position(zone: zone)); await Conquer(game);
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitConquestReturn is not null);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), TriggerKinds.ConquestDiscardReturnSelfFromGraveyard));
        await game.Replay();
    }

    [Fact]
    public async Task ARealRecycleResponseCannotReturnTheSourcesNewDeckIncarnation()
    {
        var initial = RadGearTestPosition.Put(Position(), "RESPONSE", "UNL-103/219", "P2", "HAND");
        var game = new Driver(initial); await Conquer(game); await Pay(game);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "UNL-103/219", ["ROCKET0"], Mode: "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        await game.Top();
        Assert.Contains("ROCKET0", game.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("COST0", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.Contains("ROCKET0", game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("ROCKET0", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_RETURNED_TO_HAND"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ROCKET0"));
        await game.Replay();
    }

    [Fact]
    public async Task TheOpponentAndAnUnrelatedOrStaleHandChoiceCannotPayTheCost()
    {
        var game = new Driver(Position()); await Conquer(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        foreach (var (player, id, card) in new[] { ("P2", choice.ChoiceId, "COST0"), ("P1", choice.ChoiceId, "ROCKET0"), ("P1", "old-choice", "COST0") })
            await RadGearTestPosition.Reject(game.State, player, new ChooseCardsCommand(id, choice.ChoiceWindow, [card]));
        await Pay(game); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsConquestDoesNotObserveYourGraveyardCard()
    {
        var game = new Driver(Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" });
        await Conquer(game, "P2");
        Assert.Equal(1, game.State.PlayerScores["P2"]);
        Assert.Contains("ROCKET0", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), TriggerKinds.ConquestDiscardReturnSelfFromGraveyard));
        await game.Replay();
    }

    [Fact]
    public async Task TwoGraveyardCopiesEarnIndependentSkillsAndPaySeparateHandCards()
    {
        var game = new Driver(Position(copies: 2)); await Conquer(game);
        Assert.Equal(2, game.State.StackItems.Count(item => item.UnitConquestReturn is not null));
        await Pay(game); await Pay(game, card: "COST1");
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(2, game.State.StackItems.Count(item => item.TriggerCost?.Discarded is not null));
        await game.Top(); await game.Top();
        Assert.Equal(new[] { "ROCKET0", "ROCKET1" }, game.State.PlayerZones["P1"].Hand.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "COST0", "COST1" }, game.State.PlayerZones["P1"].Graveyard.Order(StringComparer.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task HoldingAnArenaDoesNotCountAsActuallyConqueringForAGraveyardSpell()
    {
        var state = RadGearTestPosition.Put(Position(), "BF", "OGN·286/298", "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state with { ActivePlayerId = "P2", TurnPlayerId = "P2" });
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Contains("ROCKET0", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), TriggerKinds.ConquestDiscardReturnSelfFromGraveyard));
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryBindsTheOriginalGraveyardGenerationToAcceptedConquestHistory()
    {
        var baseline = Position();
        var initial = baseline with { CardObjects = new Dictionary<string, CardObjectState>(baseline.CardObjects) {
            ["ROCKET0"] = baseline.CardObjects["ROCKET0"] with { ObjectGeneration = 3 } } };
        var game = new Driver(initial); await Conquer(game);
        var skill = Assert.Single(game.State.StackItems, item => item.UnitConquestReturn is not null);
        var captured = skill.UnitConquestReturn!;
        Assert.Equal(new ObjectBinding("ROCKET0", 3), captured.Source);
        var forged = game.State with { StackItems = game.State.StackItems.Select(item => item.StackItemId == skill.StackItemId
            ? item with { UnitConquestReturn = captured with { Source = new("ROCKET0", 2) } } : item).ToArray() };
        var frame = Frame(initial, game, forged);
        Assert.Contains(frame.ValidationErrors, error => error.Contains("accepted conquest history", StringComparison.Ordinal));
        await AssertForgedStoreCannotRestore(frame, "accepted conquest history");
    }

    [Fact]
    public async Task APaidDiscardReceiptWithoutTheAcceptedDiscardCommandCannotRestore()
    {
        var initial = Position(); var game = new Driver(initial); await Conquer(game);
        var choice = game.State.PendingCardChoice!;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["COST0"]);
        var simulated = await new CoreRuleEngine().ResolveAsync(game.State,
            new("not-journalled-discard", "P1", command.CmdType), command, default);
        Assert.True(simulated.Accepted, simulated.ErrorMessage);
        Assert.NotNull(Assert.Single(simulated.State.StackItems).TriggerCost);
        var frame = Frame(initial, game, simulated.State with { Tick = game.State.Tick });
        Assert.Contains(frame.ValidationErrors, error => error.Contains("discard receipt", StringComparison.Ordinal)
            && error.Contains("accepted payment history", StringComparison.Ordinal));
        await AssertForgedStoreCannotRestore(frame, "accepted payment history");
    }

    private static async Task AssertForgedStoreCannotRestore(MatchRecoveryFrame frame, string reason)
    {
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var persisted = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame with { ValidationErrors = [] }, json), json)!;
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new RecoveryStore(persisted));
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(frame.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(frame.RoomId, default));
    }

    private sealed class RecoveryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Driver game, MatchState state)
    {
        var sequence = game.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) {
            OpeningSeed = initial.Seed };
    }
}
