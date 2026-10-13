using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HighwayRobberyCurrentTests
{
    internal const string Card = "OGN·033/298";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualResolutionWaitsForTheTargetsControllerToChooseDamageOrCasterDraw(bool draw)
    {
        var initial = Position();
        var driver = Driver(initial, draw ? "opponent-chooses-draw" : "opponent-chooses-damage");
        await driver.Act("P1", new PlayCardCommand("ROBBERY", Card, ["VICTIM"]));
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand);
        Assert.Contains("ROBBERY", driver.State.PlayerZones["P1"].Hand.Concat(driver.State.StackItems.Select(i => i.SourceObjectId)));
        var result = await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? ["ALLOW_CASTER_DRAW"] : []));
        Assert.Equal(draw ? 0 : 6, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(draw ? 2 : 0, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(result.Events, e => draw ? e.Kind == "DAMAGE_APPLIED" : e.Kind == "CARD_DRAWN");
        Assert.Contains("ROBBERY", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEchoRequestsIndependentChoicesAndDefersLethalCleanupUntilItsFinalInstruction(bool lethal)
    {
        var initial = Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        if (lethal) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = initial.CardObjects["VICTIM"] with { Power = 5 } } };
        var driver = Driver(initial, lethal ? "echo-lethal-cleanup" : "echo-independent-decisions");
        await driver.Act("P1", new PlayCardCommand("ROBBERY", Card, ["VICTIM"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["VICTIM"])]));
        await driver.Top();
        var first = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(0, Assert.Single(driver.State.StackItems).ResolutionDamageChoice!.ExecutionIndex);
        await driver.Act("P2", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []));
        Assert.Equal(6, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Contains("VICTIM", driver.State.PlayerZones["P2"].Base);
        var second = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.NotEqual(first.ChoiceId, second.ChoiceId);
        Assert.Equal(1, Assert.Single(driver.State.StackItems).ResolutionDamageChoice!.ExecutionIndex);
        await driver.Act("P2", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, ["ALLOW_CASTER_DRAW"]));
        Assert.Equal(2, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(lethal, driver.State.PlayerZones["P2"].Graveyard.Contains("VICTIM"));
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(1, driver.Events().Count(e => e.Event.Kind == "CARD_PLAYED"));
        Assert.Equal(1, driver.Events().Count(e => e.Event.Kind == "COST_PAID"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualReturnResponseSkipsDamageAndTheDependentOpponentChoice()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P2");
        var driver = Driver(initial, "invalid-target");
        await driver.Act("P1", new PlayCardCommand("ROBBERY", Card, ["VICTIM"]));
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["VICTIM"]));
        await driver.Top();
        var result = await driver.Top();
        Assert.Null(driver.State.PendingCardChoice);
        Assert.DoesNotContain(result.Events, e => e.Kind is "RESOLUTION_ALTERNATIVE_REQUESTED" or "DAMAGE_APPLIED" or "CARD_DRAWN");
        Assert.Contains("ROBBERY", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualCounterRemovesTheSpellBeforeItsOpponentDecision()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·064/298", "P2");
        var driver = Driver(initial, "counter-before-choice");
        await driver.Act("P1", new PlayCardCommand("ROBBERY", Card, ["VICTIM"]));
        var parent = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [parent.StackItemId]));
        await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Contains("ROBBERY", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DamagePreventionDoesNotRemoveTheOpponentsChoiceOrChangeTheDrawAlternative(bool draw)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = initial.CardObjects["VICTIM"] with { UntilEndOfTurnEffects = ["PREVENT_NEXT_DAMAGE_THIS_TURN"] } } };
        var driver = Driver(initial, draw ? "prevented-choose-draw" : "prevented-choose-damage"); await Open(driver);
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? ["ALLOW_CASTER_DRAW"] : []));
        Assert.Equal(0, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(draw ? 2 : 0, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(draw, driver.State.CardObjects["VICTIM"].UntilEndOfTurnEffects.Contains("PREVENT_NEXT_DAMAGE_THIS_TURN"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpellshieldIsPaidOnceAtDeclarationBeforeTheOpponentsResolutionChoice(bool draw)
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = initial.CardObjects["VICTIM"] with { Tags = initial.CardObjects["VICTIM"].Tags.Append(CardObjectTags.Spellshield).Order(StringComparer.Ordinal).ToArray() } } };
        var driver = Driver(initial, draw ? "spellshield-draw" : "spellshield-damage"); await Open(driver);
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? ["ALLOW_CASTER_DRAW"] : []));
        var cost = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(2, cost.Payload["mana"]); Assert.Equal(1, cost.Payload["spellshieldTaxPower"]);
        Assert.Equal(18, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(draw ? 0 : 6, driver.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(draw ? 2 : 0, driver.State.PlayerZones["P1"].Hand.Count);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("wrong-actor")]
    [InlineData("wrong-choice")]
    [InlineData("forged-option")]
    [InlineData("priority")]
    [InlineData("end-turn")]
    public async Task NoPlayerCanBypassOrForgeTheActiveResolutionDecision(string attempt)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Open(driver);
        var state = driver.State; var choice = state.PendingCardChoice!;
        GameCommand command = attempt switch {
            "priority" => new PassPriorityCommand(), "end-turn" => new EndTurnCommand(),
            _ => new ChooseCardsCommand(attempt == "wrong-choice" ? "FORGED" : choice.ChoiceId,
                choice.ChoiceWindow, [attempt == "forged-option" ? "FORGED" : "ALLOW_CASTER_DRAW"]) };
        var actor = attempt is "wrong-actor" or "end-turn" ? "P1" : "P2";
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged", actor, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ADeclarationCannotSelectTheRetiredCasterControlledDrawMode()
    {
        var initial = Position(); var command = new PlayCardCommand("ROBBERY", Card, ["VICTIM"], Mode: "TARGET_CONTROLLER_CHOOSES_DRAW_2");
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", new("quote", prompt.PromptId!, initial.Tick, command));
        Assert.False(quote.IsValid);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("retired-mode", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("source-generation")]
    [InlineData("target")]
    [InlineData("target-generation")]
    [InlineData("card")]
    [InlineData("actor")]
    [InlineData("execution")]
    [InlineData("chosen")]
    [InlineData("missing")]
    [InlineData("choice-actor")]
    [InlineData("choice-options")]
    public async Task RecoveryRejectsIndependentCapturedResolutionDecisionMutations(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Open(driver);
        var state = driver.State; var parent = Assert.Single(state.StackItems); var c = parent.ResolutionDamageChoice!;
        var bad = mutation switch {
            "source" => c with { Source = c.Source with { ObjectId = "HOST" } },
            "source-generation" => c with { Source = c.Source with { Generation = 999999 } },
            "target" => c with { Target = c.Target with { ObjectId = "HOST" } },
            "target-generation" => c with { Target = c.Target with { Generation = 999999 } },
            "card" => c with { CardNo = "OGN·173/298" }, "actor" => c with { TargetControllerId = "P1" },
            "execution" => c with { ExecutionIndex = 999999 }, "chosen" => c with { DrawInstead = true },
            "missing" => null, _ => c };
        var corrupted = state with { StackItems = [parent with { ResolutionDamageChoice = bad }],
            PendingCardChoice = mutation == "choice-actor" ? state.PendingCardChoice! with { PlayerId = "P1" }
                : mutation == "choice-options" ? state.PendingCardChoice! with { LegalObjectIds = ["FORGED"] } : state.PendingCardChoice };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(corrupted), e => e.Contains("damage alternative choice"));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("execution")]
    [InlineData("chosen")]
    [InlineData("missing")]
    public async Task PublicAndSpectatorRecoveryRejectForgedResolutionDecisionProjections(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Open(driver); var state = driver.State;
        var c = Assert.Single(state.StackItems).ResolutionDamageChoice!;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            var old = row["resolutionDamageChoice"];
            row["resolutionDamageChoice"] = mutation switch {
                "actor" => c with { TargetControllerId = "P1" }, "source" => c with { Source = new("HOST", 0) },
                "target" => c with { Target = new("HOST", 0) }, "execution" => c with { ExecutionIndex = 999999 },
                "chosen" => c with { DrawInstead = true }, _ => null };
            Assert.NotEmpty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
            row["resolutionDamageChoice"] = old;
        }
    }

    internal static async Task Open(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new PlayCardCommand("ROBBERY", Card, ["VICTIM"])); await driver.Top();
    }

    internal static MatchState Position()
    {
        var s = AttackDamageDistributionTests.Spell(SacredProtectorTests.Position(), "ROBBERY", Card, "P1");
        s = SacredProtectorTests.Unit(s, "VICTIM", "P2");
        s = LocalDestructionRecallTests.At(s, "VICTIM", "P2", "BASE");
        return s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20,20), ["P2"] = new(20,20) },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["VICTIM"] = s.CardObjects["VICTIM"] with { Power = 10 } } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_ROBBERY_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
