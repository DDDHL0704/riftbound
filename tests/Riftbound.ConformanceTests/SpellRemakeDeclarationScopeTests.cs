using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SpellRemakeDeclarationScopeTests
{
    [Theory]
    [InlineData(false, "OGN·083/298", 2)]
    [InlineData(true, "OGN·083/298", 2)]
    [InlineData(false, "SFD·087/221", 3)]
    [InlineData(true, "SFD·087/221", 3)]
    public async Task IntrinsicallyUntargetedSpellWithNoModesOrDestinationsDoesNotRequestAnEmptyRemake(
        bool soulMirror, string card, int draws)
    {
        var (s, events) = await Control(soulMirror, card);
        Assert.Null(s.PendingRuleChoice);
        Assert.DoesNotContain(events, e => e.Kind == "SPELL_CHOICES_REMADE");
        var controlled = Assert.Single(s.StackItems);
        Assert.Equal("P2", controlled.ControllerId);
        Assert.Empty(controlled.TargetObjectIds);
        Assert.Null(controlled.RepeatExecutions);
        var forged = new PayCostCommand("empty-remake", "RULE_REPLACEMENT", ["CONFIRM"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("forged-empty-remake", "P2", forged.CmdType), forged, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        var pool = s.RunePools["P2"];
        var oldDeck = s.PlayerZones["P1"].MainDeck;
        var expected = s.PlayerZones["P2"].MainDeck.Take(draws).ToArray();
        var hand = s.PlayerZones["P2"].Hand.Count;
        var done = await Top(s);
        Assert.Empty(done.State.StackItems);
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Equal(pool, done.State.RunePools["P2"]);
        Assert.Equal(hand + draws, done.State.PlayerZones["P2"].Hand.Count);
        Assert.All(expected, id => Assert.Contains(id, done.State.PlayerZones["P2"].Hand));
        Assert.Equal(oldDeck, done.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("R-SPELL", done.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("R-SPELL", done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P1", Assert.Single(done.State.CardPlays.Finalized, p => p.Source.ObjectId == "R-SPELL").PlayerId);
        Assert.Equal("P2", Assert.Single(done.State.CardPlays.Resolved, p => p.Source.ObjectId == "R-SPELL").PlayerId);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginallyChoosingZeroOptionalTargetsStillAllowsANewLegalTarget(bool soulMirror)
        => await OptionalZeroTargets(soulMirror, echo: false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroTargetsInOneEchoExecutionRemainLegalWhenOnlyTheOtherExecutionGetsANewTarget(bool soulMirror)
        => await OptionalZeroTargets(soulMirror, echo: true);

    private static async Task OptionalZeroTargets(bool soulMirror, bool echo)
    {
        var (s, _) = await Control(soulMirror, "SFD·080/221", costs: echo ? ["ECHO"] : null);
        var request = Assert.IsType<RuleChoiceContinuation>(s.PendingRuleChoice).Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.Equal("SPELL_CHOICE_REMAKE", request.Kind);
        var original = request.Preview!.StackItems.Single(i => i.SourceObjectId == "R-SPELL");
        Assert.Empty(original.TargetObjectIds);
        var paid = request.Preview.RunePools["P2"];
        var originalCasterPaid = request.Preview.RunePools["P1"];
        if (echo)
        {
            Assert.Equal(2, original.RepeatExecutions!.Count);
            Assert.All(original.RepeatExecutions, e => Assert.Empty(e.TargetObjectIds));
        }
        s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, echo ? "第 2 次" : "第 1 次", "添加目标 1 为", "（R-OTHER）"));
        s = await RebuttalRulesTests.Choose(s, "CONFIRM");
        var remade = Assert.Single(s.StackItems);
        Assert.Equal(echo ? Array.Empty<string>() : ["R-OTHER"], remade.TargetObjectIds);
        if (echo)
        {
            Assert.Equal(2, remade.RepeatExecutions!.Count);
            Assert.Empty(remade.RepeatExecutions[0].TargetObjectIds);
            Assert.Equal(new[] { "R-OTHER" }, remade.RepeatExecutions[1].TargetObjectIds);
        }
        Assert.Equal(original.OptionalCosts, remade.OptionalCosts);
        Assert.Equal(original.PlayCost, remade.PlayCost);
        Assert.Equal(paid, s.RunePools["P2"]);
        var done = await Top(s);
        Assert.Equal(1, done.State.CardObjects["R-OTHER"].Damage);
        Assert.Equal(9, done.State.CardObjects["R-OTHER"].Power);
        Assert.Equal(0, done.State.CardObjects["R-TARGET"].Damage);
        Assert.Equal(9, done.State.CardObjects["R-TARGET"].Power);
        Assert.Equal(paid, done.State.RunePools["P2"]);
        Assert.Equal(originalCasterPaid, done.State.RunePools["P1"]);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UntargetedModeRetainsOnlyTheChoicesGrantedByTheControllingSpell(bool soulMirror)
    {
        var (s, _) = await Control(soulMirror, "UNL-182/219", "DRAW_1");
        if (soulMirror)
        {
            Assert.Contains(s.PendingRuleChoice!.Request.Options, o => o.Label.Contains("模式 BASE_UNIT_DAMAGE_3", StringComparison.Ordinal));
            s = await RebuttalRulesTests.Choose(s, "KEEP_ALL");
        }
        else Assert.Null(s.PendingRuleChoice);
        Assert.Equal("PERFECT_FINALE_DRAW_1", Assert.Single(s.StackItems).EffectKind);
        Assert.Empty(Assert.Single(s.StackItems).TargetObjectIds);
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    private static async Task<(MatchState State, IReadOnlyList<GameEvent> Events)> Control(bool soulMirror, string card,
        string mode = "", IReadOnlyList<string>? costs = null)
    {
        // Explicit semantic fixture; separate ordinary-opening, SQL and product gates remain required.
        var initial = soulMirror ? RebuttalRulesTests.Position(card) : ReversalTargetChoiceTests.Position(card);
        var deck = Enumerable.Range(0, 8).Select(i => "NEW-CONTROLLER-DECK-" + i).ToArray();
        var cards = initial.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = initial.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in deck)
        {
            cards[id] = new(id, cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P2", "MAIN_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck } } };
        var events = new List<GameEvent>();
        var played = await Act(initial, "P1", new PlayCardCommand("R-SPELL", card, [], mode, costs)); events.AddRange(played.Events);
        var parent = Assert.Single(played.State.StackItems).StackItemId;
        var handed = await Act(played.State, "P1", new PassPriorityCommand()); events.AddRange(handed.Events);
        var control = await Act(handed.State, "P2", new PlayCardCommand(soulMirror ? "REBUTTAL" : "REVERSAL",
            soulMirror ? RebuttalRulesTests.Card : ReversalTargetChoiceTests.Card, [parent])); events.AddRange(control.Events);
        var opened = await Top(control.State); events.AddRange(opened.Events);
        if (soulMirror)
        {
            var request = Assert.IsType<RuleChoiceContinuation>(opened.State.PendingRuleChoice).Request;
            Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT", request.Kind);
            opened = await Act(opened.State, "P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["PAY:ANY"]));
            events.AddRange(opened.Events);
        }
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
        return (opened.State, events);
    }
}
