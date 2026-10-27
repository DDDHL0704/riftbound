using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualResourceOrBattleDeathRetainsAllThreePaidAgentSkills(bool battle)
    {
        const string agent = "OGN·178/298";
        var initial = AgentPosition();
        if (battle)
        {
            // Explicit mature battlefield material: both printed units have five might.
            initial = AtDrawField(AtDrawField(initial, "UNIT", "P1"), "ENEMY", "P2") with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "VEN·048", "P2") },
                UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF")] };
        }
        else initial = Add(initial, "MALZ", P4ActivatedAbilityCatalog.MalzaharCardNo, "P1", "BASE");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1");
            if (!battle)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-6"));
                await game.Act("P1", new PlayCardCommand("UNIT", agent, []));
            }
            for (var i = 0; i < 2; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(agent, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var mana = game.State.RunePools["P1"].Mana;
            if (battle)
            {
                Assert.Equal(5, game.State.CardObjects["UNIT"].Power); Assert.Equal(5, game.State.CardObjects["ENEMY"].Power);
                await game.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
                for (var step = 0; !game.State.PlayerZones["P1"].Graveyard.Contains("UNIT"); step++)
                {
                    Assert.InRange(step, 0, 12);
                    var damage = ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p => p.Actionable && p.Actions.Contains(CommandTypes.AssignCombatDamage));
                    if (damage is not null)
                        await game.Act(damage.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!, "BF",
                            [damage.PlayerId == "P1" ? new CombatDamageAssignmentDto("UNIT", "ENEMY", 5) : new CombatDamageAssignmentDto("ENEMY", "UNIT", 5)]));
                    else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
                    else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
                    else Assert.Fail("Actual combat must expose its next ordinary continuation.");
                }
                Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            }
            else
            {
                Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Act("P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["UNIT"]));
                Assert.True(game.State.CardObjects["MALZ"].IsExhausted);
                Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
            }
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(3, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i < 3; i++)
            {
                var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
                await game.Top(); var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
                Assert.Equal(2, choice.RequiredCount); Assert.Equal(2, choice.DrawCount);
                Assert.Equal(generation, choice.LastBreathDraw!.Source.Generation);
                await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, hand.Take(2).ToArray()));
                Assert.Equal(hand.Skip(2).Concat(deck.Take(2)), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(2), game.State.PlayerZones["P1"].MainDeck); Assert.Equal(2 - i, game.State.StackItems.Count);
            }
            Assert.Null(game.State.PendingHandChoice); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("agent-actual-death-path-battle-" + battle, initial, game); }
    }
}
