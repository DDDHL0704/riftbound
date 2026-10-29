using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class JayceReadiedKeywordTests
{
    private static MatchState Position(string no = "VEN·088", bool hand = true)
    {
        var s = hand ? EmpowerTests.Position(no) : AttachedRuleTextTests.Position(no, 0);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(30, 20, new Dictionary<string, int> { [RuneTrait.Orange] = 20, [RuneTrait.Blue] = 20 }), ["P2"] = new(30, 20) },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with { IsExhausted = true } } };
        foreach (var (id, card) in new[] { ("READY1", "OGN·146/298"), ("READY2", "OGN·146/298"), ("RETURN", "OGN·104/298") })
            s = AttackDamageDistributionTests.Spell(s, id, card, "P1");
        return s;
    }

    private static async Task Ready(Driver game, string spell = "READY1")
    {
        await game.Act("P1", new PlayCardCommand(spell, "OGN·146/298", ["HOST"])); await game.Top();
        Assert.Equal("READIED_MODE_CONFIRMATION", game.State.PendingCardChoice?.ChoiceWindow);
    }

    private static async Task Choose(Driver game, string mode)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(["ASSAULT_2", "WARD_2", "ROAM"], choice.LegalObjectIds);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [mode]));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
    }

    [Theory]
    [InlineData("VEN·088", 37783, "ASSAULT_2", "COMBAT_KEYWORD:强攻2:1")]
    [InlineData("VEN·088a", 37784, "ASSAULT_2", "COMBAT_KEYWORD:强攻2:1")]
    [InlineData("VEN·088", 37783, "WARD_2", "COMBAT_KEYWORD:法盾2:1")]
    [InlineData("VEN·088a", 37784, "WARD_2", "COMBAT_KEYWORD:法盾2:1")]
    [InlineData("VEN·088", 37783, "ROAM", "ROAM")]
    [InlineData("VEN·088a", 37784, "ROAM", "ROAM")]
    public async Task BothPrintingsPayOrangePowerAndGrantOnlyTheChosenKeywordAfterResponses(string no, long id, string mode, string effect)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(5, card.Power);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers);
        Assert.Equal(TriggerKinds.SourceReadiedChooseKeyword, spec.Kind); Assert.Equal(3, spec.Modes!.Count);
        var game = new Driver(Position(no)); await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Equal(26, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await Ready(game); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await Choose(game, mode); Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Top(); Assert.Equal([effect], game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·088", "mana")] [InlineData("VEN·088a", "trait")]
    [InlineData("VEN·088", "target")] [InlineData("VEN·088a", "target")]
    public async Task IllegalUnitPlayLeavesStateUntouched(string no, string kind)
    {
        var state = Position(no);
        if (kind is "mana" or "trait") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(kind == "mana" ? 3 : 30, 0, new Dictionary<string, int> { [kind == "mana" ? RuneTrait.Orange : RuneTrait.Blue] = 1 }) } };
        await Reject(state, new PlayCardCommand("HOST", no, kind == "target" ? ["HOST"] : []));
    }

    [Theory]
    [InlineData("VEN·088")] [InlineData("VEN·088a")]
    public async Task ReadyingAnAlreadyActiveSourceDoesNotTriggerAgain(string no)
    {
        var s = Position(no, false); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { IsExhausted = false } } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("READY1", "OGN·146/298", ["HOST"])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task TurnStartReadiesTheSourceAndWaitsForItsSkillBeforeTheStartStep()
    {
        var state = Position(hand: false) with { Phase = MatchPhases.TurnStart, TurnStartStep = "READY", TimingState = TimingStates.NeutralClosed };
        var deck = state.PlayerZones["P1"].MainDeck.ToArray(); var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("START", game.State.TurnStartStep); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await Choose(game, "WARD_2"); await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Contains("COMBAT_KEYWORD:法盾2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("empty")] [InlineData("unknown")] [InlineData("two")]
    public async Task InvalidModeCannotConsumeThePendingSkill(string kind)
    {
        var game = new Driver(Position(hand: false)); await Ready(game);
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            kind == "empty" ? [] : kind == "unknown" ? ["FORGED"] : ["WARD_2", "ROAM"]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnEarnedSelfGrantDoesNotAffectTheSourcesLaterIncarnation(bool replaySource)
    {
        var game = new Driver(Position(hand: false)); await Ready(game); await Choose(game, "WARD_2");
        var captured = Assert.Single(game.State.StackItems).SourceReadied!;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > captured.Source.Generation);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        // The original independent skill resolves before this physical card can be played again.
        var count = game.Events().Length; await game.Top();
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(count)));
        if (replaySource) await game.Act("P1", new PlayCardCommand("HOST", "VEN·088", []));
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task RepeatedReadyInstructionsAccumulateWardAndTurnCleanupRemovesIt()
    {
        var game = new Driver(Position(hand: false)); await Ready(game); await Choose(game, "WARD_2"); await game.Top();
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 8 && game.State.FocusPlayerId is { } focus; i++) await game.Act(focus, new PassFocusCommand());
        Assert.Null(game.State.FocusPlayerId);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await Ready(game, "READY2"); await Choose(game, "WARD_2"); await game.Top();
        Assert.Contains("COMBAT_KEYWORD:法盾2:2", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        var state = AttackDamageDistributionTests.Spell(game.State, "OPPONENT-SPELL", "OGN·004/298", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var command = new PlayCardCommand("OPPONENT-SPELL", "OGN·004/298", ["HOST"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P2", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.Cost is not null, quote.Message); Assert.Equal(4, quote.Cost!.GenericPower);
        await game.Act("P1", new EndTurnCommand()); Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task AssaultModeAddsTwoMightWhenTheSourceActuallyAttacks()
    {
        var state = Position(hand: false);
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" })
            state = OfficialHoldSequenceTests.AddUnit(state, id, "OGN·001/298", "F", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["F"] = state.CardObjects["F"] with { ControllerId = "P2" } } };
        var game = new Driver(state); await Ready(game); await Choose(game, "ASSAULT_2"); await game.Top();
        Assert.Equal(5, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++) await game.Act(focus, new PassFocusCommand());
        if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        Assert.True(game.State.CardObjects["HOST"].IsAttacking); Assert.Equal(7, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("ROAM", true)] [InlineData("WARD_2", false)]
    public async Task OnlyRoamModePermitsMovementBetweenBattlefields(string mode, bool allowed)
    {
        var state = LocalDestructionRecallTests.At(Position(hand: false), "HOST", "P1", "BATTLEFIELD", "F");
        var game = new Driver(state); await Ready(game); await Choose(game, mode); await game.Top();
        var move = new MoveUnitCommand("HOST", "BATTLEFIELD:F", "BATTLEFIELD:OTHER", ["ROAM"]);
        if (allowed) { await game.Act("P1", move); Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId); await game.Replay(); }
        else await Reject(game.State, move);
    }

    [Fact]
    public async Task AnAttachedCopyOfTheTextCreatesItsOwnIndependentModeChoice()
    {
        var game = new Driver(Position(hand: false));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("READY1", "OGN·146/298", ["HOST"])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Single(game.State.TriggerQueue, t => t.SourceReadied?.Origin is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 2; i++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [i == 0 ? "WARD_2" : "ROAM"]));
        }
        await game.Top(); await game.Top();
        Assert.Equal(["COMBAT_KEYWORD:法盾2:1", "ROAM"], game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Theory]
    [InlineData("generation")] [InlineData("tick")] [InlineData("mode")] [InlineData("target")]
    public async Task RecoveryRejectsForgedReadiedSkills(string mutation)
    {
        var game = new Driver(Position(hand: false)); await Ready(game); var item = Assert.Single(game.State.StackItems); var c = item.SourceReadied!;
        var changed = item with { SourceReadied = mutation switch {
            "generation" => c with { Source = c.Source with { Generation = c.Source.Generation + 1 } },
            "tick" => c with { CapturedTick = game.State.Tick + 1 }, "mode" => c with { Mode = "FORGED" }, _ => c },
            TargetObjectIds = mutation == "target" ? ["HOST"] : [] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [changed] }), e => e.Contains("source readied"));
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-readied-" + state.Tick, "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
