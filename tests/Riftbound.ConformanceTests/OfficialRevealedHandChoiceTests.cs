using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialRevealedHandChoiceTests
{
    [Fact]
    public async Task RecycleChoosesOnlyAfterResolutionAndCannotDeclineEligibleNonUnit()
    {
        var s = Position();
        await Reject(s, "P1", new PlayCardCommand("C", "OGN·156/298", ["SPELL"]));
        var cast = await Act(s, "P1", new PlayCardCommand("C", "OGN·156/298", []));
        Assert.Empty(OpponentHand(cast.State));
        var open = await Top(cast.State);
        var choice = open.State.PendingCardChoice!;
        Assert.Equal(new[] { "SPELL" }, choice.LegalObjectIds); Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(new[] { "SPELL", "U" }, OpponentHand(open.State));
        Assert.DoesNotContain("OGN·097/298", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(open.State)["P1"]));
        await Reject(open.State, "P2", Choose(open.State, "SPELL"));
        await Reject(open.State, "P1", Choose(open.State));
        await Reject(open.State, "P1", Choose(open.State, "U"));
        await Reject(open.State, "P1", new PassPriorityCommand());
        var done = await Select(open.State, "SPELL");
        Assert.Equal(new[] { "DECK", "SPELL" }, done.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(new[] { "U" }, done.State.PlayerZones["P2"].Hand);
        Assert.Empty(OpponentHand(done.State)); Assert.Empty(done.State.StackItems);
        Restore(open.State); Restore(done.State);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public async Task InvestigatorAlwaysRevealsThenOptionallyPaysExperienceAndOpponentDiscardsAndDraws(int experience, bool pay)
    {
        var s = Position(investigator: true, experience);
        var entered = await Act(s, "P1", new PlayCardCommand("C", "UNL-135/219", []));
        Assert.Contains("C", entered.State.PlayerZones["P1"].Base); Assert.Single(entered.State.StackItems);
        Assert.Empty(OpponentHand(entered.State)); Assert.Equal(experience, entered.State.PlayerExperience["P1"]);
        var open = await Top(entered.State);
        Assert.Equal(new[] { "SPELL", "U" }, OpponentHand(open.State));
        Assert.Equal(experience >= 2 ? 2 : 0, open.State.PendingCardChoice!.LegalObjectIds.Count);
        Assert.Equal(experience, open.State.PlayerExperience["P1"]);
        if (experience < 2) await Reject(open.State, "P1", Choose(open.State, "U"));
        var done = await Select(open.State, pay ? ["U"] : []);
        Assert.Equal(experience - (pay ? 2 : 0), done.State.PlayerExperience["P1"]);
        Assert.Equal(pay ? new[] { "SPELL", "DECK" } : new[] { "U", "SPELL" }, done.State.PlayerZones["P2"].Hand);
        Assert.Equal(pay ? new[] { "U" } : [], done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(pay ? 1 : 0, done.Events.Count(e => e.Kind == "CARD_DRAWN"));
        Assert.Empty(OpponentHand(done.State)); Assert.Empty(done.State.StackItems);
        Restore(open.State); Restore(done.State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EmptyOrIneligibleHandsStillRevealAndContinueWithoutInventingASelection(bool investigator, bool empty)
    {
        var s = Position(investigator, 2);
        s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = empty ? [] : ["U"] } } };
        var open = await Open(s, investigator);
        Assert.Contains(open.Events, e => e.Kind == "HAND_REVEALED");
        Assert.Empty(open.State.PendingCardChoice!.LegalObjectIds);
        var done = await Select(open.State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
    }

    [Fact]
    public async Task RepeatedRevealRecomputesTheRemainingHandWithoutExposingDecks()
    {
        var cast = await Act(Position(), "P1", new PlayCardCommand("C", "OGN·156/298", []));
        var s = cast.State with { StackItems = [cast.State.StackItems.Single() with { EffectRepeatCount = 2 }] };
        var first = await Top(s); var second = await Select(first.State, "SPELL");
        Assert.Equal(1, second.State.StackItems.Single().CompletedHandExecutions);
        Assert.Equal(new[] { "U" }, second.State.PendingCardChoice!.ContextObjectIds);
        Assert.Empty(second.State.PendingCardChoice.LegalObjectIds); Restore(second.State);
        var done = await Select(second.State); Assert.Empty(done.State.StackItems);
        Assert.Equal(new[] { "DECK", "SPELL" }, done.State.PlayerZones["P2"].MainDeck); Restore(done.State);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("generation")]
    [InlineData("legal")]
    [InlineData("optional")]
    [InlineData("actor")]
    [InlineData("index")]
    public async Task ForgedRecoveryCannotExpandRevealOrChoicePermissions(string mutation)
    {
        var opened = await Open(); var c = opened.State.PendingCardChoice!; var h = c.HandContext!;
        c = mutation switch {
            "owner" => c with { HandContext = h with { OwnerId = "P1" } },
            "generation" => c with { HandContext = h with { Cards = new Dictionary<string, long> { ["SPELL"] = 99, ["U"] = 0 } } },
            "legal" => c with { LegalObjectIds = ["SPELL", "U"] },
            "optional" => c with { RequiredCount = 0 },
            "actor" => c with { PlayerId = "P2" },
            _ => c with { HandContext = h with { ExecutionIndex = 7 } }
        };
        Assert.Contains("invalid revealed hand choice continuation", OfficialInsightAndSpellLockTests.Errors(opened.State with { PendingCardChoice = c }));
    }

    [Fact]
    public async Task PlayerAndSpectatorSnapshotsRecoverDuringRevealAndAfterItCloses()
    {
        var open = await Open(); var done = await Select(open.State, "SPELL");
        foreach (var state in new[] { open.State, done.State })
        {
            var snapshots=ResolutionResult.BuildSnapshots(state);var prompts=ResolutionResult.BuildPrompts(state);
            var views=state.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,state.Tick,0,snapshots[id],state.Tick,0,prompts[id]));
            var frame=MatchReplayRedactor.BuildSpectatorFrame(state.RoomId,state.Tick,0,[],state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, frame);
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
    }

    [Fact]
    public async Task CounteredRevealNeverDisclosesAnyHand()
    {
        var s = AddCounter(Position(), "OGN·064/298");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(10, 10) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = ["U", "SPELL", "COUNTER"] } } };
        var cast = await Act(s, "P1", new PlayCardCommand("C", "OGN·156/298", []));
        var pass = await Act(cast.State, "P1", new PassPriorityCommand());
        var counter = await Act(pass.State, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [cast.State.StackItems.Single().StackItemId]));
        var done = await Top(counter.State);
        Assert.DoesNotContain(done.Events, e => e.Kind == "HAND_REVEALED");
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice); Restore(done.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscardedChampionIsLegalAndEmptyDeckUsesNormalBurnout(bool winning)
    {
        var s = Position(true, 2);
        s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { MainDeck = [], Banished = ["DECK"] } },
            PlayerScores = new Dictionary<string, int>(s.PlayerScores) { ["P1"] = winning ? 7 : 0 },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["DECK"] = new("P2", "BANISHED") },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["U"] = s.CardObjects["U"] with { CardNo = "OGN·112/298" } } };
        var open = await Open(s, true); var done = await Select(open.State, "U");
        Assert.Contains(done.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(winning ? 8 : 1, done.State.PlayerScores["P1"]);
        Assert.Equal(winning ? MatchStatuses.Finished : MatchStatuses.InProgress, done.State.Status);
        Assert.Equal(winning ? "P1" : null, done.State.WinnerPlayerId);
        Assert.Null(done.State.PendingCardChoice); Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task RevealedSnapshotDoesNotTransmitOwnersHandArrangement()
    {
        var s = Position();
        var rearranged = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { Hand = s.PlayerZones["P2"].Hand.Reverse().ToArray() } } };
        var first = await Open(s); var second = await Open(rearranged);
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(first.State)["P1"]),
            JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(second.State)["P1"]));
    }

    internal static MatchState Position(bool investigator = false, int experience = 0)
    {
        var s = OfficialRevealedHandPlayTests.Position();
        return s with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = RunePool.Empty },
            PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 0 },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["C"] = s.CardObjects["C"] with {
                CardNo = investigator ? "UNL-135/219" : "OGN·156/298", Tags = [investigator ? CardObjectTags.UnitCard : CardObjectTags.SpellCard] } } };
    }
    internal static async Task<ResolutionResult> Open(MatchState? s = null, bool investigator = false)
    { var cast = await Act(s ?? Position(), "P1", new PlayCardCommand("C", investigator ? "UNL-135/219" : "OGN·156/298", [])); return await Top(cast.State); }
    internal static ChooseCardsCommand Choose(MatchState s, params string[] ids) => new(s.PendingCardChoice!.ChoiceId, "REVEALED_HAND_EFFECT", ids);
    internal static Task<ResolutionResult> Select(MatchState s, params string[] ids) => Act(s, "P1", Choose(s, ids));
    private static IReadOnlyList<string> OpponentHand(MatchState s) => ResolutionResult.BuildSnapshots(s)["P1"].Table!.Players.Single(p => p.PlayerId == "P2").Zones.Hand;
    private static async Task Reject(MatchState s, string actor, GameCommand cmd)
    { var r = await new CoreRuleEngine().ResolveAsync(s, new("invalid", actor, cmd.CmdType), cmd, default); Assert.False(r.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(r.State)); }
}
