using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PaidSpellReadyTests
{
    internal static async Task<MatchState> Position(int copies = 0)
    {
        var state = AttachedRuleTextTests.Position("UNL-005/219", 0);
        for (var i = 0; i < copies; i++) {
            var id = i == 0 ? "GEAR" : "SONG" + i;
            if (i > 0) state = AttachedRuleTextTests.AddSong(state, id);
            state = (await AttachedRuleTextTests.Attach(state, id)).State;
        }
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true } } };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ActualFourManaCompletesIntoIndependentReadySkills(int copies)
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(copies), "SPELL", "OGN·083/298");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·083/298", []));
        Assert.Equal(new CardPlayCostReceipt(4, 4), cast.State.StackItems.Single().PlayCost);
        Assert.True(cast.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(cast.State.TriggerQueue);
        var completed = await Top(cast.State);
        Assert.Equal(copies + 1, completed.State.TriggerQueue.Count + completed.State.StackItems.Count);
        Assert.True(completed.State.CardObjects["HOST"].IsExhausted);
        state = await CopiedSpellTriggerTests.Order(completed.State);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
        for (var i = 0; i <= copies; i++) {
            var ready = await Top(state);
            Assert.False(ready.State.CardObjects["HOST"].IsExhausted);
            Assert.Equal(7, ready.State.CardObjects["HOST"].Power);
            Assert.Equal(copies - i, ready.State.StackItems.Count);
            Restore(ready.State); ArmamentMightTests.PublicRecovery(ready.State);
            state = ready.State;
        }
    }

    [Theory]
    [InlineData("OGN·048/298", 2, false)]
    [InlineData("OGN·083/298", 4, true)]
    [InlineData("OGN·114/298", 6, true)]
    public async Task ActualPaidManaControlsTheThreshold(string spell, int paid, bool qualifies)
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(), "SPELL", spell);
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", spell, []));
        Assert.Equal(paid, cast.State.StackItems.Single().PlayCost!.PaidMana);
        var done = await Top(cast.State);
        Assert.Equal(qualifies ? 1 : 0, done.State.StackItems.Count);
        if (qualifies) done = await Top(done.State);
        Assert.Equal(!qualifies, done.State.CardObjects["HOST"].IsExhausted);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task RealDrakeReductionDoesNotUseHighPrintedManaAsPaidMana()
    {
        var state = MightyExperienceFrameTests.HandUnit(await Position(), "DRAKE", "OGN·031/298");
        state = (await Act(state, "P1", new PlayCardCommand("DRAKE", "OGN·031/298", []))).State;
        Assert.Single(state.StackItems);
        state = (await Top(state)).State;
        Assert.Contains(state.UntilEndOfTurnEffects, s => s.StartsWith("RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:", StringComparison.Ordinal));
        state = OfficialCopyIdentityTests.AddSpell(state, "SPELL", "OGN·114/298");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·114/298", []));
        Assert.Equal(new CardPlayCostReceipt(6, 1), cast.State.StackItems.Single().PlayCost);
        var done = await Top(cast.State);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue);
        Assert.True(done.State.CardObjects["HOST"].IsExhausted);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task RealGrantedEchoPaysFourButCompletesOnlyOnePlay()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(1), "SPELL", "OGN·048/298");
        state = LocalDestructionRecallTests.At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GATE"] = new("GATE", cardNo: "SFD·078/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) } }, "GATE", "P1", "BASE");
        state = (await Top((await Act(state, "P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []))).State)).State;
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·048/298", out var behavior));
        var echo = Assert.Single(EchoCostRules.Available(state, "P1", behavior));
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", [], OptionalCosts: [echo.Id]));
        Assert.Equal(new CardPlayCostReceipt(2, 4), cast.State.StackItems.Single().PlayCost);
        Assert.Equal(2, cast.State.StackItems.Single().EffectRepeatCount);
        state = (await Top(cast.State)).State;
        Assert.Equal(2, state.TriggerQueue.Count);
        state = await CopiedSpellTriggerTests.Order(state);
        for (var i = 0; i < 2; i++) state = (await Top(state)).State;
        Assert.False(state.CardObjects["HOST"].IsExhausted);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task CounteredSpellNeverCapturesReadySkills()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(1), "SPELL", "OGN·083/298");
        state = AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·064/298", "P2");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·083/298", []));
        state = (await Act(cast.State, "P1", new PassPriorityCommand())).State;
        state = (await Act(state, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [cast.State.StackItems.Single().StackItemId]))).State;
        state = (await Top(state)).State;
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue);
        Assert.True(state.CardObjects["HOST"].IsExhausted);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task ReturningSourceDoesNotReadyTheDifferentHandObject()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(1), "SPELL", "OGN·083/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        state = await CopiedSpellTriggerTests.Order((await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·083/298", []))).State)).State);
        state = (await Top((await Act(state, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]))).State)).State;
        Assert.Contains("HOST", state.PlayerZones["P1"].Hand);
        Assert.Equal(1, state.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(2, state.StackItems.Count);
        for (var i = 0; i < 2; i++) {
            var done = await Top(state);
            Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_READIED");
            state = done.State; Restore(state); ArmamentMightTests.PublicRecovery(state);
        }
    }

    [Theory]
    [InlineData("below-threshold")]
    [InlineData("future-source")]
    [InlineData("duplicate")]
    public async Task RecoveryRejectsForgedReadyCapture(string mutation)
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(1), "SPELL", "OGN·083/298");
        state = await CopiedSpellTriggerTests.Order((await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·083/298", []))).State)).State);
        var item = Assert.Single(state.StackItems, i => i.SpellContext?.Origin is not null);
        var context = item.SpellContext!;
        var forged = mutation == "below-threshold" ? context with { Cost = context.Cost with { PaidMana = 3 } }
            : context with { SourceGeneration = 99 };
        var invalid = mutation == "duplicate" ? state with { StackItems = state.StackItems.Append(item with { StackItemId = "FORGED-DUPLICATE" }).ToArray() }
            : state with { StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? i with { SpellContext = forged } : i).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(invalid));
    }

    [Fact]
    public async Task AlreadyReadySourceStillCapturesItsMandatoryAbility()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Position(), "SPELL", "OGN·083/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = false } } };
        state = (await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·083/298", []))).State)).State;
        Assert.Single(state.StackItems);
        var done = await Top(state);
        Assert.Single(done.Events, e => e.Kind == "UNIT_READIED");
        Assert.False(done.State.CardObjects["HOST"].IsExhausted);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task EnemyPaidSpellDoesNotCaptureYourSourceInstances()
    {
        var state = AttackDamageDistributionTests.Spell(await Position(1), "SPELL", "OGN·083/298", "P2");
        state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2", PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) } };
        var done = await Top((await Act(state, "P2", new PlayCardCommand("SPELL", "OGN·083/298", []))).State);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue);
        Assert.True(done.State.CardObjects["HOST"].IsExhausted);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
