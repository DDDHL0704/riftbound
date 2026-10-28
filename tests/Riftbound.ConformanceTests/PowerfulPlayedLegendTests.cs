using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulPlayedLegendTests
{
    private static MatchState Position(string legend)
    {
        var state = OfficialCopyIdentityTests.AddSpell(ArmamentMightTests.Position(), "PLAYED", "SFD·175/221");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PLAYED"] = state.CardObjects["PLAYED"] with { Tags = [CardObjectTags.UnitCard] },
                ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        return PrintedCostFixture.Add(state, "P1", "SFD·175/221");
    }

    private static async Task<ResolutionResult> Open(string legend)
    {
        var state = Position(legend);
        var played = await Act(state, "P1", new PlayCardCommand("PLAYED", "SFD·175/221", []));
        void Capture(string step, ResolutionResult result)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_POWERFUL_PLAYED_DIAGNOSTICS") is not { } folder) return;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, legend.Replace('/', '-') + "-" + step + ".json"),
                JsonSerializer.Serialize(new { result.Accepted, result.ErrorCode, result.ErrorMessage, result.State, result.Events },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        Capture("play", played);
        var own = Assert.Single(played.State.TriggerQueue, t => t.SourceObjectId == "PLAYED");
        var legendTrigger = Assert.Single(played.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var ordered = await Act(played.State, "P1", new OrderTriggersCommand([legendTrigger.TriggerId, own.TriggerId]));
        Capture("ordered", ordered);
        return ordered;
    }

    [Theory]
    [InlineData("OGN·249/298")]
    [InlineData("FND-249/298")]
    public async Task PlayingMightyOffersAChoiceBeforeExhaustingOrCalling(string legend)
    {
        var opened = await Open(legend);
        Assert.Contains("PLAYED", opened.State.PlayerZones["P1"].Base);
        Assert.Equal(3, opened.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(1, choice.MaxCount);
        Assert.DoesNotContain(opened.Events, e => e.Kind is "RUNES_CALLED" or "LEGEND_EXHAUSTED");
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
    }

    [Theory]
    [InlineData("OGN·249/298", true)] [InlineData("OGN·249/298", false)]
    [InlineData("FND-249/298", true)] [InlineData("FND-249/298", false)]
    public async Task ChoiceCanBeDeclinedOrPaidBeforeTheResponseWindow(string legend, bool accept)
    {
        var opened = await Open(legend);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var selected = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["LEGEND"] : []));
        Assert.Equal(accept, selected.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(3, selected.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(selected.Events, e => e.Kind == "RUNES_CALLED");
        Restore(selected.State); ArmamentMightTests.PublicRecovery(selected.State);
        if (!accept) {
            Assert.Equal("PLAYED", Assert.Single(selected.State.StackItems).SourceObjectId);
            var own = await Top(selected.State);
            Assert.DoesNotContain(own.Events, e => e.Kind == "RUNES_CALLED");
            Restore(own.State); ArmamentMightTests.PublicRecovery(own.State); return;
        }
        Assert.Equal(2, selected.State.StackItems.Count);
        var resolved = await Top(selected.State);
        Assert.Equal(2, resolved.State.PlayerZones["P1"].RuneDeck.Count);
        var rune = Assert.Single(resolved.Events, e => e.Kind == "RUNES_CALLED");
        var id = Assert.Single(Assert.IsType<string[]>(rune.Payload["runeObjectIds"]));
        Assert.Contains(id, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[id].IsExhausted);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
        var ownResolved = await Top(resolved.State);
        Assert.Empty(ownResolved.State.StackItems);
        Restore(ownResolved.State); ArmamentMightTests.PublicRecovery(ownResolved.State);
    }

    [Theory]
    [InlineData("history")] [InlineData("source-generation")] [InlineData("unit-generation")]
    [InlineData("might")] [InlineData("queue-tick")]
    public async Task TriggerOrderRejectsForgedPlayEvidenceWithoutMutation(string field)
    {
        var played = await Act(Position("OGN·249/298"), "P1", new PlayCardCommand("PLAYED", "SFD·175/221", []));
        var state = played.State;
        var trigger = Assert.Single(state.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var context = Assert.IsType<MightySkillContext>(trigger.MightySkill);
        var forged = field switch {
            "source-generation" => trigger with { MightySkill = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } },
            "unit-generation" => trigger with { MightySkill = context with { Unit = context.Unit with { Generation = context.Unit.Generation + 1 } } },
            "might" => trigger with { MightySkill = context with { AfterMight = context.AfterMight + 1 } },
            "queue-tick" => trigger with { EnqueuedAtTick = state.Tick + 1 },
            _ => trigger };
        state = state with { TriggerQueue = state.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId ? forged : t).ToArray() };
        if (field == "history") state = state with { CardPlays = state.CardPlays with { Resolved = [] } };
        var command = new OrderTriggersCommand(state.TriggerQueue.Select(t => t.TriggerId).ToArray());
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged-order", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }
}
