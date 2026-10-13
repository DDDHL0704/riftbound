using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AzirMovementAuditTests
{
    internal static MatchState AzirPosition(bool samePlace = false, string azir = "SFD·050/221")
    {
        var s = At(Position(unit: azir, power: 6), "HOST", "P1", "BATTLEFIELD", "F");
        s = OfficialHoldSequenceTests.AddUnit(s, "TARGET", "SFD·130/221", samePlace ? "F" : "OTHER", "P1");
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["TARGET"] = s.CardObjects["TARGET"] with { Power = 1 },
                ["OTHER"] = s.CardObjects["OTHER"] with { ControllerId = "P1" },
                ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = "TARGET" }
            }, RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 })
            }
        };
        return At(s, "GEAR", "P1", "BATTLEFIELD", samePlace ? "F" : "OTHER");
    }

    internal static ActivateAbilityCommand Command(bool attach = false) => new("HOST",
        P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["TARGET", "GEAR"], []);

    internal static async Task<MatchState> OpponentFocusPosition()
    {
        var s = OfficialHoldSequenceTests.AddUnit(AzirPosition(), "DEF", "OGN·001/298", "OTHER", "P2");
        s = At(s, "DEF", "P2", "BASE") with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var moved = await Act(s, "P2", new MoveUnitCommand("DEF", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(TimingStates.SpellDuelOpen, moved.State.TimingState);
        var focus = await Act(moved.State, "P2", new PassFocusCommand());
        Assert.Equal("P1", focus.State.FocusPlayerId);
        return focus.State;
    }

    [Fact]
    public async Task RealOpponentMoveCreatesTheDuelInWhichAzirCanActivate()
    {
        var s = await OpponentFocusPosition(); Restore(s); PublicRecovery(s);
        var activated = await Act(s, "P1", Command(true));
        Assert.Equal("P2", activated.State.TurnPlayerId);
        Assert.Equal(TimingStates.SpellDuelClosed, activated.State.TimingState);
        var done = await AttachTop(activated.State);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("SFD·050/221")]
    [InlineData("SFD·050a/221")]
    public async Task SameLocationDoesNotMoveButStillExecutesTheChosenAttachment(string card)
    {
        var activated = await Act(AzirPosition(true, card), "P1", Command(true));
        Assert.Equal("TARGET", activated.State.CardObjects["GEAR"].AttachedToObjectId);
        var done = await AttachTop(activated.State);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal(0, done.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal(0, done.State.CardObjects["TARGET"].MovementCountThisTurn);
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD" or "TRIGGER_QUEUED");
        Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task UnselectedEquipmentFollowsItsOriginalHostAndActualMovementIsCaptured()
    {
        var activated = await Act(AzirPosition(), "P1", Command());
        var done = await Top(activated.State);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("TARGET", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F", done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(1, done.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal(1, done.State.CardObjects["TARGET"].MovementCountThisTurn);
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_QUEUED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task SwiftAbilityIsOfferedAndAcceptedAtFocusOnTheOpponentsTurn()
    {
        var s = AzirPosition() with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1", PriorityPlayerId = null };
        var prompt = ResolutionResult.BuildPrompts(s)["P1"];
        Assert.Contains(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility && c.Enabled);
        var activated = await Act(s, "P1", Command());
        Assert.Single(activated.State.StackItems);
        Assert.Equal("TARGET", activated.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(activated.Events, e => e.Kind == "UNIT_LOCATIONS_SWAPPED");
        Assert.Equal(TimingStates.SpellDuelClosed, activated.State.TimingState);
        Restore(activated.State); PublicRecovery(activated.State);
    }

    [Theory]
    [InlineData("HOST")]
    [InlineData("TARGET")]
    public async Task AProhibitedMoveDoesNotCancelTheOtherMoveOrChosenAttachment(string restricted)
    {
        var s = AzirPosition();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            [restricted] = s.CardObjects[restricted] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1")] }
        }};
        var activated = await Act(s, "P1", Command(true)); var done = await AttachTop(activated.State);
        Assert.Equal(restricted == "HOST" ? "F" : "OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(restricted == "TARGET" ? "OTHER" : "F", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal(0, done.State.CardObjects[restricted].MovementCountThisTurn);
        Assert.Equal(1, done.State.CardObjects[restricted == "HOST" ? "TARGET" : "HOST"].MovementCountThisTurn);
        Assert.Single(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualFlashChangesTheOriginUsedByThePendingAbility()
    {
        var s = EquipChainTests.Response(AzirPosition(), "OGS·011/024");
        var activated = await Act(s, "P1", Command());
        var flash = await Act(activated.State, "P1", new PlayCardCommand("RESPONSE", "OGS·011/024", ["HOST"]));
        var moved = await Top(flash.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["HOST"].Zone);
        Assert.Single(moved.State.StackItems); Restore(moved.State); PublicRecovery(moved.State);
        var done = await Top(moved.State);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("BASE", done.State.ObjectLocations["TARGET"].Zone);
        Assert.Equal("BASE", done.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal(2, done.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal(1, done.State.CardObjects["TARGET"].MovementCountThisTurn);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("HOST")]
    [InlineData("TARGET")]
    [InlineData("GEAR")]
    public async Task LeavingAndReenteringDoesNotReuseChosenContinuousObjects(string id)
    {
        var activated = await Act(AzirPosition(), "P1", Command(true));
        // Isolate continuity; the real Flash response is tested separately.
        var replaced = activated.State with { CardObjects = new Dictionary<string, CardObjectState>(activated.State.CardObjects) {
            [id] = activated.State.CardObjects[id] with { ObjectGeneration = 2 }
        }};
        Restore(replaced); PublicRecovery(replaced); var done = await Top(replaced);
        Assert.Equal("TARGET", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Assert.Equal(id == "GEAR" ? "OTHER" : "F", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(id == "GEAR" ? "F" : "OTHER", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task IndependentActivatedAbilityCanMoveItsSourceAfterSourceControlChanges()
    {
        var activated = await Act(AzirPosition(), "P1", Command(true));
        var changed = At(activated.State, "HOST", "P2", "BATTLEFIELD", "F");
        Restore(changed); PublicRecovery(changed); var done = await AttachTop(changed);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("P2", done.State.CardObjects["HOST"].ControllerId);
        Assert.Equal("P1", done.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        var movement = Assert.Single(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD" && (string?)e.Payload["targetObjectId"] == "HOST");
        Assert.Equal("P1", movement.Payload["movingPlayerId"]); Assert.Equal("P2", movement.Payload["controllerId"]);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("missing-context")]
    [InlineData("source-id")]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    [InlineData("equipment-generation")]
    [InlineData("equipment-choice")]
    [InlineData("effect")]
    [InlineData("card")]
    public async Task PrivateRecoveryRejectsForgedMovementBindings(string forgery)
    {
        var activated = await Act(AzirPosition(), "P1", Command(true)); var item = Assert.Single(activated.State.StackItems);
        item = forgery switch {
            "missing-context" => item with { AzirMovement = null },
            "source-id" => item with { AzirMovement = item.AzirMovement! with { Source = new("TARGET", 0) } },
            "source-generation" => item with { AzirMovement = item.AzirMovement! with { Source = new("HOST", 99) } },
            "target-generation" => item with { TargetGenerations = new Dictionary<string, long> { ["TARGET"] = 99 } },
            "equipment-generation" => item with { AzirMovement = item.AzirMovement! with { Equipment = new("GEAR", 99) } },
            "equipment-choice" => item with { TargetObjectIds = ["TARGET"] },
            "effect" => item with { EffectKind = "DRAW_ONE" },
            "card" => item with { CardNo = "OGN·001/298" },
            _ => item
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(activated.State with { StackItems = [item] }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrintedOncePerTurnUseFollowsContinuityRatherThanThePlayer(bool reentered)
    {
        var activated = await Act(AzirPosition(true), "P1", Command()); var done = await Top(activated.State);
        var s = At(At(done.State, "HOST", "P2", "BATTLEFIELD", "F"), "TARGET", "P2", "BATTLEFIELD", "F");
        s = s with {
            TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P2", PriorityPlayerId = null,
            RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P2"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 })
            }, CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["HOST"] = s.CardObjects["HOST"] with { ObjectGeneration = reentered ? 2 : 0 }
            }
        };
        Restore(s); PublicRecovery(s);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("again", "P2", CommandTypes.ActivateAbility), Command(), default);
        Assert.Equal(reentered, result.Accepted);
        if (!reentered) Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State));
        else Assert.Single(result.State.StackItems);
    }

    [Fact]
    public async Task ActualTakeUpResponseReattachesTheChosenWeaponBeforeAzirMoves()
    {
        var s = EquipChainTests.Response(AzirPosition(), "SFD·011/221", "P2");
        s = At(OfficialHoldSequenceTests.AddUnit(s, "THIRD", "OGN·001/298", "F", "P1"), "THIRD", "P1", "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["P2-DRAW"] = new("P2-DRAW", cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard])
        }, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
            ["P2-DRAW"] = new("P2", "MAIN_DECK")
        }};
        var activated = await Act(s, "P1", Command(true));
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var played = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "SFD·011/221", ["THIRD", "GEAR"]));
        var response = await Top(played.State);
        Assert.Equal("THIRD", response.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(response.State.PlayerZones["P2"].Hand); Restore(response.State); PublicRecovery(response.State);
        var done = await Top(response.State);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("THIRD", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("BASE", done.State.ObjectLocations["GEAR"].Zone);
        Assert.DoesNotContain(done.Events, e => e.Kind is "EQUIPMENT_ATTACHED" or "EQUIPMENT_DETACHED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualGaleRemovesTheChosenUnitAndLeavesCostsAndOncePerTurnUsagePaid()
    {
        var s = EquipChainTests.Response(AzirPosition(true), "OGN·169/298", "P2");
        var activated = await Act(s, "P1", Command(true));
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var played = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["TARGET"]));
        var response = await Top(played.State);
        Assert.Contains("TARGET", response.State.PlayerZones["P1"].Hand); Restore(response.State); PublicRecovery(response.State);
        var done = await Top(response.State);
        Assert.Equal("F", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(0, done.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        Assert.Contains(P4ActivatedAbilityCatalog.AzirSwiftSwapUsedThisTurnEffectId("P1", "HOST"), done.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD" or "EQUIPMENT_ATTACHED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("control")]
    [InlineData("face-down")]
    [InlineData("standby")]
    public async Task ResolutionRechecksTheChosenUnitsControlAndVisibility(string change)
    {
        var activated = await Act(AzirPosition(), "P1", Command(true)); var s = activated.State;
        if (change == "control") s = At(s, "TARGET", "P2", "BATTLEFIELD", "OTHER");
        else s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["TARGET"] = s.CardObjects["TARGET"] with { IsFaceDown = change == "face-down",
                Tags = change == "standby" ? [CardObjectTags.UnitCard, CardObjectTags.Standby] : s.CardObjects["TARGET"].Tags }
        }};
        var done = await Top(s);
        if (change == "standby")
        {
            Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
            Assert.Equal("TARGET", done.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
            Assert.Contains(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
            Assert.Equal(1, done.State.CardObjects["HOST"].MovementCountThisTurn);
            return;
        }
        Assert.Equal("F", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("TARGET", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "EQUIPMENT_ATTACHED");
        Assert.Equal(0, done.State.CardObjects["HOST"].MovementCountThisTurn);
        if (change == "control") { Restore(done.State); PublicRecovery(done.State); }
    }

    [Fact]
    public async Task EffectMovementAndReattachmentPreserveDamageAndExhaustion()
    {
        var s = AzirPosition();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { IsExhausted = true, Damage = 2 },
            ["TARGET"] = s.CardObjects["TARGET"] with { IsExhausted = true },
            ["GEAR"] = s.CardObjects["GEAR"] with { IsExhausted = true }
        }};
        var done = await AttachTop((await Act(s, "P1", Command(true))).State);
        foreach (var id in new[] { "HOST", "TARGET", "GEAR" }) Assert.True(done.State.CardObjects[id].IsExhausted);
        Assert.Equal(2, done.State.CardObjects["HOST"].Damage);
        Assert.Equal("OTHER", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(done.State); PublicRecovery(done.State);
    }
    private static async Task<ResolutionResult> Top(MatchState state)
        => await AzirTestDriver.Finish(await OfficialGraveyardRecastTests.Top(state), attach: false);
    private static async Task<ResolutionResult> AttachTop(MatchState state)
        => await AzirTestDriver.Finish(await OfficialGraveyardRecastTests.Top(state), attach: true);

}
