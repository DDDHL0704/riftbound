using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadScrutinyLensTests
{
    private const string Lens = "RAD·064/167";
    private const string Skill = "RAD_064_PAY_ONE_EXHAUST_GUESS_DECK_TOP";

    [Fact]
    public async Task PrintedEquipmentPaysTwoManaAndOffersAnOpponentTargetWithoutRevealingAnything()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Lens);
        Assert.Equal(39561, card.Id); Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy);
        var state = Put(Create(), "GEAR", Lens, "P1", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 0) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("GEAR", Lens, []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(Skill, out var ability));
        Assert.Equal(1, ability.ManaCost); Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.SimpleEffect!.OpponentGuessesDeckTopType); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298", "UNIT", false)] [InlineData("OGN·219/298", "SPELL", true)]
    [InlineData("OGN·040/298", "EQUIPMENT", false)] [InlineData("OGN·040/298", "UNIT", true)]
    [InlineData("OGN·104/298", "SPELL", false)] [InlineData("OGN·104/298", "EQUIPMENT", true)]
    public async Task TheOpponentGuessesBeforeRevealingAndOnlyAMissDrawsTheOriginalCard(string top, string guess, bool draws)
    {
        var game = new Driver(Position(top));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"]));
        Assert.Equal(29, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        var item = Assert.Single(game.State.StackItems); Assert.Equal(new[] { "P2" }, item.TargetObjectIds);
        Assert.Null(item.TargetGenerations); Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        await game.Top(); AssertHiddenTop(game.State); await Guess(game, guess);
        Assert.Equal(draws, game.State.PlayerZones["P1"].Hand.Contains("P1-D0"));
        if (!draws) Assert.Equal("P1-D0", game.State.PlayerZones["P1"].MainDeck[^1]);
        Assert.DoesNotContain("P1-D1", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_TYPE_GUESSED");
        Assert.Single(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        await game.Replay();
    }

    [Fact]
    public async Task AnEmptyDeckStillAllowsTheGuessButNeverDrawsOrBurnsOut()
    {
        var state = Position();
        foreach (var id in state.PlayerZones["P1"].MainDeck.ToArray())
            state = RadUnitEffectTestFixture.Place(state, id, "P1", "GRAVEYARD");
        var game = new Driver(state); await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"]));
        await game.Top(); await Guess(game, "UNIT");
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "BURNOUT_APPLIED" or "MAIN_DECK_CARDS_REVEALED");
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "BASE")] [InlineData("P2", "BASE")]
    public async Task TheCurrentControllerCanActivateAFieldSourceEvenWhenItHasAnotherOwner(string owner, string zone)
    {
        var state = Put(Create(), "GEAR", Lens, owner, "BASE");
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        var game = new Driver(state); Assert.Contains(Skill, JsonSerializer.Serialize(game.PromptFor("P1")));
        await Reject(game.State, "P2", new ActivateAbilityCommand("GEAR", Skill, ["P1"]));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"]));
        await game.Top(); await Guess(game, "SPELL");
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.PlayerZones["P2"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("self")] [InlineData("object")] [InlineData("duplicate")]
    public Task IllegalPlayerTargetsNeverPayOrExhaustTheSource(string fault)
    {
        string[] targets = fault switch { "missing" => Array.Empty<string>(), "self" => ["P1"],
            "object" => ["ENEMY"], _ => new[] { "P2", "P2" } };
        return Reject(Position(), "P1", new ActivateAbilityCommand("GEAR", Skill, targets));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MissingManaOrAnAlreadyExhaustedSourceCannotPayTheSkill(bool exhausted)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = exhausted } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(exhausted ? 30 : 0, 0) } };
        return Reject(state, "P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"]));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task RevealedNocturneMayBeBanishedWithoutDrawingOrRecyclingTheNextCard(bool matched, bool banish)
    {
        var game = new Driver(Position(DeckObservationPlayTests.Nocturne));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"])); await game.Top();
        await Guess(game, matched ? "UNIT" : "SPELL");
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", pending.Request.Kind); Assert.Equal("P1", pending.Request.PlayerId);
        foreach (var snapshot in Views(game.State))
        {
            var json = JsonSerializer.Serialize(snapshot);
            Assert.Contains("P1-D0", json); Assert.DoesNotContain("P1-D1", json);
        }
        await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [banish ? "BANISH" : "KEEP"]));
        if (banish)
        {
            var play = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            await game.Act("P1", new ChooseCardsCommand(play.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished);
        }
        Assert.Equal(!matched && !banish, game.State.PlayerZones["P1"].Hand.Contains("P1-D0"));
        Assert.DoesNotContain("P1-D1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("P1-D1", game.State.PlayerZones["P1"].MainDeck[0]); await game.Replay();
    }

    [Fact]
    public async Task OnlyTheSelectedOpponentMayAnswerAndRecoveryRejectsAnAlteredChoice()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"])); await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(new[] { "UNIT", "EQUIPMENT", "SPELL" }, pending.Request.Options.Select(o => o.Id));
        await Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SPELL"]));
        await Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["LEGEND"]));
        await Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SPELL", "UNIT"]));
        var forged = pending with { Request = pending.Request with { PlayerId = "P1" } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingRuleChoice = forged }));
        AssertHiddenTop(game.State); await Guess(game, "SPELL"); await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("tick")] [InlineData("player")]
    public async Task RecoveryRejectsForgedCapturedPlayerTargetAndSource(string fault)
    {
        var game = new Driver(Position()); await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, ["P2"]));
        var item = Assert.Single(game.State.StackItems); var context = item.SimpleActivation!;
        var altered = fault == "player" ? item with { TargetObjectIds = ["P1"] }
            : item with { SimpleActivation = fault == "tick" ? context with { AcquiredTick = game.State.Tick + 1 }
                : context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [altered] }));
    }

    private static IEnumerable<SnapshotDto> Views(MatchState state)
        => ResolutionResult.BuildSnapshots(state).Values.Append(ResolutionResult.BuildSpectatorSnapshot(state));

    private static void AssertHiddenTop(MatchState state)
    {
        foreach (var snapshot in Views(state))
        {
            var json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("P1-D0", json); Assert.DoesNotContain("P1-D1", json);
        }
    }

    private static Task Guess(Driver game, string type)
    {
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECK_TOP_TYPE_GUESS", pending.Request.Kind); Assert.Equal("P2", pending.Request.PlayerId);
        return game.Act("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [type]));
    }

    private static MatchState Position(string top = "OGN·219/298")
    {
        var state = Put(Create(), "GEAR", Lens, "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P1-D0"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-D0", top, "P1") } };
    }
}
