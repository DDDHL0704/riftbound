using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadRadiantAltarTests
{
    private const string Altar = "RAD·046/167";
    private const string Skill = "RAD_046_EXHAUST_DRAW_CALL_RUNE";

    [Fact]
    public async Task OfficialEquipmentPaysEightManaAndOneGreenWithoutAnExhaustionTriggerOnPlay()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Altar);
        Assert.Equal(39542, card.Id); Assert.Equal(8, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        var state = RadUnitEffectTestFixture.Place(Position(), "ALTAR", "P1", "HAND");
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(7, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } },
            "P1", new PlayCardCommand("ALTAR", Altar, []));
        await Reject(state, "P1", new PlayCardCommand("ALTAR", Altar, ["ALLY"]));
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("ALTAR", Altar, []));
        Assert.Equal(22, game.State.RunePools["P1"].Mana);
        Assert.Equal(state.RunePools["P1"].TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("ALTAR", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["ALTAR"].IsExhausted);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TheCurrentControllerConfirmsTheBuffBeforeTheDrawAndDormantRuneResolve(string owner)
    {
        var game = new Driver(Position(owner));
        var initialPower = game.State.CardObjects["ALLY"].Power;
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", Skill, []));
        Assert.True(game.State.CardObjects["ALTAR"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("RUNE-0", game.State.PlayerZones["P1"].Base);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        Assert.DoesNotContain("ALTAR", choice.LegalObjectIds);
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await ConfirmBuff(game);
        await game.Top();
        Assert.Equal(initialPower + 3, game.State.CardObjects["ALLY"].Power);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P2"].Hand);
        Assert.Contains("RUNE-0", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["RUNE-0"].IsExhausted);
        Assert.Equal(new[] { "RUNE-1", "RUNE-2" }, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        var kinds = game.Events().Select(e => e.Event.Kind).ToList();
        Assert.True(kinds.IndexOf("CARD_DRAWN") < kinds.IndexOf("RUNES_CALLED"));
        await Reject(game.State, "P1", new ActivateAbilityCommand("ALTAR", Skill, []));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(initialPower, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task NoFriendlyTargetOrAnEmptyRuneDeckDoesNotPreventTheEarnedDraw(int runes)
    {
        var state = RadUnitEffectTestFixture.Place(Position(runes: runes), "ALLY", "P1", "GRAVEYARD");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", Skill, []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.NotNull(Assert.Single(game.State.StackItems).SimpleActivation);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(runes, game.State.PlayerZones["P1"].Base.Count(id => id.StartsWith("RUNE-", StringComparison.Ordinal)));
        await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task RuneCallReplacementsStackAndAllCalledRunesEnterExhausted(int wyverns)
    {
        var state = Position();
        for (var i = 0; i < wyverns; i++) state = Put(state, "WYVERN-" + i, "RAD·092/167", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", Skill, []));
        await ConfirmBuff(game); await game.Top(); await game.Top();
        var called = game.State.PlayerZones["P1"].Base.Where(id => id.StartsWith("RUNE-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(wyverns + 1, called.Length);
        Assert.All(called, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Equal(3 - called.Length, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(called.Length, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED").Event.Payload["count"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("hand")] [InlineData("opponent")]
    [InlineData("wrong-turn")] [InlineData("invented-target")]
    public async Task IllegalActivationCannotPayExhaustOrEarnEitherSkill(string scenario)
    {
        var state = Position();
        if (scenario == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALTAR"] = state.CardObjects["ALTAR"] with { IsExhausted = true } } };
        if (scenario is "hand" or "opponent")
            state = RadUnitEffectTestFixture.Place(state, "ALTAR", scenario == "opponent" ? "P2" : "P1", scenario == "hand" ? "HAND" : "BASE");
        if (scenario == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await Reject(state, "P1", new ActivateAbilityCommand("ALTAR", Skill, scenario == "invented-target" ? ["ALLY"] : []));
    }

    [Theory]
    [InlineData("source")] [InlineData("ability")] [InlineData("tick")]
    public async Task RecoveryRejectsForgedActivationBindingsWhileTheExhaustionBuffIsPending(string mutation)
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", Skill, [])); await ConfirmBuff(game);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        var item = Assert.Single(game.State.StackItems, i => i.SimpleActivation is not null);
        var context = item.SimpleActivation!;
        var forged = mutation switch {
            "source" => context with { Source = new("ALLY", context.Source.Generation) },
            "ability" => context with { AbilityId = "BOOK_REMOVE_EMPOWER_DRAW" },
            _ => context with { AcquiredTick = context.AcquiredTick + 1 }
        };
        var corrupt = game.State with { StackItems = game.State.StackItems.Select(i => i.StackItemId == item.StackItemId
            ? i with { SimpleActivation = forged } : i).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(corrupt));
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")]
    public async Task DerivedDepartureCheckpointsKeepBothAlreadyEarnedSkills(string destination)
    {
        var game = new Driver(Position());
        var power = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", Skill, [])); await ConfirmBuff(game);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        var captured = game.State.StackItems.Where(item => item.UnitEvent is not null).ToArray();
        // Structural recovery and resolution only; the normal scenarios above
        // verify full original history, privacy, retries and cold recovery.
        var state = RadUnitEffectTestFixture.Place(game.State, "ALTAR", "P1", destination);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALTAR"] = state.CardObjects["ALTAR"] with { ObjectGeneration = state.CardObjects["ALTAR"].ObjectGeneration + 1 } } };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        for (var passes = 0; state.StackItems.Count > 0; passes++)
        {
            Assert.InRange(passes, 0, 5);
            var result = await new CoreRuleEngine().ResolveAsync(state, new("altar-checkpoint-" + passes,
                state.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            if (destination == "HAND" && state.CardObjects["ALLY"].Power != power)
            {
                Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(result));
                var witness = Assert.Single(captured);
                foreach (var forged in new[] {
                    witness.UnitEvent! with { SourceGeneration = state.CardObjects["ALTAR"].ObjectGeneration },
                    witness.UnitEvent! with { CapturedTick = state.Tick + 1 } })
                    Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(result,
                        [witness with { UnitEvent = forged }]));
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result, captured);
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        }
        Assert.Equal(power + 3, state.CardObjects["ALLY"].Power);
        Assert.Single(state.PlayerZones["P1"].Hand, id => id.StartsWith("P1-D", StringComparison.Ordinal));
        Assert.Contains("RUNE-0", state.PlayerZones["P1"].Base);
    }

    private static MatchState Position(string owner = "P1", int runes = 3)
    {
        var state = Put(Create(), "ALTAR", Altar, owner, "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ALTAR", "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 30, new Dictionary<string, int> { [RuneTrait.Green] = 10 }) } };
        for (var i = 0; i < runes; i++) state = Put(state, "RUNE-" + i, "OGN·042/298", "P1", "RUNE_DECK");
        return state;
    }

    private static Task ConfirmBuff(Driver game)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("BECAME_EXHAUSTED", Assert.Single(game.State.StackItems, i => i.UnitEvent is not null).UnitEvent!.Timing);
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
    }
}
