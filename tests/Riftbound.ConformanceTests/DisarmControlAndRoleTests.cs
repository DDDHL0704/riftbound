using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DisarmTriggerTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DisarmControlAndRoleTests
{
    [Theory]
    [InlineData("HOST", "P2", true)]
    [InlineData("ENEMY1", "P1", false)]
    public async Task TakeoverRequiresALegalSourceAndCannotChangeTheAlreadyCreatedAbilityController(string target, string thief, bool allowed)
    {
        var row = "control-" + target;
        var initial = AttackDamageDistributionTests.Spell(await Position("RAD·054/167", 1), "TAKEOVER", "SFD·202/221", thief);
        var hidingField = "F";
        if (thief == "P1")
        {
            hidingField = "HOME";
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    [hidingField] = OfficialGarenConquestBattlefieldCountTests.Printed(hidingField, "OGN·275/298", "P1") with { ControllerId = "P1" },
                    ["HOME-HOLDER"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOME-HOLDER", "OGN·175/298", "P1") },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    [hidingField] = new("P1", "BATTLEFIELD", hidingField), ["HOME-HOLDER"] = new("P1", "BATTLEFIELD", hidingField) },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { Battlefields = [..initial.PlayerZones["P1"].Battlefields, hidingField, "HOME-HOLDER"] } } };
        }
        else initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var hidden = await Act(initial, thief, new HideCardCommand("TAKEOVER", "SFD·202/221", "BATTLEFIELD:" + hidingField, ["STANDBY_A"]));
        Assert.True(hidden.State.CardObjects["TAKEOVER"].IsFaceDown);
        Record(row, "hidden", hidden.State, hidden.Events);
        Restore(hidden.State); ArmamentMightTests.PublicRecovery(hidden.State);
        if (thief == "P2") hidden = await Act(hidden.State, "P2", new EndTurnCommand());
        Assert.Equal("P1", hidden.State.TurnPlayerId);
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(hidden.State)).State, "ENEMY1");
        var original = Assert.Single(confirmed.State.StackItems);
        Assert.Equal("P1", original.ControllerId);
        if (thief == "P2") confirmed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var command = new PlayCardCommand("TAKEOVER", "SFD·202/221", [target]);
        var cast = await new CoreRuleEngine().ResolveAsync(confirmed.State, new("disarm-takeover", thief, command.CmdType), command, default);
        Record(row, "declaration", cast.State, cast.Events, cast.Accepted, cast.ErrorMessage);
        Assert.Equal(allowed, cast.Accepted);
        if (allowed)
        {
            var taken = await TopWithReceipt(cast.State, row);
            Assert.Equal(thief, taken.State.CardObjects[target].ControllerId);
            Assert.Equal(initial.CardObjects[target].OwnerId, taken.State.CardObjects[target].OwnerId);
            Assert.Equal(initial.CardObjects[target].ObjectGeneration, taken.State.CardObjects[target].ObjectGeneration);
            Assert.Equal("P1", Assert.Single(taken.State.StackItems).ControllerId);
            confirmed = taken;
        }
        else
        {
            // This newly hidden card at HOME is not an allowed source for F.
            // It must not be treated as an actual target-control response.
            Assert.Empty(cast.Events);
            Assert.Equal(MatchStateHasher.Hash(confirmed.State), MatchStateHasher.Hash(cast.State));
            Assert.True(cast.State.CardObjects["TAKEOVER"].IsFaceDown);
            Assert.Equal("P2", cast.State.CardObjects["ENEMY1"].ControllerId);
        }
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        Assert.Equal(2, done.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.Single(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Record(row, "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualSameIncarnationLeavingAndRejoiningTheOngoingCombatDoesNotDisarmTwice()
    {
        var initial = await Position("RAD·054/167", 1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·175/298", "P1") } };
        initial = At(initial, "ALLY", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Spell(initial,
            "WIND-OUT", "OGN·173/298", "P1"), "WIND-BACK", "OGN·173/298", "P1");
        var state = initial; var events = new List<GameEvent>(); var order = 0;
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        async Task Submit(string player, GameCommand command)
        {
            var result = await Act(state, player, command); state = result.State; events.AddRange(result.Events);
            Record("same-combat-reacquisition", (++order).ToString("D2") + "-" + command.CmdType, state, result.Events);
            Restore(state); ArmamentMightTests.PublicRecovery(state);
        }
        async Task Top()
        {
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
        }
        await Submit("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [], ["HOST", "ALLY"]));
        var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"])); await Top();
        Assert.Equal(2, Might(state));
        var generation = state.CardObjects["HOST"].ObjectGeneration;
        foreach (var (id, destination) in new[] { ("WIND-OUT", "BASE:P1"), ("WIND-BACK", "BATTLEFIELD:F") })
        {
            if (state.FocusPlayerId != "P1") await Submit(state.FocusPlayerId!, new PassFocusCommand());
            await Submit("P1", new PlayCardCommand(id, "OGN·173/298", ["HOST"], Destination: destination)); await Top();
            Assert.True(state.SpellDuelState.IsActive); Assert.Equal(generation, state.CardObjects["HOST"].ObjectGeneration);
            Assert.Contains("HOST", destination == "BASE:P1" ? state.PlayerZones["P1"].Base : state.PlayerZones["P1"].Battlefields);
        }
        Assert.True(state.CardObjects["HOST"].IsAttacking); Assert.True(state.CardObjects["ALLY"].IsAttacking);
        Assert.Null(state.PendingCardChoice); Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue);
        Assert.Single(events, e => e.Kind == "TRIGGER_QUEUED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.Equal(2, Might(state));
        Record("same-combat-reacquisition", "done", state, events);
    }
}
