using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class EnemyAttackMightTests
{
    internal static MatchState Board(string printing = "OGN·255/298", int might = 3, bool controlled = true)
    {
        var s = AttackDamageDistributionTests.Position("OGN·001/298");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Power = might },
            ["F"] = s.CardObjects["F"] with { ControllerId = controlled ? "P2" : null },
            ["FOX"] = new("FOX", cardNo: printing, ownerId: "P2", controllerId: "P2", tags: ["LEGEND_CARD"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { LegendZone = ["FOX"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["FOX"] = new("P2", "LEGEND") } };
    }

    [Theory]
    [InlineData("OGN·255/298")]
    [InlineData("OGN·303/298")]
    [InlineData("OGN·303*/298")]
    public async Task EachPrintedLegendQueuesAResponseBeforeReducingTheAttacker(string printing)
    {
        var initial = Board(printing); Restore(initial); Assert.Equal("P2", initial.BattlefieldStates["F"].ControllerId);
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(moved.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(3, moved.State.CardObjects["HOST"].Power);
        Assert.Equal(3, ArmamentMightTests.Might(moved.State));
        var item = Assert.Single(moved.State.StackItems); Assert.Equal("FOX", item.SourceObjectId);
        Assert.Equal("P2", item.ControllerId); Assert.Empty(item.TargetObjectIds);
        Assert.Null(moved.State.PendingCardChoice); Assert.NotNull(moved.State.PriorityPlayerId);
        var done = await Top(moved.State);
        Assert.Equal(2, done.State.CardObjects["HOST"].Power);
        Assert.Equal(3, Assert.Single(done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Assert.Equal("P1", done.State.FocusPlayerId); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    internal static async Task<MatchState> Pending(MatchState initial)
        => (await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []))).State;

    [Fact]
    public async Task DirectBattleDeclarationCapturesTheSameAutomaticTrigger()
    {
        var s = LocalDestructionRecallTests.At(Board(), "HOST", "P1", "BATTLEFIELD", "F");
        s = s with { UntilEndOfTurnEffects = s.UntilEndOfTurnEffects.Append(BattlefieldTaskMarkers.SpellDuelCompleted("F")).ToArray() };
        var declared = await Act(s, "P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        var item = Assert.Single(declared.State.StackItems); Assert.Equal("FOX", item.SourceObjectId);
        Assert.Equal("HOST", item.FieldContext!.AffectedUnit!.ObjectId); Assert.Empty(item.TargetObjectIds);
        Assert.Equal(3, declared.State.CardObjects["HOST"].Power); Restore(declared.State); ArmamentMightTests.PublicRecovery(declared.State);
        var done = await Top(declared.State); Assert.Equal(2, done.State.CardObjects["HOST"].Power);
        Assert.Single(done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers); Restore(done.State);
    }

    [Theory]
    [InlineData("neutral")] [InlineData("attacker-control")] [InlineData("other-legend-controller")]
    public async Task NoEnemyAttackAtLegendControlledBattlefieldDoesNotTrigger(string branch)
    {
        var s = Board();
        if (branch == "other-legend-controller") {
            s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["FOX"] = s.CardObjects["FOX"] with { OwnerId = "P1", ControllerId = "P1" } },
                PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                    ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["FOX"] }, ["P2"] = s.PlayerZones["P2"] with { LegendZone = [] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["FOX"] = new("P1", "LEGEND") } };
        } else {
            foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" }) s = LocalDestructionRecallTests.At(s, id, "P2", "BASE");
            if (branch == "attacker-control") s = OfficialHoldSequenceTests.AddUnit(s, "FRIEND", "OGN·001/298", "F", "P1");
            s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["F"] = s.CardObjects["F"] with { ControllerId = branch == "neutral" ? null : "P1" } } };
        }
        var moved = await Pending(s);
        Assert.Equal(branch == "other-legend-controller", moved.CardObjects["HOST"].IsAttacking);
        Assert.Empty(moved.StackItems); Assert.Empty(moved.TriggerQueue);
        Assert.Equal(3, ArmamentMightTests.Might(moved)); Restore(moved);
    }

    [Theory]
    [InlineData("法盾2")] [InlineData("printed-protection")]
    public async Task AutomaticallyReferencedAttackerHasNoTargetChoiceOrDeflectCost(string protection)
    {
        var s = Board(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { CardNo = protection == "printed-protection" ? "SFD·105/221" : "OGN·001/298",
                Tags = protection == "printed-protection" ? [CardObjectTags.UnitCard] : [CardObjectTags.UnitCard, protection] } } };
        var pending = await Pending(s); Assert.Null(pending.PendingCardChoice); Assert.Null(pending.PendingPayment);
        Assert.Empty(Assert.Single(pending.StackItems).TargetObjectIds);
        var done = await Top(pending); Assert.Equal(2, done.State.CardObjects["HOST"].Power);
        Assert.Equal(s.RunePools["P2"], done.State.RunePools["P2"]); Restore(done.State);
    }

    [Theory]
    [InlineData("UNL-106/219")] [InlineData("SFD·045/221")] [InlineData("OGN·064/298")]
    public async Task ATargetFilteredOrSpellOnlyCounterCannotCounterThisAutomaticReference(string counter)
    {
        var s = AttackDamageDistributionTests.Spell(Board(), "COUNTER", counter, "P1");
        var pending = await Pending(s);
        pending = (await Act(pending, "P2", new PassPriorityCommand())).State;
        var id = Assert.Single(pending.StackItems).StackItemId;
        var command = new PlayCardCommand("COUNTER", counter, counter == "UNL-106/219" ? ["HOST", id] : [id]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(pending, "P1", PlayCostPreviewTests.Request(pending, command)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(pending, new("automatic-attacker-is-not-chosen", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(pending), MatchStateHasher.Hash(rejected.State));
        Restore(pending); ArmamentMightTests.PublicRecovery(pending);
    }

    [Theory]
    [InlineData(-2)] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task MinimumUsesResolutionMightAndNeverRaisesAnAlreadyLowerUnit(int power)
    {
        var done = await Top(await Pending(Board(might: power)));
        Assert.Equal(power > 1 ? power - 1 : power, done.State.CardObjects["HOST"].Power);
        Assert.Equal(power > 1 ? 1 : 0, done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers.Count);
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Battlefields); Restore(done.State);
    }

    [Fact]
    public async Task ResolutionSnapshotsCurrentAuraAndRetainsTheModifierAfterMovingToBase()
    {
        var s = Board(might: 1);
        s = DeathObserverAuditTests.Source(s, "OGS·013/024", "GAREN");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GAREN"] = s.CardObjects["GAREN"] with { OwnerId = "P1", ControllerId = "P1" } } };
        s = LocalDestructionRecallTests.At(s, "GAREN", "P1", "BASE");
        s = AttackDamageDistributionTests.Spell(s, "FLASH", "OGS·011/024", "P1");
        var moved = await Act(s, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [], ["HOST", "GAREN"]));
        var ordered = await Act(moved.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: moved.State.TriggerQueue
            .OrderBy(t => t.FieldContext!.AffectedUnit!.ObjectId == "GAREN" ? 0 : 1).Select(t => t.TriggerId).ToArray()));
        var pending = (await Top(ordered.State)).State; Assert.Equal(2, ArmamentMightTests.Might(pending));
        var done = await Top(pending); Assert.Equal(0, done.State.CardObjects["HOST"].Power);
        Assert.Equal(2, Assert.Single(done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        var response = await Act(done.State, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var inBase = await Top(response.State);
        Assert.Equal("BASE", inBase.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(0, ArmamentMightTests.Might(inBase.State)); Restore(inBase.State); ArmamentMightTests.PublicRecovery(inBase.State);
    }

    [Fact]
    public async Task AReactionMovingTheAttackerToBaseKeepsItsAutomaticReference()
    {
        var initial = AttackDamageDistributionTests.Spell(Board(), "FLASH", "OGS·011/024", "P1");
        var pending = await Pending(initial); pending = (await Act(pending, "P2", new PassPriorityCommand())).State;
        var response = await Act(pending, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var moved = await Top(response.State); Assert.Equal("BASE", moved.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(0, moved.State.CardObjects["HOST"].ObjectGeneration); Assert.Single(moved.State.StackItems);
        var done = await Top(moved.State); Assert.Equal(2, done.State.CardObjects["HOST"].Power);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ReturningTheAttackerToHandDoesNotReduceTheNewObject()
    {
        var initial = EquipChainTests.Response(Board(), "OGN·169/298", "P2");
        var pending = await Pending(initial);
        var response = await Act(pending, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var moved = await Top(response.State); Assert.Equal("HAND", moved.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(1, moved.State.CardObjects["HOST"].ObjectGeneration); Restore(moved.State);
        var done = await Top(moved.State); Assert.Equal(5, done.State.CardObjects["HOST"].Power);
        Assert.Empty(done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AReturnedAttackingTokenCeasesToExistAndItsCapturedTriggerStillRecovers()
    {
        var moved = await RemovedToken();
        Assert.False(moved.State.CardObjects.ContainsKey("HOST")); Assert.False(moved.State.ObjectLocations.ContainsKey("HOST"));
        Assert.True(Assert.Single(moved.State.StackItems).FieldContext!.AffectedUnitCeasedToExist);
        Restore(moved.State); ArmamentMightTests.PublicRecovery(moved.State);
        var done = await Top(moved.State); Assert.Empty(done.State.StackItems);
        Assert.DoesNotContain(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"); Restore(done.State);
    }

    private static async Task<ResolutionResult> RemovedToken()
    {
        var initial = EquipChainTests.Response(Board(), "OGN·169/298", "P2");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T02", out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = factory.CreateObject("HOST", "P1", "P1") } };
        var pending = await Pending(initial);
        var response = await Act(pending, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        return await Top(response.State);
    }

    [Theory]
    [InlineData("outside-participant")] [InlineData("live-object")]
    [InlineData("unknown-factory")] [InlineData("negative-generation")] [InlineData("battlefield-token")]
    public async Task RecoveryRejectsForgedCeasedTokenBattleParticipants(string change)
    {
        var state = (await RemovedToken()).State;
        const string prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
        var marker = Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith(prefix));
        var node = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker[prefix.Length..])))!;
        var ceased = node["CeasedTokens"]!;
        Assert.Equal(0, ceased["HOST"]!["Generation"]!.GetValue<long>());
        switch (change) {
            case "outside-participant": ceased["OTHER"] = ceased["HOST"]!.DeepClone(); break;
            case "live-object": ceased["ENEMY1"] = ceased["HOST"]!.DeepClone(); break;
            case "unknown-factory": ceased["HOST"]!["FactoryCardNo"] = "FALSE"; break;
            case "negative-generation": ceased["HOST"]!["Generation"] = -1; break;
            case "battlefield-token": ceased["HOST"]!["FactoryCardNo"] = "UNL·T01"; break;
        }
        var encoded = prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(node.ToJsonString()));
        var forged = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Select(e => e == marker ? encoded : e).ToArray() };
        Assert.Contains("invalid battle response declaration", OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Fact]
    public async Task RealTurnEndRemovesTheReductionAndItsProvenance()
    {
        var s = AttackDamageDistributionTests.Spell(Board(), "FLASH", "OGS·011/024", "P1");
        var reduced = await Top(await Pending(s));
        var response = await Act(reduced.State, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var inBase = await Top(response.State); Assert.Equal(2, inBase.State.CardObjects["HOST"].Power);
        var closed = await AttackDamageDistributionTests.Top(inBase.State);
        var ended = await Act(closed.State, "P1", new EndTurnCommand());
        Assert.Equal(3, ended.State.CardObjects["HOST"].Power); Assert.Empty(ended.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(0, ended.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(ended.State.ContinuousEffects, e => e.SourceObjectId == "FOX" && e.SnapshotMight.HasValue);
        Restore(ended.State); ArmamentMightTests.PublicRecovery(ended.State);
    }

    [Fact]
    public async Task CapturedConditionIsIndependentOfLaterBattlefieldControl()
    {
        var pending = await Pending(Board());
        pending = pending with { CardObjects = new Dictionary<string, CardObjectState>(pending.CardObjects) {
            ["F"] = pending.CardObjects["F"] with { ControllerId = "P1" } } };
        Restore(pending); var done = await Top(pending);
        Assert.Equal(2, done.State.CardObjects["HOST"].Power); Restore(done.State);
    }

    [Fact]
    public async Task GroupMovementCapturesOneIndependentTriggerPerAttacker()
    {
        var s = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(Board(), "SECOND", "OGN·001/298", "F", "P1"), "SECOND", "P1", "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["SECOND"] = s.CardObjects["SECOND"] with { Power = 3 } } };
        var moved = await Act(s, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [], ["HOST", "SECOND"]));
        Assert.Equal(2, moved.State.TriggerQueue.Count); Restore(moved.State); ArmamentMightTests.PublicRecovery(moved.State);
        Assert.Equal(new[] { "HOST", "SECOND" }, moved.State.TriggerQueue.Select(t => t.FieldContext!.AffectedUnit!.ObjectId).Order());
        var ordered = await Act(moved.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: moved.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var first = await Top(ordered.State); var done = await Top(first.State);
        Assert.Equal(2, done.State.CardObjects["HOST"].Power); Assert.Equal(2, done.State.CardObjects["SECOND"].Power);
        Assert.Single(done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers); Assert.Single(done.State.CardObjects["SECOND"].UntilEndOfTurnPowerModifiers);
        var passed = await Act(done.State, "P1", new PassFocusCommand());
        Assert.Empty(passed.State.StackItems); Assert.Empty(passed.State.TriggerQueue); Restore(passed.State);
    }

    [Theory]
    [InlineData("missing-unit")] [InlineData("future-unit")] [InlineData("source-unit")]
    [InlineData("missing-field")] [InlineData("future-source")] [InlineData("wrong-card")]
    [InlineData("chosen-target")] [InlineData("optional-cost")] [InlineData("foreign-location")]
    [InlineData("wrong-controller")]
    [InlineData("false-departure")]
    public async Task RecoveryRejectsForgedCapturedAttackerAndSchema(string change)
    {
        var state = await Pending(Board()); var item = Assert.Single(state.StackItems); var context = item.FieldContext!;
        context = change switch {
            "missing-unit" => context with { AffectedUnit = null },
            "future-unit" => context with { AffectedUnit = new("HOST", 100) },
            "source-unit" => context with { AffectedUnit = new("FOX", 0) },
            "missing-field" => context with { BattlefieldId = "FALSE" },
            "future-source" => context with { SourceGeneration = 100 },
            "wrong-card" => context with { CardNo = "OGN·303/298" },
            "foreign-location" => context with { SourceLocation = new("P2", "BASE") },
            "false-departure" => context with { AffectedUnitCeasedToExist = true },
            _ => context
        };
        item = item with { FieldContext = context, CardNo = context.CardNo };
        if (change == "chosen-target") item = item with { TargetObjectIds = ["HOST"], TargetGenerations = new Dictionary<string, long> { ["HOST"] = 0 } };
        if (change == "optional-cost") item = item with { OptionalCosts = ["PAY"] };
        if (change == "wrong-controller") item = item with { ControllerId = "P1" };
        var forged = state with { StackItems = [item] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("field trigger", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryLegendPrintingDerivesItsTriggerFromTheOfficialText()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var printings = new[] { "OGN·255/298", "OGN·303/298", "OGN·303*/298" };
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), [])
            .Where(s => printings.Contains(s.CardNo)).ToArray();
        Assert.Equal(3, specs.Length);
        foreach (var spec in specs) {
            var trigger = Assert.Single(spec.Triggers, t => t.Kind == TriggerKinds.EnemyAttackPowerModifier);
            Assert.Equal(TriggerTimings.EnemyAttacksControlledBattlefield, trigger.Timing);
            Assert.Equal(TriggerTargetScopes.TriggeringAttacker, trigger.TargetScope);
            Assert.Equal(-1, trigger.PowerDelta); Assert.Equal(1, trigger.MinimumPowerAfterModifier); Assert.Equal("UNTIL_END_OF_TURN", trigger.Duration);
        }
    }
}
