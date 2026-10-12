using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using System.Text;
using System.Text.Json.Nodes;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BattleStartRoleTests
{
    [Fact]
    public async Task BattleTriggerOrderStartsWithTheRecordedAttackerWhenItIsNotTheirTurn()
    {
        var state = SequentialMovementTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A"] = state.CardObjects["A"] with { CardNo = "SFD·128/221", Power = 2 },
            ["D"] = state.CardObjects["D"] with { CardNo = "OGN·041/298", Power = 9 } } };
        var played = await Act(state, "P1", SequentialMovementTests.Command("BATTLEFIELD:BF2", "BATTLEFIELD:BF2"));
        var contested = await Top(played.State);
        Assert.Equal("P1", contested.State.TurnPlayerId);
        Assert.True(contested.State.CardObjects["D"].IsAttacking);
        Assert.True(contested.State.CardObjects["A"].IsDefending);
        Assert.Equal(2, contested.State.TriggerQueue.Count);
        Assert.Contains(CommandTypes.OrderTriggers, contested.Prompts["P2"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.OrderTriggers, contested.Prompts["P1"].EnabledActions());
        var attacker = Assert.Single(contested.State.TriggerQueue, q => q.ControllerId == "P2");
        var defender = Assert.Single(contested.State.TriggerQueue, q => q.ControllerId == "P1");
        var wrongOrder = new OrderTriggersCommand(OrderedTriggerIds: [attacker.TriggerId, defender.TriggerId]);
        var rejected = await new CoreRuleEngine().ResolveAsync(contested.State, new("wrong-battle-order", "P2", wrongOrder.CmdType), wrongOrder, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(contested.State), MatchStateHasher.Hash(rejected.State));
        var ordered = await Act(contested.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: [defender.TriggerId, attacker.TriggerId]));
        Assert.Equal("P2", ordered.State.PendingCardChoice!.PlayerId);
        var confirmedAttack = await AttackDamageDistributionTests.Confirm(ordered.State, "ANCHOR");
        Assert.Equal("P1", confirmedAttack.State.PendingCardChoice!.PlayerId);
        var confirmedDefense = await Act(confirmedAttack.State, "P1", new ChooseCardsCommand(
            confirmedAttack.State.PendingCardChoice.ChoiceId, "TRIGGER_CONFIRMATION", ["D"]));
        Assert.Equal("DEFEND", confirmedDefense.State.StackItems.Last().FieldContext!.Kind);
        var defenseResolved = await Top(confirmedDefense.State);
        Assert.Contains("D", defenseResolved.State.PlayerZones["P2"].Base);
        Assert.Single(defenseResolved.State.StackItems);
        var attackResolved = await Top(defenseResolved.State);
        Assert.Empty(attackResolved.State.StackItems); Assert.Equal(0, attackResolved.State.CardObjects["ANCHOR"].Damage);
        Assert.Equal("P2", attackResolved.State.FocusPlayerId);
        Restore(attackResolved.State); ArmamentMightTests.PublicRecovery(attackResolved.State);
    }

    [Theory]
    [InlineData("focus-seat")]
    [InlineData("attacking-seat")]
    [InlineData("early-combat")]
    [InlineData("null-costs")]
    [InlineData("false-field")]
    public async Task RecoveryRejectsForgedInitialBattleRoleContext(string forgery)
    {
        var moved = await Act(AttackDamageDistributionTests.Position("OGN·001/298"), "P1",
            new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var state = moved.State; Restore(state); ArmamentMightTests.PublicRecovery(state);
        const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
        var marker = Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith(prefix));
        var context = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker[prefix.Length..])))!;
        switch (forgery) {
            case "focus-seat": context["InitialFocusPlayerId"] = "P2"; break;
            case "attacking-seat": context["InitialAttackingPlayerId"] = "P2"; context["InitialFocusPlayerId"] = "P2"; break;
            case "early-combat": context["CombatStarted"] = true; break;
            case "null-costs": context["OptionalCosts"] = null; break;
            case "false-field": context["BattlefieldId"] = "HOST"; break;
        }
        var encoded = prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(context.ToJsonString()));
        var forged = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Select(e => e == marker ? encoded : e).ToArray() };
        Assert.Contains("invalid battle response declaration", OfficialInsightAndSpellLockTests.Errors(forged));
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(moved.State));
    }

    [Theory]
    [InlineData("OGN·041/298", false)]
    [InlineData("OGN·041a/298", false)]
    [InlineData("OGN·041/298", true)]
    public async Task ContestEstablishesAttackRolesAndConfirmsTriggersBeforeOrdinaryFocus(string card, bool copied)
    {
        var state = AttackDamageDistributionTests.Position(card, copied);
        var result = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(result.State.CardObjects["HOST"].IsAttacking);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.True(result.State.CardObjects[id].IsDefending));
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P1"].EnabledActions());
        if (copied) Assert.Equal(2, result.State.TriggerQueue.Count);
        else Assert.Equal("TRIGGER_CONFIRMATION", result.State.PendingCardChoice!.ChoiceWindow);
        Assert.All(result.State.CardObjects.Values, c => Assert.Equal(0, c.Damage));
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task ContestWithoutTriggeredSkillsEstablishesRolesAndKeepsAttackerFocus()
    {
        var state = AttackDamageDistributionTests.Position("OGN·001/298");
        var result = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(result.State.CardObjects["HOST"].IsAttacking);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.True(result.State.CardObjects[id].IsDefending));
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Contains(CommandTypes.PassFocus, result.Prompts["P1"].EnabledActions());
        Assert.Empty(result.State.StackItems); Assert.Empty(result.State.TriggerQueue);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task ConfirmedAttackResolvesBeforeOrdinaryFocusAndIsNotRecapturedForCombat()
    {
        var moved = await Act(AttackDamageDistributionTests.Position(), "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var confirmed = await AttackDamageDistributionTests.Confirm(moved.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var pending = await AttackDamageDistributionTests.Top(confirmed.State);
        var allocated = await Act(pending.State, "P1", AttackDamageDistributionTests.Allocation(pending.State,
            ("ENEMY1", 2), ("ENEMY2", 2), ("ENEMY3", 1)));
        Assert.Equal("P1", allocated.State.FocusPlayerId);
        Assert.Contains(CommandTypes.PassFocus, allocated.Prompts["P1"].EnabledActions());
        var closed = await AttackDamageDistributionTests.Top(allocated.State);
        var combat = await Act(closed.State, "P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        Assert.Empty(combat.State.StackItems); Assert.Empty(combat.State.TriggerQueue);
        Assert.DoesNotContain(combat.Events, e => e.Kind == "TRIGGER_QUEUED" || e.Kind == "DAMAGE_APPLIED");
        Assert.Contains(CommandTypes.AssignCombatDamage, combat.Prompts["P1"].EnabledActions());
        Restore(combat.State); ArmamentMightTests.PublicRecovery(combat.State);
    }

    [Fact]
    public async Task AUnitArrivingDuringTheDuelGainsItsRoleWithoutRecapturingExistingAttackSkills()
    {
        var initial = At(OfficialHoldSequenceTests.AddUnit(AttackDamageDistributionTests.Position(),
            "LATE", "SFD·050/221", "F", "P1"), "LATE", "P1", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var confirmed = await AttackDamageDistributionTests.Confirm(moved.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var pending = await AttackDamageDistributionTests.Top(confirmed.State);
        var allocated = await Act(pending.State, "P1", AttackDamageDistributionTests.Allocation(pending.State,
            ("ENEMY1", 2), ("ENEMY2", 2), ("ENEMY3", 1)));
        var activated = await Act(allocated.State, "P1", new ActivateAbilityCommand("LATE",
            P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["HOST"], []));
        var arrived = await AzirTestDriver.Finish(await AttackDamageDistributionTests.Top(activated.State), false);
        Assert.True(arrived.State.CardObjects["LATE"].IsAttacking);
        Assert.False(arrived.State.CardObjects["HOST"].IsAttacking);
        Assert.Empty(arrived.State.StackItems); Assert.Empty(arrived.State.TriggerQueue);
        Assert.Equal(new[] { 2, 2, 1 }, new[] { "ENEMY1", "ENEMY2", "ENEMY3" }.Select(id => arrived.State.CardObjects[id].Damage));
        Restore(arrived.State); ArmamentMightTests.PublicRecovery(arrived.State);
    }

    [Fact]
    public async Task ShadowUsesTheDefendersSwiftFocusAfterAttackRolesAreEstablished()
    {
        var initial = OfficialHoldSequenceTests.AddUnit(AttackDamageDistributionTests.Position("OGN·001/298"),
            "SHADOW", "UNL-194/219", "F", "P2");
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var command = new ActivateAbilityCommand("SHADOW", P4ActivatedAbilityCatalog.ShadowStunAbilityId, ["HOST"], []);
        var wrong = await new CoreRuleEngine().ResolveAsync(moved.State, new("wrong-focus", "P2", command.CmdType), command, default);
        Assert.False(wrong.Accepted); Assert.Equal(MatchStateHasher.Hash(moved.State), MatchStateHasher.Hash(wrong.State));
        var focused = await Act(moved.State, "P1", new PassFocusCommand());
        var activated = await Act(focused.State, "P2", command);
        Assert.Single(activated.State.StackItems); Assert.True(activated.State.CardObjects["SHADOW"].IsExhausted);
        Assert.DoesNotContain(activated.Events, e => e.Kind == "DAMAGE_APPLIED");
        var resolved = await AttackDamageDistributionTests.Top(activated.State);
        Assert.Contains("STUNNED", resolved.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Equal("P1", resolved.State.FocusPlayerId);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task ShadowsSwiftAbilityCannotInterruptConfirmedAttackSkills()
    {
        var initial = OfficialHoldSequenceTests.AddUnit(AttackDamageDistributionTests.Position(copied: true),
            "SHADOW", "UNL-194/219", "F", "P2");
        var moved = await AttackDamageDistributionTests.Attack(initial);
        var ordered = await Act(moved.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: moved.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var first = await AttackDamageDistributionTests.Confirm(ordered.State, "ENEMY1");
        var second = await AttackDamageDistributionTests.Confirm(first.State, "ENEMY2");
        var response = await Act(second.State, "P1", new PassPriorityCommand());
        var command = new ActivateAbilityCommand("SHADOW", P4ActivatedAbilityCatalog.ShadowStunAbilityId, ["HOST"], []);
        var rejected = await new CoreRuleEngine().ResolveAsync(response.State, new("no-reaction", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(response.State), MatchStateHasher.Hash(rejected.State));
        Assert.False(rejected.State.CardObjects["SHADOW"].IsExhausted);
    }
}
