using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ReversalShardStartDeathTests
{
    private const string Shard = "UNL-174/219";
    private static MatchState Position(string field = "UNL-209/219")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(field), "SHARD", Shard, "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return state with { Phase = MatchPhases.TurnStart, TurnStartStep = "START",
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
    }
    private static async Task LabSacrifice(Driver game)
    {
        await game.Act("P1", new PassPriorityCommand()); var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
    }
    private static async Task UntilDestructionChoice(Driver game)
    {
        for (var count = 0; game.State.PendingRuleChoice is null; count++)
        {
            Assert.True(count < 12); Assert.True(game.State.StackItems.Count + game.State.TriggerQueue.Count > 0);
            if (game.State.TriggerQueue.Count > 0 && game.State.PriorityPlayerId is null)
                await game.Act(game.State.ActivePlayerId, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
            else await game.Top();
        }
        Assert.Equal("EACH_PLAYER_DESTROY_OWN_UNIT", game.State.PendingRuleChoice.Request.Kind);
    }
    private static async Task Drain(Driver game)
    {
        for (var count = 0; game.State.StackItems.Count + game.State.TriggerQueue.Count > 0; count++)
        {
            Assert.True(count < 12);
            if (game.State.TriggerQueue.Count > 0 && game.State.PriorityPlayerId is null)
                await game.Act(game.State.ActivePlayerId, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
            else await game.Top();
        }
    }
    private static int Earned(Driver game) => game.Events().Count(entry => entry.Event.Kind == "TRIGGER_QUEUED"
        && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "SHARD"));

    [Theory, InlineData(false), InlineData(true)]
    public async Task ARealStartPhaseDestructionCostEarnsOneOpponentInstructionForControlledUnitsIncludingTokens(bool token)
    {
        var state = Position();
        if (token)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo, out var factory));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = factory.CreateObject("ALLY", "P2", "P1") } };
        }
        var game = new Driver(state); await LabSacrifice(game); Assert.Equal(1, Earned(game));
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await UntilDestructionChoice(game); await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY"); await Drain(game);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); Assert.Equal(1, Earned(game));
        Assert.Equal(MatchPhases.Main, game.State.Phase); await game.Replay();
    }

    [Fact]
    public async Task ARecallReplacementPreventsTheQualifyingDeath()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"] } } };
        var game = new Driver(state); await LabSacrifice(game); await Drain(game);
        Assert.Equal(0, Earned(game)); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base); await game.Replay();
    }

    [Fact]
    public async Task AnEnemyDeathDuringYourStartPhaseDoesNotQualify()
    {
        var state = RadUnitEffectTestFixture.Place(Position("UNL-212/219"), "ALLY", "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Damage = state.CardObjects["ENEMY"].Power - 1 } } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand()); await Drain(game);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); Assert.Equal(0, Earned(game)); await game.Replay();
    }

    [Fact]
    public async Task ARealMainPhaseFriendlyDestructionDoesNotQualify()
    {
        var state = RadGearTestPosition.Put(Position(), "KILL", "OGN·229/298", "P1", "HAND") with {
            Phase = MatchPhases.Main, TurnStartStep = null, TimingState = TimingStates.NeutralOpen };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(0, Earned(game)); await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TwoSeparateEphemeralDestructionsInTheSameStartPhaseStillEarnOnlyOneInstruction(bool reverse)
    {
        var state = Position("OGN·290/298");
        state = RadGearTestPosition.Put(state, "SECOND", "OGN·219/298", "P1", "BASE");
        var cards = state.CardObjects.ToDictionary();
        foreach (var id in new[] { "ALLY", "SECOND" }) cards[id] = cards[id] with { Tags = cards[id].Tags.Append(CardObjectTags.Ephemeral).ToArray() };
        var game = new Driver(state with { CardObjects = cards }); await game.Act("P1", new PassPriorityCommand());
        var ids = game.State.TriggerQueue.Select(item => item.TriggerId).ToArray(); Assert.Equal(2, ids.Length);
        if (reverse) Array.Reverse(ids); await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await UntilDestructionChoice(game); await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY"); await Drain(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("SECOND", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, Earned(game)); await game.Replay();
    }

    [Theory, InlineData(0), InlineData(1)]
    public async Task SimultaneousFirstDeathsLetTheControllerSelectOneOccurrenceAndTriggerOnlyOnce(int selected)
    {
        var state = RadGearTestPosition.Put(Position("UNL-212/219"), "SECOND", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        var cards = state.CardObjects.ToDictionary();
        foreach (var id in new[] { "ALLY", "SECOND" }) cards[id] = cards[id] with { Damage = cards[id].Power - 1 };
        var game = new Driver(state with { CardObjects = cards }); await game.Act("P1", new PassPriorityCommand()); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P1", choice.Request.PlayerId); Assert.Equal("FIRST_DEATH_OCCURRENCE", choice.Request.Kind); Assert.Equal(2, choice.Request.Options.Count);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [choice.Request.Options[selected].Id]));
        Assert.Equal(1, Earned(game)); await UntilDestructionChoice(game);
        await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY"); await Drain(game); Assert.Equal(1, Earned(game)); await game.Replay();
    }

    [Theory, InlineData("occurrence"), InlineData("turn"), InlineData("player"), InlineData("origin"), InlineData("erase")]
    public async Task RecoveryRejectsChangingOrErasingTheOriginalStartPhaseDeathCapture(string mode)
    {
        var state = Position("UNL-212/219"); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { Damage = state.CardObjects["ALLY"].Power - 1 } } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand()); await game.Top();
        var item = Assert.Single(game.State.StackItems); var origin = item.UnitEvent!.StartPhaseUnitDeath!;
        var forged = game.State with { StackItems = mode == "erase" ? [] : [item with { UnitEvent = item.UnitEvent with {
            StartPhaseUnitDeath = mode == "origin" ? null : mode == "turn" ? origin with { TurnNumber = origin.TurnNumber + 1 }
                : mode == "player" ? origin with { TurnPlayerId = "P2" } : origin with { OccurrenceHash = new string('0', 64) } } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }
}
