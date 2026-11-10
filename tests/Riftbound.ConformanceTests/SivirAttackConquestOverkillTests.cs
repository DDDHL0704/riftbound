using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SivirAttackConquestOverkillTests
{
    private const string Sivir = "SFD·120/221";

    private static MatchState Position(string no = Sivir, string defender = "OGN·010/298")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·280/298"), "SIVIR", no, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "DEFENDER", defender, "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "VICTIM", "OGN·142/298", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "P2-RETURN", "OGN·104/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "OTHER", "OGN·280/298", "P2", "BATTLEFIELD", "OTHER");
        // PrintedCardFactory supplies identity. This initial field fixture
        // also needs the printed keyword normally applied by unit entry.
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SIVIR"] = state.CardObjects["SIVIR"] with {
                Tags = state.CardObjects["SIVIR"].Tags.Append("法盾2").ToArray() },
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
    }

    private static async Task Conquer(Driver game, params string[] attackers)
    {
        if (attackers.Length == 0) attackers = ["SIVIR"];
        await game.Act("P1", new MoveUnitCommand(attackers[0], "BASE", "BATTLEFIELD:BF", [], attackers));
        for (var step = 0; step < 40; step++)
        {
            if (game.Events().Any(e => e.Event.Kind == "BATTLEFIELD_CONQUERED"
                && Equals(e.Event.Payload.GetValueOrDefault("battlefieldObjectId"), "BF"))) return;
            var prompts = ResolutionResult.BuildPrompts(game.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c =>
                c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
                await game.Act(assignment.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!,
                    "BF", CombatTestDriver.Assignments(assignment)));
            else if (game.State.PriorityPlayerId is { } priority)
                await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus)
                await game.Act(focus, new PassFocusCommand());
            else
            {
                var declare = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c =>
                    c.Action == CommandTypes.DeclareBattle && c.Enabled) == true);
                Assert.NotNull(declare);
                await game.Act(declare.PlayerId, new DeclareBattleCommand("BF", game.State.BattleState.AttackerObjectIds,
                    game.State.BattleState.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        Assert.Fail("The real battlefield attack never reached its conquest.");
    }

    private static async Task Confirm(Driver game, string? target = "VICTIM")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Null(game.State.PriorityPlayerId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            target is null ? [] : [target]));
    }

    [Theory, InlineData(Sivir), InlineData("SFD·120a/221")]
    public async Task AnActualAttackCapturesFiveExcessDamageBeforeConfirmingTheIndependentSkill(string no)
    {
        var game = new Driver(Position(no)); await Conquer(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(5, skill.UnitEvent!.AssignedOverkill);
        Assert.Equal(0, game.State.CardObjects["VICTIM"].Damage);
        await Confirm(game);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(0, game.State.CardObjects["VICTIM"].Damage);
        await game.Top();
        Assert.Equal(5, game.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task FourExcessDamageDoesNotEarnTheSkill()
    {
        var game = new Driver(Position(defender: "OGN·006/298")); await Conquer(game);
        Assert.Equal(4, Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED")
            .Event.Payload["assignedOverkillDamageToEnemyUnits"]);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(0, game.State.CardObjects["VICTIM"].Damage); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnUnopposedConquestCannotInventAnAttackDamageAssignment()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "DEFENDER", "P2", "BASE");
        var game = new Driver(initial); await Conquer(game);
        var conquered = Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Equal(0, conquered.Event.Payload["assignedOverkillDamageToEnemyUnits"]);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(0, game.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TheWholeAttackAssignmentIsCapturedAndTheEnemyTargetMayBeAtAnotherBattlefield(bool elsewhere)
    {
        var state = Position();
        if (elsewhere) {
            state = RadUnitEffectTestFixture.Place(state, "VICTIM", "P2", "BATTLEFIELD", "OTHER");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P2" } } };
        }
        var game = new Driver(state); await Conquer(game, "SIVIR", "ALLY");
        Assert.Equal(9, Assert.Single(game.State.StackItems).UnitEvent!.AssignedOverkill);
        await Confirm(game); await game.Top();
        Assert.Equal(9, game.State.CardObjects["VICTIM"].Damage);
        Assert.Equal(7, game.State.CardObjects["SIVIR"].Power); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheOptionalSkillCanBeDeclinedBeforeEitherPlayerReceivesPriority()
    {
        var game = new Driver(Position()); await Conquer(game); await Confirm(game, null);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(0, game.State.CardObjects["VICTIM"].Damage); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningTheConqueringSourceCannotEraseOrRecalculateItsEarnedDamage()
    {
        var game = new Driver(Position()); await Conquer(game); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SIVIR"])); await game.Top();
        Assert.Contains("SIVIR", game.State.PlayerZones["P1"].Hand);
        var visible = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.DoesNotContain("\"SIVIR\"", visible);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["VICTIM"].Damage);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ATargetReturnedDuringResponsesIsNotDamagedInItsOwnersHand()
    {
        var game = new Driver(Position()); await Conquer(game); await Confirm(game);
        Assert.Equal("P1", game.State.PriorityPlayerId);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("P2-RETURN", "OGN·104/298", ["VICTIM"])); await game.Top();
        Assert.Contains("VICTIM", game.State.PlayerZones["P2"].Hand);
        await game.Top(); Assert.Equal(0, game.State.CardObjects["VICTIM"].Damage);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task WardMustBePaidForTheConfirmedEnemyBeforeResponsesAndDamage(bool pay)
    {
        var initial = RadGearTestPosition.Put(Position(), "WARDED", "OGN·013/298", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["WARDED"] = initial.CardObjects["WARDED"] with {
                Tags = initial.CardObjects["WARDED"].Tags.Append("法盾").ToArray() } } };
        var game = new Driver(initial); await Conquer(game);
        var pool = game.State.RunePools["P1"].TotalPower;
        await Confirm(game, "WARDED");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost); Assert.Null(game.State.PriorityPlayerId);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(pool - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        if (pay) { await game.Top(); Assert.Contains("WARDED", game.State.PlayerZones["P2"].Graveyard); }
        else { Assert.Empty(game.State.StackItems); Assert.Contains("WARDED", game.State.PlayerZones["P2"].Base); }
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AUnitInBaseDoesNotBorrowAnAlliedUnitsAttackConquest()
    {
        var state = RadGearTestPosition.Put(Position(), "OTHER-ATTACKER", "OGN·142/298", "P1", "BASE");
        var game = new Driver(state); await Conquer(game, "OTHER-ATTACKER");
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Contains("SIVIR", game.State.PlayerZones["P1"].Base); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ThePrintedDamageAndAnArmamentDrawKeepSeparateSkillsWithTheSameCapturedAssignment(bool damageFirst)
    {
        const string guard = "UNL-188/219";
        var state = RadGearTestPosition.Put(Position(), "GUARD", guard, "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GUARD"] = state.CardObjects["GUARD"] with { AttachedToObjectId = "SIVIR" } } };
        state = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        var top = state.PlayerZones["P1"].MainDeck[0];
        var game = new Driver(state); await Conquer(game);
        var pending = game.State.TriggerQueue;
        Assert.Equal(2, pending.Count);
        Assert.Equal(2, pending.Select(t => t.CardNo).Distinct().Count());
        Assert.All(pending, t => Assert.Equal(8, t.UnitEvent!.AssignedOverkill));
        var ordered = pending.OrderBy(t => (t.CardNo == Sivir) == damageFirst ? 0 : 1)
            .Select(t => t.TriggerId).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered));
        await Confirm(game);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Contains(game.State.StackItems, item => item.CardNo == guard && item.UnitEvent!.Origin!.ArmamentGranted);
        await game.Top(); await game.Top();
        Assert.Equal(8, game.State.CardObjects["VICTIM"].Damage);
        Assert.Contains(top, game.State.PlayerZones["P1"].Hand);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("amount"), InlineData("below-minimum"), InlineData("negative"),
     InlineData("erase-fact"), InlineData("erase-skill"), InlineData("duplicate"), InlineData("controller"),
     InlineData("source-generation"), InlineData("tick"), InlineData("timing")]
    public async Task CapturedDamageAndConquestCannotBeRewrittenAgainstOriginalAcceptedHistory(string mutation)
    {
        var game = new Driver(Position()); await Conquer(game); await Confirm(game);
        var skill = Assert.Single(game.State.StackItems); var context = skill.UnitEvent!;
        var changed = skill with { UnitEvent = context with {
            AssignedOverkill = mutation switch {
                "amount" => context.AssignedOverkill + 1, "below-minimum" => 4, "negative" => -1,
                "erase-fact" => null, _ => context.AssignedOverkill },
            SourceGeneration = context.SourceGeneration + (mutation == "source-generation" ? 1 : 0),
            CapturedTick = context.CapturedTick + (mutation == "tick" ? 1 : 0),
            Timing = mutation == "timing" ? "ATTACK" : context.Timing },
            ControllerId = mutation == "controller" ? "P2" : skill.ControllerId };
        var forged = game.State with { StackItems = mutation switch {
            "erase-skill" => [], "duplicate" => [skill, skill with { StackItemId = skill.StackItemId + "-copy" }],
            _ => [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AResolvedDamageSkillCannotBeResurrectedWithItsOriginalAmountAndTargets()
    {
        var game = new Driver(Position()); await Conquer(game); await Confirm(game);
        var skill = Assert.Single(game.State.StackItems); await game.Top();
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [skill] }));
        await game.ColdCheckpoint();
    }
}
