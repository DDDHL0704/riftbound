using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CopiedSpellTriggerTests
{
    [Theory]
    [InlineData("VEN·183", 1)]
    [InlineData("VEN·183", 2)]
    [InlineData("OGN·103/298", 1)]
    [InlineData("UNL-149/219", 1)]
    [InlineData("UNL-149a/219", 1)]
    public async Task EveryActivePrintedOrCopiedSpellPowerInstanceTriggersIndependently(string cardNo, int copies)
    {
        var state = AttachedRuleTextTests.Position(cardNo, 0);
        for (var i = 0; i < copies; i++) {
            var id = i == 0 ? "GEAR" : "SONG" + i;
            if (i > 0) state = AttachedRuleTextTests.AddSong(state, id);
            state = (await AttachedRuleTextTests.Attach(state, id)).State;
        }
        state = OfficialCopyIdentityTests.AddSpell(state, "SPELL", "OGN·048/298");
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var completed = await Top(played.State);
        var skills = completed.State.TriggerQueue.Count(t => t.SpellContext?.Kind == TriggerKinds.UnitSpellPlayedPowerModifier)
            + completed.State.StackItems.Count(t => t.SpellContext?.Kind == TriggerKinds.UnitSpellPlayedPowerModifier);
        Assert.Equal(copies + 1, skills);
        Restore(completed.State); ArmamentMightTests.PublicRecovery(completed.State);
        var ordered = await Order(completed.State);
        var contexts = ordered.StackItems.Where(i => i.SpellContext is not null).Select(i => i.SpellContext!).ToArray();
        Assert.Equal(copies + 1, contexts.Select(c => c.InstanceId).Distinct().Count());
        Assert.Equal(copies, contexts.Count(c => c.Origin is not null));
        var basePower = ordered.CardObjects["HOST"].Power;
        var delta = cardNo == "OGN·103/298" ? 1 : 2;
        for (var i = 0; i <= copies; i++) {
            var done = await Top(ordered);
            Assert.Equal(basePower + delta * (i + 1), done.State.CardObjects["HOST"].Power);
            Assert.Equal(copies - i, done.State.StackItems.Count);
            Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
            ordered = done.State;
        }
    }

    internal static async Task<MatchState> Order(MatchState state)
        => state.TriggerQueue.Count == 0 ? state : (await Act(state, "P1",
            new OrderTriggersCommand(state.TriggerQueue.Select(i => i.TriggerId).Reverse().ToArray()))).State;

    internal static async Task<MatchState> Attached(string card = "VEN·183")
        => (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position(card, 0))).State;

    [Theory]
    [InlineData("OGN·048/298", 0)]
    [InlineData("OGN·114/298", 2)]
    public async Task HighPrintedCostChecksEveryGrantedInstance(string spell, int expected)
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached("OGS·006/024"), "SPELL", spell);
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", spell, []));
        var completed = await Top(cast.State);
        Assert.Equal(expected, completed.State.TriggerQueue.Count + completed.State.StackItems.Count);
        var final = await Order(completed.State);
        while (final.StackItems.Count > 0) final = (await Top(final)).State;
        Assert.Equal(state.CardObjects["HOST"].Power + expected * 3, final.CardObjects["HOST"].Power);
        Restore(final); ArmamentMightTests.PublicRecovery(final);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetachingBeforeCompletionRemovesOnlyFutureCopiedTriggers(bool beforeCompletion)
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        state = beforeCompletion ? cast.State : await Order((await Top(cast.State)).State);
        var capturedIds = state.StackItems.Where(i => i.SpellContext?.Origin is not null).Select(i => i.SpellContext!.InstanceId).ToArray();
        var response = await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        var detached = await Top(response.State);
        Assert.Null(detached.State.CardObjects["GEAR"].AttachedRuleText);
        // The detach spell completes after removal and therefore sees only the
        // original text, while previously captured copied skills remain.
        if (!beforeCompletion) Assert.All(capturedIds, id => Assert.Contains(detached.State.StackItems, i => i.SpellContext?.InstanceId == id));
        state = await Order(detached.State);
        while (state.StackItems.Count > 0) {
            state = (await Top(state)).State;
            state = await Order(state);
            Restore(state); ArmamentMightTests.PublicRecovery(state);
        }
        Assert.Equal(beforeCompletion ? 7 : 9, state.CardObjects["HOST"].Power);
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("equipment")]
    [InlineData("generation")]
    [InlineData("revision")]
    [InlineData("source")]
    [InlineData("null-equipment")]
    [InlineData("null-unit")]
    public async Task CurrentRecoveryRejectsForgedCopiedSpellOrigins(string mutation)
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        state = await Order((await Top(cast.State)).State);
        var item = Assert.Single(state.StackItems, i => i.SpellContext?.Origin is not null);
        var context = item.SpellContext!; var origin = context.Origin!;
        var forged = mutation switch {
            "instance" => context with { InstanceId = "ATTACHED:OTHER:0:1" },
            "equipment" => context with { Origin = origin with { Equipment = new("HOST", 0) } },
            "generation" => context with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } },
            "revision" => context with { Origin = origin with { Revision = 99 } },
            "null-equipment" => context with { Origin = origin with { Equipment = null! } },
            "null-unit" => context with { Origin = origin with { Unit = null! } },
            _ => context with { SourceGeneration = 99 }
        };
        var invalid = state with { StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? i with { SpellContext = forged } : i).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(invalid));
    }

    [Fact]
    public async Task CurrentRecoveryCannotDuplicateOneCapturedInstanceForTheSameCompletedSpell()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        state = await Order((await Top(cast.State)).State);
        var item = Assert.Single(state.StackItems, i => i.SpellContext?.Origin is not null);
        var invalid = state with { StackItems = state.StackItems.Append(item with { StackItemId = "FORGED-DUPLICATE" }).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(invalid));
    }

    [Fact]
    public async Task ReattachingDoesNotRelabelSkillsCapturedFromTheEarlierTextVersion()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        state = OfficialCopyIdentityTests.AddSpell(state, "ATTACH", "SFD·011/221");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        state = await Order((await Top(cast.State)).State);
        var captured = Assert.Single(state.StackItems, i => i.SpellContext?.Origin is not null).SpellContext!;
        var revision = captured.Origin!.Revision;
        var detached = await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]))).State);
        var attach = await Act(await Order(detached.State), "P1", new PlayCardCommand("ATTACH", "SFD·011/221", ["HOST", "GEAR"]));
        var reattached = await Top(attach.State);
        Assert.True(reattached.State.CardObjects["GEAR"].AttachmentRevision > revision);
        Assert.Contains(reattached.State.StackItems, i => i.SpellContext == captured);
        Restore(reattached.State); ArmamentMightTests.PublicRecovery(reattached.State);
        state = await Order(reattached.State);
        while (state.StackItems.Count > 0) state = (await Top(state)).State;
        Assert.Equal(13, state.CardObjects["HOST"].Power);
        Restore(state);
    }

    [Fact]
    public async Task CounteredSpellDoesNotCaptureEitherOriginalOrCopiedInstances()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        state = AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·064/298", "P2");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var passed = await Act(cast.State, "P1", new PassPriorityCommand());
        var counter = await Act(passed.State, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [cast.State.StackItems.Single().StackItemId]));
        var final = await Top(counter.State);
        Assert.Empty(final.State.StackItems); Assert.Empty(final.State.TriggerQueue);
        Assert.Equal(3, final.State.CardObjects["HOST"].Power);
        Restore(final.State); ArmamentMightTests.PublicRecovery(final.State);
    }

    [Fact]
    public async Task EchoHasOneCompletedPlayButEachActiveTextInstanceStillTriggersOnce()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        state = LocalDestructionRecallTests.At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GATE"] = new("GATE", cardNo: "SFD·078/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) } }, "GATE", "P1", "BASE");
        state = (await Top((await Act(state, "P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []))).State)).State;
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·048/298", out var behavior));
        var echo = Assert.Single(EchoCostRules.Available(state, "P1", behavior));
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", [], OptionalCosts: [echo.Id]));
        Assert.Equal(2, cast.State.StackItems.Single().EffectRepeatCount);
        var completed = await Top(cast.State);
        Assert.Equal(2, completed.State.TriggerQueue.Count);
        state = await Order(completed.State);
        Assert.Equal(2, state.StackItems.Select(i => i.SpellContext!.InstanceId).Distinct().Count());
        while (state.StackItems.Count > 0) state = (await Top(state)).State;
        Assert.Equal(7, state.CardObjects["HOST"].Power);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task CurrentRecoveryRejectsAFutureOriginalSourceGeneration()
    {
        var state = OfficialCopyIdentityTests.AddSpell(AttachedRuleTextTests.Position("VEN·183", 0), "SPELL", "OGN·048/298");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        state = (await Top(cast.State)).State;
        var item = Assert.Single(state.StackItems); var context = item.SpellContext!;
        var forged = context with { SourceGeneration = 99, InstanceId = "PRINTED:HOST:99" };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { SpellContext = forged }] }));
    }

    [Fact]
    public async Task SourceReturningToHandCannotReceiveOldOriginalOrCopiedPowerSkills()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attached(), "SPELL", "OGN·048/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        state = await Order((await Top(cast.State)).State);
        var generation = state.CardObjects["HOST"].ObjectGeneration;
        var returned = await Top((await Act(state, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]))).State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        Assert.True(returned.State.CardObjects["HOST"].ObjectGeneration > generation);
        Assert.Equal(2, returned.State.StackItems.Count);
        state = returned.State; Restore(state); ArmamentMightTests.PublicRecovery(state);
        while (state.StackItems.Count > 0) {
            var done = await Top(state);
            Assert.DoesNotContain(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
            state = done.State; Restore(state); ArmamentMightTests.PublicRecovery(state);
        }
        Assert.Equal(0, state.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
    }
}
