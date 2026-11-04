using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// RAD 139 / 140: a free replay still plays a unit. The next-unit grant belongs
// to its player, while its delayed destruction belongs to that played object.
public sealed class RadChronobreakNextUnitGrantTests
{
    private const string Chronobreak = "RAD·140/167", OrdinaryUnit = "OGN·219/298", NextUnit = "SFD·143/221";

    private static MatchState Position(bool borrowed = false, bool secondFriendly = false)
    {
        var state = VenUnitEntryBasicTests.Position(Chronobreak);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };
        state = HandDiscardObserverTests.Put(state, "NEXT", NextUnit, "P1", "HAND");
        state = AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·064/298", "P2");
        if (borrowed) state = LocalDestructionRecallTests.At(state, "ENEMY", "P1", "BASE");
        if (secondFriendly) state = HandDiscardObserverTests.Put(state, "SECOND", OrdinaryUnit, "P1", "BASE");
        return state;
    }

    private static async Task Activate(Driver game)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Top();
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
    }

    private static PlayCardCommand Cast(int count)
        => new("UNIT", Chronobreak, [], OptionalCosts: ["ADDITIONAL_POWER:" + count]);

    private static async Task SelectIfNeeded(Driver game, params string[] ids)
    {
        foreach (var id in ids)
        {
            var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [id]));
        }
    }

    private static async Task CloseFocus(Driver game)
    {
        for (var i = 0; game.State.FocusPlayerId is { } player; i++)
        {
            Assert.True(i < 4);
            Assert.Empty(game.State.StackItems);
            await game.Act(player, new PassFocusCommand());
        }
    }

    private static string[] Markers(MatchState state, string id)
        => state.CardObjects[id].UntilEndOfTurnEffects.Where(effect =>
            effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal)).ToArray();

    private static async Task ReplayPending(Driver game, string player, string id, string no = OrdinaryUnit)
    {
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(player, pending.PlayerId);
        Assert.Equal(id, Assert.Single(pending.Sources).Key);
        var resources = game.State.RunePools.ToDictionary(pair => pair.Key, pair => pair.Value);
        var command = new PlayCardCommand(id, no, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, player, PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act(player, command);
        Assert.All(resources, pair => Assert.Equal(pair.Value, game.State.RunePools[pair.Key]));
    }

    private static async Task EndAndDestroy(Driver game, string destroyed)
    {
        await CloseFocus(game);
        await game.Act("P1", new EndTurnCommand());
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(CoreRuleEngine.TimeAssassinEndDestructionKind, item.EndAbility!.Kind);
        Assert.Equal(destroyed, item.SourceObjectId);
        await game.Top();
        Assert.Contains(destroyed, game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), destroyed));
        await game.Replay();
    }

    [Fact]
    public async Task AFreeReplayConsumesTheNextUnitGrantAndItsNewObjectDiesAtTurnEnd()
    {
        var game = new Driver(Position());
        await Activate(game);
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Act("P1", Cast(1));
        await game.Top();
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        await ReplayPending(game, "P1", "ALLY");
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        var context = CoreRuleEngine.ReadTimeAssassinDestructionMarker(Assert.Single(Markers(game.State, "ALLY")))!;
        Assert.Equal(game.State.CardObjects["ALLY"].ObjectGeneration, context.Target.Generation);
        await EndAndDestroy(game, "ALLY");
    }

    [Fact]
    public async Task OnlyTheFirstUnitInTheChosenReplayOrderConsumesTheGrant()
    {
        var game = new Driver(Position(secondFriendly: true));
        await Activate(game);
        await game.Act("P1", Cast(2));
        await game.Top();
        await SelectIfNeeded(game, "SECOND");
        Assert.Equal(["SECOND", "ALLY"], game.State.StackItems.Select(item => item.SourceObjectId));
        await ReplayPending(game, "P1", "SECOND");
        Assert.Single(Markers(game.State, "SECOND"));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await ReplayPending(game, "P1", "ALLY");
        Assert.Empty(Markers(game.State, "ALLY"));
        await EndAndDestroy(game, "SECOND");
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task BanishmentAndReplayRemoveAnOldGenerationsScheduledDestruction()
    {
        var game = new Driver(Position());
        await Activate(game);
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, [], Destination: "BASE"));
        Assert.Single(Markers(game.State, "NEXT"));
        var old = game.State.CardObjects["NEXT"].ObjectGeneration;
        await game.Act("P1", Cast(1));
        await game.Top();
        await SelectIfNeeded(game, "NEXT");
        Assert.Empty(Markers(game.State, "NEXT"));
        await ReplayPending(game, "P1", "NEXT", NextUnit);
        Assert.True(game.State.CardObjects["NEXT"].ObjectGeneration > old);
        Assert.Empty(Markers(game.State, "NEXT"));
        await CloseFocus(game);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        Assert.Contains("NEXT", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED");
        await game.Replay();
    }

    [Fact]
    public async Task TheBorrowedOwnersReplayCannotConsumeTheCastersNextUnitGrant()
    {
        var game = new Driver(Position(borrowed: true));
        await Activate(game);
        await game.Act("P1", Cast(1));
        await game.Top();
        await SelectIfNeeded(game, "ENEMY");
        await ReplayPending(game, "P2", "ENEMY");
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Empty(Markers(game.State, "ENEMY"));
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await CloseFocus(game);
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, [], Destination: "BASE"));
        Assert.Equal(before.Mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Single(Markers(game.State, "NEXT"));
        await EndAndDestroy(game, "NEXT");
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CounteredChronobreakPreservesTheGrantForTheNextOrdinaryUnitPlay()
    {
        var game = new Driver(Position());
        await Activate(game);
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Act("P1", Cast(1));
        await game.Act("P1", new PassPriorityCommand());
        var spell = Assert.Single(game.State.StackItems);
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId]));
        await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(generation, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_BANISHED");
        await CloseFocus(game);
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, [], Destination: "BASE"));
        Assert.Single(Markers(game.State, "NEXT"));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await EndAndDestroy(game, "NEXT");
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
    }
}
