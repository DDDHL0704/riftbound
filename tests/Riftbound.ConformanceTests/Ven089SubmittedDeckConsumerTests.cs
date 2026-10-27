using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven089SubmittedDeckConsumerTests
{
    [Theory]
    [InlineData("P1", true)] [InlineData("P2", true)]
    [InlineData("P1", false)] [InlineData("P2", false)]
    public async Task SubmittedOrangeYellowDecksPayThePrintedSpellAndActuallyPlayAPrivatePermanentBeforeIndependentEmpower(string actor, bool create)
    {
        const string no = "VEN·089";
        var additions = new[] { no, no, no, "VEN·124", "VEN·124", "VEN·124", "VEN·137", "VEN·137", "VEN·137", "VEN·070", "VEN·070", "VEN·070" };
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R04", additions);
        try
        {
            await game.WaitFor(actor, 8, no); var runes = game.Runes(actor);
            Assert.InRange(runes.Length, 8, 12); Assert.Equal(12, runes.Length + game.State.PlayerZones[actor].RuneDeck.Count);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            foreach (var id in runes.Take(7)) await game.Act(actor, new TapRuneCommand(id));
            await game.Act(actor, new RecycleRuneCommand(runes[0]));
            Assert.Equal(7, game.State.RunePools[actor].Mana); Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Orange]);
            var parent = game.Hand(actor, no).First(); await game.Act(actor, new PlayCardCommand(parent, no, []));
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower); await game.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal(actor, choice.PlayerId); Assert.Equal(5, choice.ContextObjectIds!.Count); Assert.Equal(0, choice.RequiredCount);
            await game.ColdCheckpoint();
            var selected = choice.LegalObjectIds.First(id => game.State.CardObjects[id].CardNo is "VEN·124" or "VEN·137" or "VEN·070");
            var selectedNo = game.State.CardObjects[selected].CardNo!; var generation = game.State.CardObjects[selected].ObjectGeneration;
            var existingUnits = game.State.PlayerZones[actor].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)).ToArray();
            await game.Act(actor, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [selected]));
            var decision = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice); Assert.Equal("DECK_REFLEXIVE_CREATE", decision.Request.Kind);
            var preview = Assert.IsType<MatchState>(decision.Request.Preview); Assert.Equal("STACK", preview.ObjectLocations[selected].Zone); Assert.False(preview.CardObjects[selected].IsEmpowered);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach (var viewer in game.State.Seats.Keys)
            {
                var snapshot = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)[viewer], json);
                var staged = Assert.Single(snapshot.GetProperty("stack").EnumerateArray(), item => item.GetProperty("sourceObjectId").GetString() == selected);
                Assert.Equal(selectedNo, staged.GetProperty("cardNo").GetString());
                Assert.Equal(generation + 1, staged.GetProperty("deckPlayPendingCard").GetProperty("card").GetProperty("generation").GetInt32());
                if (viewer == actor) continue;
                var visible = snapshot.GetRawText() + JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)[viewer], json);
                foreach (var hidden in choice.ContextObjectIds.Where(id => id != selected)) Assert.DoesNotContain(hidden, visible, StringComparison.Ordinal);
            }
            await game.ColdCheckpoint();
            await game.Act(actor, new PayCostCommand(decision.Request.Id, "RULE_REPLACEMENT", [create ? "CREATE" : "DECLINE"]));
            Assert.Contains(parent, game.State.PlayerZones[actor].Graveyard); Assert.Equal(5, game.State.PendingEffectPlay!.ManaReduction);
            Assert.Equal(generation + 1, game.State.CardObjects[selected].ObjectGeneration); Assert.False(game.State.CardObjects[selected].IsEmpowered);
            Assert.All(choice.ContextObjectIds.Where(id => id != selected), id => Assert.Contains(id, game.State.PlayerZones[actor].MainDeck));
            await game.ColdCheckpoint();
            if (selectedNo == "VEN·070") await game.Act(actor, new RecycleRuneCommand(runes[1]));
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(selectedNo == "VEN·070" ? 1 : 0, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new PlayCardCommand(selected, selectedNo, []));
            Assert.Null(game.State.PendingEffectPlay); Assert.Contains(selected, game.State.PlayerZones[actor].Base);
            Assert.Equal(generation + 2, game.State.CardObjects[selected].ObjectGeneration); Assert.False(game.State.CardObjects[selected].IsEmpowered);
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Equal(selectedNo switch { "VEN·124" => 3, "VEN·070" => 4, _ => 0 }, game.Might(actor, selected));
            Assert.All(existingUnits, id => Assert.Contains(id, game.State.PlayerZones[actor].Base));
            if (create)
            {
                var ability = Assert.Single(game.State.StackItems).DeckPlayReflexive!;
                Assert.True(ability.Confirmed); Assert.Equal(new ObjectBinding(selected, generation + 2), ability.Played); Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
            }
            else Assert.Empty(game.State.StackItems);
            await game.ColdCheckpoint();
            var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                [selected] = game.State.CardObjects[selected] with { IsEmpowered = true } } };
            await game.RejectCoordinatedForgery("unearned-empower-after-paid-private-play-" + actor + "-" + create, forged);
            if (create) await game.Top();
            Assert.Equal(create, game.State.CardObjects[selected].IsEmpowered);
            Assert.Equal(selectedNo switch { "VEN·124" => create ? 5 : 3, "VEN·070" => create ? 6 : 4, _ => 0 }, game.Might(actor, selected));
            Assert.Empty(game.State.StackItems); Assert.Equal(12, game.Runes(actor).Length + game.State.PlayerZones[actor].RuneDeck.Count);
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven089-submitted-" + actor + "-" + create); }
    }
}
