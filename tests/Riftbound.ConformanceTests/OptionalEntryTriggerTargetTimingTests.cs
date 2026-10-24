using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryTriggerTargetTimingTests
{
    [Theory]
    [InlineData("blast-crew-apprentice", "optional-damage", true, 2)]
    [InlineData("blast-crew-apprentice", "no-optional-damage", false, 2)]
    [InlineData("frostcoat-cub", "optional-power-minus-two", true, 3)]
    [InlineData("frostcoat-cub", "no-optional-power-minus-two", false, 3)]
    public async Task PaidEntryChoosesAfterEnteringAndUnpaidEntryDoesNotCreateAnAbility(
        string card, string branch, bool paid, int printedMana)
    {
        var fixture = await ConformanceFixture.LoadAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", $"p2-preflight-play-{card}-{branch}.fixture.json"), default);
        var original = fixture.Commands[0];
        var raw = JsonNode.Parse(original.Cmd.GetRawText())!.AsObject();
        var source = raw["sourceObjectId"]!.GetValue<string>();
        var target = paid ? raw["targetObjectIds"]![0]!.GetValue<string>() : "";
        raw["targetObjectIds"] = new JsonArray();
        var firstFixture = fixture with { Commands = [original with { Cmd = JsonSerializer.SerializeToElement(raw) }] };
        if (!paid)
        {
            // Having enough resources must not implicitly opt into the additional cost.
            var initial = Assert.IsType<ConformanceInitialState>(firstFixture.InitialState);
            var pools = new Dictionary<string, RunePool>(Assert.IsAssignableFrom<IReadOnlyDictionary<string, RunePool>>(initial.RunePools))
            {
                ["P1"] = new(printedMana + (card == "blast-crew-apprentice" ? 1 : 0), 0,
                    new Dictionary<string, int> { [card == "blast-crew-apprentice" ? RuneTrait.Red : RuneTrait.Blue] = 1 })
            };
            firstFixture = firstFixture with { InitialState = initial with { RunePools = pools } };
        }
        var first = await ConformanceFixtureRunner.RunAsync(firstFixture, new CoreRuleEngine(), default);
        Assert.Equal(1, first.FinalTick);
        Assert.Contains(source, first.FinalState.PlayerZones["P1"].Base);
        Assert.Equal("BASE", first.FinalState.ObjectLocations[source].Zone);
        Assert.DoesNotContain(first.Events, e => e.Kind is "DAMAGE_APPLIED" or "POWER_MODIFIED_UNTIL_END_OF_TURN");
        var payment = Assert.Single(first.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(paid && card == "blast-crew-apprentice" ? printedMana + 1 : printedMana, payment.Payload["mana"]);
        Assert.Equal(paid ? 1 : 0, payment.Payload["power"]);
        Assert.Equal(paid ? 0 : 1, first.FinalState.RunePools["P1"].TotalPower);
        Assert.Equal(!paid && card == "blast-crew-apprentice" ? 1 : 0, first.FinalState.RunePools["P1"].Mana);
        if (!paid)
        {
            Assert.Empty(first.FinalState.StackItems);
            Assert.Null(first.FinalState.PendingCardChoice);
            Assert.Null(first.FinalState.PriorityPlayerId);
            Assert.DoesNotContain(first.Events, e => e.Kind is "TRIGGER_QUEUED" or "TRIGGERED_ABILITY_DECLARED" or "TRIGGER_CONFIRMED");
            return;
        }
        var choice = Assert.IsType<PendingCardChoiceState>(first.FinalState.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Contains(target, choice.LegalObjectIds);
        if (card == "frostcoat-cub")
        {
            Assert.Contains(source, choice.LegalObjectIds);
        }
        else
        {
            Assert.DoesNotContain(source, choice.LegalObjectIds);
        }
        var engine = new CoreRuleEngine();
        var selected = await engine.ResolveAsync(first.FinalState,
            new PlayerIntent("entry-target-" + card, "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]), default);
        Assert.True(selected.Accepted, selected.ErrorMessage);
        Assert.Equal(0, selected.State.RunePools["P1"].Mana);
        Assert.Equal(0, selected.State.RunePools["P1"].TotalPower);
        Assert.Contains(selected.Events, e => e.Kind == "TRIGGER_CONFIRMED");
        var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, selected);
        if (card == "blast-crew-apprentice")
        {
            Assert.Equal(2, resolved.State.CardObjects[target].Damage);
        }
        else
        {
            Assert.Equal(1, resolved.State.CardObjects[target].Power);
        }
        Assert.Equal(0, resolved.State.RunePools["P1"].TotalPower);
    }
}
