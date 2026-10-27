using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven124SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SubmittedDecksReallyPlayBothUnitsBeforePayingFriendlyDestructionAndRejectARefundInOriginalHistory(string actor)
    {
        // The common opening begins after seats are assigned, then actually
        // submits both decks, readies, mulligans, draws and channels runes.
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R04",
            ["VEN·124", "VEN·124", "VEN·124", "VEN·074", "VEN·074", "VEN·074"]);
        try
        {
            await game.WaitFor(actor, 5, "VEN·124", "VEN·074");
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            await game.Tap(actor); var mana = game.State.RunePools[actor].Mana;
            var costUnit = game.Hand(actor, "VEN·074").First(); var unit = game.Hand(actor, "VEN·124").First();
            await game.Act(actor, new PlayCardCommand(costUnit, "VEN·074", []));
            Assert.Equal(mana - 2, game.State.RunePools[actor].Mana); Assert.False(game.State.CardObjects[costUnit].IsEmpowered);
            await game.Act(actor, new PlayCardCommand(unit, "VEN·124", []));
            Assert.Equal(mana - 5, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Equal(3, game.Might(actor, unit)); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Empty(game.State.StackItems); await game.ColdCheckpoint();
            var before = game.State; var generation = before.CardObjects[unit].ObjectGeneration;
            var costGeneration = before.CardObjects[costUnit].ObjectGeneration;
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(before)[actor].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
                r => (string?)r["sourceObjectId"] == unit && (string?)r["abilityId"] == "EMPOWER:0:0");
            Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
            var choice = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]),
                c => c.ObjectIds is not null && c.ObjectIds.SequenceEqual([costUnit]));
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", [], OptionalCosts: [choice.Id]));
            Assert.DoesNotContain(costUnit, game.State.PlayerZones[actor].Base); Assert.Contains(costUnit, game.State.PlayerZones[actor].Graveyard);
            Assert.Equal(costGeneration + 1, game.State.CardObjects[costUnit].ObjectGeneration);
            Assert.Equal(mana - 5, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(3, game.Might(actor, unit));
            Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds); await game.ColdCheckpoint();
            // Keep the real post-destruction incarnation and original journal.
            // Forge only a coherent refund of that public unit to the base.
            var refunded = game.State with {
                CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) { [costUnit] = before.CardObjects[costUnit] with { ObjectGeneration = game.State.CardObjects[costUnit].ObjectGeneration } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(game.State.ObjectLocations) { [costUnit] = before.ObjectLocations[costUnit] },
                PlayerZones = new Dictionary<string, PlayerZones>(game.State.PlayerZones) { [actor] = game.State.PlayerZones[actor] with {
                    Base = before.PlayerZones[actor].Base, Graveyard = before.PlayerZones[actor].Graveyard } } };
            await game.RejectCoordinatedForgery("refunded-real-friendly-unit-cost-" + actor, refunded);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(3, game.State.CardObjects[unit].Power); Assert.Equal(5, game.Might(actor, unit));
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Contains(costUnit, game.State.PlayerZones[actor].Graveyard); await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(5, game.Might(actor, unit));
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven124-submitted-real-friendly-cost-" + actor); }
    }
}
