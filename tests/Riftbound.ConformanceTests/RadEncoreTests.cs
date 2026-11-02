using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEncoreTests
{
    private const string Card = "RAD·152/167";
    private static MatchState Position() => VenUnitEntryBasicTests.Position(Card);
    private static PlayCardCommand Cast(params SpellModeChoice[] choices)
        => new("UNIT", Card, [], ModeChoices: choices);
    private static int Minions(MatchState state) => state.CardObjects.Values.Count(c => c.TokenFactoryCardNo is not null);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task TokensAndUpToTwoMightTargetsPayOnceAndResolveInPrintedOrder(int count)
    {
        var initial = Position(); var game = new Driver(initial);
        var targets = new[] { "ALLY", "ENEMY" }.Take(count).ToArray();
        await game.Act("P1", Cast(new("MIGHT", targets), new("MINIONS", [])));
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(new[] { "MINIONS", "MIGHT" }, item.ModeExecutions!.Select(m => m.Mode));
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, Minions(game.State));
        await game.Top();
        Assert.Equal(2, Minions(game.State)); Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        foreach (var id in targets) Assert.Equal(initial.CardObjects[id].Power + 1, game.State.CardObjects[id].Power);
        Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID");
    }

    [Fact]
    public async Task DifferentModesMayTargetTheSameUnitAndDamagePrecedesMight()
    {
        var game = new Driver(Position()); var power = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", Cast(new("MIGHT", ["ALLY"]), new("DAMAGE", ["ALLY"])));
        Assert.Equal(new[] { "ALLY", "ALLY" }, Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(power + 1, game.State.CardObjects["ALLY"].Power);
    }

    [Fact]
    public async Task TokensPrecedePrivateLookAndSourceWaitsForTheDeckChoice()
    {
        var game = new Driver(Position());
        await game.Act("P1", Cast(new("LOOK", []), new("MINIONS", []))); await game.Top();
        Assert.Equal(2, Minions(game.State));
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("DECK_EFFECT", choice.ChoiceWindow); Assert.Equal(2, choice.ContextObjectIds.Count);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]));
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Empty(game.State.StackItems); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, Minions(game.State));
    }

    [Fact]
    public async Task ReturningTheOnlyTargetStillAllowsTheUntargetedMode()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(Position(), "RETURN", "OGN·104/298"));
        await game.Act("P1", Cast(new("DAMAGE", ["ALLY"]), new("MINIONS", [])));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, Minions(game.State)); Assert.Empty(game.State.StackItems);
    }

    [Fact]
    public async Task EchoUsesDifferentModesAndDoesNotRepeatTheFirstPair()
    {
        var initial = Position(); initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var game = new Driver(initial);
        var command = Cast(new("MINIONS", []), new("MIGHT", ["ALLY"])) with {
            OptionalCosts = ["ECHO"], RepeatChoices = [new("", [], ModeChoices: [new("DAMAGE", ["ENEMY"]), new("LOOK", [])])] };
        await game.Act("P1", command); Assert.Equal(4, Assert.Single(game.State.StackItems).ModeExecutions!.Count);
        await game.Top(); Assert.Equal(2, Minions(game.State)); Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]));
        Assert.Equal(2, Minions(game.State)); Assert.Empty(game.State.StackItems);
    }

    [Fact]
    public async Task TokenReplacementResumesTheModeBeforeOpeningThePrivateLook()
    {
        var initial = LastBreathDrawBoundaryTests.Add(Position(), "Z", "UNL-086/219", "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "Z", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", Cast(new("MINIONS", []), new("LOOK", []))); await game.Top();
        var replacement = game.State.PendingCardChoice!;
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", replacement.ChoiceWindow); Assert.Equal(0, Minions(game.State));
        await game.Act("P1", new ChooseCardsCommand(replacement.ChoiceId, replacement.ChoiceWindow, ["Z"]));
        // Each unrestricted token now chooses its play location after the
        // replacement, before this mode commits and the private look opens.
        for (var i = 0; i < 3; i++)
        {
            var location = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("TOKEN_PLAY_LOCATION", location.Request.Kind);
            Assert.Contains(location.Request.Options, option => option.Id == "BASE");
            Assert.Equal(0, Minions(game.State));
            await game.Act("P1", new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(3, Minions(game.State));
        var look = game.State.PendingCardChoice!; Assert.Equal("DECK_EFFECT", look.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(look.ChoiceId, look.ChoiceWindow, [look.LegalObjectIds[0]]));
        Assert.Equal(3, Minions(game.State)); Assert.Empty(game.State.StackItems);
    }

    [Fact]
    public async Task NewControllerMayRemakeBothModesButCannotConfirmDuplicateModes()
    {
        var initial = RebuttalRulesTests.Position(Card);
        var deck = new[] { "NEW-CONTROLLER-DECK-1", "NEW-CONTROLLER-DECK-2" };
        var cards = initial.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = initial.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        foreach (var card in deck)
        {
            cards[card] = new(card, cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
            locations[card] = new("P2", "MAIN_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("R-SPELL", Card, [], ModeChoices: [new("MINIONS", []), new("MIGHT", [])]));
        var id = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("REBUTTAL", RebuttalRulesTests.Card, [id])); await game.Top();
        async Task Choose(string option)
        {
            var request = game.State.PendingRuleChoice!.Request;
            await game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        await Choose("PAY:ANY");
        await Choose(RebuttalRulesTests.Option(game.State, "第 1 次", "模式 MIGHT"));
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "CONFIRM");
        await Choose(RebuttalRulesTests.Option(game.State, "第 1 次", "模式 LOOK"));
        await Choose("CONFIRM");
        var spell = Assert.Single(game.State.StackItems); Assert.Equal("P2", spell.ControllerId);
        Assert.Equal(new[] { "MIGHT", "LOOK" }, spell.ModeExecutions!.Select(m => m.Mode));
        await game.Top();
        var look = game.State.PendingCardChoice!; Assert.Equal("P2", look.PlayerId);
        await game.Act("P2", new ChooseCardsCommand(look.ChoiceId, look.ChoiceWindow, [look.LegalObjectIds[0]]));
        Assert.Contains("R-SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
    }

    [Fact]
    public void CostPreviewRequiresModesAndPricesEveryChosenWardTargetWithoutChangingState()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = initial.CardObjects["ENEMY"].Tags.Append("法盾2").ToArray() } } };
        var before = MatchStateHasher.Hash(initial);
        var engine = new CoreRuleEngine();
        var incomplete = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial,
            new PlayCardCommand("UNIT", Card, [])));
        Assert.True(incomplete.RequiresTargetSelection); Assert.False(incomplete.CanPay);
        Assert.Equal(3, incomplete.Cost!.Mana);
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial,
            Cast(new("DAMAGE", ["ENEMY"]), new("MIGHT", ["ENEMY"]))));
        Assert.True(quote.CanPay, quote.Message); Assert.False(quote.RequiresTargetSelection);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(5, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(4, Assert.Single(quote.Cost.Adjustments, adjustment => adjustment.Label == "法盾费用").Power);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    [Theory]
    [InlineData("missing")] [InlineData("count")] [InlineData("duplicate")] [InlineData("unknown")]
    [InlineData("private")] [InlineData("target")] [InlineData("child")]
    public async Task InvalidModeDeclarationsCannotPayOrChangeTheAuthoritativeState(string branch)
    {
        var initial = Position();
        var command = branch switch {
            "missing" => new PlayCardCommand("UNIT", Card, []),
            "count" => Cast(new SpellModeChoice("MINIONS", [])),
            "duplicate" => Cast(new("MINIONS", []), new("MINIONS", [])),
            "unknown" => Cast(new("MINIONS", []), new("OTHER", [])),
            "private" => Cast(new("MINIONS", []), new("LOOK", ["P1-DRAW-1"])),
            "target" => Cast(new("MINIONS", []), new("DAMAGE", ["GEAR"])),
            _ => new PlayCardCommand("UNIT", Card, [], Mode: "MINIONS")
        };
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("bad-mode", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task EchoLookChoicesHaveDifferentIdsAndEarlierModesNeverRunTwice()
    {
        var initial = Position();
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var game = new Driver(initial);
        await game.Act("P1", Cast(new("MINIONS", []), new("LOOK", [])) with { OptionalCosts = ["ECHO"] });
        await game.Top();
        Assert.Equal(2, Minions(game.State));
        var first = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, [first.LegalObjectIds[0]]));
        Assert.Equal(4, Minions(game.State));
        var second = game.State.PendingCardChoice!;
        Assert.NotEqual(first.ChoiceId, second.ChoiceId);
        await game.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, [second.LegalObjectIds[0]]));
        Assert.Equal(4, Minions(game.State)); Assert.Empty(game.State.StackItems);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(game.State.CardPlays.Finalized, play => play.Source.ObjectId == "UNIT");
    }

    [Fact]
    public async Task TargetsInLaterEchoModesAreObservedEvenWhenTheFirstPairHasNoTargets()
    {
        var initial = Position();
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1"),
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "VEN·174", "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", Cast(new("MINIONS", []), new("LOOK", [])) with { OptionalCosts = ["ECHO"],
            RepeatChoices = [new("", [], ModeChoices: [new("DAMAGE", ["ENEMY"]), new("MIGHT", ["ALLY"])])] });
        Assert.Single(game.State.StackItems, item => item.FieldContext?.Kind == "CONTROLLER_TARGETED_OR_READIED_SELF_POWER");
        Assert.Empty(game.State.StackItems.Single(item => item.SourceObjectId == "UNIT").TargetObjectIds);
    }

    [Fact]
    public async Task RecoveryRejectsDuplicateModesFutureBindingsAndWrongProgress()
    {
        var game = new Driver(Position());
        await game.Act("P1", Cast(new("DAMAGE", ["ALLY"]), new("MINIONS", [])));
        var item = Assert.Single(game.State.StackItems); var modes = item.ModeExecutions!;
        var future = modes[0] with { Execution = modes[0].Execution with { TargetGenerations = new Dictionary<string, long> { ["ALLY"] = 99 } } };
        foreach (var bad in new[] { item with { ModeExecutions = [modes[0], modes[0]] },
            item with { ModeExecutions = [future, modes[1]] }, item with { CompletedModeExecutions = 1 } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [bad] }), e => e.Contains("spell mode"));
    }
}
