using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class StunObserverTests
{
    private static MatchState Position(bool legend = false, bool watcher = false, bool alreadyStunned = false)
    {
        var state = VenUnitEntryBasicTests.Position("OGN·051/298");
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        if (legend) {
            cards["LEGEND"] = new("LEGEND", cardNo: "OGN·261/298", ownerId: "P1", controllerId: "P1", tags: ["传奇"]);
            zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"] }; locations["LEGEND"] = new("P1", "LEGEND");
        }
        if (watcher) cards["ALLY"] = cards["ALLY"] with { CardNo = "OGN·059/298", Power = 7, IsExhausted = true };
        if (alreadyStunned) cards["ENEMY"] = cards["ENEMY"] with { UntilEndOfTurnEffects = ["STUNNED"] };
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    private static async Task ResolveStun(VenBasicFlowSpellTests.Driver game, string target = "ENEMY")
    {
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·051/298", [target], Destination: "BASE"));
        await game.Top();
    }

    [Fact]
    public async Task LeonaChoosesTheBoonTargetAfterAnActualEnemyStunAndAllowsResponses()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position(legend: true));
        await ResolveStun(game);
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount); Assert.Contains("ALLY", choice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY", choice.LegalObjectIds); Assert.DoesNotContain("GEAR", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.NotNull(game.State.PriorityPlayerId);
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task EclipseVanguardReadiesOnlyAfterItsOwnIndependentTriggerResolves()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position(watcher: true));
        await ResolveStun(game);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        Assert.Equal("ENEMY_STUNNED_SOURCE_READY_POWER", Assert.Single(game.State.StackItems).FieldContext!.Kind);
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted); Assert.Equal(8, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FriendlyOrAlreadyStunnedTargetsDoNotEarnEnemyStunTriggers(bool alreadyStunned)
    {
        var state = Position(legend: true, watcher: true, alreadyStunned: alreadyStunned);
        var game = new VenBasicFlowSpellTests.Driver(state);
        await ResolveStun(game, alreadyStunned ? "ENEMY" : "ALLY");
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task MultipleObserversAreOrderedAndResolveIndependently()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position(legend: true, watcher: true));
        await ResolveStun(game);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var ids = game.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Top(); await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task IllegalBoonTargetDoesNotChangeThePendingTrigger()
    {
        var played = await Act(Position(legend: true), "P1", new PlayCardCommand("UNIT", "OGN·051/298", ["ENEMY"], Destination: "BASE"));
        var resolved = await Top(played.State); var choice = resolved.State.PendingCardChoice!;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(resolved.State, new("illegal", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(resolved.State), MatchStateHasher.Hash(rejected.State));
        ArmamentMightTests.PublicRecovery(resolved.State);
    }
    [Fact]
    public async Task AnEnemyEntryStunUsesTheVexControllersLegendAndOpensASeparateTrigger()
    {
        var state = OfficialUnitEntryTriggerTests.Position();
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEONA-P2"] = new("LEONA-P2", cardNo: "OGN·261/298", ownerId: "P2", controllerId: "P2", tags: ["传奇"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { LegendZone = ["LEONA-P2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEONA-P2"] = new("P2", "LEGEND") } };
        var entered = await Act(state, "P1", new PlayCardCommand("C", "OGN·219/298", []));
        var stunned = await Top(entered.State);
        Assert.Contains("STUNNED", stunned.State.CardObjects["C"].UntilEndOfTurnEffects);
        var choice = Assert.IsType<PendingCardChoiceState>(stunned.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId); Assert.Contains("ALLY", choice.LegalObjectIds);
        var selected = await Act(stunned.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.DoesNotContain(CardObjectTags.Boon, selected.State.CardObjects["ALLY"].Tags);
        var done = await Top(selected.State);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ALLY"].Tags);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AlreadyStunnedEntryStillGetsTheMovementRestrictionWithoutASecondStunEvent()
    {
        var entered = await Act(OfficialUnitEntryTriggerTests.Position(), "P1", new PlayCardCommand("C", "OGN·219/298", []));
        var state = entered.State with { CardObjects = new Dictionary<string, CardObjectState>(entered.State.CardObjects) {
            ["C"] = entered.State.CardObjects["C"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        var done = await Top(state);
        Assert.False(MovementRestrictionRules.CanMove(done.State.CardObjects["C"], "P1"));
        Assert.DoesNotContain(done.Events, e => e.Kind == "STATUS_EFFECT_APPLIED" && Equals(e.Payload.GetValueOrDefault("effectId"), "STUNNED"));
        Assert.Contains(done.Events, e => e.Kind == "MOVEMENT_RESTRICTION_APPLIED");
    }

    [Fact]
    public async Task AReturnedWatcherCannotAffectItsNewHandIncarnationOrLeakItsIdentity()
    {
        var state = AttackDamageDistributionTests.Spell(Position(watcher: true), "RETURN", "OGN·104/298", "P1");
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "OGN·051/298", ["ENEMY"], Destination: "BASE"));
        var stunned = await Top(played.State);
        var response = await Act(stunned.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        var returned = await Top(response.State);
        Assert.Contains("ALLY", returned.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"ALLY\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        ArmamentMightTests.PublicRecovery(returned.State);
        var done = await Top(returned.State);
        Assert.DoesNotContain("\"ALLY\"", JsonSerializer.Serialize(done.Events));
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_READIED" or "POWER_MODIFIED_UNTIL_END_OF_TURN");
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ACounterRequiringAnEnemySkillToTargetItsOwnFriendIsIllegalHere()
    {
        var state = AttackDamageDistributionTests.Spell(Position(legend: true), "COUNTER", "SFD·045/221", "P2");
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "OGN·051/298", ["ENEMY"], Destination: "BASE"));
        var stunned = await Top(played.State); var choice = stunned.State.PendingCardChoice!;
        var selected = await Act(stunned.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var target = Assert.Single(selected.State.StackItems).StackItemId;
        var passed = await Act(selected.State, "P1", new PassPriorityCommand());
        var command = new PlayCardCommand("COUNTER", "SFD·045/221", [target]);
        var rejected = await new CoreRuleEngine().ResolveAsync(passed.State, new("invalid-counter", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(passed.State), MatchStateHasher.Hash(rejected.State));
        var done = await Act(passed.State, "P2", new PassPriorityCommand());
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ALLY"].Tags);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ReturningTheChosenFriendInResponsePreventsTheBoon()
    {
        var state = AttackDamageDistributionTests.Spell(Position(legend: true), "RETURN", "OGN·169/298", "P2");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { Power = 2 } } };
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "OGN·051/298", ["ENEMY"], Destination: "BASE"));
        var stunned = await Top(played.State); var choice = stunned.State.PendingCardChoice!;
        var selected = await Act(stunned.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var passed = await Act(selected.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RETURN", "OGN·169/298", ["ALLY"]));
        var returned = await Top(response.State);
        Assert.Contains("ALLY", returned.State.PlayerZones["P1"].Hand);
        var done = await Top(returned.State);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain("\"ALLY\"", JsonSerializer.Serialize(done.Events));
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("actor")] [InlineData("generation")] [InlineData("card")]
    public async Task RecoveryRejectsForgedStunObservers(string change)
    {
        var played = await Act(Position(watcher: true), "P1", new PlayCardCommand("UNIT", "OGN·051/298", ["ENEMY"], Destination: "BASE"));
        var state = (await Top(played.State)).State; var item = Assert.Single(state.StackItems);
        var field = item.FieldContext!; var observer = field.StunObserver!;
        var forged = change switch {
            "actor" => observer with { Actor = "P2" },
            "generation" => observer with { Source = observer.Source with { Generation = observer.Source.Generation + 1 } },
            _ => observer with { StunnedCardNo = "FORGED" } };
        state = state with { StackItems = [item with { FieldContext = field with { StunObserver = forged } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state));
    }

}
