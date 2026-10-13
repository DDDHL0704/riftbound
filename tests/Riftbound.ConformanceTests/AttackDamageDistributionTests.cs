using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttackDamageDistributionTests
{
    internal static MatchState Position(string card = "OGN·041/298", bool copied = false)
    {
        var s = ArmamentMightTests.Position(gear: copied ? "SFD·059/221" : "SFD·022/221", unit: card, power: 9, attached: copied);
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" })
            s = OfficialHoldSequenceTests.AddUnit(s, id, "OGN·001/298", "F", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["F"] = s.CardObjects["F"] with { ControllerId = "P2" }
        }, RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) }};
        return EquipChainTests.Response(s, "UNL-106/219", "P2");
    }

    internal static MatchState Spell(MatchState state, string id, string card, string player)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(card, out var behavior));
        var s = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: card, manaCost: behavior.ManaCost, ownerId: player, controllerId: player, tags: [CardObjectTags.SpellCard]) } };
        return s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                [player] = s.PlayerZones[player] with { Hand = [..s.PlayerZones[player].Hand, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new(player, "HAND") } };
    }

    internal static async Task<ResolutionResult> Confirm(MatchState state, params string[] targets)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        var result = await Act(state, choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
        return result;
    }

    internal static async Task<ResolutionResult> Top(MatchState state)
    {
        var closingFocus = state.StackItems.Count == 0 && state.FocusPlayerId is not null;
        var first = await Act(state, closingFocus ? state.FocusPlayerId! : state.PriorityPlayerId!,
            closingFocus ? new PassFocusCommand() : new PassPriorityCommand());
        Restore(first.State); ArmamentMightTests.PublicRecovery(first.State);
        var second = await Act(first.State, closingFocus ? first.State.FocusPlayerId! : first.State.PriorityPlayerId!,
            closingFocus ? new PassFocusCommand() : new PassPriorityCommand());
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
        return second;
    }

    internal static AssignEffectDamageCommand Allocation(MatchState state, params (string Id, int Damage)[] targets)
        => new(state.PendingCardChoice!.ResolvingStackItemId!, targets.Select(t => new CombatDamageAssignmentDto("HOST", t.Id, t.Damage)).ToArray());

    internal static async Task<ResolutionResult> Attack(MatchState initial)
    {
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var current = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var index = 0; index < 4 && current.State.FocusPlayerId is not null; index++)
            current = await Act(current.State, current.State.FocusPlayerId, new PassFocusCommand());
        if (ResolutionResult.BuildPrompts(current.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            current = await Act(current.State, "P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        return current;
    }

    [Theory]
    [InlineData("OGN·041/298")]
    [InlineData("OGN·041a/298")]
    public async Task AttackConfirmsItsPrintedDamageTargetsBeforeResponses(string card)
    {
        var current = await Attack(Position(card));
        Assert.True(current.State.CardObjects["HOST"].IsAttacking);
        Assert.Contains(current.Events, e => e.Kind == "TRIGGER_QUEUED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.NotNull(current.State.PendingCardChoice);
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" }) Assert.Equal(0, current.State.CardObjects[id].Damage);
        Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
    }

    [Fact]
    public async Task ConfirmedTargetsArePublicButAmountsWaitForResolutionAndRemainIndependentOfMight()
    {
        var attack = await Attack(Position());
        var confirmed = await Confirm(attack.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var item = Assert.Single(confirmed.State.StackItems);
        Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, item.TargetObjectIds);
        Assert.Equal(5, item.DamageAmount); Assert.Null(item.DamageDistribution!.ResolutionDamage);
        Assert.Equal(9, confirmed.State.CardObjects["HOST"].Power);
        Assert.Equal("P1", confirmed.State.PriorityPlayerId); Assert.Null(confirmed.State.PendingCardChoice);
        var resolving = await Top(confirmed.State);
        Assert.Equal("EFFECT_DAMAGE_DISTRIBUTION", resolving.State.PendingCardChoice!.ChoiceWindow);
        Assert.Contains(CommandTypes.AssignEffectDamage, resolving.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.PassPriority, resolving.Prompts["P1"].EnabledActions());
        Assert.Equal(5, resolving.State.StackItems.Single().DamageDistribution!.ResolutionDamage);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, resolving.State.CardObjects[id].Damage));
        var done = await Act(resolving.State, "P1", Allocation(resolving.State, ("ENEMY1", 2), ("ENEMY2", 2), ("ENEMY3", 1)));
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Equal(new[] { 2, 2, 1 }, new[] { "ENEMY1", "ENEMY2", "ENEMY3" }.Select(id => done.State.CardObjects[id].Damage));
        Assert.Equal(3, done.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.DoesNotContain(done.Events, e => e.Kind == "PRIORITY_PASSED");
        Assert.Equal(9, done.State.CardObjects["HOST"].Power);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("duplicate")]
    [InlineData("new-target")]
    [InlineData("source")]
    [InlineData("under")]
    [InlineData("over")]
    [InlineData("stack")]
    [InlineData("opponent")]
    [InlineData("pass")]
    public async Task InvalidAllocationCannotChangeTargetsSpendResourcesOrAdvanceBattle(string change)
    {
        var attack = await Attack(Position()); var confirmed = await Confirm(attack.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var state = (await Top(confirmed.State)).State;
        var command = Allocation(state, ("ENEMY1", 2), ("ENEMY2", 2), ("ENEMY3", 1));
        var values = command.Assignments!.ToArray();
        values[0] = change switch {
            "zero" => values[0] with { Damage = 0 }, "negative" => values[0] with { Damage = -1 },
            "duplicate" => values[0] with { TargetObjectId = "ENEMY2" }, "new-target" => values[0] with { TargetObjectId = "HOST" },
            "source" => values[0] with { SourceObjectId = "GEAR" }, "under" => values[0] with { Damage = 1 },
            "over" => values[0] with { Damage = int.MaxValue }, _ => values[0] };
        command = command with { Assignments = values, StackItemId = change == "stack" ? "FORGED" : command.StackItemId };
        GameCommand submitted = change == "pass" ? new PassPriorityCommand() : command;
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("invalid-allocation", change == "opponent" ? "P2" : "P1", submitted.CmdType), submitted, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        Restore(rejected.State); ArmamentMightTests.PublicRecovery(rejected.State);
    }

    [Fact]
    public async Task AttachedPrintedCopyCreatesTwoIndependentAttackTriggersAndConfirmationWindows()
    {
        var attack = await Attack(Position(copied: true));
        Assert.Equal(2, attack.State.TriggerQueue.Count); Assert.Null(attack.State.PendingCardChoice);
        var ids = attack.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        var ordered = await Act(attack.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: Enumerable.Reverse(ids).ToArray()));
        Restore(ordered.State); ArmamentMightTests.PublicRecovery(ordered.State);
        var first = await Confirm(ordered.State, "ENEMY1");
        Assert.NotNull(first.State.PendingCardChoice); Assert.Null(first.State.PriorityPlayerId);
        var second = await Confirm(first.State, "ENEMY2", "ENEMY3");
        Assert.Equal(new[] { "ENEMY1" }, second.State.StackItems[0].TargetObjectIds);
        Assert.Equal(new[] { "ENEMY2", "ENEMY3" }, second.State.StackItems[1].TargetObjectIds);
        Assert.NotNull(second.State.StackItems[1].DamageDistribution!.Origin);
        var pending = await Top(second.State);
        Assert.Equal(second.State.StackItems[1].StackItemId, pending.State.PendingCardChoice!.ResolvingStackItemId);
        var done = await Act(pending.State, "P1", Allocation(pending.State, ("ENEMY2", 4), ("ENEMY3", 1)));
        Assert.Single(done.State.StackItems); Assert.Equal(new[] { "ENEMY1" }, done.State.StackItems[0].TargetObjectIds);
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task FlashingTwoConfirmedTargetsToBaseStillPreventsRepulseButLeavesOneAllocationTarget()
    {
        var initial = Spell(Position(), "FLASH", "OGS·011/024", "P2");
        var attack = await Attack(initial); var confirmed = await Confirm(attack.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var priority = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var flash = await Act(priority.State, "P2", new PlayCardCommand("FLASH", "OGS·011/024", ["ENEMY1", "ENEMY2"]));
        var moved = await Top(flash.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["ENEMY1"].Zone); Assert.Equal("BASE", moved.State.ObjectLocations["ENEMY2"].Zone);
        var ready = await Act(moved.State, "P1", new PassPriorityCommand());
        var counter = new PlayCardCommand("RESPONSE", "UNL-106/219", ["ENEMY3", ready.State.StackItems.Single().StackItemId]);
        var engine = new CoreRuleEngine(); Assert.False(engine.PreviewPlayCard(ready.State, "P2", PlayCostPreviewTests.Request(ready.State, counter)).CanPay);
        var rejected = await engine.ResolveAsync(ready.State, new("flash-repulse", "P2", counter.CmdType), counter, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(ready.State), MatchStateHasher.Hash(rejected.State));
        var pending = await Act(ready.State, "P2", new PassPriorityCommand());
        Assert.Equal(new[] { "ENEMY3" }, pending.State.PendingCardChoice!.LegalObjectIds);
        var done = await Act(pending.State, "P1", Allocation(pending.State, ("ENEMY3", 5)));
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage); Assert.Equal(0, done.State.CardObjects["ENEMY2"].Damage);
        Assert.Contains("ENEMY3", done.State.PlayerZones["P2"].Graveyard);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task TwoCruelRevivalCostsMoveTargetsToNonfieldAndPermitRepulseWithoutDestroyingTheAbilitySource()
    {
        var initial = Spell(Spell(Position(), "REVIVE1", "UNL-142/219", "P2"), "REVIVE2", "UNL-142/219", "P2");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "SCRAP", "OGN·001/298", "F", "P2");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Battlefields = initial.PlayerZones["P2"].Battlefields.Where(id => id != "SCRAP").ToArray(), Graveyard = ["SCRAP"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SCRAP"] = new("P2", "GRAVEYARD") } };
        var attack = await Attack(initial); var confirmed = await Confirm(attack.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var original = confirmed.State.StackItems.Single();
        var ready = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var first = await Act(ready.State, "P2", new PlayCardCommand("REVIVE1", "UNL-142/219", ["SCRAP"], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:ENEMY1"]));
        Restore(first.State); ArmamentMightTests.PublicRecovery(first.State);
        var second = await Act(first.State, "P2", new PlayCardCommand("REVIVE2", "UNL-142/219", ["SCRAP"], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:ENEMY2"]));
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
        Assert.Contains("ENEMY1", second.State.PlayerZones["P2"].Graveyard); Assert.Contains("ENEMY2", second.State.PlayerZones["P2"].Graveyard);
        var counter = new PlayCardCommand("RESPONSE", "UNL-106/219", ["ENEMY3", original.StackItemId]);
        var quote = new CoreRuleEngine().PreviewPlayCard(second.State, "P2", PlayCostPreviewTests.Request(second.State, counter));
        Assert.True(quote.CanPay, quote.Message);
        var done = await Top((await Act(second.State, "P2", counter)).State);
        Assert.DoesNotContain(done.State.StackItems, i => i.StackItemId == original.StackItemId);
        Assert.Equal("F", done.State.ObjectLocations["HOST"].BattlefieldObjectId); Assert.Equal(0, done.State.CardObjects["ENEMY3"].Damage);
        Assert.Single(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED" && Equals(e.Payload["stackObjectKind"], "ABILITY"));
        Assert.Equal(2, done.State.StackItems.Count);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task MovingSourceToBaseChangesHereAtResolutionAndDoesNotRetargetOriginalEnemies()
    {
        var attack = await Attack(Spell(Position(), "FLASH", "OGS·011/024", "P1"));
        var confirmed = await Confirm(attack.State, "ENEMY1", "ENEMY2", "ENEMY3");
        var moved = await Top((await Act(confirmed.State, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]))).State);
        Assert.Equal("BASE", moved.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("BASE", moved.State.StackItems.Single().DamageDistribution!.SourceLocation.Zone);
        var done = await Top(moved.State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, done.State.CardObjects[id].Damage));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AnnieAddsOneToTheWholePoolAndItsDepartureReducesTheNumberOfSurvivingChosenTargets()
    {
        var initial = Position();
        for (var n = 4; n <= 6; n++) initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY" + n, "OGN·001/298", "F", "P2");
        initial = At(OfficialHoldSequenceTests.AddUnit(initial, "ANNIE", "OGS·001/024", "F", "P1"), "ANNIE", "P1", "BASE");
        initial = Spell(initial, "SOUL", "SFD·163/221", "P1");
        var attack = await Attack(initial); Assert.Equal(6, attack.State.PendingCardChoice!.MaxCount);
        var confirmed = await Confirm(attack.State, Enumerable.Range(1, 6).Select(n => "ENEMY" + n).ToArray());
        Assert.Equal(6, confirmed.State.StackItems.Single().DamageAmount);
        var lost = await Top((await Act(confirmed.State, "P1", new PlayCardCommand("SOUL", "SFD·163/221", ["ANNIE", "HOST"]))).State);
        Assert.Contains("ANNIE", lost.State.PlayerZones["P1"].Graveyard);
        var pending = await Top(lost.State);
        Assert.Equal(6, pending.State.PendingCardChoice!.LegalObjectIds.Count); Assert.Equal(5, pending.State.PendingCardChoice.RequiredCount);
        Assert.Equal(5, pending.State.StackItems.Single().DamageDistribution!.ResolutionDamage);
        var done = await Act(pending.State, "P1", Allocation(pending.State, ("ENEMY1", 1), ("ENEMY2", 1), ("ENEMY3", 1), ("ENEMY4", 1), ("ENEMY5", 1)));
        Assert.Equal(5, done.Events.Where(e => e.Kind == "DAMAGE_APPLIED").Sum(e => (int)e.Payload["damage"]!));
        Assert.Equal(0, done.State.CardObjects["ENEMY6"].Damage);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("context")]
    [InlineData("budget")]
    [InlineData("source")]
    [InlineData("location")]
    [InlineData("early-amounts")]
    [InlineData("window")]
    [InlineData("legal")]
    [InlineData("count")]
    public async Task RecoveryRejectsForgedDistributionAndContinuation(string change)
    {
        var confirmed = await Confirm((await Attack(Position())).State, "ENEMY1", "ENEMY2", "ENEMY3");
        var state = (await Top(confirmed.State)).State; var item = state.StackItems.Single(); var context = item.DamageDistribution!;
        var choice = state.PendingCardChoice!;
        var forged = state with { StackItems = [item with { DamageDistribution = change switch {
            "context" => null, "budget" => context with { ResolutionDamage = 7 },
            "source" => context with { Source = new("GEAR", 0) },
            "location" => context with { SourceLocation = new("P1", "BATTLEFIELD", "OTHER") },
            "early-amounts" => context with { Assignments = Allocation(state, ("ENEMY1", 5)).Assignments }, _ => context } }],
            PendingCardChoice = change switch { "window" => choice with { ChoiceWindow = "TRIGGER_CONFIRMATION" },
                "legal" => choice with { LegalObjectIds = ["HOST"] }, "count" => choice with { RequiredCount = 1, MaxCount = 1 }, _ => choice } };
        var errors = OfficialInsightAndSpellLockTests.Errors(forged);
        Assert.Contains(errors, e => e.Contains("distributed damage", StringComparison.Ordinal) || e.Contains("effect damage distribution", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SourceDestroyedAfterConfirmationKeepsItsCapturedAbilityAndLastFieldLocation()
    {
        var initial = Spell(Position(), "SOUL", "SFD·163/221", "P1");
        initial = At(OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1"), "ALLY", "P1", "BASE");
        var confirmed = await Confirm((await Attack(initial)).State, "ENEMY1", "ENEMY2", "ENEMY3");
        var killed = await Top((await Act(confirmed.State, "P1", new PlayCardCommand("SOUL", "SFD·163/221", ["HOST", "ALLY"]))).State);
        Assert.Contains("HOST", killed.State.PlayerZones["P1"].Graveyard);
        Assert.True(killed.State.CardObjects["HOST"].ObjectGeneration > killed.State.StackItems.Single().DamageDistribution!.Source.Generation);
        // Current CN 359.3.f.2 and Vendetta FAQ: the ability survives, but
        // "here" needs the same source's current field location on execution.
        var done = await Top(killed.State);
        Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, done.State.CardObjects[id].Damage));
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Graveyard); Assert.Empty(done.State.StackItems);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("PAY")]
    [InlineData("DECLINE")]
    public async Task MultipleChosenSpellshieldTargetsRequireOneCombinedPaymentBeforeAnyResponse(string paymentChoice)
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] },
            ["ENEMY2"] = initial.CardObjects["ENEMY2"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var selected = await Confirm((await Attack(initial)).State, "ENEMY1", "ENEMY2", "ENEMY3");
        var payment = selected.State.PendingPayment!; Assert.NotNull(payment); Assert.Equal(4, payment.PowerCost);
        Assert.Null(selected.State.PriorityPlayerId); Assert.Null(selected.State.PendingCardChoice);
        var paid = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [paymentChoice]));
        if (paymentChoice == "PAY") {
            Assert.Single(paid.State.StackItems); Assert.Equal(initial.RunePools["P1"].Power - 4, paid.State.RunePools["P1"].Power);
            Assert.NotNull(paid.State.PriorityPlayerId); Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        } else {
            Assert.Empty(paid.State.StackItems); Assert.Equal(initial.RunePools["P1"], paid.State.RunePools["P1"]);
            Assert.DoesNotContain(paid.Events, e => e.Kind == "DAMAGE_APPLIED");
        }
        Restore(paid.State); ArmamentMightTests.PublicRecovery(paid.State);
    }

    [Theory]
    [InlineData("source-gone", "P2")]
    [InlineData("defenders-gone", "P1")]
    [InlineData("both-gone", null)]
    public async Task AnEntireSideLeavingDuringAttackResponsesClosesBattleWithoutCombatDamage(string scenario, string? winner)
    {
        var initial = Position();
        var onlyOneDefender = scenario != "source-gone";
        if (onlyOneDefender) {
            initial = At(initial, "ENEMY2", "P2", "BASE");
            initial = At(initial, "ENEMY3", "P2", "BASE");
        }
        initial = At(OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1"), "ALLY", "P1", "BASE");
        if (scenario != "defenders-gone") initial = Spell(initial, "SOUL", "SFD·163/221", "P1");
        if (scenario == "both-gone")
        {
            initial = At(OfficialHoldSequenceTests.AddUnit(initial, "P2-ALLY", "OGN·001/298", "F", "P2"), "P2-ALLY", "P2", "BASE");
            initial = Spell(initial, "P2-SOUL", "SFD·163/221", "P2");
            // Keep the destruction response's draw on an actual deck; an empty
            // fixture would burn out and move the just-destroyed unit to hand.
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["P2-DRAW"] = new("P2-DRAW", cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["P2-DRAW"] = new("P2", "MAIN_DECK") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P2"] = initial.PlayerZones["P2"] with { MainDeck = initial.PlayerZones["P2"].MainDeck.Prepend("P2-DRAW").ToArray() } }
            };
        }
        var current = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal("TRIGGER_CONFIRMATION", current.State.PendingCardChoice!.ChoiceWindow);
        current = await Confirm(current.State, onlyOneDefender ? ["ENEMY1"] : ["ENEMY1", "ENEMY2", "ENEMY3"]);
        if (scenario != "defenders-gone") {
            current = await Top((await Act(current.State, "P1", new PlayCardCommand("SOUL", "SFD·163/221", ["HOST", "ALLY"]))).State);
            Assert.Contains("HOST", current.State.PlayerZones["P1"].Graveyard);
        }
        if (scenario == "both-gone")
        {
            if (current.State.PriorityPlayerId != "P2")
                current = await Act(current.State, current.State.PriorityPlayerId!, new PassPriorityCommand());
            current = await Top((await Act(current.State, "P2", new PlayCardCommand("P2-SOUL", "SFD·163/221", ["ENEMY1", "P2-ALLY"]))).State);
            Assert.Contains(current.Events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload["targetObjectId"], "ENEMY1"));
            Assert.DoesNotContain("ENEMY1", current.State.PlayerZones["P2"].Battlefields);
        }
        current = await Top(current.State);
        if (scenario == "defenders-gone")
            current = await Act(current.State, "P1", Allocation(current.State, ("ENEMY1", 5)));
        else
        {
            Assert.Null(current.State.PendingCardChoice);
            Assert.DoesNotContain(current.Events, e => e.Kind == "DAMAGE_APPLIED");
        }
        var closed = await Top(current.State);
        Assert.False(closed.State.BattleState.IsActive);
        Assert.Null(closed.State.PendingCardChoice);
        Assert.DoesNotContain(closed.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Contains(closed.Events, e => e.Kind == "BATTLE_CLOSED");
        Assert.Equal(winner, closed.State.BattleResolutions.First().WinnerPlayerId);
        Assert.All(closed.State.CardObjects.Values, c => { Assert.False(c.IsAttacking); Assert.False(c.IsDefending); Assert.Equal(0, c.Damage); });
        Assert.Equal(winner, closed.State.CardObjects["F"].ControllerId);
        Restore(closed.State); ArmamentMightTests.PublicRecovery(closed.State);
    }

    [Theory]
    [InlineData("field")]
    [InlineData("unknown-unit")]
    [InlineData("empty-side")]
    [InlineData("duplicate-unit")]
    public async Task RecoveryRejectsForgedCapturedBattleDeclarations(string change)
    {
        const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
        var state = (await Attack(Position())).State;
        var marker = state.UntilEndOfTurnEffects.Single(e => e.StartsWith(prefix, StringComparison.Ordinal));
        var context = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker[prefix.Length..])))!;
        if (change == "field") context["BattlefieldId"] = "UNKNOWN";
        else context["AttackerObjectIds"] = JsonSerializer.SerializeToNode(change switch {
            "unknown-unit" => new[] { "UNKNOWN" }, "empty-side" => Array.Empty<string>(), _ => new[] { "ENEMY1" } });
        var forged = prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(context.ToJsonString()));
        var errors = OfficialInsightAndSpellLockTests.Errors(state with {
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Select(e => e == marker ? forged : e).ToArray() });
        Assert.Contains("invalid battle response declaration", errors);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("spectator")]
    public async Task PublicRecoveryRejectsAChangedCapturedDamagePool(string viewer)
    {
        var state = (await Confirm((await Attack(Position())).State, "ENEMY1", "ENEMY2", "ENEMY3")).State;
        var snapshots = ResolutionResult.BuildSnapshots(state).ToDictionary(e => e.Key, e => e.Value); var prompts = ResolutionResult.BuildPrompts(state);
        var frame = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var snapshot = viewer == "player" ? snapshots["P1"] : frame.SpectatorSnapshot;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var stack = snapshot.Stack!.ToArray(); var node = JsonNode.Parse(JsonSerializer.Serialize(stack[0], options))!;
        node["damageDistribution"]!["initialDamage"] = 99;
        stack[0] = JsonSerializer.SerializeToElement(node, options);
        if (viewer == "player") snapshots["P1"] = snapshot with { Stack = stack };
        else frame = frame with { SpectatorSnapshot = snapshot with { Stack = stack } };
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, frame);
        Assert.Contains((viewer == "player" ? "snapshot for P1" : "spectator replay frame") + " distributed damage context differs from authoritative state", errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepulseReferencesTheConfirmedGameObjectRatherThanAReenteredCardWithTheSameId(bool chosenReentered)
    {
        var state = (await Confirm((await Attack(Position())).State, "ENEMY1", "ENEMY2", "ENEMY3")).State;
        // A restored reference fixture: original ENEMY1 crossed a nonfield zone;
        // its reentered physical card is now generation 1 at the same battlefield.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY1"] = state.CardObjects["ENEMY1"] with { ObjectGeneration = 1, IsDefending = false } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = state.PlayerZones["P2"].Battlefields.Where(id => id != "ENEMY2" && (chosenReentered ? id != "ENEMY3" : true)).ToArray(),
                    Graveyard = chosenReentered ? ["ENEMY2", "ENEMY3"] : ["ENEMY2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["ENEMY2"] = new("P2", "GRAVEYARD"), ["ENEMY3"] = chosenReentered ? new("P2", "GRAVEYARD") : state.ObjectLocations["ENEMY3"] } };
        Restore(state); ArmamentMightTests.PublicRecovery(state);
        state = (await Act(state, "P1", new PassPriorityCommand())).State;
        var command = new PlayCardCommand("RESPONSE", "UNL-106/219", [chosenReentered ? "ENEMY1" : "ENEMY3", state.StackItems.Single().StackItemId]);
        var engine = new CoreRuleEngine(); var quote = engine.PreviewPlayCard(state, "P2", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(!chosenReentered, quote.CanPay);
        var result = await engine.ResolveAsync(state, new("reentered-repulse", "P2", command.CmdType), command, default);
        Assert.Equal(!chosenReentered, result.Accepted);
        if (chosenReentered) Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
