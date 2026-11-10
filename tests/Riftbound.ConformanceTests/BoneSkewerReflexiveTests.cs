using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BoneSkewerReflexiveTests
{
    private const string Spell = "UNL-139/219";
    private static MatchState Position(string unit = "OGN·208/298")
    {
        var state = OfficialRevealedHandPlayTests.Position(unit);
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "COUNTER", "SFD·045/221", "P2", "HAND");
        return state with { Tick = 0, RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
    }

    private static async Task PlaySelectedUnit(Driver game, string unit = "OGN·208/298")
    {
        await game.Act("P1", new PlayCardCommand("C", Spell, ["BF"])); await game.Top();
        var permission = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        await game.Act("P1", new ChooseCardsCommand(permission.ChoiceId, "REVEALED_HAND_PLAY", ["U"]));
        Assert.Equal("P2", game.State.PendingEffectPlay!.PlayerId);
        await game.Act("P2", new PlayCardCommand("U", unit, [], Destination: "BATTLEFIELD:BF"));
    }

    [Theory]
    [InlineData("OGN·208/298")] [InlineData("SFD·075/221")] [InlineData("SFD·071/221")]
    public async Task ActualOpponentPlayEarnsAnIndependentUnselectedStunAfterEntry(string unit)
    {
        var game = new Driver(Position(unit)); await PlaySelectedUnit(game, unit);
        Assert.Null(game.State.PendingEffectPlay); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal("BF", game.State.ObjectLocations["U"].BattlefieldObjectId);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId); Assert.Equal(Spell, skill.CardNo);
        Assert.NotNull(skill.UnitEvent!.SpellInstruction!.PlayedUnit);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(skill.CardNo, out var behavior));
        Assert.Equal(CoreRuleEngine.PlayedUnitStatusReflexiveEffect, behavior.UnitEventAbility!.Effect);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.Equal(new ObjectBinding("U", game.State.CardObjects["U"].ObjectGeneration),
            skill.UnitEvent.SpellInstruction!.PlayedUnit!.Binding);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "STATUS_EFFECT_APPLIED");
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["BF"].UntilEndOfTurnEffects);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "STATUS_EFFECT_APPLIED"); await game.Replay();
    }

    [Fact]
    public async Task ARealEntryReplacementStillStunsTheUnitAtItsActualNewBattlefield()
    {
        var game = new Driver(Position("UNL-147/219")); await PlaySelectedUnit(game, "UNL-147/219");
        var nest = Assert.Single(game.State.CardObjects.Values, card =>
            card.TokenFactoryCardNo == P6TokenFactoryCatalog.BaronNestTokenCardNo);
        Assert.NotEqual("BF", nest.ObjectId); Assert.Equal(nest.ObjectId, game.State.ObjectLocations["U"].BattlefieldObjectId);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task ARealReturnResponseDoesNotApplyThePendingStunToTheHandCard()
    {
        var game = new Driver(Position()); await PlaySelectedUnit(game);
        Assert.Equal("P1", game.State.PriorityPlayerId); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["U"])); await game.Top();
        Assert.Contains("U", game.State.PlayerZones["P2"].Hand); await game.Top();
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "STATUS_EFFECT_APPLIED"); await game.Replay();
    }

    [Fact]
    public async Task AConditionalTargetCounterCannotSelectTheUnselectedReferencedUnitAbility()
    {
        var game = new Driver(Position()); await PlaySelectedUnit(game);
        var skill = Assert.Single(game.State.StackItems); await game.Act("P1", new PassPriorityCommand());
        await game.ActRetained(new("P2", "invalid-target-counter", JsonSerializer.SerializeToElement(
            new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId]), new JsonSerializerOptions(JsonSerializerDefaults.Web))), false);
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Contains("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "STACK_ITEM_COUNTERED"); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheOpponentsActualPlaySkillSharesTheApnapOrderingWindowWithTheEarnedStun(bool moveInResponse)
    {
        const string swap = "UNL-083/219";
        var initial = Position("SFD·062/221");
        var responseRunes = Enumerable.Range(0, 4).Select(i => "RESPONSE-RUNE-" + i).ToArray();
        if (moveInResponse)
        {
            initial = RadGearTestPosition.Put(initial, "SWAP", swap, "P2", "HAND");
            initial = RadGearTestPosition.Put(initial, "REFLECTION", "UNL-081/219", "P2", "BATTLEFIELD", "BF2");
            Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-081/219", out var attendant));
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["REFLECTION"] = initial.CardObjects["REFLECTION"] with { Tags = initial.CardObjects["REFLECTION"].Tags
                    .Concat(attendant.SourceUnitTags!.Split('|')).Distinct(StringComparer.Ordinal).ToArray() } },
                TurnPlayerId = "P2", ActivePlayerId = "P2" };
            foreach (var id in responseRunes)
                initial = RadGearTestPosition.Put(initial, id, "UNL-R05", "P1", "BASE");
            for (var i = 0; i < 4; i++)
                initial = RadGearTestPosition.Put(initial, "RESPONSE-DRAW-" + i, "OGN·219/298", "P1", "MAIN_DECK");
        }
        var game = new Driver(initial);
        if (moveInResponse)
        {
            // CN309.1.a and811: a real previous-turn standby supplies Reaction;
            // a Swift card cannot respond to either already confirmed skill.
            Assert.Contains(CardObjectTags.Ephemeral, game.State.CardObjects["REFLECTION"].Tags);
            await game.Act("P2", new HideCardCommand("SWAP", swap, "BATTLEFIELD:BF2", ["STANDBY_A"]));
            await game.Act("P2", new EndTurnCommand());
            Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, game.State.Phase);
            await game.TapRunes("P1", responseRunes);
            foreach (var id in responseRunes.Take(2)) await game.Act("P1", new RecycleRuneCommand(id));
        }
        await game.Act("P1", new PlayCardCommand("C", Spell, ["BF"])); await game.Top();
        await game.Act("P1", new ChooseCardsCommand(game.State.PendingEffectPlay!.ChoiceId, "REVEALED_HAND_PLAY", ["U"]));
        await game.Act("P2", new PlayCardCommand("U", "SFD·062/221", ["ALLY"], Destination: "BATTLEFIELD:BF"));
        while (ResolutionResult.HasOpenOrderTriggersWindow(game.State))
            await game.Act(ResolutionResult.OrderTriggersOrderingPlayerId(game.State)!,
                new OrderTriggersCommand(OrderedTriggerIds: CoreRuleEngine.TriggerResolutionOrderIds(game.State)));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal("P2", game.State.StackItems[^1].ControllerId);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_ORDERING_DEFERRED");
        if (moveInResponse)
        {
            var generation = game.State.CardObjects["U"].ObjectGeneration;
            // CN811.1.d.2: first select the unit at the standby battlefield.
            await game.Act("P2", new RevealCardCommand("SWAP", swap, ["REFLECTION", "U"],
                "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
            await game.Top();
            Assert.Equal("BF2", game.State.ObjectLocations["U"].BattlefieldObjectId);
            Assert.Equal("BF", game.State.ObjectLocations["REFLECTION"].BattlefieldObjectId);
            Assert.Equal(generation, game.State.CardObjects["U"].ObjectGeneration);
        }
        await game.ColdCheckpoint(); await game.Top();
        Assert.Equal(moveInResponse ? "BF2" : "BF", game.State.ObjectLocations["U"].BattlefieldObjectId);
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        await game.ColdCheckpoint(); await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.Equal(moveInResponse ? "BF2" : "BF", game.State.ObjectLocations["U"].BattlefieldObjectId); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DecliningOrHavingNoUnitInTheRevealedHandCreatesNoStun(bool noUnit)
    {
        var state = noUnit ? RadUnitEffectTestFixture.Place(Position(), "U", "P2", "GRAVEYARD") : Position();
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("C", Spell, ["BF"])); await game.Top();
        if (game.State.PendingEffectPlay is { } permission)
        {
            if (noUnit) Assert.Empty(permission.Sources);
            await game.Act("P1", new ChooseCardsCommand(permission.ChoiceId, "REVEALED_HAND_PLAY", []));
        }
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "STATUS_EFFECT_APPLIED" or "UNIT_PLAYED_TO_BATTLEFIELD");
        await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("generation")] [InlineData("card")] [InlineData("face")]
    [InlineData("empty-id")] [InlineData("null-binding")]
    public async Task OriginalAcceptedHistoryRejectsErasingOrForgingThePlayedUnitReference(string mutation)
    {
        var game = new Driver(Position()); await PlaySelectedUnit(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var origin = context.SpellInstruction!; var unit = origin.PlayedUnit!;
        var changed = mutation switch {
            "generation" => unit with { Binding = unit.Binding with { Generation = unit.Binding.Generation + 1 } },
            "card" => unit with { CardNo = "OGN·219/298" },
            "face" => unit with { FaceRevision = -1 },
            "empty-id" => unit with { Binding = unit.Binding with { ObjectId = "" } },
            "null-binding" => unit with { Binding = null! },
            _ => unit
        };
        var forged = game.State with { StackItems = mutation == "erase" ? []
            : [item with { UnitEvent = context with { SpellInstruction = origin with { PlayedUnit = changed } } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }
}
