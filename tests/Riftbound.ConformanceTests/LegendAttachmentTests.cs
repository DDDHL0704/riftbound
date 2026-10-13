using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.AttachmentObserverTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class LegendAttachmentTests
{
    internal const string Attach = LegendActionAbilityCatalog.JaxLegendAttachAbilityId;
    internal const string Reattach = LegendActionAbilityCatalog.JaxLegendReattachAbilityId;
    internal const string Forge = "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD";

    internal static MatchState PositionFor(string legend = "SFD·193/221", bool attached = false)
    {
        var s = At(Jax(), "HOST", "P1", "BATTLEFIELD", "F");
        return s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["传奇"]),
                ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = attached ? "HOST" : null }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") }
        };
    }

    internal static LegendActCommand Command(string ability = Attach) => new("LEGEND", ability, ["HOST", "GEAR"], ability == Attach ? ["SPEND_MANA:1"] : []);

    internal static MatchState ForgePosition()
    {
        var s = At(PositionFor("OGN·265/298"), "HOST", "P1", "BASE");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["F"] = s.CardObjects["F"] with { CardNo = "SFD·208/221" }
        }};
    }

    [Theory]
    [InlineData("SFD·193/221")]
    [InlineData("SFD·245/221")]
    public async Task LegendAttachmentPaysAndExhaustsBeforeBothResponsePasses(string card)
    {
        var initial = PositionFor(card);
        var activated = await Act(initial, "P1", Command());
        Assert.Equal(19, activated.State.RunePools["P1"].Mana);
        Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Null(activated.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(activated.State.StackItems);
        Assert.DoesNotContain(activated.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Restore(activated.State); PublicRecovery(activated.State);
        var first = await Act(activated.State, "P1", new PassPriorityCommand());
        Assert.Null(first.State.CardObjects["GEAR"].AttachedToObjectId);
        var resolved = await Act(first.State, "P2", new PassPriorityCommand());
        Assert.Equal("HOST", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F", resolved.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Contains("GEAR", resolved.State.PlayerZones["P1"].Battlefields);
        Assert.NotNull(resolved.State.PendingPayment);
        Assert.Empty(resolved.State.PlayerZones["P1"].Hand);
        Restore(resolved.State); PublicRecovery(resolved.State);
        var draw = await FinishPaidDraw(await Pay(resolved.State));
        Assert.Single(draw.State.PlayerZones["P1"].Hand); Restore(draw.State); PublicRecovery(draw.State);
    }

    [Fact]
    public async Task SameHostReattachmentIsLegalButDoesNotAttachOrTriggerDraw()
    {
        var s = At(PositionFor(attached: true), "GEAR", "P1", "BATTLEFIELD", "F");
        var activated = await Act(s, "P1", Command(Reattach));
        Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(20, activated.State.RunePools["P1"].Mana);
        Assert.Single(activated.State.StackItems);
        var done = await Top(activated.State);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(done.Events, e => e.Kind is "EQUIPMENT_ATTACHED" or "EQUIPMENT_DETACHED" or "TRIGGER_QUEUED");
        Assert.Null(done.State.PendingPayment); Assert.Empty(done.State.StackItems);
        Assert.Empty(done.State.PlayerZones["P1"].Hand); Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ReattachmentDetachesOldHostAndMovesEquipmentToNewHost()
    {
        var s = PositionFor();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SECOND"] = new("SECOND", cardNo: "OGN·001/298", power: 2, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = "SECOND", OwnerId = "P2" }
        }};
        s = At(s, "SECOND", "P1", "BASE");
        var activated = await Act(s, "P1", Command(Reattach));
        Assert.Equal("SECOND", activated.State.CardObjects["GEAR"].AttachedToObjectId);
        var done = await Top(activated.State);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("P2", done.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P1", done.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("F", done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        var detached = Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_DETACHED");
        Assert.Equal("SECOND", detached.Payload["unitObjectId"]);
        Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Assert.NotNull(done.State.PendingPayment); Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualGaleResponseReturnsTheChosenUnitWithoutRefundingActivation()
    {
        var s = EquipChainTests.Response(PositionFor(), "OGN·169/298", "P2");
        var activated = await Act(s, "P1", Command());
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var gale = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var returned = await Top(gale.State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        Assert.Single(returned.State.StackItems); Restore(returned.State); PublicRecovery(returned.State);
        var done = await Top(returned.State);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(19, done.State.RunePools["P1"].Mana);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Assert.Null(done.State.PendingPayment); Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("GEAR")]
    [InlineData("HOST")]
    public async Task CapturedTargetsChangingControlInvalidateLegendAttachment(string objectId)
    {
        var activated = await Act(PositionFor(), "P1", Command());
        // Explicit same-object control change, not a claim that a control spell
        // or a whole control-change family has been certified by this test.
        var changed = At(activated.State, objectId, "P2", "BASE");
        Restore(changed); PublicRecovery(changed);
        var done = await Top(changed);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(19, done.State.RunePools["P1"].Mana);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Assert.Null(done.State.PendingPayment); Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task CapturedForgeGrantedAbilitySurvivesLosingBattlefieldControl()
    {
        var s = ForgePosition();
        var activated = await Act(s, "P1", Command(Forge));
        Assert.Null(activated.State.CardObjects["GEAR"].AttachedToObjectId);
        var lost = activated.State with {
            CardObjects = new Dictionary<string, CardObjectState>(activated.State.CardObjects) {
                ["F"] = activated.State.CardObjects["F"] with { ControllerId = "P2" }
            }
        };
        Restore(lost); PublicRecovery(lost);
        var done = await Top(lost);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        var grant = Assert.Single(done.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED");
        Assert.Equal("F", grant.Payload["battlefieldObjectId"]);
        Assert.Equal("SFD·208/221", grant.Payload["battlefieldCardNo"]);
        Assert.NotNull(done.State.PendingPayment); Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task SecondPlayerCanAttachOwnedByOtherPlayerEquipmentToItsOwnUnit()
    {
        var s = At(At(PositionFor(), "HOST", "P2", "BASE"), "GEAR", "P2", "BASE");
        s = s with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { LegendZone = [] },
                ["P2"] = s.PlayerZones["P2"] with { LegendZone = ["LEGEND"] }
            }, CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["LEGEND"] = s.CardObjects["LEGEND"] with { ControllerId = "P2" },
                ["HOST"] = s.CardObjects["HOST"] with { CardNo = "OGN·001/298", Power = 2 }
            }, ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["LEGEND"] = new("P2", "LEGEND") },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) }
        };
        var activated = await Act(s, "P2", Command());
        Assert.Null(activated.State.CardObjects["GEAR"].AttachedToObjectId);
        var done = await Top(activated.State);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("P1", done.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P2", done.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal(19, done.State.RunePools["P2"].Mana);
        Assert.Equal(20, done.State.RunePools["P1"].Mana);
        Assert.Equal("P2", Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED").Payload["attachingPlayerId"]);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("GEAR")]
    [InlineData("HOST")]
    public async Task LeavingAndReenteringAChosenTargetDoesNotReuseItsObjectHandle(string id)
    {
        var activated = await Act(PositionFor(), "P1", Command());
        // Isolate the final state of a leave/reentry response sequence. The
        // actual return-to-hand spell response is covered separately above.
        var reentered = activated.State with { CardObjects = new Dictionary<string, CardObjectState>(activated.State.CardObjects) {
            [id] = activated.State.CardObjects[id] with { ObjectGeneration = 2 }
        }};
        var done = await Top(reentered);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(19, done.State.RunePools["P1"].Mana);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Null(done.State.PendingPayment); Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualTakeUpResponseMakesThePreviouslyUnattachedTargetIllegal()
    {
        var s = EquipChainTests.Response(PositionFor(), "SFD·011/221", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SECOND"] = new("SECOND", cardNo: "OGN·001/298", power: 2, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
            ["P2-DRAW"] = new("P2-DRAW", cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard])
        }, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] }
        }};
        s = At(s, "SECOND", "P1", "BASE");
        var activated = await Act(s, "P1", Command());
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var takeUp = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "SFD·011/221", ["SECOND", "GEAR"]));
        var response = await Top(takeUp.State);
        Assert.Equal("SECOND", response.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(response.State.PlayerZones["P2"].Hand);
        var done = await Top(response.State);
        Assert.Equal("SECOND", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        Assert.Equal(19, done.State.RunePools["P1"].Mana);
        Assert.Null(done.State.PendingPayment); Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnrevealedHostIsNeitherOfferedNorAccepted(bool faceDown)
    {
        var s = PositionFor();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { IsFaceDown = faceDown, Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] }
        }};
        var prompt = ResolutionResult.BuildPrompts(s)["P1"];
        if (!faceDown)
        {
            Assert.Contains(prompt.Candidates ?? [], c => c.Action == CommandTypes.LegendAct && c.Enabled);
            var activated = await Act(s, "P1", Command());
            Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(19, activated.State.RunePools["P1"].Mana);
            var done = await Top(activated.State);
            Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
            return;
        }
        Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action == CommandTypes.LegendAct && c.Enabled);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("hidden-host", "P1", CommandTypes.LegendAct), Command(), default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        Assert.False(rejected.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(20, rejected.State.RunePools["P1"].Mana);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ability")]
    [InlineData("card")]
    [InlineData("future-source")]
    [InlineData("future-target")]
    [InlineData("unbound-target")]
    [InlineData("fake-grant")]
    [InlineData("source-object")]
    public async Task PrivateRecoveryRejectsForgedAttachmentContext(string scenario)
    {
        var activated = await Act(PositionFor(), "P1", Command());
        var item = Assert.Single(activated.State.StackItems);
        var context = Assert.IsType<LegendAttachmentContext>(item.LegendAttachment);
        item = scenario switch {
            "missing" => item with { LegendAttachment = null },
            "ability" => item with { LegendAttachment = context with { AbilityId = "FAKE" } },
            "card" => item with { CardNo = "OGN·001/298" },
            "future-source" => item with { LegendAttachment = context with { SourceGeneration = 1 } },
            "future-target" => item with { TargetGenerations = new Dictionary<string, long> { ["HOST"] = 1, ["GEAR"] = 0 } },
            "unbound-target" => item with { TargetGenerations = null },
            "fake-grant" => item with { LegendAttachment = context with { BattlefieldObjectId = "F", BattlefieldCardNo = "SFD·208/221" } },
            "source-object" => item with { SourceObjectId = "F" },
            _ => throw new InvalidOperationException(scenario)
        };
        var forged = activated.State with { StackItems = [item] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged),
            e => e.Contains("legend attachment", StringComparison.Ordinal));
    }
}
