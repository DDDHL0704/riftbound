using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AzirReferenceConfirmationTests
{
    [Fact]
    public async Task ExistingChosenArmamentIsPublishedAsSecondTargetBeforeResponse()
    {
        var result = await Act(AzirMovementAuditTests.AzirPosition(), "P1", AzirMovementAuditTests.Command(true));
        Assert.Equal(new[] { "TARGET", "GEAR" }, result.State.StackItems.Single().TargetObjectIds);
        Assert.Equal("TARGET", result.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotNull(result.State.PriorityPlayerId);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task OmittingMandatoryWeaponReferenceCannotConsumeCostOrOnceUse()
    {
        var s = AzirMovementAuditTests.AzirPosition();
        var result = await new CoreRuleEngine().ResolveAsync(s, new("missing-equipment-reference", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["TARGET"], []), default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ChosenOptionalReattachIsOfferedOnlyAfterMovements()
    {
        var result = await Act(AzirMovementAuditTests.AzirPosition(), "P1", AzirMovementAuditTests.Command(true));
        result = await Top(result.State);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal("AZIR_ARMAMENT_ATTACHMENT", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "GEAR" }, choice.LegalObjectIds);
        Assert.Equal("OTHER", result.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", result.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("TARGET", result.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(result.State.PriorityPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalDecisionKeepsConfirmedTargetsAndDoesNotMoveUnitsAgain(bool attach)
    {
        var activated = await Act(AzirMovementAuditTests.AzirPosition(), "P1", AzirMovementAuditTests.Command());
        var moved = await Top(activated.State);
        Restore(moved.State); ArmamentMightTests.PublicRecovery(moved.State);
        var done = await AzirTestDriver.Finish(moved, attach);
        Assert.Equal(attach ? "HOST" : "TARGET", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(1, done.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal(1, done.State.CardObjects["TARGET"].MovementCountThisTurn);
        Assert.Equal(2, done.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(attach ? 1 : 0, done.Events.Count(e => e.Kind == "EQUIPMENT_ATTACHED"));
        Assert.Equal(1, done.Events.Count(e => e.Kind == "TRIGGER_QUEUED"));
        var resolved = Assert.Single(done.Events, e => e.Kind == "ABILITY_RESOLVED");
        Assert.Equal(attach, resolved.Payload!["armamentReattachChosen"]);
        Assert.Equal(new[] { "TARGET", "GEAR" }, resolved.Payload["targetObjectIds"]);
    }

    [Fact]
    public async Task UnitWithoutArmamentNeedsOnlyItsOwnReferenceAndNoLaterAttachmentChoice()
    {
        var s = AzirMovementAuditTests.AzirPosition();
        s = LocalDestructionRecallTests.At(s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = null }
        }}, "GEAR", "P1", "BASE");
        var activated = await Act(s, "P1", new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["TARGET"], []));
        Assert.Equal(new[] { "TARGET" }, activated.State.StackItems.Single().TargetObjectIds);
        var done = await Top(activated.State);
        Assert.Null(done.State.PendingCardChoice);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("other-object")]
    [InlineData("opponent")]
    [InlineData("pass")]
    [InlineData("activate")]
    public async Task LaterDecisionRejectsChangingTheReferenceOrTakingAnotherAction(string change)
    {
        var moved = await Top((await Act(AzirMovementAuditTests.AzirPosition(), "P1", AzirMovementAuditTests.Command())).State);
        var s = moved.State; var choice = s.PendingCardChoice!;
        GameCommand command = change switch {
            "pass" => new PassPriorityCommand(),
            "activate" => AzirMovementAuditTests.Command(),
            _ => new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [change == "other-object" ? "HOST" : "GEAR"])
        };
        var result = await new CoreRuleEngine().ResolveAsync(s, new("invalid-late-choice", change == "opponent" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State));
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Theory]
    [InlineData("legal")]
    [InlineData("context")]
    [InlineData("count")]
    [InlineData("window")]
    [InlineData("stack")]
    [InlineData("decision")]
    [InlineData("missing")]
    public async Task RecoveryRejectsForgedOptionalContinuation(string change)
    {
        var s = (await Top((await Act(AzirMovementAuditTests.AzirPosition(), "P1", AzirMovementAuditTests.Command())).State)).State;
        Restore(s); var choice = s.PendingCardChoice!; var item = s.StackItems.Last();
        s = change switch {
            "legal" => s with { PendingCardChoice = choice with { LegalObjectIds = ["HOST"] } },
            "context" => s with { PendingCardChoice = choice with { ContextObjectIds = ["HOST", "TARGET"] } },
            "count" => s with { PendingCardChoice = choice with { RequiredCount = 1 } },
            "window" => s with { PendingCardChoice = choice with { ChoiceWindow = "TRIGGER_CONFIRMATION" } },
            "stack" => s with { PendingCardChoice = choice with { ResolvingStackItemId = "OTHER" } },
            "decision" => s with { StackItems = [item with { AzirMovement = item.AzirMovement! with { AttachmentChosen = true } }] },
            _ => s with { PendingCardChoice = null }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s));
    }

    [Theory]
    [InlineData("armament")]
    [InlineData("unit")]
    public async Task RecoveryRejectsAConsistentReferenceToANonArmamentObject(string reference)
    {
        var s = (await Act(AzirMovementAuditTests.AzirPosition(), "P1", AzirMovementAuditTests.Command())).State;
        var item = s.StackItems.Single();
        var unitId = reference == "unit" ? "OTHER" : "TARGET";
        var gearId = reference == "armament" ? "OTHER" : "GEAR";
        s = s with { StackItems = [item with { TargetObjectIds = [unitId, gearId],
            TargetGenerations = new Dictionary<string, long> { [unitId] = s.CardObjects[unitId].ObjectGeneration, [gearId] = s.CardObjects[gearId].ObjectGeneration },
            AzirMovement = item.AzirMovement! with { Equipment = new(gearId, s.CardObjects[gearId].ObjectGeneration) } }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s), error => error.Contains("Azir"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrintedAndCopiedInstancesUseTheSameTwoReferencesAndLaterDecision(bool copied)
    {
        var s = await CopiedActivatedAbilityTests.Position();
        s = LocalDestructionRecallTests.At(s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR2"] = new("GEAR2", cardNo: "SFD·022/221", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: "TARGET")
        }}, "GEAR2", "P1", "BASE");
        var command = (copied ? CopiedActivatedAbilityTests.Copy(s) : CopiedActivatedAbilityTests.Native())
            with { TargetObjectIds = ["TARGET", "GEAR2"] };
        var activated = await Act(s, "P1", command);
        var item = activated.State.StackItems.Single(); Assert.Equal(copied, item.AzirMovement!.CopiedInstance);
        Assert.Equal(new[] { "TARGET", "GEAR2" }, item.TargetObjectIds);
        var moved = await Top(activated.State); Restore(moved.State); ArmamentMightTests.PublicRecovery(moved.State);
        var done = await AzirTestDriver.Finish(moved, true);
        Assert.Equal("HOST", done.State.CardObjects["GEAR2"].AttachedToObjectId);
        var other = Assert.Single(CopiedActivatedAbilityTests.Requirements(done.State));
        Assert.Equal(!copied, other.GetValueOrDefault("abilityInstanceId") is not null);
    }
}
