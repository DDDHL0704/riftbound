using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ShadowguardStartPhaseEntryTests
{
    private const string Guard = "UNL-037/219";
    private static MatchState Position(string field = "UNL-209/219")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(field), "GUARD", Guard, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < 4; i++) state = RadGearTestPosition.Put(state, "RUNE" + i, "UNL-R01", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GREEN", "UNL-R02", "P1", "BASE");
        return state with { Phase = MatchPhases.TurnStart, TurnStartStep = "START",
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
    }
    private static Task Choose(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }
    private static async Task LabStart(Driver game, bool accept = true)
    {
        await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("BF", game.State.PendingCardChoice!.SourceObjectId);
        await Choose(game, accept ? ["ALLY"] : []);
        if (accept) await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
    }
    private static async Task PlayFromHand(Driver game, string source = "GUARD")
    {
        string[] runes = ["RUNE0", "RUNE1", "RUNE2", "RUNE3"];
        await game.Act("P1", new TapRuneCommand(runes[0], runes));
        await game.Act("P1", new RecycleRuneCommand("GREEN"));
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new PlayCardCommand(source, Guard, []));
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        Assert.Equal(power - 1, game.State.RunePools["P1"].TotalPower);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task ARealFriendlyStartPhaseDestructionMakesThePaidEntryReadyRegardlessOfOwnerOrToken(
        bool borrowed, bool token)
    {
        var state = Position();
        if (token)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo, out var factory));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = factory.CreateObject("ALLY", "P1", "P1") } };
        }
        else if (borrowed) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { OwnerId = "P2" } } };
        var game = new Driver(state); await LabStart(game);
        Assert.Equal(new[] { "P1" }, game.State.DeathLedger.StartPhaseUnitControllers);
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        await PlayFromHand(game);
        Assert.False(game.State.CardObjects["GUARD"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["GUARD"].Power); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DecliningOrReplacingTheDestructionWithRecallDoesNotQualify(bool recall)
    {
        var state = Position();
        if (recall) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with {
                UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"] } } };
        var game = new Driver(state); await LabStart(game, recall);
        Assert.Null(game.State.DeathLedger.StartPhaseUnitControllers);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        await PlayFromHand(game); Assert.True(game.State.CardObjects["GUARD"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task ARealMainPhaseFriendlyDeathDoesNotEstablishTheStartPhaseCondition()
    {
        var state = RadGearTestPosition.Put(Position(), "KILL", "OGN·229/298", "P1", "HAND");
        state = state with { Phase = MatchPhases.Main, TurnStartStep = null, TimingState = TimingStates.NeutralOpen };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Null(game.State.DeathLedger.StartPhaseUnitControllers);
        await game.Act("P1", new PlayCardCommand("GUARD", Guard, []));
        Assert.True(game.State.CardObjects["GUARD"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task AnEnemyDeathDuringYourActualStartPhaseDoesNotQualify()
    {
        var state = Position("UNL-212/219");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Damage = state.CardObjects["ENEMY"].Power - 1 } } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand()); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Null(game.State.DeathLedger.StartPhaseUnitControllers);
        await PlayFromHand(game); Assert.True(game.State.CardObjects["GUARD"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task TheEstablishedConditionExpiresBeforeTheNextOwnTurn()
    {
        var state = RadGearTestPosition.Put(Position(), "SECOND", Guard, "P1", "HAND");
        var game = new Driver(state); await LabStart(game); await PlayFromHand(game);
        Assert.False(game.State.CardObjects["GUARD"].IsExhausted);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Null(game.State.DeathLedger.StartPhaseUnitControllers);
        await PlayFromHand(game, "SECOND"); Assert.True(game.State.CardObjects["SECOND"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task TheActualHoldGrantedGraveyardPlayUsesTheSameReadyEntryCondition()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "GUARD", "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "KEEPER", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "RITUAL", "SFD·150/221", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RITUAL"] = state.CardObjects["RITUAL"] with { AttachedToObjectId = "KEEPER" } } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        await Choose(game, "ALLY"); await game.Top();
        Assert.Equal("KEEPER", game.State.PendingCardChoice!.SourceObjectId);
        await Choose(game, "GUARD"); await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("GUARD", Guard, []));
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "GUARD"));
        Assert.Equal(4, JsonSerializer.SerializeToElement(paid.Event.Payload).GetProperty("mana").GetInt32());
        Assert.False(game.State.CardObjects["GUARD"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("player")] [InlineData("turn")]
    public async Task RecoveryRejectsErasingDuplicatingOrInventingTheOriginalPhaseQualifiedHistory(string change)
    {
        var game = new Driver(Position()); await LabStart(game);
        var ledger = game.State.DeathLedger;
        var forged = game.State with { DeathLedger = change switch {
            "erase" => ledger with { StartPhaseUnitControllers = null },
            "duplicate" => ledger with { StartPhaseUnitControllers = ["P1", "P1"] },
            "player" => ledger with { StartPhaseUnitControllers = ["P2"] },
            _ => ledger with { TurnNumber = ledger.TurnNumber - 1 } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }
}
