using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    private const string No = "VEN·035";
    private const string UnitNo = "VEN·057";
    private const string Empower = "EMPOWER_THEN_REMOVE";
    private const string Remove = "REMOVE_THEN_EMPOWER";
    private const string Text = "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n选择一个效果 —\n- 强化一名单位。回合结束时，解除其强化。\n- 解除一名{{已强化}}单位的强化。在回合结束时，强化该单位。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactPrintingPreservesEveryPriorCatalogGroupAndMember()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var old = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1085, old.Length); Assert.Equal(1086, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(old); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(866, prior.Count); Assert.Equal(867, current.Count);
        foreach (var group in prior) Assert.Equal(JsonSerializer.Serialize(group), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == group.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
    }

    [Fact]
    public async Task ExactGreenReactionSeparatesTheTwoPrintedModesFromAnActivatedEmpowerKeyword()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37723, card.Id); Assert.Equal("念化盈虚", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal([RuneTrait.Green], card.CardColorList); Assert.Equal(["spell"], card.CardCategoryList);
        Assert.Empty(EmpowerParser.Parse(card.CardEffect)); Assert.Equal(1, PrintedPowerCostRules.ForCard(No).Amount);
        Assert.Equal(2, CardBehaviorRegistry.GetAll().Count(c => c.CardNo == No));
        foreach (var mode in new[] { Empower, Remove })
        {
            Assert.True(CardBehaviorRegistry.TryGetByCardNoAndMode(No, mode, out var behavior));
            Assert.Equal(3, behavior.ManaCost); Assert.Equal(1, behavior.RequiredTargetCount);
            Assert.Equal(CardTargetScopes.AnyUnit, behavior.TargetScope); Assert.True(behavior.CanPlayDuringPriority);
        }
    }

    [Theory]
    [InlineData(false, false, "P1")] [InlineData(false, true, "P1")] [InlineData(true, true, "P1")]
    [InlineData(false, false, "P2")] [InlineData(false, true, "P2")] [InlineData(true, true, "P2")]
    public async Task AnActualModePaysOneGreenAndReversesItsCapturedUnitAtThisTurnsEnd(bool remove, bool initiallyEmpowered, string owner)
    {
        var initial = Position(owner); var game = new Driver(initial);
        try
        {
            var unit = await PrepareTarget(game, owner, initiallyEmpowered); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Green]);
            var mana = game.State.RunePools["P1"].Mana; var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: remove ? Remove : Empower));
            Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(initiallyEmpowered, game.State.CardObjects[unit].IsEmpowered);
            var pending = Assert.Single(game.State.StackItems); Assert.Equal(No, pending.CardNo); Assert.Equal([unit], pending.TargetObjectIds);
            await game.Top(); Assert.Equal(!remove, game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration); Assert.Equal(owner, game.State.CardObjects[unit].OwnerId);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
            await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
            Assert.Equal("P2", game.State.TurnPlayerId); Assert.Equal(remove, game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration); Assert.Equal(owner, game.State.CardObjects[unit].ControllerId);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save($"mode-remove-{remove}-initial-{initiallyEmpowered}-owner-{owner}", initial, game); }
    }

    [Fact]
    public async Task TheRemovalModeRejectsAnActuallyUnempoweredUnitWithoutSpendingOrChangingState()
    {
        var initial = Position("P1"); var game = new Driver(initial);
        ResolutionResult? probe = null;
        try
        {
            var unit = await PrepareTarget(game, "P1", false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            Assert.False(game.State.CardObjects[unit].IsEmpowered); var before = MatchStateHasher.Hash(game.State);
            var engine = new CoreRuleEngine(); var command = new PlayCardCommand("SPELL", No, [unit], Mode: Remove);
            var result = probe = await engine.ResolveAsync(game.State, new("remove-unempowered", "P1", CommandTypes.PlayCard), command, default);
            Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Empty(result.Events);
            Assert.Equal(before, MatchStateHasher.Hash(result.State)); await game.Replay(); Restore(game.State);
        }
        finally { await Save("removal-mode-unempowered-target", initial, game, probe); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualSameGenerationRemovalChangesFutureMoveBirthButKeepsAnAlreadyEarnedDraw(bool moveFirst)
    {
        var initial = Position("P1"); var game = new Driver(initial);
        try
        {
            var unit = await PrepareTarget(game, "P1", true);
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            Assert.False(game.State.CardObjects[unit].IsExhausted); var generation = game.State.CardObjects[unit].ObjectGeneration;
            var draws = Draws(game); string? earned = null;
            if (moveFirst)
            {
                await game.Act("P1", new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:BF", []));
                earned = Assert.Single(game.State.StackItems, item => item.SourceObjectId == unit).StackItemId;
                if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            }
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: Remove)); await game.Top();
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(draws, Draws(game));
            if (moveFirst)
            {
                Assert.Equal(earned, Assert.Single(game.State.StackItems, item => item.SourceObjectId == unit).StackItemId);
                await game.Top(); Assert.Equal(draws + 1, Draws(game));
            }
            else
            {
                await game.Act("P1", new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:BF", []));
                Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == unit);
                Assert.DoesNotContain(game.State.TriggerQueue, item => item.SourceObjectId == unit); Assert.Equal(draws, Draws(game));
            }
            await FinishFieldWindow(game); Assert.Equal(1, game.State.CardObjects[unit].MovementCountThisTurn);
            await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("same-generation-removal-move-first-" + moveFirst, initial, game); }
    }

    private static MatchState Position(string owner)
    {
        var state = Add(VenUnitEntryBasicTests.Position(UnitNo), "SPELL", No);
        if (owner == "P2") state = Add(state, "ENEMY35", UnitNo, "P2");
        state = state with { TurnPlayerId = owner, ActivePlayerId = owner,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        for (var i = 0; i < 9; i++) state = Add(state, "STATUS-RUNE-" + i, i == 0 ? "UNL-R03" : "UNL-R02", "P1", "BASE");
        return state;
    }
    private static async Task<string> PrepareTarget(Driver game, string owner, bool empower)
    {
        var unit = owner == "P1" ? "UNIT" : "ENEMY35";
        if (owner == "P1") { await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-0")); }
        await game.Act(owner, new PlayCardCommand(unit, UnitNo, []));
        if (empower) { await game.Act(owner, new ActivateAbilityCommand(unit, "EMPOWER:0:0", [])); await game.Top(); }
        if (owner == "P2") { await game.Act("P2", new EndTurnCommand()); await TapRunes(game); }
        return unit;
    }
    private static async Task TapRunes(Driver game)
    {
        foreach (var id in game.State.PlayerZones["P1"].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !game.State.CardObjects[id].IsExhausted).ToArray()) await game.Act("P1", new TapRuneCommand(id));
    }
    private static async Task FinishEnd(Driver game, string player)
    {
        for (var i = 0; game.State.TurnPlayerId == player; i++)
        {
            Assert.InRange(i, 0, 15);
            if (game.State.TriggerQueue.Count > 0) await game.Act(game.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else Assert.Fail("The actual end-step continuation did not expose its ordinary next action.");
        }
    }
    private static async Task FinishFieldWindow(Driver game)
    {
        for (var i = 0; game.State.TimingState != TimingStates.NeutralOpen; i++)
        {
            Assert.InRange(i, 0, 11);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else Assert.Fail("The actual movement did not expose the next response step.");
        }
    }
    private static int Draws(Driver game) => game.Events().Count(e => e.Event.Kind == "CARD_DRAWN" && (string?)e.Event.Payload.GetValueOrDefault("playerId") == "P1");
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1", string zone = "HAND") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone == "BASE" ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] }
            : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static void Restore(MatchState state) => Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(OfficialInsightAndSpellLockTests.Restore(state)));
    private static async Task Save(string label, MatchState initial, Driver game, ResolutionResult? directEngineProbe = null)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN035_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN VEN035 explicit midgame, physical P1 rune costs, real mode/turn/movement/status commands and original replay. Invalid-target probe is separate direct engine resolution. No same-generation status edit. Applicability/final L2/L3/whole-card OPEN",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), directEngineProbe }, Json));
    }
}
