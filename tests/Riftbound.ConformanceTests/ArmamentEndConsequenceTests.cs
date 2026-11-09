using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 383.2.a.1, 383.4.c/g, 436 and 718–719: the host owns the lower box,
// and a real conquest belongs to that unit incarnation, even before assembly.
public sealed class ArmamentEndConsequenceTests
{
    private const string Axe = "UNL-019/219", Host = "UNL-026/219";

    private static MatchState Position(bool attached = true, int copies = 1, bool battlefield = false,
        string gearOwner = "P1")
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "HOST", Host, "P1",
            battlefield ? "BATTLEFIELD" : "BASE", battlefield ? "BF" : null);
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "DETACH", "SFD·011/221", "P1", "HAND");
        for (var i = 0; i < copies; i++)
        {
            var id = i == 0 ? "AXE" : "AXE" + i;
            s = RadGearTestPosition.Put(s, id, Axe, gearOwner,
                attached && battlefield ? "BATTLEFIELD" : "BASE", attached && battlefield ? "BF" : null);
            if (attached) s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                [id] = s.CardObjects[id] with { AttachedToObjectId = "HOST" } } };
        }
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(s);
    }

    private static async Task Assemble(Driver game)
    {
        await game.Act("P1", new AssembleEquipmentCommand("AXE", "HOST", ["ASSEMBLE_RED"]));
        await game.Top(); Assert.Equal("HOST", game.State.CardObjects["AXE"].AttachedToObjectId);
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Contains(new ObjectBinding("HOST", game.State.CardObjects["HOST"].ObjectGeneration),
            game.State.BattlefieldConquests!.Units!);
        Assert.Empty(game.State.StackItems);
    }

    private static async Task End(Driver game)
    {
        await game.Act("P1", new EndTurnCommand());
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static RecoveredEvent[] Damage(Driver game) => game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED"
        && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")).ToArray();

    [Fact]
    public async Task PlayingAndAssemblingTheAxeEarnsARespondableEndSkillThatDetachesBeforeFourDamage()
    {
        var s = RadUnitEffectTestFixture.Place(Position(false), "AXE", "P1", "HAND");
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("AXE", Axe, [], Destination: "BASE"));
        await Assemble(game); await End(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("END", skill.UnitEvent!.Timing); Assert.Equal("P1", skill.ControllerId);
        Assert.False(skill.UnitEvent.SourceConqueredBeforeEnd);
        Assert.Equal(new ObjectBinding("AXE", game.State.CardObjects["AXE"].ObjectGeneration), skill.UnitEvent.Origin!.Equipment);
        Assert.Equal("HOST", game.State.CardObjects["AXE"].AttachedToObjectId); Assert.Empty(Damage(game));
        var original = Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("triggeredByEventKind"), "TURN_END_STEP_STARTED"));
        Assert.True(original.Event.Payload.ContainsKey("armamentGrantHash"));
        Assert.True(original.Event.Payload.ContainsKey("observedUnitEventCaptureHash"));
        await game.Top();
        Assert.Null(game.State.CardObjects["AXE"].AttachedToObjectId);
        Assert.Contains("AXE", game.State.PlayerZones["P1"].Base);
        Assert.Equal(4, Assert.Single(Damage(game)).Event.Payload["damage"]);
        Assert.True(game.Events().Single(e => e.Event.Kind == "EQUIPMENT_DETACHED").Sequence
            < Damage(game)[0].Sequence);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.CardObjects["HOST"].Damage); // normal end cleanup clears damage
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnUnattachedAxeOrTheOpponentsTurnEndDoesNotTriggerTheLowerBox(bool opponentsTurn)
    {
        var s = Position(opponentsTurn);
        if (opponentsTurn) s = s with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(s);
        await game.Act(opponentsTurn ? "P2" : "P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitEvent?.CardNo == Axe);
        Assert.Empty(Damage(game)); await game.Replay();
    }

    [Fact]
    public async Task ARealConquestBeforeAssemblyStillExemptsThatSameUnitAtTurnEnd()
    {
        var game = new Driver(Position(false)); await Conquer(game); await Assemble(game); await End(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(Damage(game));
        Assert.Equal("HOST", game.State.CardObjects["AXE"].AttachedToObjectId); await game.Replay();
    }

    [Fact]
    public async Task ReturningAndReplayingAConquerorDoesNotExemptItsNewIncarnation()
    {
        var game = new Driver(Position(false)); await Conquer(game);
        var old = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("HOST", Host, [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > old);
        await Assemble(game); await End(game); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Equal(4, Assert.Single(Damage(game)).Event.Payload["damage"]); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHostAfterTheEndTriggerLeavesTheSkillButCannotDamageItsPrivateNewIncarnation()
    {
        var game = new Driver(Position()); await End(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems); Assert.Null(game.State.CardObjects["AXE"].AttachedToObjectId);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(MatchReplayRedactor.BuildSpectatorFrame(
            game.State.RoomId, game.State.Tick, game.Events().Last().Sequence, [], game.State)));
        await game.Top(); Assert.Empty(Damage(game)); await game.Replay();
    }

    [Fact]
    public async Task AnActualTakeUpResponseCannotCancelTheIndependentDamageInstruction()
    {
        var game = new Driver(Position()); await End(game);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "AXE"])); await game.Top();
        Assert.Null(game.State.CardObjects["AXE"].AttachedToObjectId);
        var detached = game.Events().Count(e => e.Event.Kind == "EQUIPMENT_DETACHED");
        await game.Top(); Assert.Equal(4, Assert.Single(Damage(game)).Event.Payload["damage"]);
        Assert.Equal(detached, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_DETACHED")); await game.Replay();
    }

    [Fact]
    public async Task TwoAxesGiveTwoIndependentEndSkillsAndEachDamagesTheOriginalHost()
    {
        var game = new Driver(Position(copies: 2)); await End(game);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal(2, game.State.StackItems.Select(item => item.UnitEvent!.Origin!.Equipment!.ObjectId).Distinct().Count());
        await game.Top(); Assert.Single(Damage(game)); Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await game.Top(); Assert.Equal(2, Damage(game).Length);
        Assert.All(Damage(game), e => Assert.Equal(4, e.Event.Payload["damage"]));
        Assert.Null(game.State.CardObjects["AXE"].AttachedToObjectId); Assert.Null(game.State.CardObjects["AXE1"].AttachedToObjectId);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RecoveryRejectsErasingOrRetimingTheUnitsOriginalConquestBinding(bool erase)
    {
        var game = new Driver(Position(false)); await Conquer(game);
        var forged = game.State with { BattlefieldConquests = game.State.BattlefieldConquests! with {
            Units = erase ? null : [new("HOST", game.State.CardObjects["HOST"].ObjectGeneration + 1)] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await Assemble(game); await End(game); Assert.Empty(Damage(game)); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsChangingTheCapturedEndConditionAfterTheSkillWasEarned()
    {
        var game = new Driver(Position()); await End(game); var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = [skill with {
            UnitEvent = skill.UnitEvent! with { SourceConqueredBeforeEnd = true } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); Assert.Single(Damage(game)); await game.Replay();
    }

    [Fact]
    public async Task HoldingTheArenaOnlyActivatesNamedConquestSkillsAndDoesNotExemptTheAxe()
    {
        var s = RadGearTestPosition.Put(Position(battlefield: true), "BF", "OGN·286/298", "P1", "BATTLEFIELD", "BF")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(s); await game.Act("P2", new EndTurnCommand());
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal("ACTIVATE_CONQUEST", Assert.Single(game.State.StackItems).HeldContext!.Kind);
        await game.Top(); Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Null(game.State.BattlefieldConquests);
        await End(game); Assert.Single(game.State.StackItems); await game.Top();
        Assert.Equal(4, Assert.Single(Damage(game)).Event.Payload["damage"]); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheHostsControllerOwnsTheSkillEvenWhenTheWeaponBelongsToTheOpponent(bool amplified)
    {
        var s = Position(battlefield: true, gearOwner: "P2");
        if (amplified) s = RadGearTestPosition.Put(s, "BF", "OGN·296/298", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(s); await End(game);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId); await game.Top();
        Assert.Contains("AXE", game.State.PlayerZones["P2"].Base);
        Assert.Equal(amplified ? 5 : 4, Assert.Single(Damage(game)).Event.Payload["damage"]); await game.Replay();
    }
}
