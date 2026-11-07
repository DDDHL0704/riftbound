using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class NativeUnitActivationContinuityTests
{
    public static IEnumerable<object[]> Printings() => ColoredHeroActivationFeeTests.Printings();
    private static bool Xerath(string no) => no == "UNL-026/219";
    private static ActivateAbilityCommand Activate(string no, string source = "HOST", string target = "ENEMY")
        => new(source, Xerath(no) ? P4ActivatedAbilityCatalog.XerathDamageAbilityId
            : P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, Xerath(no) ? [target] : []);
    private static MatchState Position(string no)
        => AttackDamageDistributionTests.Spell(ColoredHeroActivationFeeTests.Position(no,
            ColoredHeroActivationFeeTests.RedPool(30, 10, 30)), "RETURN", "OGN·104/298", "P1");

    private static MatchState Reenter(MatchState state, string id, string player)
    {
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { ObjectGeneration = state.CardObjects[id].ObjectGeneration + 1 } },
            PlayerZones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value with {
                Hand = pair.Value.Hand.Where(card => card != id).ToArray() }) };
        return LocalDestructionRecallTests.At(state, id, player, "BASE");
    }

    private static void Invalid(MatchState state, StackItemState item)
        => Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item] }),
            error => error.Contains("native unit activation context is invalid", StringComparison.Ordinal));

    [Theory, MemberData(nameof(Printings))]
    public async Task RealReturnResponseKeepsTheSkillAndHidesTheSourcesNewPrivateIncarnation(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", Activate(no));
        var original = Assert.Single(game.State.StackItems).NativeUnitActivation!;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > original.Source.Generation);
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"],
            ResolutionResult.BuildSpectatorSnapshot(game.State) })
        {
            var shown = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal("HIDDEN", shown["sourceObjectId"]);
            Assert.Equal(no, shown["cardNo"]);
        }
        OfficialInsightAndSpellLockTests.Restore(game.State);
        var privateSource = MatchStateHasher.HashValue(game.State.CardObjects["HOST"]);
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(privateSource, MatchStateHasher.HashValue(game.State.CardObjects["HOST"]));
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Equal(Xerath(no) ? 3 : 0, game.State.CardObjects["ENEMY"].Damage);
        foreach (var entry in game.Events().Skip(firstEvent))
            foreach (var key in new[] { "sourceObjectId", "unitSourceObjectId" })
                Assert.NotEqual("HOST", entry.Event.Payload.GetValueOrDefault(key));
        await game.Replay();
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task ActualImageCopyCanActivateAndItsPaidSkillSurvivesTheTokenCeasing(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(
            AttackDamageDistributionTests.Spell(Position(no), "C", "UNL-200/219", "P1"), "READY", "UNL-009/219", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("C", "UNL-200/219", ["HOST"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:F", []));
        if (Xerath(no)) { await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", [image.ObjectId])); await game.Top(); }
        await game.Act("P1", Activate(no, image.ObjectId));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        var item = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<NativeUnitActivationContext>(item.NativeUnitActivation);
        var receipt = Assert.IsType<CeasedSelfAbilitySource>(context.CeasedSource);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, receipt.TokenFactoryCardNo);
        foreach (var forged in new[] {
            context with { CeasedSource = null }, context with { TokenFactoryCardNo = null },
            context with { CeasedSource = receipt with { Source = new(image.ObjectId, receipt.Source.Generation + 1) } },
            context with { CeasedSource = receipt with { CardNo = "OGN·001/298" } },
            context with { CeasedSource = receipt with { FaceRevision = receipt.FaceRevision + 1 } },
            context with { CeasedSource = receipt with { AcquiredTick = receipt.AcquiredTick + 1 } },
            context with { CeasedSource = receipt with { TokenFactoryCardNo = P6TokenFactoryCatalog.BombTokenCardNo } },
            context with { CeasedSource = receipt with { DepartedTick = receipt.AcquiredTick } },
            context with { CeasedSource = receipt with { DepartedTick = game.State.Tick + 1 } } })
            Invalid(game.State, item with { NativeUnitActivation = forged });
        OfficialInsightAndSpellLockTests.Restore(game.State);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        if (Xerath(no)) Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task TheSameFieldObjectStillReceivesItsSkillAfterChangingControllerAndLeavingTheBattlefield(string no)
    {
        var capture = new Driver(Position(no));
        await capture.Act("P1", Activate(no));
        await capture.Replay();
        // These two changes preserve the object generation. In particular,
        // Xerath's battlefield condition is not a resolution restriction.
        var changed = LocalDestructionRecallTests.At(capture.State, "HOST", "P2", "BASE");
        OfficialInsightAndSpellLockTests.Restore(changed);
        var done = await OfficialInsightAndSpellLockTests.ResolveTop(changed);
        Assert.Equal("P2", done.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(Xerath(no) ? 3 : 6, done.State.CardObjects["HOST"].Power);
        Assert.Equal(Xerath(no) ? 3 : 0, done.State.CardObjects["ENEMY"].Damage);
        OfficialInsightAndSpellLockTests.Restore(done.State);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task ANewFieldIncarnationDoesNotBecomeTheOldSkillsImplicitSource(string no)
    {
        var capture = new Driver(Position(no));
        await capture.Act("P1", Activate(no));
        await capture.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await capture.Top(); await capture.Replay();
        // Return is real; stage the later field-entry boundary with its new
        // generation while the already captured skill remains on the chain.
        var reentered = Reenter(capture.State, "HOST", "P1");
        var source = MatchStateHasher.HashValue(reentered.CardObjects["HOST"]);
        OfficialInsightAndSpellLockTests.Restore(reentered);
        var done = await OfficialInsightAndSpellLockTests.ResolveTop(reentered);
        Assert.Equal(source, MatchStateHasher.HashValue(done.State.CardObjects["HOST"]));
        Assert.Equal(Xerath(no) ? 3 : 0, done.State.CardObjects["ENEMY"].Damage);
        OfficialInsightAndSpellLockTests.Restore(done.State);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task XerathUsesTheSharedTargetGenerationBindingAfterARealReturnResponse()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("UNL-026/219"), "ENEMY-RETURN", "OGN·104/298", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 20) } };
        var capture = new Driver(initial);
        await capture.Act("P1", Activate("UNL-026/219"));
        var target = Assert.Single(capture.State.StackItems).TargetGenerations!["ENEMY"];
        await capture.Act("P1", new PassPriorityCommand());
        await capture.Act("P2", new PlayCardCommand("ENEMY-RETURN", "OGN·104/298", ["ENEMY"]));
        await capture.Top(); await capture.Replay();
        Assert.True(capture.State.CardObjects["ENEMY"].ObjectGeneration > target);
        var reentered = Reenter(capture.State, "ENEMY", "P2");
        OfficialInsightAndSpellLockTests.Restore(reentered);
        var done = await OfficialInsightAndSpellLockTests.ResolveTop(reentered);
        Assert.Equal(0, done.State.CardObjects["ENEMY"].Damage);
        OfficialInsightAndSpellLockTests.Restore(done.State);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task RecoveryRejectsMissingOrForgedNativeContextsAndExtraInstructions(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", Activate(no));
        var item = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<NativeUnitActivationContext>(item.NativeUnitActivation);
        foreach (var forged in new NativeUnitActivationContext?[] {
            null, context with { AbilityId = "OTHER" }, context with { Source = new("OTHER", 0) },
            context with { Source = context.Source with { Generation = context.Source.Generation + 1 } },
            context with { ControllerId = "P2" }, context with { CardNo = "OGN·001/298" },
            context with { FaceRevision = context.FaceRevision + 1 },
            context with { AcquiredTick = 0 }, context with { AcquiredTick = game.State.Tick + 1 },
            context with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo } })
            Invalid(game.State, item with { NativeUnitActivation = forged });
        foreach (var forged in new[] {
            item with { StackItemId = "forged" }, item with { DamageAmount = 99 }, item with { EffectRepeatCount = 2 },
            item with { OptionalCosts = ["FORGED"] }, item with { TargetObjectIds = ["HOST", "ENEMY"] },
            item with { Destination = "BASE" }, item with { EffectPlayCompleted = true },
            item with { TargetGenerations = Xerath(no) ? null : new Dictionary<string, long> { ["HOST"] = 0 } } })
            Invalid(game.State, forged);
        await game.Top(); await game.Replay();
    }
}
