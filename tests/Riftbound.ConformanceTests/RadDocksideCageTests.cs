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
