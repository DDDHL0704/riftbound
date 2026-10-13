using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ShadySpectaclesTests
{
    [Fact]
    public void CurrentPrintingHasAnEquipmentPlayBehavior()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("VEN·137", out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsEquipment);
        Assert.Equal(4, behavior.ManaCost);
    }

    [Fact]
    public async Task TypedAssemblePaysPrintedManaAndOneTypedPower()
    {
        var state = ArmamentMightTests.Position("SFD·059/221");
        var mana = state.RunePools["P1"].Mana;
        var power = state.RunePools["P1"].TotalPower;
        var started = await Act(state, "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        Assert.Equal(mana - 1, started.State.RunePools["P1"].Mana);
        Assert.Equal(power - 1, started.State.RunePools["P1"].TotalPower);
    }

    internal static MatchState Position(string source = "SFD·050/221")
    {
        var state = AttachedRuleTextTests.Position("OGN·096/298", 0);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = new("GEAR", cardNo: "VEN·137", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) } };
        state = OfficialHoldSequenceTests.AddUnit(state, "COPY-SOURCE", source, "F", "P1");
        return LocalDestructionRecallTests.At(state, "COPY-SOURCE", "P1", "BASE");
    }

    internal static async Task<MatchState> Attach(MatchState state, string equipment = "GEAR", string source = "COPY-SOURCE")
    {
        var cast = await Act(state, "P1", new AssembleEquipmentCommand(equipment, "HOST", ["ASSEMBLE_YELLOW"]));
        var waiting = await Top(cast.State);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", waiting.State.PendingRuleChoice!.Request.Kind);
        Assert.Equal("HOST", waiting.State.PendingRuleChoice.Request.Preview!.CardObjects[equipment].AttachedToObjectId);
        Restore(waiting.State); ArmamentMightTests.PublicRecovery(waiting.State);
        var pending = waiting.State.PendingRuleChoice;
        return (await Act(waiting.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:" + source]))).State;
    }

    [Fact]
    public async Task RealAttachmentCopiesPrintedTraitsAndDetachmentRestoresTheBasis()
    {
        var initial = Position(); var state = await Attach(initial);
        Assert.Equal("SFD·050/221", state.CardObjects["HOST"].CardNo);
        Assert.Equal(initial.CardObjects["HOST"].ObjectGeneration, state.CardObjects["HOST"].ObjectGeneration);
        Assert.NotNull(state.CardObjects["HOST"].AttachmentUnitCopies);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        state = (await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]))).State)).State;
        Assert.Equal("OGN·096/298", state.CardObjects["HOST"].CardNo);
        Assert.Null(state.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Equal(initial.CardObjects["HOST"].Power, state.CardObjects["HOST"].Power);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task CopyKeepsReceiverModificationsAndDoesNotCopySourceModifications()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 3, UntilEndOfTurnPowerModifier = 2,
                IsExhausted = true, Tags = state.CardObjects["HOST"].Tags.Append(CardObjectTags.Boon).ToArray() },
            ["COPY-SOURCE"] = state.CardObjects["COPY-SOURCE"] with { Power = 17, UntilEndOfTurnPowerModifier = 12,
                GrantedTags = ["游走"], Tags = state.CardObjects["COPY-SOURCE"].Tags.Append("游走").ToArray() } } };
        var copied = await Attach(state); var host = copied.CardObjects["HOST"];
        // Printed 5, the receiver's temporary +2 and its existing +1 buff.
        Assert.Equal(8, host.Power); Assert.Equal(2, host.UntilEndOfTurnPowerModifier);
        Assert.True(host.IsExhausted); Assert.Contains(CardObjectTags.Boon, host.Tags);
        Assert.DoesNotContain("游走", host.Tags); Assert.Equal("P1", host.OwnerId); Assert.Equal("P1", host.ControllerId);
        Restore(copied); ArmamentMightTests.PublicRecovery(copied);
    }

    [Fact]
    public async Task GainedAttachmentAbilityCannotSeeTheEarlierAttachmentEvent()
    {
        var state = await Attach(Position("SFD·119/221"));
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue); Assert.Null(state.PendingPayment);
        state = CopiedAttachmentObserverTests.AddSword(state);
        var later = await EquipTestDriver.FinishAsync(await Act(state, "P1", new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"])));
        var observer = Assert.Single(later.State.StackItems);
        Assert.True(observer.AttachmentObserver!.CopiedInstance);
        Assert.Equal("GEAR", observer.AttachmentObserver.Origin!.Equipment.ObjectId);
        Restore(later.State); ArmamentMightTests.PublicRecovery(later.State);
    }

    [Fact]
    public async Task ReacquiringTheSameAzirFaceCreatesAFreshOncePerTurnInstance()
    {
        var state = await Attach(Position()); var first = CopiedActivatedAbilityTests.Copy(state);
        state = (await Top((await Act(state, "P1", first with { TargetObjectIds = ["COPY-SOURCE"] })).State)).State;
        Assert.Empty(CopiedActivatedAbilityTests.Requirements(state));
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        state = (await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]))).State)).State;
        Assert.Equal("OGN·096/298", state.CardObjects["HOST"].CardNo);
        state = await Attach(state); var fresh = CopiedActivatedAbilityTests.Copy(state);
        Assert.NotEqual(first.AbilityInstanceId, fresh.AbilityInstanceId);
        var old = await new CoreRuleEngine().ResolveAsync(state, new("old-copy", "P1", CommandTypes.ActivateAbility),
            first with { TargetObjectIds = ["COPY-SOURCE"] }, default);
        Assert.False(old.Accepted);
        state = (await Top((await Act(state, "P1", fresh with { TargetObjectIds = ["COPY-SOURCE"] })).State)).State;
        Assert.Empty(CopiedActivatedAbilityTests.Requirements(state)); Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task CapturedCopiedSpellSkillSurvivesDetachmentAndAffectsTheSameObject()
    {
        var state = await Attach(Position("VEN·183"));
        state = OfficialCopyIdentityTests.AddSpell(state, "SPELL", "OGN·048/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        state = (await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []))).State)).State;
        Assert.Equal(2, state.TriggerQueue.Count);
        state = await CopiedSpellTriggerTests.Order(state);
        var skill = Assert.Single(state.StackItems, item => item.SourceObjectId == "HOST"); Assert.NotNull(skill.SpellContext!.Origin);
        state = (await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]))).State)).State;
        Assert.Equal("OGN·096/298", state.CardObjects["HOST"].CardNo);
        Assert.Contains(state.StackItems, item => item.StackItemId == skill.StackItemId);
        state = await CopiedSpellTriggerTests.Order(state);
        Assert.Equal(3, state.StackItems.Count);
        while (state.StackItems.Count > 0) state = (await Top(state)).State;
        Assert.Equal(3, state.CardObjects["HOST"].Power);
        Assert.Equal(7, state.CardObjects["COPY-SOURCE"].Power);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("SFD·030/221", "ASSEMBLE_RED")]
    [InlineData("SFD·059/221", "ASSEMBLE_GREEN")]
    [InlineData("SFD·090/221", "ASSEMBLE_BLUE")]
    [InlineData("SFD·118/221", "ASSEMBLE_ORANGE")]
    [InlineData("SFD·118a/221·P", "ASSEMBLE_ORANGE")]
    [InlineData("UNL-019/219", "ASSEMBLE_RED")]
    [InlineData("VEN·137", "ASSEMBLE_YELLOW")]
    public async Task EveryListedTypedManaAssemblePaysOneManaAndOnePower(string card, string cost)
    {
        var state = ArmamentMightTests.Position(card);
        var done = await Act(state, "P1", new AssembleEquipmentCommand("GEAR", "HOST", [cost]));
        Assert.Equal(19, done.State.RunePools["P1"].Mana);
        Assert.Equal(19, done.State.RunePools["P1"].TotalPower);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ReturningTheCopiedUnitRestoresItsPhysicalCardBeforeReentry()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Attach(Position()), "RETURN", "OGN·104/298");
        state = (await Top((await Act(state, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]))).State)).State;
        Assert.Contains("HOST", state.PlayerZones["P1"].Hand);
        Assert.Equal("OGN·096/298", state.CardObjects["HOST"].CardNo);
        Assert.Equal(1, state.CardObjects["HOST"].ObjectGeneration);
        Assert.Null(state.CardObjects["HOST"].AttachmentUnitCopies); Assert.Null(state.CardObjects["GEAR"].AttachedToObjectId);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
        state = (await Act(state, "P1", new PlayCardCommand("HOST", "OGN·096/298", []))).State;
        Assert.Equal(2, state.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal("OGN·096/298", state.CardObjects["HOST"].CardNo);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task DestroyingTheGearCanMakeTheRestoredUnitLethal()
    {
        var state = await Attach(Position());
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Damage = 2, DamageMarkedByPlayerIds = ["P2"] } } };
        state = OfficialCopyIdentityTests.AddSpell(state, "DESTROY", "SFD·005/221");
        var done = await Top((await Act(state, "P1", new PlayCardCommand("DESTROY", "SFD·005/221", ["GEAR"]))).State);
        Assert.Contains("GEAR", done.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Graveyard);
        Assert.Null(done.State.CardObjects["HOST"].AttachmentUnitCopies);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualFourManaEquipmentPlayEntersWithoutApplyingAnyCopyYet()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { ManaCost = 4 } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "GEAR").ToArray(), Hand = ["GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P1", "HAND") } };
        var played = await Act(state, "P1", new PlayCardCommand("GEAR", "VEN·137", []));
        Assert.Equal(16, played.State.RunePools["P1"].Mana); Assert.Contains("GEAR", played.State.PlayerZones["P1"].Base);
        Assert.Equal("OGN·096/298", played.State.CardObjects["HOST"].CardNo);
        Assert.Null(played.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EndingOneOfTwoCopyEffectsKeepsTheOtherAndEventuallyRestoresTheBasis(bool topFirst)
    {
        var state = await Attach(Position());
        state = AttachedRuleTextTests.AddSong(state, "SECOND-GEAR");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND-GEAR"] = state.CardObjects["SECOND-GEAR"] with { CardNo = "VEN·137" } } };
        state = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(state, "SECOND-SOURCE", "OGN·001/298", "F", "P1"), "SECOND-SOURCE", "P1", "BASE");
        state = await Attach(state, "SECOND-GEAR", "SECOND-SOURCE");
        Assert.Equal("OGN·001/298", state.CardObjects["HOST"].CardNo);
        Assert.Equal(2, state.CardObjects["HOST"].AttachmentUnitCopies!.Effects.Count);
        var order = topFirst ? new[] { "SECOND-GEAR", "GEAR" } : ["GEAR", "SECOND-GEAR"];
        for (var i = 0; i < 2; i++) {
            state = OfficialCopyIdentityTests.AddSpell(state, "DETACH" + i, "SFD·011/221");
            state = (await Top((await Act(state, "P1", new PlayCardCommand("DETACH" + i, "SFD·011/221", ["HOST", order[i]]))).State)).State;
            Assert.Equal(i == 1 ? "OGN·096/298" : topFirst ? "SFD·050/221" : "OGN·001/298", state.CardObjects["HOST"].CardNo);
            Restore(state); ArmamentMightTests.PublicRecovery(state);
        }
        Assert.Null(state.CardObjects["HOST"].AttachmentUnitCopies); Assert.Equal(1, state.CardObjects["HOST"].Power);
    }

    [Fact]
    public async Task CopiedSandSoldierKeepsIntrinsicTokenIdentityAndCeasesToExistOnReturn()
    {
        var state = Position();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.SandSoldierTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = factory.CreateObject("HOST", "P1", "P1") } };
        state = await Attach(state);
        Assert.True(TokenObjectRules.IsToken(state.CardObjects["HOST"]));
        Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, state.CardObjects["HOST"].TokenFactoryCardNo);
        state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        state = (await Top((await Act(state, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]))).State)).State;
        Assert.False(state.CardObjects.ContainsKey("HOST")); Assert.DoesNotContain("HOST", state.PlayerZones["P1"].Hand);
        Assert.Null(state.CardObjects["GEAR"].AttachedToObjectId); Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task WithNoOtherFriendlyUnitTheAttachmentCompletesWithoutAReplacementChoice()
    {
        var state = Position();
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
            Base = state.PlayerZones["P1"].Base.Where(id => id != "COPY-SOURCE").ToArray() } },
            CardObjects = state.CardObjects.Where(pair => pair.Key != "COPY-SOURCE").ToDictionary(pair => pair.Key, pair => pair.Value),
            ObjectLocations = state.ObjectLocations.Where(pair => pair.Key != "COPY-SOURCE").ToDictionary(pair => pair.Key, pair => pair.Value) };
        var done = await Top((await Act(state, "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]))).State);
        Assert.Null(done.State.PendingRuleChoice); Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("OGN·096/298", done.State.CardObjects["HOST"].CardNo); Assert.Null(done.State.CardObjects["HOST"].AttachmentUnitCopies);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("SFD·059/221", "ASSEMBLE_GREEN", 0, "green")]
    [InlineData("VEN·137", "ASSEMBLE_YELLOW", 0, "yellow")]
    [InlineData("VEN·137", "ASSEMBLE_YELLOW", 1, "green")]
    public async Task MissingPrintedManaOrWrongTypedPowerRejectsWithoutMutation(string card, string cost, int mana, string trait)
    {
        var state = ArmamentMightTests.Position(card);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [trait] = 1 }) } };
        var done = await new CoreRuleEngine().ResolveAsync(state, new("unpayable", "P1", CommandTypes.AssembleEquipment),
            new AssembleEquipmentCommand("GEAR", "HOST", [cost]), default);
        Assert.False(done.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(done.State));
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("basis-missing")]
    [InlineData("power")]
    [InlineData("tags")]
    [InlineData("equipment")]
    [InlineData("revision")]
    [InlineData("face-version")]
    [InlineData("duplicate")]
    [InlineData("preview")]
    [InlineData("null-equipment")]
    [InlineData("missing-equipment-id")]
    public async Task RecoveryRejectsForgedAttachmentCopyOrReplacementPreview(string mutation)
    {
        if (mutation == "preview") {
            var opened = await Act(Position(), "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
            var waiting = (await Top(opened.State)).State; var pending = waiting.PendingRuleChoice!;
            var preview = pending.Request.Preview!;
            var forgedPreview = preview with { CardObjects = new Dictionary<string, CardObjectState>(preview.CardObjects) {
                ["HOST"] = preview.CardObjects["HOST"] with { Power = 99 } } };
            var forged = waiting with { PendingRuleChoice = pending with { Request = pending.Request with { Preview = forgedPreview } } };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged)); return;
        }
        var state = await Attach(Position()); var host = state.CardObjects["HOST"]; var ledger = host.AttachmentUnitCopies!;
        var copy = Assert.Single(ledger.Effects);
        var changed = mutation switch {
            "basis" => ledger with { Basis = ledger.Basis with { CardNo = "OGN·001/298" } },
            "basis-missing" => ledger with { BasisFaceRevision = null },
            "power" => ledger with { Effects = [copy with { Traits = copy.Traits with { Power = 99 } }] },
            "tags" => ledger with { Effects = [copy with { Traits = copy.Traits with { Tags = ["游走"] } }] },
            "equipment" => ledger with { Effects = [copy with { Equipment = new("HOST", 0) }] },
            "revision" => ledger with { Effects = [copy with { Revision = 99 }] },
            "face-version" => ledger with { Effects = [copy with { FaceRevision = 99 }] },
            "duplicate" => ledger with { Effects = [copy, copy] },
            "null-equipment" => ledger with { Effects = [copy with { Equipment = null! }] },
            "missing-equipment-id" => ledger with { Effects = [copy with { Equipment = new(null!, 0) }] },
            _ => ledger with { BasisCapturedTick = state.Tick + 1 }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = host with { AttachmentUnitCopies = changed } } }));
    }
}
