using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class WildClawReflexiveRecoveryTests
{
    [Theory]
    [InlineData("missing-context")]
    [InlineData("wrong-effect")]
    [InlineData("wrong-source")]
    [InlineData("null-source")]
    [InlineData("null-source-id")]
    [InlineData("source-generation")]
    [InlineData("null-card")]
    [InlineData("wrong-card")]
    [InlineData("negative-execution")]
    [InlineData("future-execution")]
    [InlineData("wrong-controller")]
    [InlineData("target-injected")]
    [InlineData("missing-selected")]
    [InlineData("null-selected-binding")]
    [InlineData("null-selected-id")]
    [InlineData("selected-generation")]
    [InlineData("selected-before-generation")]
    [InlineData("selected-card")]
    [InlineData("selected-spell")]
    [InlineData("selected-owner")]
    [InlineData("early-confirmed")]
    [InlineData("early-played")]
    [InlineData("before-card")]
    public async Task IndependentRecoveryRejectsMalformedOrForgedPendingReflexivesBeforeTheRealCardConfirms(string mutation)
    {
        var driver = await Pending(); var state = driver.State;
        var item = state.StackItems.Single(i => i.DeckPlayReflexive is not null); var c = item.DeckPlayReflexive!;
        var selected = c.Selected!;
        var changed = mutation switch {
            "null-source" => c with { Source = null! }, "null-source-id" => c with { Source = c.Source with { ObjectId = null! } },
            "wrong-source" => c with { Source = new("HOST", 0) }, "source-generation" => c with { Source = c.Source with { Generation = 9999 } },
            "null-card" => c with { CardNo = null! }, "wrong-card" => c with { CardNo = SingleDeckPlaySequenceTests.Reinforce },
            "negative-execution" => c with { ExecutionIndex = -1 }, "future-execution" => c with { ExecutionIndex = 1 },
            "missing-selected" => c with { Selected = null }, "null-selected-binding" => c with { Selected = selected with { Card = null! } },
            "null-selected-id" => c with { Selected = selected with { Card = selected.Card with { ObjectId = null! } } },
            "selected-generation" => c with { Selected = selected with { Card = selected.Card with { Generation = 9999 } } },
            "selected-before-generation" => c with { Selected = selected with { BeforeBanishGeneration = long.MaxValue } },
            "selected-card" => c with { Selected = selected with { CardNo = "SFD·042/221" } },
            "selected-spell" => c with { Selected = selected with { CardNo = "OGN·048/298" } },
            "selected-owner" => c with { Selected = selected with { OwnerId = "P2" } },
            "early-confirmed" => c with { Confirmed = true }, "early-played" => c with { Played = new("P1_TOP", 3) }, _ => c };
        item = item with { DeckPlayReflexive = mutation == "missing-context" ? null : changed,
            EffectKind = mutation == "wrong-effect" ? "DRAW_ONE" : item.EffectKind,
            ControllerId = mutation == "wrong-controller" ? "P2" : item.ControllerId,
            TargetObjectIds = mutation == "target-injected" ? ["P1_TOP"] : [] };
        var cards = state.StackItems.Where(i => i.DeckPlayReflexive is null).ToArray();
        var forged = state with { StackItems = mutation == "before-card" ? new[] { item }.Concat(cards).ToArray() : cards.Append(item).ToArray() };
        var error = Record.Exception(() => Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("deck play reflexive")));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("execution")]
    [InlineData("confirmation")]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("omitted-confirmation")]
    [InlineData("private-reference-injected")]
    public async Task BothPlayersAndSpectatorRejectForgedOrExtraPublicReflexivePayloads(string mutation)
    {
        var driver = await Pending(); var state = driver.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
        object? forged = mutation switch {
            "execution" => new DeckPlayReflexiveView(1, false), "confirmation" => new DeckPlayReflexiveView(0, true),
            "missing" => null, "malformed" => "forged", "omitted-confirmation" => new { executionIndex = 0 },
            _ => new { executionIndex = 0, confirmed = false, selected = new { objectId = "N1" } } };
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var row = Assert.IsType<Dictionary<string, object?>>(snapshot.Stack.Single(p => JsonSerializer.SerializeToElement(p).TryGetProperty("deckPlayReflexive", out _)));
            var old = row["deckPlayReflexive"]; row["deckPlayReflexive"] = forged;
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator), e => e.Contains("deck"));
            row["deckPlayReflexive"] = old;
        }
    }

    private static async Task<StandbyDefendObserverTests.Driver> Pending()
    {
        var driver = new StandbyDefendObserverTests.Driver(WildClawPendingReflexiveTests.Position());
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await WildClawPendingReflexiveTests.Decide(driver, true);
        return driver;
    }
}
