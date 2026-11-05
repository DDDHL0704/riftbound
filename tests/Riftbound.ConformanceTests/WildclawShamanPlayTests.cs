using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class WildclawShamanPlayTests
{
    private const string Shaman = "OGN·147/298";

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task PaysDuringConfirmationThenGainsAPersistentBoonAndReadiesAfterResponses(string destination)
    {
        var game = await Open(Position(), destination);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        Assert.Null(game.State.PriorityPlayerId);
        await Pay(game, "ALLY");
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(item.TargetObjectIds);
        Assert.Equal(new ObjectBinding("ALLY", game.State.CardObjects["ALLY"].ObjectGeneration), item.TriggerCost!.ConsumedBoon);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        Assert.NotNull(game.State.PriorityPlayerId);
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task DecliningKeepsTheBoonAndLeavesTheShamanExhausted()
    {
        var game = await Open(Position());
        await Pay(game, null);
        Assert.Empty(game.State.StackItems);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task EnemyAndEquipmentBoonsCannotConfirmTheAbility()
    {
        var initial = Boon(Boon(VenUnitEntryBasicTests.Position(Shaman), "ENEMY"), "GEAR");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Shaman, [], Destination: "BASE"));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        await game.Replay();
    }

    [Fact]
    public async Task IllegalPaymentsAndWrongPlayerAreRejectedWithoutMutation()
    {
        var game = await Open(Boon(Boon(Position(), "ENEMY"), "GEAR"));
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(new[] { "ALLY" }, choice.LegalObjectIds);
        foreach (var ids in new[] { new[] { "ENEMY" }, new[] { "GEAR" }, new[] { "UNIT" }, new[] { "ALLY", "ALLY" } })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await Pay(game, "ALLY");
        await game.Top();
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")]
    [InlineData("UNIT")]
    public async Task AResponseCanReturnThePaidUnitOrSourceWithoutRefundOrChangingItsNewGeneration(string returned)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var game = await Open(initial);
        await Pay(game, "ALLY");
        var generation = game.State.CardObjects[returned].ObjectGeneration;
        if (game.State.PriorityPlayerId != "P1") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(generation + 1, game.State.CardObjects[returned].ObjectGeneration);
        await game.Top();
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal(returned == "UNIT" ? 3 : 4, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(returned != "UNIT", game.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Boon));
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        await game.Replay();
    }

    [Fact]
    public async Task ASourceBoonAlreadyPresentAtConfirmationCanPayItsOwnCost()
    {
        // Boundary fixture: the source is already a field unit at confirmation.
        // Model an entry boon here, never an impossible boon on a hand card.
        var game = await Open(Position());
        var state = Boon(game.State, "UNIT");
        var choice = state.PendingCardChoice!;
        state = state with { PendingCardChoice = choice with { LegalObjectIds = ["ALLY", "UNIT"] } };
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("shaman-own-boon", "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var item = Assert.Single(result.State.StackItems);
        Assert.Equal(new ObjectBinding("UNIT", state.CardObjects["UNIT"].ObjectGeneration), item.TriggerCost!.ConsumedBoon);
        Assert.True(CoreRuleEngine.ValidTriggerCostReceipt(result.State, item));
        Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["UNIT"].Tags);
        Assert.Equal(3, result.State.CardObjects["UNIT"].Power);
        for (var pass = 0; pass < 2; pass++)
        {
            var priority = result.State.PriorityPlayerId!;
            result = await new CoreRuleEngine().ResolveAsync(result.State,
                new("shaman-own-boon-pass-" + pass, priority, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Equal(4, result.State.CardObjects["UNIT"].Power);
        Assert.Contains(CardObjectTags.Boon, result.State.CardObjects["UNIT"].Tags);
        Assert.False(result.State.CardObjects["UNIT"].IsExhausted);
    }

    [Fact]
    public async Task AnEarlierResolvedRallyBoonDoesNotStackButTheShamanStillReadies()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RALLY", TurnUnitPlayGrantsTests.Rally, "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RALLY", TurnUnitPlayGrantsTests.Rally, []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("UNIT", Shaman, [], Destination: "BASE"));
        var rally = Assert.Single(game.State.TriggerQueue, t => t.TurnUnitPlayBoon is not null);
        var shaman = Assert.Single(game.State.TriggerQueue, t => t.UnitEvent is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [rally.TriggerId, shaman.TriggerId]));
        await Pay(game, "ALLY");
        await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Single(game.State.CardObjects["UNIT"].Tags, tag => tag == CardObjectTags.Boon);
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_GRANTED");
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("rebound")]
    [InlineData("unjournalled")]
    public async Task RecoveryRejectsMissingOrForgedPaymentReceipts(string mutation)
    {
        var initial = Boon(Position(), "ENEMY");
        var game = await Open(initial);
        MatchState forged;
        if (mutation == "unjournalled")
        {
            var choice = game.State.PendingCardChoice!;
            var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]);
            var simulated = await new CoreRuleEngine().ResolveAsync(game.State,
                new("shaman-unjournalled", "P1", command.CmdType), command, default);
            Assert.True(simulated.Accepted, simulated.ErrorMessage);
            forged = simulated.State with { Tick = game.State.Tick, CardObjects = game.State.CardObjects };
        }
        else
        {
            await Pay(game, "ALLY");
            var item = Assert.Single(game.State.StackItems);
            var receipt = item.TriggerCost! with { ConsumedBoon = mutation == "missing" ? null
                : new ObjectBinding("ENEMY", game.State.CardObjects["ENEMY"].ObjectGeneration) };
            forged = game.State with { StackItems = [item with { TriggerCost = receipt }] };
        }
        var sequence = game.Events().Last().Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, forged.Tick, sequence,
            snapshots[p], forged.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, sequence, game.Commands(), game.Events(), views, forged, forged.Tick, spectator);
        Assert.Contains(errors, e => mutation == "missing" ? e.Contains("invalid trigger cost receipt", StringComparison.Ordinal)
            : e.Contains("boon cost", StringComparison.Ordinal) && e.Contains("payment history", StringComparison.Ordinal));
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, game.Commands(), game.Events(), views,
            errors, forged, initial, spectator) { OpeningSeed = initial.Seed };
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
    }

    [Fact]
    public async Task ConsumingASingleBoonCleansUpLethalDamageBeforeResponsesAndKeepsThePaidReceipt()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Damage = 4 } } };
        var game = await Open(initial);
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await Pay(game, "ALLY");
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new ObjectBinding("ALLY", generation), Assert.Single(game.State.StackItems).TriggerCost!.ConsumedBoon);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    private static MatchState Position() => Boon(VenUnitEntryBasicTests.Position(Shaman), "ALLY");
    private static MatchState Boon(MatchState state, string id) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { Power = state.CardObjects[id].Power + 1,
                Tags = [..state.CardObjects[id].Tags, CardObjectTags.Boon] } } };
    private static async Task<Driver> Open(MatchState initial, string destination = "BASE")
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Shaman, [], Destination: destination));
        Assert.Equal("TRIGGER_COST_CONFIRMATION", Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice).ChoiceWindow);
        return game;
    }
    private static async Task Pay(Driver game, string? id)
    {
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, id is null ? [] : [id]));
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("shaman-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
