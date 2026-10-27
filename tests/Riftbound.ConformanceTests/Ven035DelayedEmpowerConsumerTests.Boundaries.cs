using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Fact]
    public async Task ActualStatusInstructionsRecomputeBothSidesOfTheEmpoweredUnitAura()
    {
        var initial = Add(Position("P1"), "AURA", "VEN·130", zone: "BASE");
        var game = new Driver(initial);
        try
        {
            var unit = await PrepareTarget(game, "P1", true);
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            Assert.Equal(4, StatusMight(game.State, "P1", unit)); Assert.Equal(5, StatusMight(game.State, "P1", "AURA"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["AURA"], Mode: Empower)); await game.Top();
            Assert.True(game.State.CardObjects["AURA"].IsEmpowered);
            Assert.Equal(6, StatusMight(game.State, "P1", unit)); Assert.Equal(7, StatusMight(game.State, "P1", "AURA"));
            await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
            Assert.False(game.State.CardObjects["AURA"].IsEmpowered); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(4, StatusMight(game.State, "P1", unit)); Assert.Equal(5, StatusMight(game.State, "P1", "AURA"));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-status-derived-aura", initial, game); }
    }

    [Fact]
    public async Task DelayedReempowerReallyCreatesTheCurrentSourcesBanishSkill()
    {
        // Explicit controlled initial condition; removal and the later 0 -> 1
        // are actual paid spell / end-step actions, with no status edits in play.
        var initial = Add(Position("P1"), "MEL", "VEN·110", zone: "BASE");
        initial = Add(initial, "VICTIM", "OGN·096/298", "P2", "BASE");
        initial = StatusBattlefield(initial, "VICTIM", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["MEL"] = initial.CardObjects["MEL"] with { IsEmpowered = true } } };
        var game = new Driver(initial);
        try
        {
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var generation = game.State.CardObjects["MEL"].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["MEL"], Mode: Remove)); await game.Top();
            Assert.False(game.State.CardObjects["MEL"].IsEmpowered);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED" && (string?)e.Event.Payload.GetValueOrDefault("effectKind") == "FIELD_TRIGGER");
            await game.Act("P1", new EndTurnCommand()); await game.Top();
            Assert.True(game.State.CardObjects["MEL"].IsEmpowered); Assert.Equal(generation, game.State.CardObjects["MEL"].ObjectGeneration);
            var skill = Assert.Single(game.State.StackItems, i => i.FieldContext?.Kind == "BECAME_EMPOWERED_BANISH");
            Assert.Equal("MEL", skill.SourceObjectId); Assert.Equal(generation, skill.FieldContext!.SourceGeneration);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["VICTIM"])); await game.Top();
            Assert.Contains("VICTIM", game.State.PlayerZones["P2"].Banished);
            Assert.Equal("P2", game.State.TurnPlayerId); Assert.True(game.State.CardObjects["MEL"].IsEmpowered);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("delayed-real-empower-banish-observer", initial, game); }
    }

    [Fact]
    public async Task AnAlreadyCreatedInverseKeepsItsOldBindingWhenTheUnitRemainsPrivate()
    {
        var initial = Add(Position("P1"), "RETURN", "OGN·104/298");
        var game = new Driver(initial);
        try
        {
            var unit = await PrepareTarget(game, "P1", false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: Empower)); await game.Top();
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [unit])); await game.Top();
            Assert.Contains(unit, game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects[unit].ObjectGeneration);
            await game.Act("P1", new EndTurnCommand());
            var skill = Assert.Single(game.State.StackItems);
            Assert.Equal(generation, skill.DelayedEmpower!.Unit.Generation);
            Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
            var opponent = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"], Json);
            Assert.False(opponent.GetProperty("players").GetProperty("P1").GetProperty("objects").TryGetProperty(unit, out _));
            var shown = Assert.Single(opponent.GetProperty("stack").EnumerateArray());
            Assert.Empty(shown.GetProperty("targetObjectIds").EnumerateArray()); Assert.False(shown.TryGetProperty("delayedEmpower", out _));
            await FinishEnd(game, "P1"); Assert.Contains(unit, game.State.PlayerZones["P1"].Hand);
            Assert.Equal(generation + 1, game.State.CardObjects[unit].ObjectGeneration); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("delayed-old-private-target", initial, game); }
    }

    [Fact]
    public async Task AbnormalVictoryStopsBeforeAnyPendingInverseCanExecute()
    {
        var initial = Position("P1"); var game = new Driver(initial); ResolutionResult? probe = null;
        try
        {
            var unit = await PrepareTarget(game, "P1", false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: Empower)); await game.Top();
            Assert.True(game.State.CardObjects[unit].IsEmpowered);
            await game.Act("P1", new SurrenderCommand()); Assert.Equal("P2", game.State.WinnerPlayerId);
            var hash = MatchStateHasher.Hash(game.State);
            probe = await new CoreRuleEngine().ResolveAsync(game.State, new("after-surrender-end", "P1", CommandTypes.EndTurn), new EndTurnCommand(), default);
            Assert.False(probe.Accepted); Assert.Empty(probe.Events); Assert.Equal(hash, MatchStateHasher.Hash(probe.State));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("delayed-inverse-after-abnormal-victory", initial, game, probe); }
    }

    private static int StatusMight(MatchState state, string player, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], Json)
            .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    private static MatchState StatusBattlefield(MatchState state, string id, string player)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["BF"] = state.CardObjects["BF"] with { ControllerId = player } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with {
                Base = state.PlayerZones[player].Base.Where(x => x != id).ToArray(), Battlefields = [..state.PlayerZones[player].Battlefields, id] } } };
}
